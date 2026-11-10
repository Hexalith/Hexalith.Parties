namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

using Shouldly;

/// <summary>Exercises actual local diagnostic file boundaries without simulating a CI or native owner.</summary>
public sealed class NativeAssemblyObservationTests
{
    /// <summary>Preserves the original private diagnostic and refuses a duplicate write.</summary>
    [Fact]
    public void PrivateDiagnosticRetainsFirstBytesAndRefusesOverwrite()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This file boundary control requires Linux.");
        string directory = Directory.CreateTempSubdirectory("parties-diagnostic-io-control-").FullName;
        try
        {
            const string fileIdentity = "local-io-control";
            const string observation = "{\"scope\":\"local-file-control-no-native-credit\"}";
            NativeAssemblyObservations.WritePrivateObservation(directory, fileIdentity, observation);
            string path = Path.Combine(directory, "loaded-assemblies-" + fileIdentity + ".json");
            byte[] original = File.ReadAllBytes(path);
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Should.Throw<IOException>(() => NativeAssemblyObservations.WritePrivateObservation(directory, fileIdentity, "replacement"));
            File.ReadAllBytes(path).ShouldBe(original);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Refuses a linked directory before creating any diagnostic file.</summary>
    [Fact]
    public void PrivateDiagnosticRefusesSymbolicDirectory()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This file boundary control requires Linux.");
        string directory = Directory.CreateTempSubdirectory("parties-diagnostic-link-control-").FullName;
        try
        {
            string target = Directory.CreateDirectory(Path.Combine(directory, "target")).FullName;
            string linked = Path.Combine(directory, "linked");
            Directory.CreateSymbolicLink(linked, target);
            Should.Throw<IOException>(() => NativeAssemblyObservations.WritePrivateObservation(linked, "local-io-control", "{}"));
            Directory.GetFiles(target).ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    /// <summary>Separates copied-file metadata from loaded identity and refuses a replaced disk module.</summary>
    [Fact]
    public void CopiedOutputReportsActualFileHashWithoutLoadingIt()
    {
        string directory = Directory.CreateTempSubdirectory("parties-local-output-control-").FullName;
        try
        {
            string path = Path.Combine(directory, "Hexalith.LocalControl.dll");
            File.Copy(typeof(JsonDocument).Assembly.Location, path);
            AppDomain.CurrentDomain.GetAssemblies().Any(value => !value.IsDynamic && value.Location == path).ShouldBeFalse();
            object[] observations = NativeAssemblyObservations.ReadCompositionOutput(path);
            AppDomain.CurrentDomain.GetAssemblies().Any(value => !value.IsDynamic && value.Location == path).ShouldBeFalse();
            using JsonDocument output = JsonDocument.Parse(JsonSerializer.Serialize(observations));
            JsonElement observed = output.RootElement.EnumerateArray().Single();
            observed.GetProperty("scope").GetString().ShouldBe("copied-native-composition-output");
            observed.GetProperty("copiedPath").GetString().ShouldBe(path);
            observed.GetProperty("sha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));

            AssemblyLoadContext context = new("native-module-identity-control", isCollectible: true);
            try
            {
                Assembly loaded = context.LoadFromAssemblyPath(path);
                using JsonDocument matching = JsonDocument.Parse(JsonSerializer.Serialize(NativeAssemblyObservations.ReadLoadedAssembly(loaded)));
                matching.RootElement.GetProperty("moduleVersionId").GetGuid().ShouldBe(loaded.ManifestModule.ModuleVersionId);
                matching.RootElement.GetProperty("sha256").GetString().ShouldBe(observed.GetProperty("sha256").GetString());
                matching.RootElement.GetProperty("scope").GetString().ShouldBe("loaded-native-test-process");

                // Replace the directory entry, preserving the inode mapped by the live assembly.
                string replacement = Path.Combine(directory, "replacement.dll");
                File.Copy(typeof(Enumerable).Assembly.Location, replacement);
                typeof(Enumerable).Assembly.ManifestModule.ModuleVersionId.ShouldNotBe(loaded.ManifestModule.ModuleVersionId);
                File.Move(replacement, path, overwrite: true);
                Should.Throw<InvalidDataException>(() => NativeAssemblyObservations.ReadLoadedAssembly(loaded));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Refuses a linked copied output rather than presenting another file as native output.</summary>
    [Fact]
    public void CopiedOutputRefusesLinkedAssembly()
    {
        string directory = Directory.CreateTempSubdirectory("parties-linked-output-control-").FullName;
        try
        {
            string path = Path.Combine(directory, "Hexalith.LocalControl.dll");
            File.CreateSymbolicLink(path, typeof(NativeAssemblyObservationTests).Assembly.Location);
            Should.Throw<InvalidDataException>(() => NativeAssemblyObservations.ReadCompositionOutput(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

}
