# DW-121 consumer consent validation evidence

`validation.json` records commands, counts, source/XML hashes and limitations.
The corrected original-code regression has ten validation failures and seventeen
passing controls. The fixed adapter passes focused and full UI/consumer suites.
Raw build logs and xUnit XML remain local under `/tmp/dw-121-*`.

Malformed historical identifiers are synthetic; no real tenant incident is claimed.
The separate Aspire baseline timed out at the 20-second healthy wait and was stopped.
Independent review and follow-up are complete; all four test improvements are
confirmed, with no remaining concrete issue or new deferred work. Final focused
tests pass 44/44; full UI 388/388 and ConsumerPortal 82/82 (470/470 combined).
