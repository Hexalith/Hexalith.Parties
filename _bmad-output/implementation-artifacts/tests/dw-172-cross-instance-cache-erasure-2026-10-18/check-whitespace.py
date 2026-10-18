"""Check CRLF, final newline, and trailing whitespace in DW-172 C# changes."""

from pathlib import Path

paths = [
    "src/Hexalith.Parties/Queries/PartySdkQueryService.cs",
    "tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs",
    "tests/Hexalith.Parties.Tests/Gateway/PartySdkCrossInstanceErasureTests.cs",
    "tests/Hexalith.Parties.Tests/Gateway/PartySdkFaultingReadModelStore.cs",
]
for name in paths:
    data = Path(name).read_bytes()
    assert data.endswith(b"\r\n"), f"{name}: missing CRLF final newline"
    assert b"\n" not in data.replace(b"\r\n", b""), f"{name}: bare LF"
    assert all(line.rstrip() == line for line in data.decode().splitlines()), (
        f"{name}: trailing whitespace"
    )
print("PASS: CRLF, final newline, and trailing whitespace in four changed C# files")
