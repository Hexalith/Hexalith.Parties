#:property PublishAot=false
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

// Invoke the pinned producer's resolver and materializer; this helper selects no roots.
string toolDirectory = Path.GetFullPath(args[0]);
string manifest = Path.GetFullPath(args[1]);
string workspace = Path.GetFullPath(args[2]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string dependency = Path.Combine(toolDirectory, name.Name + ".dll");
    return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
};
Assembly producer = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(toolDirectory, "Hexalith.Builds.Tooling.dll"));
object mapping;
if (args.Length == 4)
{
    Type mappingType = producer.GetType("Hexalith.Builds.Tooling.Workspace.SourceMapping", throwOnError: true)!;
    mapping = JsonSerializer.Deserialize(await File.ReadAllTextAsync(args[3]), mappingType)!;
    string expected = (string)mappingType.GetProperty("ContentHash")!.GetValue(mapping)!;
    mapping = mappingType.GetMethod("LoadVerified")!.Invoke(null, [args[3], expected])!;
}
else
{
    Type modeType = producer.GetType("Hexalith.Builds.Tooling.Workspace.WorkspaceMode", throwOnError: true)!;
    Type resolver = producer.GetType("Hexalith.Builds.Tooling.Workspace.WorkspaceRootResolver", throwOnError: true)!;
    MethodInfo resolve = resolver.GetMethod("ResolveAsync", [typeof(string), modeType, typeof(CancellationToken)])!;
    Task resolution = (Task)resolve.Invoke(null, [manifest, Enum.Parse(modeType, "Source"), CancellationToken.None])!;
    await resolution;
    mapping = resolution.GetType().GetProperty("Result")!.GetValue(resolution)!;
}
Type materializer = producer.GetType("Hexalith.Builds.Tooling.Workspace.SourceMappingMaterializer", throwOnError: true)!;
await (Task)materializer.GetMethod("WriteAsync")!.Invoke(null, [mapping, workspace, CancellationToken.None])!;
object environment = materializer.GetMethod("Environment")!.Invoke(null, [mapping, workspace])!;
await File.WriteAllTextAsync(Path.Combine(workspace, "source-environment.json"), JsonSerializer.Serialize(environment, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(args.Length == 4 ? "Pinned historical source mapping materialized." : "Pinned current source mapping regenerated.");
