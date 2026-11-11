using System.Collections.Concurrent;
using System.Globalization;

using Hexalith.Parties.IntegrationTests.HealthChecks;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Captures the complete descendant tree used to qualify ordinary launcher stop timing.</summary>
/// <param name="enumerateTasks">Optional process task-directory reader for fault controls.</param>
/// <param name="readText">Optional process text reader for fault controls.</param>
/// <param name="readIdentity">Optional process start-identity reader for fault controls.</param>
internal sealed class OrdinaryLauncherProcessCapture(
    Func<int, string[]>? enumerateTasks = null,
    Func<string, string>? readText = null,
    Func<int, long?>? readIdentity = null)
{
    private readonly Func<int, string[]> _enumerateTasks = enumerateTasks ?? (id => Directory.GetDirectories($"/proc/{id}/task"));
    private readonly Func<string, string> _readText = readText ?? File.ReadAllText;
    private readonly Func<int, long?> _readIdentity = readIdentity ?? PartiesAspireTopologyFixture.ReadWorkerStartIdentity;
    private readonly ConcurrentQueue<string> _failures = new();

    /// <summary>Gets retained process-read diagnostics, including failures in otherwise populated trees.</summary>
    internal string[] Failures => _failures.ToArray();

    /// <summary>Captures descendants without allowing one unreadable process to abandon siblings or owned teardown.</summary>
    /// <param name="parent">Launcher process identifier.</param>
    /// <param name="resource">DCP launcher resource name.</param>
    /// <param name="workers">Observed workers and their start identities.</param>
    internal void CaptureDescendants(int parent, string resource,
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers)
        => CaptureDescendants(parent, resource, workers, [], workers.TryGetValue(parent, out var launcher) ? launcher.Started : null);

    /// <summary>Returns every reason that the current capture cannot qualify stop timing.</summary>
    /// <param name="launchers">Observed launcher processes.</param>
    /// <param name="workers">Observed descendants and start identities.</param>
    /// <returns>Read failures and missing required per-launcher roles.</returns>
    internal string[] QualificationFailures(ConcurrentDictionary<string, int> launchers,
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers)
    {
        List<string> failures = [.. Failures];
        foreach ((string resource, int launcher) in launchers)
        {
            foreach (string role in new[] { "cli", "daprd" })
            {
                if (!workers.Any(worker => worker.Key != launcher && worker.Value.Resource == resource && worker.Value.Role == role))
                {
                    failures.Add($"{resource}: no {role} descendant with a live start identity was captured");
                }
            }
        }

        return [.. failures];
    }

    private void CaptureDescendants(int parent, string resource,
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers, HashSet<(int Id, long Started)> visited, long? expected)
    {
        string[] tasks;
        long started;
        try
        {
            if (_readIdentity(parent) is not long identity || expected is { } prior && prior != identity)
            {
                _failures.Enqueue($"{resource}: pid {parent} lifetime changed before descendant enumeration");
                return;
            }
            started = identity;
            if (!visited.Add((parent, started))) { return; }
            tasks = _enumerateTasks(parent);
            if (tasks.Length == 0) { _failures.Enqueue($"{resource}: empty task enumeration for pid {parent}"); }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _failures.Enqueue($"{resource}: task enumeration for pid {parent}: {exception}");
            return;
        }

        foreach (string task in tasks)
        {
            string[] children;
            try { children = _readText(Path.Combine(task, "children")).Split(' ', StringSplitOptions.RemoveEmptyEntries); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _failures.Enqueue($"{resource}: children read for {task}: {exception}");
                continue;
            }
            if (!SameLifetime(parent, started, resource)) { return; }

            foreach (string child in children)
            {
                if (!int.TryParse(child, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                {
                    _failures.Enqueue($"{resource}: invalid child pid '{child}' in {task}");
                    continue;
                }
                try
                {
                    if (_readIdentity(id) is not long childStarted)
                    {
                        _failures.Enqueue($"{resource}: pid {id} has no live start identity");
                        continue;
                    }
                    if (!SameLifetime(parent, started, resource)) { return; }
                    string[] parentLines = _readText($"/proc/{id}/status").Split('\n')
                        .Where(line => line.StartsWith("PPid:", StringComparison.Ordinal)).ToArray();
                    if (!SameLifetime(id, childStarted, resource)) { continue; }
                    if (!SameLifetime(parent, started, resource)) { return; }
                    if (parentLines.Length != 1 || !int.TryParse(parentLines[0].AsSpan(5).Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int actualParent) || actualParent != parent)
                    {
                        _failures.Enqueue($"{resource}: pid {id} parent membership is missing, malformed or differs from pid {parent}");
                        continue;
                    }

                    string name = _readText($"/proc/{id}/comm").Trim();
                    if (name.Length == 0)
                    {
                        _failures.Enqueue($"{resource}: pid {id} has no process role");
                        continue;
                    }
                    if (!SameLifetime(id, childStarted, resource)) { continue; }
                    workers[id] = (childStarted, name switch { "daprd" => "daprd", "dapr" => "cli", _ => name }, resource);
                    CaptureDescendants(id, resource, workers, visited, childStarted);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _failures.Enqueue($"{resource}: identity/role read for pid {id}: {exception}");
                    continue;
                }
            }
        }
        _ = SameLifetime(parent, started, resource);
    }

    private bool SameLifetime(int id, long started, string resource)
    {
        try
        {
            if (_readIdentity(id) == started) { return true; }
            _failures.Enqueue($"{resource}: pid {id} lifetime changed during capture");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { _failures.Enqueue($"{resource}: lifetime recheck for pid {id}: {exception}"); }
        return false;
    }
}
