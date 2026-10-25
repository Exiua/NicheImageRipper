// CheckSharedAssembliesProgram.cs — Auxiliary project.
//
// Loads a built NicheImageRipper.Sdk.dll via reflection, walks every public type's public-facing
// surface (base types, interfaces, method params/returns, property/field types, ctor params) and
// collects which external assemblies those types come from. Anything whose types cross the Sdk
// boundary must load once, shared, in the Default AssemblyLoadContext — a plugin loading its own
// copy would produce types that fail identity checks (is/as/pattern-match) against Core's or another
// plugin's copy of the "same" type. This reports that surface and diffs it against the hardcoded
// SharedAssemblyNames list in ModuleLoadContext.cs. Report-only — never edits ModuleLoadContext.cs.
//
// Usage:
//   dotnet run -- check-shared-assemblies <path-to-NicheImageRipper.Sdk.dll> [path-to-ModuleLoadContext.cs]
//
// Requires: no extra packages beyond System.Reflection (already available) — unlike CleanupProgram,
// this doesn't touch MSBuildWorkspace, so none of that registration-order concern applies here.

using System.Text.RegularExpressions;

namespace NicheImageRipper.Auxiliary;

public static class CheckSharedAssembliesProgram
{
    public static Task<int> Run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run -- check-shared-assemblies <path-to-NicheImageRipper.Sdk.dll> [path-to-ModuleLoadContext.cs]");
            return Task.FromResult(1);
        }

        var sdkPath = args[0];
        var moduleLoadContextPath = args.Length > 1 ? args[1] : null;

        var sdkAssembly = System.Reflection.Assembly.LoadFrom(sdkPath);

        var externalAssemblies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var perAssemblyExamples = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        CollectExternalAssemblies(sdkAssembly, externalAssemblies, perAssemblyExamples);

        Console.WriteLine("External assemblies referenced by Sdk's public surface:");
        Console.WriteLine();
        foreach (var name in externalAssemblies)
        {
            Console.WriteLine($"  {name}");
            foreach (var example in perAssemblyExamples[name])
            {
                Console.WriteLine($"      via {example}");
            }
        }

        if (moduleLoadContextPath is null || !File.Exists(moduleLoadContextPath))
        {
            Console.WriteLine();
            Console.WriteLine("(No ModuleLoadContext.cs path given/found — skipping diff against SharedAssemblyNames.)");
            return Task.FromResult(0);
        }

        var exitCode = DiffAgainstSharedAssemblyNames(moduleLoadContextPath, externalAssemblies);
        return Task.FromResult(exitCode);
    }

    private static void CollectExternalAssemblies(
        System.Reflection.Assembly sdkAssembly,
        SortedSet<string> externalAssemblies,
        Dictionary<string, List<string>> perAssemblyExamples)
    {
        void Note(Type? type, string context)
        {
            if (type is null)
            {
                return;
            }

            if (type.IsArray)
            {
                Note(type.GetElementType(), context);
                return;
            }

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    Note(arg, context);
                }
            }

            var asm = type.Assembly;
            var name = asm.GetName().Name;
            if (asm == sdkAssembly || name is null)
            {
                return;
            }

            if (name.StartsWith("System.") || name.StartsWith("Microsoft.CSharp") || name is "mscorlib" or "netstandard")
            {
                return; // Always-shared runtime assemblies, not worth tracking
            }

            externalAssemblies.Add(name);
            if (!perAssemblyExamples.TryGetValue(name, out var examples))
            {
                examples = [];
                perAssemblyExamples[name] = examples;
            }

            if (/*examples.Count < 5 && */!examples.Contains(context))
            {
                examples.Add(context);
            }
        }

        foreach (var type in sdkAssembly.GetExportedTypes())
        {
            Note(type.BaseType, $"{type.FullName} (base type)");
            foreach (var iface in type.GetInterfaces())
            {
                Note(iface, $"{type.FullName} (interface)");
            }

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
                                                         | System.Reflection.BindingFlags.NonPublic
                                                         | System.Reflection.BindingFlags.Instance
                                                         | System.Reflection.BindingFlags.Static
                                                         | System.Reflection.BindingFlags.DeclaredOnly;

            static bool IsExposedToSubclasses(System.Reflection.MethodBase? method) =>
                method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
            
            foreach (var member in type.GetMembers(flags))
            {
                switch (member)
                {
                    case System.Reflection.MethodInfo { IsSpecialName: false } method when IsExposedToSubclasses(method):
                        Note(method.ReturnType, $"{type.FullName}.{method.Name}() return");
                        foreach (var p in method.GetParameters())
                        {
                            Note(p.ParameterType, $"{type.FullName}.{method.Name}({p.Name})");
                        }

                        break;
                    case System.Reflection.PropertyInfo prop
                        when IsExposedToSubclasses(prop.GetMethod) || IsExposedToSubclasses(prop.SetMethod):
                        Note(prop.PropertyType, $"{type.FullName}.{prop.Name}");
                        break;
                    case System.Reflection.FieldInfo field when field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly:
                        Note(field.FieldType, $"{type.FullName}.{field.Name}");
                        break;
                    case System.Reflection.ConstructorInfo ctor when IsExposedToSubclasses(ctor):
                        foreach (var p in ctor.GetParameters())
                        {
                            Note(p.ParameterType, $"{type.FullName}(..{p.Name}..)");
                        }

                        break;
                }
            }
        }
    }

    private static int DiffAgainstSharedAssemblyNames(string moduleLoadContextPath, SortedSet<string> externalAssemblies)
    {
        // Pull the current hardcoded list out of ModuleLoadContext.cs by scanning for quoted strings
        // inside the SharedAssemblyNames initializer block. Report-only — never rewrites the file,
        // same reasoning as CleanupProgram avoiding blind regex edits to C# source.
        var source = File.ReadAllText(moduleLoadContextPath);
        var blockMatch = Regex.Match(source, @"SharedAssemblyNames\s*=.*?\{(.*?)\};", RegexOptions.Singleline);
        if (!blockMatch.Success)
        {
            Console.WriteLine();
            Console.WriteLine("Could not locate SharedAssemblyNames initializer in the given file — skipping diff.");
            return 0;
        }

        var currentNames = Regex.Matches(blockMatch.Groups[1].Value, "\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = externalAssemblies.Except(currentNames, StringComparer.OrdinalIgnoreCase).ToList();
        var stale = currentNames.Except(externalAssemblies, StringComparer.OrdinalIgnoreCase).ToList();

        Console.WriteLine();
        Console.WriteLine("Diff against current SharedAssemblyNames:");
        if (missing.Count == 0 && stale.Count == 0)
        {
            Console.WriteLine("  Up to date.");
            return 0;
        }

        foreach (var name in missing)
        {
            Console.WriteLine($"  MISSING (add to SharedAssemblyNames): {name}");
        }

        foreach (var name in stale)
        {
            Console.WriteLine($"  UNUSED (safe to consider removing, but verify): {name}");
        }

        return missing.Count > 0 ? 1 : 0;
    }
}