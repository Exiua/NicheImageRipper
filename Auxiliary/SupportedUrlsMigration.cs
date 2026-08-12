// SupportedUrlsMigration.cs
//
// One-time migration: moves entries out of UrlUtility.SupportedSites into each parser's new
// IHtmlParser.SupportedUrls property, using the REAL UrlUtility/HtmlParserFactory resolution
// logic (no duplicated site-name-guessing) and Roslyn for guaranteed-valid source edits.
//
// Add to this Auxiliary project. Requires project reference to NicheImageRipper.Core, plus
// NuGet packages: Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.MSBuild,
// Microsoft.Build.Locator.
//
// Usage:
//   dotnet run --project Auxiliary -- <path-to-solution.sln> [--dry-run]
//
// Run with --dry-run first and read the console output before running for real.

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Auxiliary;

public static class SupportedUrlsMigrationProgram
{
    private static bool _dryRun;

    public static async Task Main(string[] args)
    {
        var solutionPath = args.FirstOrDefault(a => !a.StartsWith("--"));
        _dryRun = args.Contains("--dry-run");

        if (solutionPath is null)
        {
            Console.WriteLine("Usage: dotnet run -- <path-to-solution.sln> [--dry-run]");
            return;
        }

        Console.WriteLine(_dryRun
            ? "Running in DRY-RUN mode — no files will be modified.\n"
            : "Running for real — files WILL be modified.\n");

        // MUST happen before any method that references Microsoft.CodeAnalysis.MSBuild types is
        // JIT-compiled — keeping that logic in a separate method (RunMigrationAsync) prevents the
        // JIT from touching those types during Main's own compilation.
        if (!MSBuildLocator.IsRegistered)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Count == 0)
            {
                Console.WriteLine("ERROR: No MSBuild instance found. Install the .NET SDK (not just the runtime), " +
                                  "or run 'dotnet workload list' to confirm build tools are available.");
                return;
            }

