from pathlib import Path
paths = ['src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs', 'src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs', 'src/Hexalith.Parties.Projections/Handlers/PartyProcessingActivityFold.cs', 'src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFoldLog.cs', 'tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionCategoryTests.cs', 'tests/Hexalith.Parties.Projections.Tests/Handlers/ProjectionCategoryLogRecord.cs', 'tests/Hexalith.Parties.Projections.Tests/Handlers/ProjectionCategoryLogger.cs', 'tests/Hexalith.Parties.Projections.Tests/Handlers/ProjectionCategoryLoggerProvider.cs', 'tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs', 'docs/development-guide.md']
for name in paths:
    data = Path(name).read_bytes()
    assert data.endswith(b"\r\n"), name + ": missing CRLF final newline"
    assert b"\n" not in data.replace(b"\r\n", b""), name + ": bare LF"
    assert b"\r" not in data.replace(b"\r\n", b""), name + ": bare CR"
    assert all(line == line.rstrip(b" \t") for line in data.split(b"\r\n")), name + ": trailing whitespace"
    data.decode("utf-8")
print(f"PASS: {len(paths)} implementation files use UTF-8, CRLF, final newlines, and no trailing whitespace")
