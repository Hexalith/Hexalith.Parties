using System.Text.Json;

using Hexalith.EventStore.Testing;

namespace Hexalith.Parties.Platform.AttachedLifecycleTests;

/// <summary>Exercises real attached fixture clients under the runner's original finite hold.</summary>
public sealed class AttachedLifecycleQualificationTests
{
    /// <summary>Publishes readiness only after bounded real-service reads, then awaits the exact controller.</summary>
    /// <returns>The explicitly controlled first outcome, or the original hold's cancellation.</returns>
    [Fact]
    public async Task ReadRealServicesThenObserveAttachedControl()
    {
        using TestEnvironmentDescriptor descriptor = TestEnvironmentDescriptorReader.ReadFromEnvironment();
        Assert.NotNull(descriptor.AttachmentExpiresAtUtc);
        Assert.NotNull(descriptor.OriginalAttachmentRequestId);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, descriptor.RequestCancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(240));
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hexalith-builds", "ci-qualification", descriptor.RunId);
        string control = Path.Combine(directory, "control.json");
        while (!File.Exists(control))
            await Task.Delay(TimeSpan.FromMilliseconds(100), bound.Token).ConfigureAwait(true);
        _ = await ActionAsync(control, descriptor, bound.Token).ConfigureAwait(true);
        using HttpClient tenants = descriptor.CreateHttpClient("tenants-api", "tenants", "http", TimeSpan.FromSeconds(10));
        using HttpClient memories = descriptor.CreateDaprInvocationClient("memories", "memories", "dapr-invocation", TimeSpan.FromSeconds(10));
        await ReadServicesAsync(tenants, memories, descriptor.SyntheticTenantId, bound.Token).ConfigureAwait(true);
        string marker = JsonSerializer.Serialize(new
        {
            runId = descriptor.RunId, ownerRunId = descriptor.OwnerRunId, environmentId = descriptor.EnvironmentId,
            originalAttachmentRequestId = descriptor.OriginalAttachmentRequestId, expiresAtUtc = descriptor.AttachmentExpiresAtUtc,
            profile = "attached-lifecycle-package", processId = Environment.ProcessId, observedAtUtc = DateTimeOffset.UtcNow,
            realServiceReadsPassed = true,
        });
        string temporary = Path.Combine(directory, ".execution-ready-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, marker, bound.Token).ConfigureAwait(true);
            bound.Token.ThrowIfCancellationRequested();
            File.Move(temporary, Path.Combine(directory, "execution-ready.json"));
        }
        finally { File.Delete(temporary); }
        while (true)
        {
            string action = await ActionAsync(control, descriptor, bound.Token).ConfigureAwait(true);
            Assert.True(action != "fail", "HEXALITH_ATTACHED_LIFECYCLE_INTENTIONAL_FAILURE:" + descriptor.RunId);
            await ReadServicesAsync(tenants, memories, descriptor.SyntheticTenantId, bound.Token).ConfigureAwait(true);
            if (action == "pass") return;
            await Task.Delay(TimeSpan.FromSeconds(1), bound.Token).ConfigureAwait(true);
        }
    }

    private static async Task ReadServicesAsync(HttpClient tenants, HttpClient memories, string tenant, CancellationToken token)
    {
        using HttpResponseMessage tenantRead = await tenants.GetAsync("/api/tenants/" + Uri.EscapeDataString(tenant), token).ConfigureAwait(false);
        tenantRead.EnsureSuccessStatusCode();
        using HttpResponseMessage memoryRead = await memories.GetAsync("tenants/" + Uri.EscapeDataString(tenant), token).ConfigureAwait(false);
        memoryRead.EnsureSuccessStatusCode();
    }

    private static async Task<string> ActionAsync(string path, TestEnvironmentDescriptor descriptor, CancellationToken token)
    {
        FileInfo file = new(path);
        Assert.True(file.Exists && file.LinkTarget is null && file.Length is > 0 and <= 4096, "Missing or unsafe explicit attached control.");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false));
        JsonElement value = document.RootElement;
        Assert.Equal(descriptor.RunId, value.GetProperty("runId").GetString());
        Assert.Equal(descriptor.OwnerRunId, value.GetProperty("ownerRunId").GetString());
        Assert.Equal(descriptor.EnvironmentId, value.GetProperty("environmentId").GetString());
        Assert.Equal(descriptor.OriginalAttachmentRequestId, value.GetProperty("originalAttachmentRequestId").GetString());
        string action = value.GetProperty("action").GetString() ?? string.Empty;
        Assert.Contains(action, new[] { "wait", "pass", "fail" });
        return action;
    }
}
