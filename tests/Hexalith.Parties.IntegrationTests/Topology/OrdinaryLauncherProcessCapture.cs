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
        => CaptureDescendants(parent, resource, workers, []);

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
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers, HashSet<int> visited)
    {
        if (!visited.Add(parent)) { return; }
        string[] tasks;
        try { tasks = _enumerateTasks(parent); }
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

            foreach (string child in children)
            {
                if (!int.TryParse(child, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                {
                    _failures.Enqueue($"{resource}: invalid child pid '{child}' in {task}");
                    continue;
                }
                try
                {
                    if (_readIdentity(id) is not long started)
                    {
                        _failures.Enqueue($"{resource}: pid {id} has no live start identity");
                        continue;
                    }

                    string name = _readText($"/proc/{id}/comm").Trim();
                    if (name.Length == 0)
                    {
                        _failures.Enqueue($"{resource}: pid {id} has no process role");
                        continue;
                    }
                    workers[id] = (started, name switch { "daprd" => "daprd", "dapr" => "cli", _ => name }, resource);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _failures.Enqueue($"{resource}: identity/role read for pid {id}: {exception}");
                    continue;
                }

                CaptureDescendants(id, resource, workers, visited);
            }
        }
    }
}
