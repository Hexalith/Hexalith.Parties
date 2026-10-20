#!/usr/bin/env python3
"""Render the Platform declaration from the Parties-owned, deny-by-default ACL.

The parser is the strict YAML subset used by Platform's
scripts/test-reference-module-declarations.py; no nested checkout or PyYAML is needed.
"""
from __future__ import annotations

import argparse
import ast
import copy
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ACL = ROOT / "src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml"
MANIFEST = ROOT / "module/hexalith.module-manifest.v2.json"

def _acl_yaml_lines(content: str) -> list[tuple[int, str]]:
    """Tokenize the policy's supported YAML subset, ignoring comments outside quotes."""
    lines = []
    for raw in content.splitlines():
        assert "\t" not in raw, "ACL YAML tabs are unsupported"
        quote = None
        for position, character in enumerate(raw):
            if character in "\"'" and (position == 0 or raw[position - 1] != "\\"):
                quote = None if quote == character else character if quote is None else quote
            elif character == "#" and quote is None and (position == 0 or raw[position - 1].isspace()):
                raw = raw[:position]
                break
        if raw.strip():
            lines.append((len(raw) - len(raw.lstrip(" ")), raw.strip()))
    return lines


def _acl_pair(text: str) -> tuple[str, object]:
    key, separator, raw_value = text.partition(":")
    assert separator and re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", key), f"Unsupported ACL YAML field: {text}"
    raw_value = raw_value.strip()
    if raw_value.startswith(("[", "'", '"')):
        value = ast.literal_eval(raw_value)
    else:
        assert not raw_value.startswith(("&", "*", "!", "{", "}")), f"Unsupported ACL YAML value: {text}"
        value = raw_value
    return key, value


def _acl_set(target: dict, key: str, value: object) -> None:
    assert key not in target, f"Duplicate ACL YAML field: {key}"
    target[key] = value


def parse_effective_acl(content: str) -> dict:
    """Parse the ACL's map/list YAML shape without adding a checkout dependency."""
    lines = _acl_yaml_lines(content)
    assert sum(indent == 0 and text.startswith("spec:") for indent, text in lines) == 1, "Duplicate or missing ACL spec"
    assert sum(indent == 2 and text.startswith("accessControl:") for indent, text in lines) == 1, (
        "Duplicate or missing spec.accessControl")
    assert (0, "spec:") in lines, "Unsupported ACL spec mapping"
    spec_index = lines.index((0, "spec:"))
    assert (2, "accessControl:") in lines[spec_index + 1:], "Missing spec.accessControl"
    start = lines.index((2, "accessControl:"), spec_index + 1) + 1
    acl: dict = {}
    policy = None
    operation = None
    for indent, text in lines[start:]:
        if indent <= 2:
            break
        if indent == 4:
            key, value = _acl_pair(text)
            if key == "policies":
                assert value == "", "ACL policies must be a sequence"
                value = []
            _acl_set(acl, key, value)
            policy = operation = None
        elif indent == 6:
            assert text.startswith("- ") and isinstance(acl.get("policies"), list)
            key, value = _acl_pair(text[2:])
            assert key == "appId"
            policy = {key: value}
            acl["policies"].append(policy)
            operation = None
        elif indent == 8:
            assert policy is not None
            key, value = _acl_pair(text)
            if key == "operations":
                assert value == "", "ACL operations must be a sequence"
                value = []
            _acl_set(policy, key, value)
            operation = None
        elif indent == 10:
            assert policy is not None and isinstance(policy.get("operations"), list) and text.startswith("- ")
            key, value = _acl_pair(text[2:])
            assert key == "name"
            operation = {key: value}
            policy["operations"].append(operation)
        elif indent == 12:
            assert operation is not None
            key, value = _acl_pair(text)
            _acl_set(operation, key, value)
        else:
            raise AssertionError(f"Unsupported ACL YAML indentation: {indent}: {text}")
    return acl



