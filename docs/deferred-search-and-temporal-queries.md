# Deferred Search and Temporal Query Extension Points

Hexalith.Parties MVP search is display-name search only. Active `MatchMetadata.MatchedField`
values emitted by MVP search responses are limited to `displayName`; email, identifier,
contact-channel, semantic, graph, hybrid, memory, provider, duplicate, type, and temporal
match concepts are reserved for future compatibility and are not available in MVP.

Semantic, graph, and hybrid search are deferred extension points. They require an explicit
future provider and must not be inferred from current DTO names, enum values, local fallback
behavior, or score/source metadata. The contracts package must not require a semantic backend,
embedding model, vector store, graph provider, temporal database, or concrete infrastructure
dependency.

Temporal name queries are also deferred. `NameHistoryEntry`, `PartyDetail.NameHistory`, and
`TemporalNameResult` preserve contract space for future name-as-of reads, but MVP exposes no
temporal query endpoint, typed client method, MCP tool, REST route, admin behavior, picker
behavior, or Memories integration. Name-history values are personal data and erased parties
must clear or suppress historical names.

Unsupported future search modes should fail closed with a bounded unsupported-capability
outcome. Responses and diagnostics must not echo search terms, display names, email addresses,
identifiers, contact values, tenant data, provider names, backend details, vectors, graph paths,
stack traces, or internal capability metadata.

## MVP wire-mode allowlist

The `PartySearch` query envelope accepts a `mode` payload field of type string. Only the
following values are honoured by the MVP query actor; everything else fails closed before
the projection read:

- omitted / `null` / empty — treated as MVP display-name search.
- `"Lexical"` — MVP display-name lexical search.
- `"DisplayName"` — wire-level alias for the same display-name path.

Reserved future wire values such as `"Hybrid"`, `"Semantic"`, `"Graph"`, `"Email"`,
`"Identifier"`, and `"TemporalName"` are rejected with an `UnsupportedQueryType` failure and
no tenant, query, or capability detail is echoed back. The in-process `PartySearchMode` enum
(`Hybrid`, `Lexical`, `Semantic`, `Graph`) is wider than this allowlist because reserved
values must remain in the contract for future v1.1 work; the wire layer is the single
allowlist consumers must rely on.
