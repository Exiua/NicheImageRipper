// RipInfoAsyncMigration.cs
//
// One-time migration: moves callers of the synchronous RipInfo.FromUrlList / FromUrlListWithFilenames (which block
// on URL expansion, e.g. Google Drive folders) to their async counterparts. Syntax-only, so it needs no MSBuild, and
// it edits nodes in place so the rest of each file keeps its formatting.
//
// Rewrites:
//   return RipInfo.FromUrlList(...);                    in an async method or lambda
//     -> return await RipInfo.FromUrlListAsync(...);
//   return Task.FromResult(RipInfo.FromUrlList(...));   in a non-async method
//     -> return RipInfo.FromUrlListAsync(...);
// Any other use is listed for manual attention and left as is.
//
// Usage:
//   dotnet run --project Auxiliary -- <source-root> [--dry-run]
//
// Run with --dry-run first and read the console output before running for real.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NicheImageRipper.Auxiliary;

public static class RipInfoAsyncMigrationProgram
{
    private static readonly Dictionary<string, string> AsyncNames = new()
    {
        ["FromUrlList"] = "FromUrlListAsync",
        ["FromUrlListWithFilenames"] = "FromUrlListWithFilenamesAsync",
    };

    private static readonly string[] ExcludedDirectories = ["bin", "obj", "Workspace", "Test", "Auxiliary"];

    public static async Task Run(string[] args)
    {
        var root = args.FirstOrDefault(a => !a.StartsWith("--"));
        var dryRun = args.Contains("--dry-run");

        if (root is null)
        {
            Console.WriteLine("Usage: dotnet run -- <source-root> [--dry-run]");
            return;
        }

        Console.WriteLine(dryRun
            ? "Running in DRY-RUN mode — no files will be modified.\n"
            : "Running for real — files WILL be modified.\n");

        var rewritten = 0;
        var changedFiles = 0;
        var manual = new List<string>();

        foreach (var path in EnumerateSourceFiles(Path.GetFullPath(root)))
        {
            var text = await File.ReadAllTextAsync(path);
            if (!AsyncNames.Keys.Any(name => text.Contains($"RipInfo.{name}(")))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(text, path: path);
            var syntaxRoot = await tree.GetRootAsync();
            var calls = syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(IsSyncFactoryCall).ToList();

            var replacements = new Dictionary<SyntaxNode, SyntaxNode>();
            foreach (var call in calls)
            {
                var replacement = TryRewrite(call, out var target);
                if (replacement is null)
                {
                    var line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    manual.Add($"{Path.GetRelativePath(root, path)}:{line}: {call.Parent?.ToString().Trim()}");
                    continue;
                }

                replacements[target!] = replacement;
            }

            if (replacements.Count == 0)
            {
                continue;
            }

            var newRoot = syntaxRoot.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
            rewritten += replacements.Count;
            changedFiles++;
            Console.WriteLine($"{(dryRun ? "[dry-run] " : "")}{Path.GetRelativePath(root, path)}: {replacements.Count} call(s)");

            if (!dryRun)
            {
                // Keep the file's existing encoding (BOM or not)
                var encoding = HasUtf8Bom(path) ? new UTF8Encoding(true) : new UTF8Encoding(false);
                await File.WriteAllTextAsync(path, newRoot.ToFullString(), encoding);
            }
        }

        Console.WriteLine($"\nRewrote {rewritten} call(s) in {changedFiles} file(s).");
        if (manual.Count > 0)
        {
            Console.WriteLine("\n--- NEEDS MANUAL ATTENTION (left unchanged) ---");
            foreach (var entry in manual)
            {
                Console.WriteLine($"  {entry}");
            }
        }

        Console.WriteLine("\n--- MANUAL FOLLOW-UP ---");
        Console.WriteLine("- Build the solution before committing.");
    }

    /// <summary>
    ///     Returns the node to swap in, and in <paramref name="target"/> the node it replaces, or null when the call
    ///     isn't in a shape this migration understands.
    /// </summary>
    private static SyntaxNode? TryRewrite(InvocationExpressionSyntax call, out SyntaxNode? target)
    {
        target = null;
        var asyncCall = ToAsyncCall(call);

        // return RipInfo.FromUrlList(...);  in an async function
        if (call.Parent is ReturnStatementSyntax && IsInAsyncFunction(call))
        {
            target = call;
            return SyntaxFactory.AwaitExpression(
                                     SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                                     asyncCall.WithoutLeadingTrivia())
                                .WithTriviaFrom(call);
        }

        // return Task.FromResult(RipInfo.FromUrlList(...));  in a non-async function
        if (call.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Arguments.Count: 1, Parent: InvocationExpressionSyntax wrapper } }
            && wrapper.Expression.ToString() == "Task.FromResult"
            && wrapper.Parent is ReturnStatementSyntax
            && !IsInAsyncFunction(wrapper))
        {
            target = wrapper;
            return asyncCall.WithTriviaFrom(wrapper);
        }

        return null;
    }

    private static InvocationExpressionSyntax ToAsyncCall(InvocationExpressionSyntax call)
    {
        var access = (MemberAccessExpressionSyntax)call.Expression;
        var asyncName = SyntaxFactory.IdentifierName(AsyncNames[access.Name.Identifier.Text]).WithTriviaFrom(access.Name);
        return call.WithExpression(access.WithName(asyncName));
    }

    private static bool IsSyncFactoryCall(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.Text: "RipInfo" }
        } access && AsyncNames.ContainsKey(access.Name.Identifier.Text);

    /// <summary>Whether the innermost method, local function or lambda around <paramref name="node"/> is async.</summary>
    private static bool IsInAsyncFunction(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return method.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case LocalFunctionStatementSyntax local:
                    return local.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case AnonymousFunctionExpressionSyntax lambda:
                    return lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                case AccessorDeclarationSyntax or ConstructorDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                        .Where(path => !Path.GetRelativePath(root, path)
                                            .Split(Path.DirectorySeparatorChar)
                                            .Any(part => ExcludedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase)));
    }

    private static bool HasUtf8Bom(string path)
    {
        Span<byte> bom = stackalloc byte[3];
        using var stream = File.OpenRead(path);
        return stream.Read(bom) == 3 && bom.SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF });
    }
}