            Console.WriteLine($"Using MSBuild from: {instances[0].MSBuildPath}");
            MSBuildLocator.RegisterInstance(instances[0]);
        }

        await RunMigrationAsync(solutionPath);
    }

    // Isolated on purpose — nothing in Main references Microsoft.CodeAnalysis.MSBuild directly,
    // so the JIT never needs to resolve that assembly before MSBuildLocator has registered it.
    private static async Task RunMigrationAsync(string solutionPath)
    {
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => Console.WriteLine($"[workspace] {e.Diagnostic}"));

        Console.WriteLine($"Loading solution: {solutionPath}");
        var solution = await workspace.OpenSolutionAsync(solutionPath);

        // ------------------------------------------------------------------
        // Step 1: extract URLs from UrlUtility.SupportedSites via Roslyn (so we get exact
        // string literals, not a regex approximation), then resolve each one using the REAL
        // production code path — UrlUtility.SiteCheck + HtmlParserFactory — no duplicated logic.
        // ------------------------------------------------------------------
        var urlUtilityDoc = FindSingleDocument(solution, "UrlUtility.cs");
        var (urls, commentedOutUrls) = await ExtractSupportedSitesUrlsAsync(urlUtilityDoc);
        Console.WriteLine($"Found {urls.Count} active URLs in SupportedSites ({commentedOutUrls.Count} commented-out entries ignored).\n");

        var matched = new Dictionary<string, List<string>>(); // ParserName -> urls
        var unmatched = new List<(string Url, string Reason)>();

        foreach (var url in urls)
        {
            try
            {
                var (siteName, _) = UrlUtility.SiteCheck(url, new Dictionary<string, string>());
                var parserType = HtmlParserFactory.ResolveType(siteName);
                if (parserType is null)
                {
                    unmatched.Add((url, $"UrlCheck passed but no parser registered for site '{siteName}'"));
                    continue;
                }

                if (!matched.TryGetValue(parserType.Name, out var list))
                {
                    matched[parserType.Name] = list = [];
                }

                list.Add(url);
            }
            catch (RipperException e)
            {
                unmatched.Add((url, e.Message));
            }
            catch (Exception e)
            {
                unmatched.Add((url, $"{e.GetType().Name}: {e.Message}"));
            }
        }

        Console.WriteLine($"Matched {urls.Count - unmatched.Count} URLs across {matched.Count} parsers. {unmatched.Count} unmatched.\n");

        // ------------------------------------------------------------------
        // Step 2: for each matched parser, find its declaring document and add/merge SupportedUrls.
        // ------------------------------------------------------------------
        foreach (var (parserTypeName, parserUrls) in matched)
        {
            var doc = await FindParserDocumentAsync(solution, parserTypeName);
            if (doc is null)
            {
                Console.WriteLine($"WARNING: could not locate source file for parser type '{parserTypeName}' — skipping {parserUrls.Count} URL(s).");
                unmatched.AddRange(parserUrls.Select(u => (u, $"Resolved to parser type '{parserTypeName}' but its source file could not be located")));
                continue;
            }

            var updatedDoc = await AddSupportedUrlsAsync(doc, parserUrls);
            var newRoot = await updatedDoc.GetSyntaxRootAsync();
            var formatted = newRoot!.NormalizeWhitespace().ToFullString();

            Console.WriteLine($"{(_dryRun ? "[dry-run] " : "")}{Path.GetFileName(doc.FilePath)}: adding {parserUrls.Count} URL(s) to SupportedUrls");

            if (!_dryRun)
            {
                await File.WriteAllTextAsync(doc.FilePath!, formatted);
            }
        }

        // ------------------------------------------------------------------
        // Step 3: rewrite UrlUtility.SupportedSites with only the unmatched URLs remaining.
        // ------------------------------------------------------------------
        var remainingUrls = unmatched.Select(u => u.Url).ToList();
        var updatedUrlUtilityDoc = await RewriteSupportedSitesAsync(urlUtilityDoc, remainingUrls);
        var updatedRoot = await updatedUrlUtilityDoc.GetSyntaxRootAsync();
        var urlUtilityFormatted = updatedRoot!.NormalizeWhitespace().ToFullString();

        Console.WriteLine($"\n{(_dryRun ? "[dry-run] " : "")}UrlUtility.cs: SupportedSites reduced from {urls.Count} to {remainingUrls.Count} entries.");

        if (!_dryRun)
        {
            await File.WriteAllTextAsync(urlUtilityDoc.FilePath!, urlUtilityFormatted);
        }

        // ------------------------------------------------------------------
        // Summary
        // ------------------------------------------------------------------
        if (unmatched.Count > 0)
        {
            Console.WriteLine("\n--- UNMATCHED (left in SupportedSites — needs manual attention) ---");
            foreach (var (url, reason) in unmatched)
            {
                Console.WriteLine($"  {url}\n      reason: {reason}");
            }
        }

        if (commentedOutUrls.Count > 0)
        {
            Console.WriteLine("\n--- COMMENTED-OUT entries in SupportedSites (left untouched) ---");
            foreach (var url in commentedOutUrls)
            {
                Console.WriteLine($"  {url}");
            }
        }

        Console.WriteLine("\n--- MANUAL FOLLOW-UP ---");
        Console.WriteLine("- The `// Fake site used as control signal...` comment on booru.com was not");
        Console.WriteLine("  preserved. If booru.com was matched above, consider adding that explanation");
        Console.WriteLine("  as a doc-comment on AllBooruParser.SupportedUrls.");
        Console.WriteLine("- Build the solution before committing.");
    }

    // -----------------------------------------------------------------------
    // Extraction: read UrlUtility's SupportedSites array as actual syntax nodes.
    // -----------------------------------------------------------------------
    private static async Task<(List<string> Urls, List<string> CommentedOut)> ExtractSupportedSitesUrlsAsync(Document doc)
    {
        var root = await doc.GetSyntaxRootAsync();
        var fieldDecl = root!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "SupportedSites"));

        var literals = fieldDecl.DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            .ToList();

        var urls = new List<string>();
        var commentedOut = new List<string>();

        foreach (var literal in literals)
        {
            var value = literal.Token.ValueText;
            if (!value.StartsWith("http"))
            {
                continue;
            }

            // A literal is "commented out" if it's inside block-comment trivia rather than live code —
            // Roslyn still walks trivia text separately from the syntax tree, so any literal we found via
            // DescendantNodes() here is by definition live code, not inside a /* */ block. Block-comment
            // contents are captured separately below via trivia inspection.
            urls.Add(value);
        }

        // Trivia (comments) aren't part of DescendantNodes()'s literal walk — pull URL-shaped substrings
        // out of block-comment trivia text directly so we can report them without touching them.
        foreach (var trivia in fieldDecl.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.MultiLineCommentTrivia)))
        {
            var text = trivia.ToFullString();
            foreach (var quoted in ExtractQuotedStrings(text).Where(s => s.StartsWith("http")))
            {
                commentedOut.Add(quoted);
            }
        }

        return (urls.Distinct().ToList(), commentedOut.Distinct().ToList());
    }

    private static IEnumerable<string> ExtractQuotedStrings(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf('"', i);
            if (start == -1)
            {
                yield break;
            }

            var end = text.IndexOf('"', start + 1);
            if (end == -1)
            {
                yield break;
            }

            yield return text[(start + 1)..end];
            i = end + 1;
        }
    }

    // -----------------------------------------------------------------------
    // Rewrite: replace SupportedSites' array contents with only the remaining (unmatched) URLs.
    // -----------------------------------------------------------------------
    private static async Task<Document> RewriteSupportedSitesAsync(Document doc, IReadOnlyList<string> remainingUrls)
    {
        var root = await doc.GetSyntaxRootAsync();
        var fieldDecl = root!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "SupportedSites"));

        var arrayCreation = fieldDecl.DescendantNodes()
            .OfType<ImplicitArrayCreationExpressionSyntax>()
            .Single();

        var newInitializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.ArrayInitializerExpression,
            SyntaxFactory.SeparatedList(
                remainingUrls.Select(ExpressionSyntax (u) => SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(u)))));

        var newArrayCreation = arrayCreation.WithInitializer(newInitializer);
        var newRoot = root.ReplaceNode(arrayCreation, newArrayCreation);
        return doc.WithSyntaxRoot(newRoot);
    }

    // -----------------------------------------------------------------------
    // Locate a parser's source document by its declaring type's simple name.
    // -----------------------------------------------------------------------
    private static async Task<Document?> FindParserDocumentAsync(Solution solution, string parserTypeName)
    {
        foreach (var project in solution.Projects)
        {
            foreach (var doc in project.Documents)
            {
                var root = await doc.GetSyntaxRootAsync();
                if (root is null)
                {
                    continue;
                }

                var hasClass = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Any(c => c.Identifier.Text == parserTypeName);

                if (hasClass)
                {
                    return doc;
                }
            }
        }

        return null;
    }

    private static Document FindSingleDocument(Solution solution, string fileName)
    {
        var matches = solution.Projects
            .SelectMany(p => p.Documents)
            .Where(d => Path.GetFileName(d.FilePath ?? "") == fileName)
            .ToList();

        return matches.Count switch
        {
            0 => throw new FileNotFoundException($"Could not find {fileName} in the solution."),
            > 1 => throw new InvalidOperationException($"Found multiple files named {fileName}: {string.Join(", ", matches.Select(m => m.FilePath))}"),
            _ => matches[0]
        };
    }

    // -----------------------------------------------------------------------
    // Add (or merge into) a parser class's SupportedUrls property, inserted directly after ParserName.
    // -----------------------------------------------------------------------
    private static async Task<Document> AddSupportedUrlsAsync(Document? document, IReadOnlyList<string> urls)
    {
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync())!;
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Members.OfType<PropertyDeclarationSyntax>().Any(p => p.Identifier.Text == "ParserName"));

        var existing = classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == "SupportedUrls");

        var newLiterals = urls.Select(u => (ExpressionSyntax)SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(u)));

        ClassDeclarationSyntax updatedClass;

        if (existing is not null)
        {
            var currentElements = existing.ExpressionBody!.Expression is CollectionExpressionSyntax collectionExpr
                ? collectionExpr.Elements.OfType<ExpressionElementSyntax>().Select(e => e.Expression)
                : [];

            var mergedLiterals = currentElements
                .Concat(newLiterals)
                .DistinctBy(e => e.ToString())
                .ToList();

            var newProp = existing.WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(BuildCollectionExpression(mergedLiterals)));

            updatedClass = classDecl.ReplaceNode(existing, newProp);
        }
        else
        {
            var parserNameProp = classDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .Single(p => p.Identifier.Text == "ParserName");

            var newProp = SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.ArrayType(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                        SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier())),
                    SyntaxFactory.Identifier("SupportedUrls"))
                .AddModifiers(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(BuildCollectionExpression(newLiterals.ToList())))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(parserNameProp.GetLeadingTrivia())
                .WithTrailingTrivia(parserNameProp.GetTrailingTrivia());

            updatedClass = classDecl.InsertNodesAfter(parserNameProp, [newProp]);
        }

        var newRoot = root.ReplaceNode(classDecl, updatedClass);
        return document.WithSyntaxRoot(newRoot);
    }

    private static CollectionExpressionSyntax BuildCollectionExpression(IReadOnlyList<ExpressionSyntax> elements)
    {
        return SyntaxFactory.CollectionExpression(
            SyntaxFactory.SeparatedList<CollectionElementSyntax>(
                elements.Select(SyntaxFactory.ExpressionElement)));
    }
}