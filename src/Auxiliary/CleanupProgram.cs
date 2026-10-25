// CleanupProgram.cs — Auxiliary project, two independent passes:
//   1. Remove duplicate `using` directives per file.
//   2. For any `await X(...)` call inside a method/lambda that has a CancellationToken parameter in scope,
//      if X has an overload/parameter accepting CancellationToken and the call doesn't already pass one
//      (positionally or named), add it as `cancellationToken: <name>`.
//
// Usage:
//   dotnet run -- <path-to-solution.sln> [--dry-run] [--imports-only] [--cancellation-only]
//
// Requires: Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.MSBuild,
// Microsoft.Build.Locator (see load-order notes at the bottom of this file — MSBuildLocator must be
// registered before any Microsoft.CodeAnalysis.MSBuild type is touched).

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace NicheImageRipper.Auxiliary;

public static class CleanupProgram
{
    private static bool _dryRun;

    public static async Task Run(string[] args)
    {
        var solutionPath = args.FirstOrDefault(a => !a.StartsWith("--"));
        _dryRun = args.Contains("--dry-run");
        var importsOnly = args.Contains("--imports-only");
        var cancellationOnly = args.Contains("--cancellation-only");

        if (solutionPath is null)
        {
            Console.WriteLine("Usage: dotnet run -- <path-to-solution.sln> [--dry-run] [--imports-only] [--cancellation-only]");
            return;
        }

        Console.WriteLine(_dryRun
            ? "Running in DRY-RUN mode — no files will be modified.\n"
            : "Running for real — files WILL be modified.\n");

        if (!MSBuildLocator.IsRegistered)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Count == 0)
            {
                Console.WriteLine("ERROR: No MSBuild instance found.");
                return;
            }

