# Hexalith.Parties

Hexalith.Parties is a ready-to-deploy party management microservice that lets you store, query, and manage people and organizations through a REST API, AI-friendly MCP tools, or a typed .NET client package -- so you can integrate party management into your system instead of rebuilding it.

> **GDPR Notice:** This MVP does **not** include GDPR compliance features (crypto-shredding, consent management, right to erasure). **Do not store regulated EU personal data.** GDPR features are planned for v1.1 -- see the [roadmap](docs/getting-started.md#whats-next).

## Key Features

- **Event-Sourced CQRS** -- Commands and queries separated with full event history via DAPR-backed event store
- **REST API** -- Standard HTTP endpoints for creating, updating, querying, and searching parties
- **MCP AI Tools** -- Five Model Context Protocol tools (`create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`) for AI assistant integration
- **NuGet Client Package** -- Typed `IPartiesCommandClient` and `IPartiesQueryClient` interfaces for .NET service-to-service calls
- **Multi-Tenant** -- Tenant isolation via JWT claims, enforced at every layer
- **.NET Aspire** -- One-command local deployment with dashboard, DAPR sidecars, and optional Keycloak

## Quick Start

```bash
git clone https://github.com/Hexalith/Hexalith.Parties.git
cd Hexalith.Parties
dotnet aspire run --project src/Hexalith.Parties.AppHost
```

Open the Aspire dashboard (URL shown in terminal output) to verify all resources are running. See the [Getting Started Guide](docs/getting-started.md) for the full walkthrough including your first API call.

## Documentation

- [Getting Started Guide](docs/getting-started.md) -- Deploy and send your first command in under 30 minutes
- [Architecture Overview](_bmad-output/planning-artifacts/architecture.md) -- System topology and design decisions
- API Reference -- Available at `/openapi/v1.json` (Swagger UI in development mode)

## Project Structure

```
Hexalith.Parties/
  src/
    Hexalith.Parties.AppHost/        # Aspire orchestration (entry point)
    Hexalith.Parties.Aspire/         # Aspire hosting extensions
    Hexalith.Parties.Client/         # NuGet client package (commands + queries)
    Hexalith.Parties.CommandApi/     # REST API, MCP server, controllers
    Hexalith.Parties.Contracts/      # Shared DTOs, commands, events, value objects
    Hexalith.Parties.Projections/    # Read model projections and actors
    Hexalith.Parties.Server/         # Domain logic and event store integration
    Hexalith.Parties.ServiceDefaults/# Shared service configuration
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

- [.NET 10 SDK](https://dot.net) (10.0.103+)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git

## License

This project is licensed under the [MIT License](LICENSE).
