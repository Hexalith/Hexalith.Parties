# Hexalith.Parties

Hexalith.Parties is a ready-to-deploy party management domain service for people and organizations. Public command and query traffic goes through Hexalith.EventStore; the `parties` service runs the domain actor host behind that gateway, and consumers normally use the typed .NET client package.

> **GDPR Notice:** This MVP does **not** include GDPR compliance features (crypto-shredding, consent management, right to erasure). **Do not store regulated EU personal data.** GDPR features are planned for v1.1 -- see the [roadmap](docs/getting-started.md#whats-next).

## Key Features

- **EventStore gateway** -- Public command/query ingress uses `POST /api/v1/commands` and `POST /api/v1/queries` with `Domain="party"`.
- **Parties actor host** -- The `parties` resource owns domain execution, projections, and DAPR actor hosting behind EventStore.
- **Typed client package** -- `IPartiesCommandClient` and `IPartiesQueryClient` hide EventStore envelope plumbing for .NET consumers.
- **Separate MCP host** -- `parties-mcp` exposes `create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`, and `get_party_name_at` through the typed client boundary.
- **DAPR event subscription** -- Subscriber apps consume EventStore-published party events with their own idempotent handlers.
- **EventStore Admin UI** -- Use `eventstore-admin-ui` for generic stream and event browsing.
- **.NET Aspire** -- One-command local topology with EventStore, Parties, Tenants, DAPR sidecars, Redis, and optional Keycloak.

## Quick Start

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
git submodule update --init Hexalith.EventStore Hexalith.Tenants Hexalith.Memories
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

Open the Aspire dashboard (URL shown in terminal output) and verify these resources are running: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants`. The local AppHost also runs `parties-mcp` alongside `parties` as a separate MCP resource — AI assistants connect to that host rather than the `parties` actor host.

The local run path uses root-level submodules only. Do not initialize nested submodules unless a separate story or maintainer asks for that explicitly.

> **Prerequisite - tenant access state.** Provision or use an active Hexalith.Tenants tenant membership before the first Parties call. EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties consumes the authorized command/query behind the actor host and does not manage tenant lifecycle or roles itself.

See the [Getting Started Guide](docs/getting-started.md) for the full EventStore-fronted walkthrough.

## Documentation

- [Getting Started Guide](docs/getting-started.md) -- Deploy and send your first EventStore-fronted command in under 30 minutes
- [Tenants Access Projection](docs/tenant-access-projection.md) -- Event-driven local tenant access state, consistency window, and fail-closed rules
- [Embeddable Party Picker](docs/frontend/party-picker.md) -- Blazor/custom-element picker integration for consuming applications
- [Architecture Overview](_bmad-output/planning-artifacts/architecture.md) -- System topology and design decisions
- Event streams -- Browse through the EventStore Admin UI resource in the Aspire dashboard

## Project Structure

Adopter-facing packages (left column) are the only modules consumers normally reference. Modules under "Internal" are private to the actor host and not adopter-facing dependencies — do not reference them from consumer applications.

```
Hexalith.Parties/
  src/
    Hexalith.Parties.AppHost/        # Aspire orchestration (entry point, dev-only)
    # Adopter-facing
    Hexalith.Parties.Client/         # Typed EventStore gateway client (IPartiesCommandClient / IPartiesQueryClient)
    Hexalith.Parties.Contracts/      # Shared DTOs, commands, events, value objects
    Hexalith.Parties.ServiceDefaults/# Shared service configuration helpers (optional)
    Hexalith.Parties.Mcp/            # Separate parties-mcp host over the typed client
    # Internal (actor host private — not adopter-facing dependencies)
    Hexalith.Parties/                # Domain actor host behind EventStore
    Hexalith.Parties.Server/         # Domain logic and event store integration (internal)
    Hexalith.Parties.Projections/    # Read model projections and actors (internal)
    Hexalith.Parties.Testing/        # Test utilities
  tests/                             # Unit, integration, and architectural tests
  samples/
    Hexalith.Parties.Sample/         # Sample integration project
  docs/
    getting-started.md               # Step-by-step onboarding guide
```

## Positioning

Hexalith.Parties manages **party records** -- people and organizations with contact channels and identifiers. It is **not** an authentication provider, CRM, or identity server. Use it as the party/contact data backbone behind your own application logic.

## Prerequisites

- [.NET 10 SDK](https://dot.net) (10.0.300+)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git

## License

This project is licensed under the [MIT License](LICENSE).