            Console.WriteLine($"Using MSBuild from: {instances[0].MSBuildPath}");
            MSBuildLocator.RegisterInstance(instances[0]);
        }

        await RunAsync(solutionPath, importsOnly, cancellationOnly);
    }

    // Isolated from Main on purpose — see notes at the bottom of this file about JIT/assembly-load ordering
    // with MSBuildLocator.
    private static async Task RunAsync(string solutionPath, bool importsOnly, bool cancellationOnly)
    {
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => Console.WriteLine($"[workspace] {e.Diagnostic}"));

        Console.WriteLine($"Loading solution: {solutionPath}");
        var solution = await workspace.OpenSolutionAsync(solutionPath);

        if (!cancellationOnly)
        {
            Console.WriteLine("\n=== Pass 1: removing duplicate using directives ===");
            solution = await RemoveDuplicateUsingsAsync(solution);
        }

        if (!importsOnly)
        {
            Console.WriteLine("\n=== Pass 2: propagating CancellationToken to eligible calls ===");
            solution = await PropagateCancellationTokensAsync(solution);
        }

        Console.WriteLine(_dryRun
            ? "\nDry run complete — no files were modified."
            : "\nDone. Build the solution and review the diff before committing.");
    }

    // =====================================================================================
    // Pass 1: duplicate using directives
    // =====================================================================================
    private static async Task<Solution> RemoveDuplicateUsingsAsync(Solution solution)
    {
        var updatedSolution = solution;
        var filesChanged = 0;

        foreach (var project in solution.Projects)
        {
            foreach (var doc in project.Documents)
            {
                if (doc.FilePath is null)
                {
                    continue;
                }

                var root = (CompilationUnitSyntax?)await doc.GetSyntaxRootAsync();
                if (root is null)
                {
                    continue;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var toRemove = new List<UsingDirectiveSyntax>();

                foreach (var usingDirective in root.Usings)
                {
                    // Normalize away trivia/whitespace differences so "using Foo;" and "using   Foo ;"
                    // are recognized as the same duplicate.
                    var key = usingDirective.Name?.ToString() ?? "";
                    var prefix = usingDirective.Alias is not null ? $"{usingDirective.Alias.Name}=" : "";
                    var staticPrefix = usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) ? "static " : "";
                    var fullKey = $"{staticPrefix}{prefix}{key}";

                    if (!seen.Add(fullKey))
                    {
                        toRemove.Add(usingDirective);
                    }
                }

                if (toRemove.Count == 0)
                {
                    continue;
                }

                var newRoot = root.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia)!;
                var formatted = newRoot.NormalizeWhitespace().ToFullString();

                Console.WriteLine($"{(_dryRun ? "[dry-run] " : "")}{Path.GetFileName(doc.FilePath)}: removed {toRemove.Count} duplicate using(s)");
                filesChanged++;

                if (!_dryRun)
                {
                    await File.WriteAllTextAsync(doc.FilePath, formatted);
                }

                updatedSolution = updatedSolution.WithDocumentText(doc.Id, Microsoft.CodeAnalysis.Text.SourceText.From(formatted));
            }
        }

        Console.WriteLine($"Pass 1 complete: {filesChanged} file(s) had duplicate usings removed.");
        return updatedSolution;
    }

    // =====================================================================================
    // Pass 2: CancellationToken propagation
    //
    // For every invocation expression inside a method/local-function/lambda body that has a
    // CancellationToken parameter in scope:
    //   - Skip if the call already passes a CancellationToken argument (positional or named).
    //   - Skip if the invoked method has no parameter of type CancellationToken at all (nothing to pass).
    //   - Skip if adding the argument would be ambiguous (more than one CancellationToken parameter on
    //     the target — flagged for manual review instead of guessed at).
    //   - Otherwise, add `cancellationToken: <name>` as a named argument.
    //
    // Uses the semantic model (not text/regex) to resolve the actual invoked symbol's parameter list,
    // so this doesn't misfire on overloads, extension methods, or calls where a CancellationToken-typed
    // argument is already present under a different parameter name.
    // =====================================================================================
    private static async Task<Solution> PropagateCancellationTokensAsync(Solution solution)
    {
        var updatedSolution = solution;
        var totalEdits = 0;
        var flaggedForReview = new List<string>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                continue;
            }

            foreach (var doc in project.Documents)
            {
                if (doc.FilePath is null)
                {
                    continue;
                }

                var root = await doc.GetSyntaxRootAsync();
                var semanticModel = await doc.GetSemanticModelAsync();
                if (root is null || semanticModel is null)
                {
                    continue;
                }

                var editor = new List<(InvocationExpressionSyntax Old, InvocationExpressionSyntax New)>();

                // Every method/local function/lambda/anonymous method that declares (or captures) a
                // CancellationToken parameter is a scope worth scanning inside.
                var candidateScopes = root.DescendantNodes()
                    .Where(n => n is MethodDeclarationSyntax or LocalFunctionStatementSyntax
                             or ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax
                             or AnonymousMethodExpressionSyntax);

                foreach (var scope in candidateScopes)
                {
                    var tokenParamName = GetCancellationTokenParameterName(scope, semanticModel);
                    if (tokenParamName is null)
                    {
                        continue; // this scope has no CancellationToken parameter to propagate
                    }

                    var body = GetScopeBody(scope);
                    if (body is null)
                    {
                        continue;
                    }

                    foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        // Don't descend into a nested scope that has its own CancellationToken parameter —
                        // that nested scope will be handled on its own iteration of candidateScopes, using
                        // its own (possibly differently-named) token.
                        if (IsInsideNestedTokenScope(invocation, scope))
                        {
                            continue;
                        }

                        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                        var method = symbolInfo.Symbol as IMethodSymbol;
                        if (method is null)
                        {
                            continue; // couldn't resolve (dynamic, error, etc.) — skip rather than guess
                        }

                        var tokenParams = method.Parameters
                            .Where(p => p.Type.ToDisplayString() == "System.Threading.CancellationToken")
                            .ToList();

                        if (tokenParams.Count == 0)
                        {
                            continue; // nothing to pass
                        }

                        if (tokenParams.Count > 1)
                        {
                            flaggedForReview.Add(
                                $"{doc.FilePath}: {invocation.ToString().Split('(')[0]} has multiple CancellationToken parameters — skipped, review manually");
                            continue;
                        }

                        var tokenParam = tokenParams[0];

                        if (InvocationAlreadyPassesToken(invocation, method, tokenParam, semanticModel))
                        {
                            continue;
                        }

                        var newArg = SyntaxFactory.Argument(
                                SyntaxFactory.IdentifierName(tokenParamName))
                            .WithNameColon(SyntaxFactory.NameColon(tokenParam.Name));

                        var newArgList = invocation.ArgumentList.AddArguments(newArg);
                        var newInvocation = invocation.WithArgumentList(newArgList);

                        editor.Add((invocation, newInvocation));
                    }
                }

                if (editor.Count == 0)
                {
                    continue;
                }

                var newRoot = root.ReplaceNodes(
                    editor.Select(e => e.Old),
                    (old, _) => editor.First(e => e.Old == old).New);

                var formatted = newRoot.NormalizeWhitespace().ToFullString();

                Console.WriteLine($"{(_dryRun ? "[dry-run] " : "")}{Path.GetFileName(doc.FilePath)}: added cancellationToken to {editor.Count} call(s)");
                totalEdits += editor.Count;

                if (!_dryRun)
                {
                    await File.WriteAllTextAsync(doc.FilePath, formatted);
                }

                updatedSolution = updatedSolution.WithDocumentText(doc.Id, Microsoft.CodeAnalysis.Text.SourceText.From(formatted));
            }
        }

        Console.WriteLine($"Pass 2 complete: {totalEdits} call(s) updated.");

        if (flaggedForReview.Count > 0)
        {
            Console.WriteLine($"\n--- FLAGGED FOR MANUAL REVIEW ({flaggedForReview.Count}) ---");
            foreach (var item in flaggedForReview)
            {
                Console.WriteLine($"  {item}");
            }
        }

        return updatedSolution;
    }

    private static string? GetCancellationTokenParameterName(SyntaxNode scope, SemanticModel semanticModel)
    {
        var parameters = scope switch
        {
            MethodDeclarationSyntax m => m.ParameterList.Parameters,
            LocalFunctionStatementSyntax l => l.ParameterList.Parameters,
            ParenthesizedLambdaExpressionSyntax p => p.ParameterList.Parameters,
            AnonymousMethodExpressionSyntax a => a.ParameterList?.Parameters ?? default,
            _ => default
        };

        if (parameters.Equals(default))
        {
            return null; // e.g. a SimpleLambdaExpressionSyntax (single unparenthesized param) — never CancellationToken-typed implicitly
        }

        foreach (var param in parameters)
        {
            var typeInfo = param.Type is null ? default : semanticModel.GetTypeInfo(param.Type);
            if (typeInfo.Type?.ToDisplayString() == "System.Threading.CancellationToken")
            {
                return param.Identifier.Text;
            }
        }

        return null;
    }

    private static SyntaxNode? GetScopeBody(SyntaxNode scope) => scope switch
    {
        MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
        LocalFunctionStatementSyntax l => (SyntaxNode?)l.Body ?? l.ExpressionBody,
        ParenthesizedLambdaExpressionSyntax p => p.Body,
        SimpleLambdaExpressionSyntax s => s.Body,
        AnonymousMethodExpressionSyntax a => a.Body,
        _ => null
    };

    private static bool IsInsideNestedTokenScope(SyntaxNode invocation, SyntaxNode outerScope)
    {
        var current = invocation.Parent;
        while (current is not null && current != outerScope)
        {
            if (current is MethodDeclarationSyntax or LocalFunctionStatementSyntax
                or ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax
                or AnonymousMethodExpressionSyntax)
            {
                return true; // there's a nested scope between this invocation and the outer scope
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool InvocationAlreadyPassesToken(
        InvocationExpressionSyntax invocation, IMethodSymbol method, IParameterSymbol tokenParam,
        SemanticModel semanticModel)
    {
        var args = invocation.ArgumentList.Arguments;

        // Named: cancellationToken: something
        if (args.Any(a => a.NameColon?.Name.Identifier.Text == tokenParam.Name))
        {
            return true;
        }

        // Positional: an argument sits in tokenParam's position (accounting for named args elsewhere
        // and params/optional parameters before it) and its type is CancellationToken.
        var positionalIndex = 0;
        foreach (var arg in args)
        {
            if (arg.NameColon is not null)
            {
                continue; // already named, not positional
            }

            if (positionalIndex == tokenParam.Ordinal)
            {
                var argType = semanticModel.GetTypeInfo(arg.Expression).Type;
                return argType?.ToDisplayString() == "System.Threading.CancellationToken";
            }

            positionalIndex++;
        }

        // Also: any argument (named to a different CancellationToken-typed parameter, if the method
        // somehow had another one — already excluded by the caller's tokenParams.Count > 1 check, so
        // this is just a defensive fallback) whose resolved parameter is the token parameter.
        return false;
    }
}

// -----------------------------------------------------------------------------------------
// MSBuildLocator / MSBuildWorkspace notes (same as the earlier migration tool):
//
// - MSBuildLocator.RegisterInstance/RegisterDefaults must run before any method containing a reference
//   to Microsoft.CodeAnalysis.MSBuild types is JIT-compiled. That's why RunAsync is a separate method
//   from Main — keeps Main itself free of any such reference until after registration.
// - .csproj needs:
//     <PackageReference Include="Microsoft.Build.Locator" Version="1.11.2" />
//     <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.6.0" />
//     <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.6.0" PrivateAssets="all" />
//     <PackageReference Include="Microsoft.Build.Framework" Version="<version from build error>" ExcludeAssets="runtime" PrivateAssets="all" />
//   (add any other Microsoft.Build.* packages the build complains about with the same ExcludeAssets="runtime")
// -----------------------------------------------------------------------------------------