TEMPLATE = json.loads(r'''
{
  "schema": "hexalith.module-manifest.v2",
  "modules": [
    {
      "identity": {
        "moduleId": "parties",
        "servers": [
          {
            "id": "domain",
            "appId": "parties",
            "resourceId": "parties",
            "enabled": true,
            "required": true
          }
        ],
        "dependencies": [
          "eventstore",
          "tenants",
          "memories"
        ]
      },
      "runtime": {
        "dapr": [
          {
            "role": "state",
            "capability": "state",
            "recoveryClass": "authoritative-restore",
            "configurationKey": "Parties:Security:StateStoreName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "state",
            "capability": "state",
            "recoveryClass": "authoritative-restore",
            "configurationKey": "EventStore:CommandStatus:StateStoreName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "state",
            "capability": "state",
            "recoveryClass": "authoritative-restore",
            "configurationKey": "EventStore:DataProtection:StateStoreName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "state",
            "capability": "state",
            "recoveryClass": "authoritative-restore",
            "configurationKey": "EventStore:Projections:ReadModelStateStoreName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "state",
            "capability": "state",
            "recoveryClass": "authoritative-restore",
            "configurationKey": "EventStore:Projections:CheckpointStateStoreName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "state",
            "capability": "state",
            "recoveryClass": "authoritative-restore",
            "configurationKey": "Parties:MemoriesSearch:MappingStore:StateStoreName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "events",
            "capability": "pubsub",
            "recoveryClass": "rebuild-only",
            "configurationKey": "Tenants:PubSubName",
            "servers": [
              "domain"
            ]
          },
          {
            "role": "events",
            "capability": "pubsub",
            "recoveryClass": "rebuild-only",
            "configurationKey": "EventStore:ProjectionChanges:PubSubName",
            "servers": [
              "domain"
            ]
          }
        ],
        "extensions": [
          {
            "packageId": "Hexalith.Parties",
            "configurationInputs": [
              {
                "key": "EventStore:DomainService:AppId",
                "source": "configuration",
                "reference": "eventstore-domainservice-appid"
              },
              {
                "key": "Authentication:JwtBearer:Authority",
                "source": "configuration",
                "reference": "authentication-jwtbearer-authority"
              },
              {
                "key": "Authentication:JwtBearer:Issuer",
                "source": "configuration",
                "reference": "authentication-jwtbearer-issuer"
              },
              {
                "key": "Authentication:JwtBearer:Audience",
                "source": "configuration",
                "reference": "authentication-jwtbearer-audience"
              },
              {
                "key": "Authentication:JwtBearer:ValidAudiences",
                "source": "configuration",
                "reference": "authentication-jwtbearer-validaudiences"
              },
              {
                "key": "Authentication:JwtBearer:AllowedAlgorithms",
                "source": "configuration",
                "reference": "authentication-jwtbearer-allowedalgorithms"
              },
              {
                "key": "Authentication:JwtBearer:RequireHttpsMetadata",
                "source": "configuration",
                "reference": "authentication-jwtbearer-requirehttpsmetadata"
              },
              {
                "key": "Authentication:JwtBearer:AllowInsecureSymmetricKey",
                "source": "configuration",
                "reference": "authentication-jwtbearer-allowinsecuresymmetrickey"
              },
              {
                "key": "Authentication:JwtBearer:SigningKey",
                "source": "secret",
                "reference": "domain-jwt-signing-key"
              },
              {
                "key": "Authentication:Workload:Audience",
                "source": "configuration",
                "reference": "authentication-workload-audience"
              },
              {
                "key": "Authentication:Workload:AllowedCallers",
                "source": "configuration",
                "reference": "authentication-workload-allowedcallers"
              },
              {
                "key": "Authentication:Workload:MaximumLifetimeSeconds",
                "source": "configuration",
                "reference": "authentication-workload-maximumlifetimeseconds"
              },
              {
                "key": "EventStore:AppChannelToken",
                "source": "secret",
                "reference": "app-channel-token"
              },
              {
                "key": "EventStore:DataProtection:PersistToStateStore",
                "source": "configuration",
                "reference": "eventstore-dataprotection-persisttostatestore"
              },
              {
                "key": "Tenants:Enabled",
                "source": "configuration",
                "reference": "tenants-enabled"
              },
              {
                "key": "Tenants:ServiceName",
                "source": "configuration",
                "reference": "tenants-servicename"
              },
              {
                "key": "Tenants:CommandApiAppId",
                "source": "configuration",
                "reference": "tenants-commandapiappid"
              },
              {
                "key": "Tenants:TopicName",
                "source": "configuration",
                "reference": "tenants-topicname"
              },
              {
                "key": "Parties:MemoriesSearch:Enabled",
                "source": "configuration",
                "reference": "parties-memoriessearch-enabled"
              }
            ]
          }
        ],
        "resources": [
          {
            "server": "domain",
            "requests": {},
            "limits": {},
            "volumes": []
          }
        ]
      },
      "surfaces": {
        "interfaces": [
          {
            "name": "party-domain",
            "server": "domain",
            "protocol": "http",
            "routePrefix": "/",
            "exposure": "internal-only",
            "surfaceClass": "service",
            "authorization": {
              "policy": "authenticated",
              "permissions": []
            },
            "callers": [],
            "operations": [
              {
                "name": "process",
                "kind": "command",
                "authorizationPolicy": "authenticated"
              },
              {
                "name": "query",
                "kind": "query",
                "authorizationPolicy": "authenticated"
              },
              {
                "name": "project",
                "kind": "command",
                "authorizationPolicy": "authenticated"
              },
              {
                "name": "replay-state",
                "kind": "administration",
                "authorizationPolicy": "authenticated"
              },
              {
                "name": "admin-operational-index-metadata",
                "kind": "administration",
                "authorizationPolicy": "authenticated"
              }
            ],
            "invocationRules": []
          }
        ]
      },
      "integration": {
        "topics": [
          {
            "name": "tenants-events",
            "role": "events",
            "deadLetter": {
              "strategy": "none"
            },
            "subscription": {
              "server": "domain",
              "route": "/tenants/events",
              "topic": "system.tenants.events"
            }
          }
        ],
        "secrets": [
          {
            "name": "domain-jwt-signing-key",
            "configurationKey": "Authentication:JwtBearer:SigningKey"
          },
          {
            "name": "app-channel-token",
            "configurationKey": "EventStore:AppChannelToken"
          }
        ],
        "dynamicSecretNamespaces": [],
        "identityNeeds": [],
        "egress": [
          {
            "name": "domain-oidc-metadata",
            "protocol": "https",
            "destination": "Authentication:JwtBearer:Authority/.well-known/openid-configuration"
          },
          {
            "name": "domain-oidc-jwks",
            "protocol": "https",
            "destination": "Authentication:JwtBearer:Authority -> metadata.jwks_uri"
          }
        ],
        "providers": [],
        "workers": []
      },
      "lifecycle": {
        "readiness": [
          {
            "server": "domain",
            "kind": "http",
            "endpoint": "/ready"
          }
        ],
        "tasks": [],
        "recoveryHooks": [],
        "fenceHooks": [],
        "replay": {
          "reopenBeforeReplay": false
        },
        "startup": {},
        "criticalFlows": [
          {
            "name": "party-management",
            "surface": "party-domain",
            "operations": [
              "process",
              "query"
            ]
          }
        ],
        "smokeSurfaces": [],
        "classification": {
          "changeClass": "additive",
          "reason": "Enroll the existing Parties domain server; retain legacy hosts outside Platform and keep optional rich search disabled."
        },
        "recoveryInventory": []
      }
    }
  ]
}
''')


