---
stateFile: "/home/administrator/projects/hexalith/parties/_bmad-output/story-automator/orchestration-1-20260609-205725.md"
createdAt: "2026-06-12T06:31:23Z"
---

# Agents Plan: parties - Epic Breakdown

```json
{
  "version": "1.0.0",
  "stateFile": "/home/administrator/projects/hexalith/parties/_bmad-output/story-automator/orchestration-1-20260609-205725.md",
  "epic": "1",
  "epicName": "parties - Epic Breakdown",
  "createdAt": "2026-06-12T06:31:23Z",
  "stories": [
    {
      "storyId": "1.1",
      "title": "Stand up the Hexalith.Parties.UI Blazor Server host",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.2",
      "title": "Host-owned OIDC sign-in (server-side, tokens never reach the browser)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.3",
      "title": "Role-based landing and policy-gated navigation",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.4",
      "title": "Fail-closed `party_id` claim resolution",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.5",
      "title": "Consumer own-data self-authorization (defense-in-depth)",
      "complexity": "high",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.6",
      "title": "Canonical StatusKind\u2192UI mapping with aria-live politeness split",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.7",
      "title": "Live freshness via SignalR + shared optimistic-reconcile effect",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.8",
      "title": "Shared domain components (party-state badge, freshness indicator, GDPR destructive button)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.9",
      "title": "Accessibility foundation and CI a11y gate (WCAG 2.2 AA)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "1.10",
      "title": "Deploy parties-ui (container + K8s) with production-KMS prerequisite gate",
      "complexity": "high",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "2.1",
      "title": "Embed the Admin area behind the Admin policy",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "2.2",
      "title": "Parties list with search, filters, and paging (FR-Admin-1)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "2.3",
      "title": "Party detail (FR-Admin-2)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "2.4",
      "title": "Create and edit a party with validation (FR-Admin-3)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "2.5",
      "title": "Party picker re-skin + full WAI-ARIA combobox (D11 / UX-DR7)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "3.1",
      "title": "GDPR operations page",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "3.2",
      "title": "Erase a party with typed-name confirmation",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "3.3",
      "title": "Restrict / lift restriction and record / revoke consent",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "3.4",
      "title": "Data export (Art.20) and processing records (Art.30)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "3.5",
      "title": "EventStore erasure-verification contract (backend, cross-submodule \u2014 approval-gated)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "3.6",
      "title": "Admin erasure-verification report (UI \u2014 consumes the D7 contract)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "4.1",
      "title": "Decide the Consumer identity \u2192 `party_id` binding mechanism (design spike \u2192 ADR)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "4.2",
      "title": "Implement the chosen `party_id` binding-provisioning mechanism",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "4.3",
      "title": "Stand up the ConsumerPortal RCL and Consumer area",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "4.4",
      "title": "My profile (FR-Consumer-1)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "4.5",
      "title": "Edit my profile (FR-Consumer-2)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "5.1",
      "title": "My consent \u2014 grant / withdraw with honest lawful-basis split (FR-Consumer-3)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "5.2",
      "title": "My data & privacy \u2014 export my data (FR-Consumer-4)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "5.3",
      "title": "My data & privacy \u2014 request / cancel erasure (FR-Consumer-4)",
      "complexity": "low",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    },
    {
      "storyId": "5.4",
      "title": "My data & privacy \u2014 see what's processed about me (FR-Consumer-4)",
      "complexity": "medium",
      "tasks": {
        "create": {
          "primary": "codex",
          "fallback": "claude"
        },
        "dev": {
          "primary": "codex",
          "fallback": "claude"
        },
        "auto": {
          "primary": "codex",
          "fallback": "claude"
        },
        "review": {
          "primary": "claude",
          "fallback": "codex"
        }
      }
    }
  ]
}
```
