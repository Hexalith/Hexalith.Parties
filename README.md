# Hexalith.Parties

Hexalith.Parties is a ready-to-deploy party management domain service for people and organizations. Public command and query traffic goes through Hexalith.EventStore; the `parties` service runs the domain actor host behind that gateway, and consumers normally use the typed .NET client package. The solution also includes `parties-ui`, a Blazor Server browser UI/BFF for the Admin and Consumer experiences.

> **GDPR Notice:** Some GDPR infrastructure exists, and crypto-shredding is enabled by default, but the default key store is `LocalDevKeyStorageBackend` (in-memory, dev-only). **Do not store regulated EU personal data** until a production KMS or secret-store-backed key provider is provisioned. The MVP warning switch is separate from the crypto feature; see the [deployment security checklist](docs/deployment-security-checklist.md).

## Key Features

- **EventStore gateway** -- Public command/query ingress uses `POST /api/v1/commands` and `POST /api/v1/queries` with `Domain="party"`.
- **Parties actor host** -- The `parties` resource owns domain execution, projections, and DAPR actor hosting behind EventStore.
- **Typed client package** -- `IPartiesCommandClient` and `IPartiesQueryClient` hide EventStore envelope plumbing for .NET consumers.
- **Parties UI/BFF** -- `parties-ui` is a Blazor Server host with FrontComposer, FluentUI, host-owned OIDC, role-gated Admin/Consumer areas, accessibility gates, and server-side token handling.
- **Admin portal RCL** -- `Hexalith.Parties.AdminPortal` provides the protected `/admin/parties*` browse/detail/create/edit/GDPR surfaces embedded by `parties-ui`.
- **Consumer portal RCL** -- `Hexalith.Parties.ConsumerPortal` provides the protected `/me*` Consumer profile, consent, data export, erasure, and processing-transparency surfaces embedded by `parties-ui`.
- **Embeddable party picker** -- `Hexalith.Parties.Picker` provides a Blazor/custom-element selector that searches through `IPartiesQueryClient` and emits durable party-id selections.
- **Separate MCP host** -- `parties-mcp` exposes `create_party`, `get_party`, `find_parties`, `update_party`, and `delete_party` through the typed client boundary.
- **DAPR event subscription** -- Subscriber apps consume EventStore-published party events with their own idempotent handlers.
- **EventStore Admin UI** -- Use `eventstore-admin-ui` for generic stream and event browsing.
- **.NET Aspire** -- One-command local topology with EventStore, Parties, Parties UI, Tenants, DAPR sidecars, Redis, and optional Keycloak.

## Quick Start

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

Open the Aspire dashboard (URL shown in terminal output) and verify these resources are running: `security`, `eventstore`, `eventstore-admin`, `parties`, `parties-ui`, `tenants`, `redis`, the DAPR sidecars, `statestore`, and `pubsub`. The AppHost also declares `eventstore-admin-ui` and `parties-mcp` as explicit-start auxiliary resources; start them from the dashboard when you need stream browsing or MCP access. AI assistants connect to `parties-mcp` rather than the `parties` actor host.

The default local run path uses repository-level submodules under `references/` only. Do not initialize nested submodules unless a separate story or maintainer asks for that explicitly. Rich Memories-backed search is optional for local development; enable it separately with `EnableMemoriesSearch=true` after initializing the `references/Hexalith.Memories` submodule.

> **Prerequisite - tenant access state.** Provision or use an active Hexalith.Tenants tenant membership before the first Parties call. EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping. Parties consumes the authorized command/query behind the actor host and does not manage tenant lifecycle or roles itself.

See the [Getting Started Guide](docs/getting-started.md) for the full EventStore-fronted walkthrough.

## Documentation

- [Getting Started Guide](docs/getting-started.md) -- Deploy and send your first EventStore-fronted command in under 30 minutes
- [Accessibility Contract](docs/accessibility.md) -- Parties UI WCAG 2.2 AA guardrails and test expectations
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
    Hexalith.Parties.AdminPortal/    # Protected Admin party records and GDPR RCL
    Hexalith.Parties.ConsumerPortal/ # Protected Consumer /me self-service RCL
    Hexalith.Parties.Picker/         # Embeddable Blazor/custom-element party picker
    Hexalith.Parties.UI/             # Blazor Server browser UI/BFF for Admin and Consumer experiences
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
  references/
    Hexalith.EventStore/             # Gateway/eventing submodule
    Hexalith.Tenants/                # Tenancy submodule
    Hexalith.FrontComposer/          # UI shell submodule
    Hexalith.Memories/               # Optional rich-search submodule
```

## Positioning

Hexalith.Parties manages **party records** -- people and organizations with contact channels and identifiers. It is **not** an authentication provider, CRM, or identity server. Use it as the party/contact data backbone behind your own application logic.

## Prerequisites

- [.NET 10 SDK](https://dot.net) (10.0.300+)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git

## License

This project is licensed under the [MIT License](LICENSE).
