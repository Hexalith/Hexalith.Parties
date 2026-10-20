# DW-69 consumer NuGet source isolation

Baseline: `a4924d3eda23a9a57ddf3e859161077806b14490`.

The new regressions fail against the baseline: generated configuration omits
`clear`, and an offline restore obtains a missing package from an undeclared
parent feed. With the fix, the inherited source is excluded and selecting it
explicitly restores the package. Each restore has a separate disposable cache.
Four CLI cases cover omitted, single, repeated, and explicit public sources;
XML-sensitive paths and source URLs survive configuration round trips.

The Debug build passes with zero warnings/errors. Focused tests pass 16/16;
the complete affected CI assembly passes 130/130 without errors, failures,
skips or unexecuted tests. Python syntax, warning-override and whitespace
checks pass. Pinned commitlint 21.2.2 accepts the exact commit message.

`validation.json` records exact commands, counts, source hashes and limits.
`baseline-tests.txt` retains the original regression failures. The successful
CLI probe detects in-memory mutations that remove `clear` or retain the public
default with explicit sources; the unmodified control passes.

The existing-build Aspire baseline reaches a healthy `parties` resource and
is stopped afterwards. This is not fresh full-topology qualification. The
complete Release package-consumer restore/build remains a separate CI gate;
the new executable restore proof uses synthetic local packages and feeds.

Independent review produced four findings. Credential guidance and optimization
safety were corrected and verified. The two pre-existing limitations involving
fallback folders and disabled sources are documented and deferred separately.
`review-limit-probes.py` reproduces both offline; its successful execution means
these known limitations remain observable, not that they have been fixed.
Final post-review evidence is in `review-build.txt`, `review-focused.txt`,
`optimized-python.txt` and `review-full.txt`.
