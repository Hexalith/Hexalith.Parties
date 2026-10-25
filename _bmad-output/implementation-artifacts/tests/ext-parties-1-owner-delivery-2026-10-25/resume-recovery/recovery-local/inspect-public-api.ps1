$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
public static class InspectPartiesApi
{
    public static void Run()
    {
        string directory = "/tmp/ext-parties-1-owner-recovery-20261025-r1/artifacts/bin/Hexalith.Parties.Contracts.Tests/debug";
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            string path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Hexalith.Parties.Contracts.Tests.dll"));
        var type = assembly.GetType("Hexalith.Parties.Contracts.Tests.Package.ContractsPublicApiSnapshotTests", true);
        var method = type.GetMethod("BuildPublicApiSnapshot", BindingFlags.NonPublic | BindingFlags.Static);
        var lines = (System.Collections.Generic.IReadOnlyList<string>)method.Invoke(null, Array.Empty<object>());
        File.WriteAllText("/tmp/ext-parties-1-owner-recovery-20261025-r1/actual-public-api.txt", string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
'@
[InspectPartiesApi]::Run()