def declaration(content: str) -> dict:
    """Fail closed before deriving the effective invocation grants."""
    lines = _acl_yaml_lines(content)
    assert [(indent, text) for indent, text in lines if indent == 0] == [
        (0, "apiVersion: dapr.io/v1alpha1"), (0, "kind: Configuration"),
        (0, "metadata:"), (0, "spec:")], "Unsupported or duplicate ACL root fields"
    metadata = lines[lines.index((0, "metadata:")) + 1:lines.index((0, "spec:"))]
    assert metadata == [(2, "name: accesscontrol-parties")], "Invalid ACL metadata"
    assert [line for line in lines if line[0] == 2 and line not in metadata] == [
        (2, "accessControl:")], "Unsupported ACL spec fields"
    acl = parse_effective_acl(content)
    assert set(acl) == {"defaultAction", "trustDomain", "policies"}, "Unsupported ACL fields"
    assert acl["defaultAction"] == "deny" and acl["trustDomain"] == "public", "Unsafe ACL defaults"
    assert len(acl["policies"]) == 1, "Only the EventStore caller is approved"
    policy = acl["policies"][0]
    assert set(policy) == {"appId", "defaultAction", "trustDomain", "namespace", "operations"}, "Unsupported caller fields"
    assert (policy["appId"], policy["defaultAction"], policy["trustDomain"], policy["namespace"]) == (
        "eventstore", "deny", "public", "default"), "Unsafe caller policy"
    rules = []
    for operation in policy["operations"]:
        assert set(operation) == {"name", "httpVerb", "action"}, "Unsupported operation fields"
        path = operation["name"]
        assert isinstance(path, str) and re.fullmatch(r"/[a-z0-9/-]+", path), "Unsafe operation path"
        assert "//" not in path and not path.endswith("/"), "Unsafe operation path"
        assert operation["httpVerb"] == ["POST"] and operation["action"] == "allow", "Unsafe operation grant"
        rules.append({"caller": policy["appId"], "path": path, "verbs": operation["httpVerb"]})
    assert len(rules) == len({rule["path"] for rule in rules}) == 13, "Expected thirteen unique owner routes"
    result = copy.deepcopy(TEMPLATE)
    surface = result["modules"][0]["surfaces"]["interfaces"][0]
    surface["callers"] = [policy["appId"]]
    surface["invocationRules"] = rules
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail if the declaration is missing or stale")
    arguments = parser.parse_args()
    try:
        rendered = json.dumps(declaration(ACL.read_text(encoding="utf-8")), indent=2) + "\n"
        if arguments.check:
            if not MANIFEST.is_file() or MANIFEST.read_text(encoding="utf-8") != rendered:
                raise ValueError("declaration is missing or stale; run python3 scripts/render-platform-declaration.py")
        else:
            MANIFEST.parent.mkdir(exist_ok=True)
            MANIFEST.write_text(rendered, encoding="utf-8")
    except (OSError, AssertionError, ValueError, SyntaxError) as error:
        print(f"{ACL}: Platform declaration: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
