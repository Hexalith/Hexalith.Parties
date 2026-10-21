# Resumed independent Blind Hunter review

The reviewer inspected the producer source commit plus consumer assertions and
evidence. Finding floor: 24.419 kB; min(floor(sqrt(24.419) + 1), 10) = 5.

- **Medium, patched — supported tabindex values.** FcPageHeader accepts authored
  values and focusRouteHeading preserves them. Recognize focusable h1 elements
  with tabindex; regressions cover 0 and -2 without changing authored values.
- **Medium, patched — pointer choice after overlay restoration.** Keep the
  pointer-focused heading and its URL across settings focus restoration;
  pointer events inside the modal do not discard the underlying choice.
- **Medium, patched — pointer traversal assertion.** Compare actual focus with
  the same browser's native destination, in addition to default-prevention.
- **Medium, patched — obsolete consumer receipt.** Preserve the earlier JSON
  as historical and link replacement source/package/negative evidence here.
- **Medium, patched — missing ignored logs.** Preserve current execution output
  as tracked .txt artifacts, rather than references to ignored .log files.
- **Medium, patched in follow-up — reused active heading.** New URL detection at
  keydown restores skip access even when focusing the same node emits no event.
- **Medium, patched in follow-up — fresh pointer input on a reused route.**
  Record the current URL on pointer input so the route-change fallback cannot
  override a fresh click. Both reused-route variants have browser coverage.
- **Medium, patched in follow-up — tabindex-0 loop.** Native Tab entry into the
  heading must not rearm a route announcement. The regression activates the
  content skip, traverses through the heading and reaches the Overview control.

Firefox's native destination after clicking a heading can differ from its
native destination after an overlay restores focus. The overlay regression
therefore measures the native destination after the same overlay lifecycle.
The reused-heading regression locates the updated accessible name after
changing its text. These were test-comparator corrections, not relaxed checks.

Publication/adoption is deferred under DW-91. None of these source results
certifies a new package or production release.
