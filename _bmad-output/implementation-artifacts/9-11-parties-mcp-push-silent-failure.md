# Story 9.11: parties-mcp Container Push Silent Failure Diagnostic

Status: backlog

## Story

As an operator running `publish.ps1`,
I want either (a) the `parties-mcp` image to actually appear in Zot at the MinVer tag publish.ps1 reports as pushed, OR (b) a clear error message when the push fails,
so that the cluster does not enter ImagePullBackOff for a service that the operator believes was just pushed.

## Discovery Context (2026-05-20)

Surfaced during the first successful `publish.ps1` run after Story 9.5 review. The build output reported:

```
Executing: dotnet publish ".../Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj" -t:PublishContainer
    --verbosity "quiet" --nologo -r "linux-x64"
    -p:ContainerRegistry="registry.hexalith.com"
    -p:ContainerRepository="parties-mcp"
    -p:ContainerImageTag="0.1.0"
(✔) Done: Building and Pushing container for project parties-mcp
```

aspirate confirmed success with the green `(✔) Done` line. **But Zot does not contain `parties-mcp:0.1.0`** — only `:latest` from an older push:

```bash
$ curl -u parties-publisher:<pwd> https://registry.hexalith.com/v2/parties-mcp/tags/list
{"name":"parties-mcp","tags":["latest"]}
```

The pod consequently enters ImagePullBackOff:

```
Failed to pull image "registry.hexalith.com/parties-mcp:0.1.0":
    manifest for registry.hexalith.com/parties-mcp:0.1.0 not found: manifest unknown
```

The `--verbosity quiet` flag passed to dotnet publish by aspirate suppresses the actual error. The image is **other than** parties-mcp (parties, memories) all successfully pushed at `:0.1.0` from the same `publish.ps1` run.

## Hypotheses (to verify)

1. **Project shape difference**: `Hexalith.Parties.Mcp.csproj` may have a different SDK type, missing `<EnableSdkContainerSupport>`, missing `<ContainerBaseImage>` reference, or some other config gap that makes `/t:PublishContainer` silently no-op. Compare against `Hexalith.Parties.csproj` (which works).

2. **Image push permission**: the `parties-publisher` Zot account may have push rights for `parties` and `memories` repos but not `parties-mcp` (per-repo ACL in Zot). The `:latest` tag on `parties-mcp` may have been pushed by a different account previously.

3. **Image size or content issue**: `parties-mcp` may be larger or contain layers that trigger a Zot config limit (e.g., `maxBlobSize`). The push fails at the manifest-upload step but the SDK reports success.

4. **Stdout swallowing by aspirate**: aspirate's stdout aggregation may be losing the actual dotnet error message between the per-project Process invocations.

## Acceptance Criteria

1. **Root cause identified**. One of (1)–(4) above or a new finding documented in the story Dev Notes.

2. **Fix applied based on root cause**:
   - If (1): fix the csproj (add missing properties or imports).
   - If (2): add `parties-mcp` to the relevant Zot accessControl policy.
   - If (3): identify the layer/config issue and split or compress.
   - If (4): file an aspirate issue and add an explicit post-push verification in publish.ps1 (`crane manifest registry.hexalith.com/<repo>:<tag>` per service).

3. **Post-push verification in publish.ps1** (defense-in-depth, regardless of root cause): after Step 3 (aspirate generate + push), publish.ps1 verifies each `registry.hexalith.com/<app>:<minver>` is actually pullable by Zot (HEAD request via curl or `crane manifest`). Exit with bounded error on any missing tag.

4. **Test coverage**: add `K8sManifestPublishTests.PublishPs1_AllConsumerImagesActuallyPushed_AreVerifiable` that mocks the aspirate-output success then asserts the manifest-verify call runs.

## Out of Scope

- Re-tagging the existing `parties-mcp:latest` to be a permanent alias for the SemVer build — that's a Zot operational concern, not a publish.ps1 concern.
- Investigating the same failure pattern for other services if it doesn't reproduce.
- Replacing aspirate with a different generator — orthogonal (Epic 10 tool-choice review).

## Dev Notes

- Compare `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj` against `src/Hexalith.Parties/Hexalith.Parties.csproj` field-by-field. Look for missing `<UserSecretsId>`, `<ContainerImageName>`, etc.
- Aspirate verbose flag: rerun the single failing project with `dotnet publish ... --verbosity "diagnostic"` to capture the real error.
- Zot accessControl debugging: `curl -u parties-publisher:<pwd> -X PUT -T <fake-layer-blob> https://registry.hexalith.com/v2/parties-mcp/blobs/uploads/` should return 202; if it returns 403, it's a permission issue.
- Current workaround applied in deploy/k8s/parties-mcp/deployment.yaml: `image: registry.hexalith.com/parties-mcp:latest` (uses the existing `:latest` tag in Zot). NOT AC4-compliant.

## References

- [Source: deploy/k8s/parties-mcp/deployment.yaml] — current workaround (`:latest`)
- [Source: src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj] — project file (verify config)
- [Source: src/Hexalith.Parties/Hexalith.Parties.csproj] — working reference for comparison
- [Source: _bmad-output/implementation-artifacts/9-5-zot-registry-build-push-pipeline.md § Review Findings] — discovery context
- [Aspirate 9.1.0 source](https://github.com/prom3theu5/aspirational-manifests) — for issue filing if needed
