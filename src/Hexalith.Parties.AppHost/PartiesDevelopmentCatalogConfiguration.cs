using System.Text.Json;
using System.Text.Json.Nodes;

using YamlDotNet.Serialization;

namespace Hexalith.Parties.AppHost;

/// <summary>Renders private JSON transport inputs from existing owner declarations without consulting a candidate catalog.</summary>
internal static class PartiesDevelopmentCatalogConfiguration
{
    /// <summary>Reads native Dapr YAML or JSON with its actual values retained.</summary>
    internal static JsonObject Read(string path)
    {
        object graph = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization()
            .Build().Deserialize<object>(File.ReadAllText(path));
        string json = new SerializerBuilder().JsonCompatible().Build().Serialize(graph);
        return JsonNode.Parse(json)!.AsObject();
    }

    /// <summary>Restricts the existing receiver grants to its finite owner rules and retains native mTLS settings.</summary>
    internal static (JsonObject Configuration, JsonArray Invocations) Receiver(string source,
        string? declarationPath, string? server, IReadOnlyDictionary<string, string> selectedApps, bool auxiliary, bool technical = false)
    {
        JsonObject configuration = Read(source);
        JsonObject spec = configuration["spec"]!.AsObject();
        JsonObject original = spec["accessControl"]?.AsObject() ?? new JsonObject
        {
            ["defaultAction"] = "deny", ["trustDomain"] = "public", ["policies"] = new JsonArray(),
        };
        if (original["defaultAction"]?.GetValue<string>() != "deny")
        {
            throw new InvalidDataException("The private catalog profile cannot qualify a default-open receiver.");
        }
        // This profile does not interpret Dapr deny precedence. Refuse explicit denies before
        // narrowing any overlapping allow, including for an otherwise client-only participant.
        if ((original["policies"]?.AsArray() ?? new JsonArray()).Any(policy =>
            policy!["operations"]!.AsArray().Any(operation => operation!["action"]?.GetValue<string>() != "allow")))
        {
            throw new InvalidDataException("Deny-bearing or unsupported receiver rules cannot be expanded into private catalog authority.");
        }
        JsonArray policies = new();
        JsonArray invocations = new();
        if (!auxiliary)
        {
            JsonArray declared = new();
            if (declarationPath is not null)
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(declarationPath));
                foreach (JsonElement module in document.RootElement.GetProperty("modules").EnumerateArray())
                foreach (JsonElement surface in module.GetProperty("surfaces").GetProperty("interfaces").EnumerateArray())
                {
                    if (surface.GetProperty("server").GetString() != server
                        || !surface.TryGetProperty("invocationRules", out JsonElement rules))
                    {
                        continue;
                    }
                    foreach (JsonElement rule in rules.EnumerateArray())
                    {
                        string caller = rule.GetProperty("caller").GetString()!;
                        declared.Add(new JsonObject
                        {
                            ["caller"] = selectedApps.GetValueOrDefault(caller, caller),
                            ["path"] = rule.GetProperty("path").GetString(),
                            ["verbs"] = JsonNode.Parse(rule.GetProperty("verbs").GetRawText()),
                        });
                    }
                }
            }
            foreach (JsonNode? item in original["policies"]?.AsArray() ?? new JsonArray())
            {
                JsonObject policy = item!.AsObject();
                if (policy["defaultAction"]?.GetValue<string>() != "deny")
                {
                    throw new InvalidDataException("The private catalog profile cannot qualify a default-open caller.");
                }
                string caller = policy["appId"]!.GetValue<string>();
                if (!selectedApps.Values.Contains(caller, StringComparer.Ordinal))
                {
                    // Optional hosts grant no authority in this private topology. Bound the
                    // actual ACL and its invocation observations to the same modeled Dapr IDs.
                    continue;
                }
                JsonArray grants = new();
                foreach (JsonNode? operation in policy["operations"]!.AsArray())
                {
                    if (operation!["action"]!.GetValue<string>() != "allow")
                    {
                        continue;
                    }
                    string method = operation["name"]!.GetValue<string>();
                    if (!method.Contains('*', StringComparison.Ordinal))
                    {
                        Add(operation.DeepClone().AsObject());
                        continue;
                    }
                    if (method is not "/**" && !method.EndsWith("/**", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("An unsupported wildcard cannot become private catalog authority.");
                    }
                    if (technical)
                    {
                        // SDK technical-v2 closes this unchanged owner grant to actual typed endpoint/verb/policy/code observations.
                        Add(operation.DeepClone().AsObject());
                        continue;
                    }
                    bool matched = false;
                    string prefix = method[..^2];
                    string[] allowedVerbs = operation["httpVerb"]!.AsArray().Select(verb => verb!.GetValue<string>()).ToArray();
                    foreach (JsonNode? rule in declared.Where(rule => rule!["caller"]!.GetValue<string>() == caller
                        && rule["path"]!.GetValue<string>().StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        string[] verbs = rule!["verbs"]!.AsArray().Select(verb => verb!.GetValue<string>()).ToArray();
                        if (verbs.Length > 0 && verbs.All(verb => allowedVerbs.Contains(verb, StringComparer.Ordinal)))
                        {
                            matched = true;
                            Add(new JsonObject { ["name"] = rule["path"]!.DeepClone(), ["httpVerb"] = rule["verbs"]!.DeepClone(), ["action"] = "allow" });
                        }
                    }
                    if (!matched)
                    {
                        throw new InvalidDataException("The receiver wildcard has no qualified owner-declared finite transport.");
                    }
                }
                if (grants.Count > 0)
                {
                    JsonObject bounded = policy.DeepClone().AsObject();
                    bounded["operations"] = grants;
                    policies.Add(bounded);
                }

                void Add(JsonObject grant)
                {
                    string name = grant["name"]!.GetValue<string>();
                    JsonObject? existing = grants.SingleOrDefault(item => item!["name"]!.GetValue<string>() == name)?.AsObject();
                    string[] verbs = grant["httpVerb"]!.AsArray().Select(verb => verb!.GetValue<string>())
                        .Concat(existing?["httpVerb"]!.AsArray().Select(verb => verb!.GetValue<string>()) ?? [])
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                    if (verbs.Length == 0 || verbs.Any(verb => verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")))
                    {
                        throw new InvalidDataException("Private receiver rules require a finite supported HTTP verb set.");
                    }
                    JsonArray boundedVerbs = new(verbs.Select(verb => (JsonNode?)JsonValue.Create(verb)).ToArray());
                    if (existing is null)
                    {
                        grant["httpVerb"] = boundedVerbs.DeepClone();
                        grants.Add(grant);
                        invocations.Add(new JsonObject { ["CallerAppId"] = caller, ["Method"] = name, ["HttpMethods"] = boundedVerbs });
                    }
                    else
                    {
                        existing["httpVerb"] = boundedVerbs.DeepClone();
                        invocations.Single(item => item!["CallerAppId"]!.GetValue<string>() == caller
                            && item["Method"]!.GetValue<string>() == name)!["HttpMethods"] = boundedVerbs;
                    }
                }
            }
        }
        JsonObject access = original.DeepClone().AsObject();
        access["policies"] = policies;
        spec["accessControl"] = access;
        return (configuration, invocations);
    }
}
