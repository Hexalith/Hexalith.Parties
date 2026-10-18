# DW-35 whole-payload redaction verification

The projection fold recognizes `json-redacted` empty objects and historical null
before resolving a CLR event type. Such events advance checkpoints and create a
Redacted audit record without changing party data. Nonempty field-redacted
objects and ordinary JSON keep their previous behavior.

The initial corrected regression assertions reproduce 11 focused failures (19 cases) and six
persisted-handler failures (seven cases) against the baseline production source.
The final candidate passes 131 focused cases, all 291 projection tests and all
1,157 service tests, without failures, errors or skips. Both Debug/source builds
have zero warnings/errors. Exact commands, counts, source identities and local
log/XML hashes are recorded in [validation.json](validation.json).

Run each affected test project build individually with `-c Debug -m:1
-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`. The receipts retain
any environment pins used on earlier builds. Then invoke its built assembly directly,
using `-result-xml` for output and single-dash `-class` filters when focusing.

Persisted handler tests read serialized Detail, Processing and Index rows back
from the stateful test store. Rebuild plans are compared with persisted live
state while ignoring dictionary insertion order. Executed rebuild plans also
repair seeded historical inactive state in Detail and Index, with corrected
processing outcomes confirmed through stored read-back. Old projection mistakes require
a full SDK projection rebuild; already checkpointed incremental events are
skipped.

The isolated no-build Aspire baseline reported Parties ready and was stopped.
Tenants, Parties UI and EventStore Admin were Finished, so this run does not
qualify the full topology. No production data or dependencies were changed; nothing was staged, committed
or pushed. Raw logs remain locally under `/tmp/dw-35-validation/`.

All six independent review findings were patched and verified in follow-up; no
new deferred work remains from this item. Production source did not change during
review, so the passing service suite applies to the final production bytes.
