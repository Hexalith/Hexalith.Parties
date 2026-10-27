#!/usr/bin/env python3
"""Verify executed Debug PDB document checksums against their current owning sources."""
from pathlib import Path
import json
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
PROBE = r"""
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json;
var root = args[0];
var inputs = new[] {
  ("src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.FrontComposer.Shell", "references/Hexalith.FrontComposer/", new[] {
    "src/Hexalith.FrontComposer.Shell/Services/Downloads/FcBrowserDownloadService.cs",
    "src/Hexalith.FrontComposer.Shell/Services/Feedback/FcOptimisticReconciler.cs",
    "src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor.cs",
    "src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor" }),
  ("src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.FrontComposer.Contracts.UI", "references/Hexalith.FrontComposer/", new[] {
    "src/Hexalith.FrontComposer.Contracts.UI/Downloads/IFcBrowserDownloadService.cs",
    "src/Hexalith.FrontComposer.Contracts.UI/Freshness/FcOptimisticReconcileRequest.cs" }),
  ("src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.Parties.UI", "", new[] {
    "src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs",
    "src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.cs" }),
  ("src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.Parties.AdminPortal", "", new[] {
    "src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor" }),
  ("src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.Parties.ConsumerPortal", "", new[] {
    "src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor" }),
};
var receipts = new List<object>();
foreach (var (binary, owner, sources) in inputs) {
  using var stream = File.OpenRead(Path.Combine(root, binary + ".pdb"));
  using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
  var reader = provider.GetMetadataReader();
  var documents = reader.Documents.Select(handle => reader.GetDocument(handle)).ToArray();
  var verified = new List<object>();
  foreach (string source in sources) {
    var matches = documents.Where(document => reader.GetString(document.Name).Replace('\\','/').EndsWith(source, StringComparison.Ordinal)).ToArray();
    if (matches.Length != 1) throw new InvalidOperationException($"Expected one PDB document for {source}, found {matches.Length}");
    var document = matches[0];
    byte[] expected = reader.GetBlobBytes(document.Hash);
    byte[] content = File.ReadAllBytes(Path.Combine(root, owner, source));
    byte[] actual = expected.Length == 32 ? SHA256.HashData(content) : SHA1.HashData(content);
    if (!actual.SequenceEqual(expected)) throw new InvalidOperationException($"Source changed since compilation: {source}");
    verified.Add(new { source = owner + source, document = reader.GetString(document.Name), sha256 = Convert.ToHexStringLower(SHA256.HashData(content)) });
  }
  receipts.Add(new { binary, dll_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, binary + ".dll")))),
    pdb_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, binary + ".pdb")))), documents = verified });
}
Console.WriteLine(JsonSerializer.Serialize(receipts, new JsonSerializerOptions { WriteIndented = true }));
"""
with tempfile.TemporaryDirectory(prefix='five-fc-pdb-') as temporary:
    directory = Path(temporary)
    (directory / 'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>')
    (directory / 'Program.cs').write_text(PROBE)
    result = subprocess.run(['dotnet','run','--project',str(directory/'Probe.csproj'),'--',str(ROOT)],capture_output=True,text=True,check=True)
    data = json.loads(result.stdout)
    (HERE/'source-binaries.json').write_text(json.dumps(data,indent=2)+'\n')
    print('Verified PDB source documents for', len(data), 'executed assemblies.')
