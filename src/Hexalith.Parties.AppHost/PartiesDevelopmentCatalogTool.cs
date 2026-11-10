using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.Parties.AppHost;

/// <summary>Invokes the existing Platform catalog executable over immutable copies of prepared owner assemblies.</summary>
internal static class PartiesDevelopmentCatalogTool
{
    /// <summary>Builds the existing tool in a private directory and returns its actual catalog receipt.</summary>
    /// <param name="repositoryRoot">The owning Parties checkout.</param>
    /// <param name="directory">The private directory owned by this activation attempt.</param>
    /// <param name="instanceId">The bounded catalog instance identity.</param>
    /// <param name="gatewayAppId">The actual gateway application identity.</param>
    /// <param name="sdkVersion">The exact published SDK cohort prepared for every host.</param>
    /// <param name="owners">The prepared declaration and adapter assemblies with actual receiver identities.</param>
    /// <param name="token">The bounded startup lifetime.</param>
    internal static async Task<JsonDocument> GenerateAsync(DirectoryInfo repositoryRoot, string directory,
        string instanceId, string gatewayAppId, string sdkVersion,
        IReadOnlyList<(string OwnerPath, string AdapterPath, string AppId)> owners, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        if (owners.Count == 0 || owners.Any(owner => !Path.IsPathFullyQualified(owner.OwnerPath)
            || !Path.IsPathFullyQualified(owner.AdapterPath) || string.IsNullOrWhiteSpace(owner.AppId)))
        {
            throw new InvalidDataException("Exact prepared owner/adapter assemblies and application identities are required.");
        }
        string template = ReferenceProjectResolver.ResolveRequired(repositoryRoot, "Hexalith.Platform",
            Path.Combine("src", "Hexalith.Platform.CatalogBuilder", "Hexalith.Platform.CatalogBuilder.csproj"));
        string build = Path.Combine(directory, "reader");
        CreatePrivateDirectory(build);
        File.Copy(Path.Combine(repositoryRoot.FullName, "global.json"), Path.Combine(build, "global.json"));
        string project = Path.Combine(build, "Hexalith.Platform.CatalogBuilder.csproj");
        XDocument xml = XDocument.Load(template);
        xml.Root!.AddFirst(new XElement("Import", new XAttribute("Project", Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"))));
        xml.Root.AddFirst(new XElement("Import", new XAttribute("Project", Path.Combine(repositoryRoot.FullName, "Directory.Build.props"))));
        XElement references = new("ItemGroup");
        Dictionary<string, string> copied = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> inputHashes = new(StringComparer.Ordinal);
        foreach (string source in owners.SelectMany(owner => new[] { owner.OwnerPath, owner.AdapterPath }).Distinct(StringComparer.Ordinal))
        {
            string name = AssemblyName.GetAssemblyName(source).Name!;
            string destination = Path.Combine(build, "owners", name);
            CreatePrivateDirectory(destination);
            foreach (string dependency in Directory.EnumerateFiles(Path.GetDirectoryName(source)!, "*.dll"))
            {
                string filename = Path.GetFileName(dependency);
                byte[] bytes = await File.ReadAllBytesAsync(dependency, token).ConfigureAwait(false);
                string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                inputHashes[dependency] = hash;
                string target = Path.Combine(destination, filename);
                await File.WriteAllBytesAsync(target, bytes, token).ConfigureAwait(false);
                if (!copied.TryAdd(filename, target)
                    && !File.ReadAllBytes(copied[filename]).AsSpan().SequenceEqual(File.ReadAllBytes(target)))
                {
                    throw new InvalidDataException("Prepared catalog owners contain different copies of " + filename + ".");
                }
            }
            references.Add(new XElement("Reference", new XAttribute("Include", name),
                new XElement("HintPath", Path.Combine(destination, Path.GetFileName(source)))));
        }
        string bindings = Path.Combine(build, "bindings.props");
        new XDocument(new XElement("Project", references)).Save(bindings);
        xml.Save(project);
        foreach (string source in Directory.EnumerateFiles(Path.GetDirectoryName(template)!, "*.cs"))
        {
            byte[] bytes = await File.ReadAllBytesAsync(source, token).ConfigureAwait(false);
            inputHashes[source] = Convert.ToHexStringLower(SHA256.HashData(bytes));
            await File.WriteAllBytesAsync(Path.Combine(build, Path.GetFileName(source)), bytes, token).ConfigureAwait(false);
        }
        string output = Path.Combine(build, "output");
        await RunAsync(build, "build.log", ["build", project, "-c", "Release", "-m:1", "--disable-build-servers", "-nr:false", "-p:UseSharedCompilation=false", "--output", output,
            "-p:GeneratePackageOnBuild=false", "-p:CentralPackageVersionsFileImported=true",
            "-p:UseHexalithProjectReferences=false", "-p:HexalithEventStoreVersion=" + sdkVersion,
            "-p:PlatformCatalogBindings=" + bindings, "--nologo", "-warnaserror"], token).ConfigureAwait(false);
        using (JsonDocument assets = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(build, "obj", "project.assets.json"), token).ConfigureAwait(false)))
        {
            JsonProperty[] sdk = assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Where(library => library.Name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal)).ToArray();
            if (sdk.Length == 0 || sdk.Any(library => library.Name[(library.Name.LastIndexOf('/') + 1)..] != sdkVersion
                || library.Value.GetProperty("type").GetString() != "package"))
            {
                throw new InvalidDataException("The actual private catalog reader did not restore the exact published SDK cohort.");
            }
        }
        foreach ((string filename, string source) in copied)
        {
            string target = Path.Combine(output, filename);
            if (File.Exists(target))
            {
                if (!File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(target)))
                {
                    throw new InvalidDataException("The catalog reader restored a different prepared runtime binary: " + filename + ".");
                }
            }
            else
            {
                File.Copy(source, target);
            }
        }
        string request = Path.Combine(build, "request.json");
        await File.WriteAllBytesAsync(request, JsonSerializer.SerializeToUtf8Bytes(new
        {
            generation = 1, instanceId, outputDirectory = directory, gatewayAppId,
            owners = owners.Select(owner => new
            {
                assembly = AssemblyName.GetAssemblyName(owner.OwnerPath).Name!,
                adapterAssembly = AssemblyName.GetAssemblyName(owner.AdapterPath).Name!,
                appId = owner.AppId,
            }),
        }), token).ConfigureAwait(false);
        await RunAsync(build, "generation.log", [Path.Combine(output, "Hexalith.Platform.CatalogBuilder.dll"), request], token).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(build, "preparation.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            instanceId, gatewayAppId, sdkVersion,
            templateSource = template,
            templateHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(build, "Program.cs")))),
            inputHashes,
        }), token).ConfigureAwait(false);
        return JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "receipt.json"), token).ConfigureAwait(false));
    }

    /// <summary>Creates a private run-owned directory without broadening existing permissions.</summary>
    internal static void CreatePrivateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            throw new InvalidDataException("A catalog preparation directory is already occupied.");
        }
        if (OperatingSystem.IsWindows()) { Directory.CreateDirectory(path); }
        else { Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }

    /// <summary>Admits a fully initialized private directory atomically and removes stopped failed preparation.</summary>
    /// <param name="path">The final private preparation directory.</param>
    /// <param name="owner">The marker identifying this invocation.</param>
    /// <param name="prepare">Preparation that may begin only after ownership admission.</param>
    /// <param name="initializeMarker">An optional controlled marker writer for initialization failure tests.</param>
    internal static async Task RunOwnedPreparationAsync(string path, string owner, Func<Task> prepare,
        Action<StreamWriter, string>? initializeMarker = null)
    {
        if (Directory.Exists(path))
        {
            throw new InvalidDataException("A catalog preparation directory is already occupied.");
        }
        string staging = path + ".preparing-" + Guid.NewGuid().ToString("N");
        bool staged = false;
        bool admitted = false;
        try
        {
            CreatePrivateDirectory(staging);
            staged = true;
            using (var marker = new StreamWriter(new FileStream(Path.Combine(staging, ".owner"), FileMode.CreateNew)))
            {
                if (initializeMarker is null) { marker.Write(owner); }
                else { initializeMarker(marker, owner); }
            }
            Directory.Move(staging, path);
            admitted = true;
            await prepare().ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            try
            {
                if (staged && !admitted) { Directory.Delete(staging, recursive: true); }
                else if (admitted && !failure.Data.Contains("CatalogOwnedProcessStillRunning")) { DeleteOwnedDirectory(path, owner); }
            }
            catch (Exception cleanup)
            {
                failure.Data["CatalogOwnedDirectoryCleanupFailure"] = cleanup;
            }
            throw;
        }
    }

    /// <summary>Deletes only a preparation directory whose private ownership marker still matches this run.</summary>
    internal static void DeleteOwnedDirectory(string path, string owner)
    {
        if (!Directory.Exists(path)) { return; }
        string marker = Path.Combine(path, ".owner");
        if (!File.Exists(marker) || File.ReadAllText(marker) != owner)
        {
            throw new InvalidDataException("A foreign preparation directory must not be deleted.");
        }
        Directory.Delete(path, recursive: true);
    }

    private static Task RunAsync(string directory, string log, IReadOnlyList<string> arguments, CancellationToken token)
    {
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        ConfigurePublishedPackageProcess(start);
        return RunProcessAsync(start, Path.Combine(directory, log), token);
    }

    /// <summary>Keeps intentional private package builds independent of a parent's mapped source build.</summary>
    /// <param name="start">The isolated published-package process configuration.</param>
    internal static void ConfigurePublishedPackageProcess(ProcessStartInfo start)
    {
        foreach (string name in new[]
        {
            "CustomBeforeDirectoryBuildProps", "CustomAfterMicrosoftCommonProps",
            "CustomBeforeDirectoryBuildTargets", "CustomAfterDirectoryBuildTargets",
            "AfterMicrosoftNETSdkTargets", "HEXALITH_SOURCE_MAPPING_HASH", "Configuration",
        })
        {
            start.Environment.Remove(name);
        }
    }

    /// <summary>Drains both owned process streams while retaining only bounded diagnostic tails.</summary>
    /// <param name="start">The explicitly selected owned executable.</param>
    /// <param name="log">The owned diagnostic file.</param>
    /// <param name="token">The bounded preparation lifetime.</param>
    internal static async Task RunProcessAsync(ProcessStartInfo start, string log, CancellationToken token)
    {
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The actual catalog executable did not start.");
        Task<string> output = ReadBoundedDiagnosticsAsync(process.StandardOutput, token);
        Task<string> error = ReadBoundedDiagnosticsAsync(process.StandardError, token);
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellation)
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(30));
            try
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                if (!process.HasExited)
                {
                    cancellation.Data["CatalogOwnedProcessStillRunning"] = true;
                    cancellation.Data["CatalogOwnedProcessCleanupFailure"] = failure;
                }
            }
            throw;
        }
        string diagnostics = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
        await File.WriteAllTextAsync(log, diagnostics, token).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            // The owned preparation directory is removed on failure; retain bounded details in the exception.
            throw new InvalidDataException("The actual Platform catalog tool refused preparation: "
                + diagnostics[^Math.Min(diagnostics.Length, 4096)..]);
        }
    }

    private static async Task<string> ReadBoundedDiagnosticsAsync(StreamReader reader, CancellationToken token)
    {
        const int Capacity = 4096;
        char[] buffer = new char[Capacity];
        StringBuilder retained = new(Capacity);
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            int discard = Math.Max(0, retained.Length + count - Capacity);
            if (discard != 0) { retained.Remove(0, discard); }
            retained.Append(buffer, 0, count);
        }
        return retained.ToString();
    }

}
