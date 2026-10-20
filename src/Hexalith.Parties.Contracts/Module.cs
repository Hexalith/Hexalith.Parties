using Hexalith.McpCli.Abstractions;

[assembly: HexalithModule(
    "parties",
    "Manage people and organizations, their contact channels, identifiers and active status.",
    IdentifierKind.String,
    WireTypeConvention = WireTypeConvention.FullTypeName)]
