# Plaintext and DEK lifetime evidence digest

Research brief: assess irreversible expiry of plaintext and DEKs **already copied into application processes**, at the original exclusive deadline, including stopped/crashed processes and persisted memory images. Project files were not consulted. Only this newly created digest is written. No vendor was contacted and no deployment or physical test was performed.

Accessed: **2026-10-25**. Completed two rounds, nine web calls. Round 1: eight selected primary sources. Round 2: six selected primary sources, plus an old Thales XML-export page inspected but excluded from conclusions because it is superseded and not evidence about the proposed holder. Publication dates below are undated unless a publication date was actually observed; crawl dates and copyright dates are not publication dates.

## Epistemics rules (verbatim)

1. Never conclude from training data alone. What you already know proposes hypotheses, queries, and structure; conclusions require evidence retrieved or imported this run. A claim you cannot evidence is stated as an unverified belief or not at all.
2. The research firewall. Project context — briefs, PRDs, code, memory, {workflow.persistent_facts} — shapes what to ask, never what is true. It is inadmissible as evidence: every claim in a research artifact traces to a digest or import file with a source. Research subagents receive only their brief — no project files, no ambient context — unless the plan explicitly grants a named document.

## Decision supported by both rounds

**The described ordinary .NET/Linux holder cannot support the universal claim as stated.** Linux directly documents stopped execution, retained RAM during suspend, and persistent hibernation snapshots; Microsoft documents independent plaintext copies and an explicit zeroing API whose target is only the supplied buffer. An uncatchable stop can prevent cleanup at the deadline. A crash can bypass cleanup entirely. A read-time check can reject a later request, but is not a destruction event for existing bytes. This conclusion rests on C2–C10, independently of the AWS illustration in C1. It is a high-confidence inference from documented counterexamples, not a tested implementation result or a theorem excluding all specialized hardware.

**Destroying an upstream HSM root/DEK does not establish erasure of an independent plaintext buffer already outside that boundary.** This follows from the independent-copy and explicit-buffer-operation contracts in C3/C9; C1 supplies a vendor-documented contrary illustration. For a non-exported hardware key, the platform-specific key-custody guarantee may differ, but that would change the premise that plaintext/DEKs are already copied into ordinary processes. Evidence class: primary-document synthesis; confidence: high.

## Claims and sources

### C1 — Root-key unavailability is not revocation of a retained DEK

- Source: [How unusable KMS keys affect data keys](https://docs.aws.amazon.com/kms/latest/developerguide/unusable-kms-keys.html), publisher: Amazon Web Services; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct vendor documentation.
- AWS says root-key unavailability fails subsequent cryptographic requests, subject to eventual consistency, but affects an encrypted data key only when that root is needed again. Causes include deleting the CloudHSM backing key and expiring imported key material.
- **Counterexample:** AWS documents an attached EBS volume retaining its DEK in Nitro hardware and continuing I/O after its KMS key becomes unusable. Detachment removes that retained key; only reattachment needs KMS again.
- Inference: root destruction cannot alone establish that independent copies already in application memory are destroyed at the same instant. A plaintext copy is outside the upstream unwrap operation's control. This source is AWS-specific evidence for the mechanism, not a claim that every HSM has identical deletion semantics. Destruction of all retained DEK copies would require additional evidence.

### C2 — Nonpageable memory is a bounded control

- Source: [mlock(2)](https://man7.org/linux/man-pages/man2/mlock.2.html), publisher: Linux man-pages project; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct OS interface documentation.
- Successful `mlock` keeps the specified pages resident and prevents paging those pages to swap until unlocked. Failure must be handled; an unsuccessful call is not a memory-lock guarantee.
- The documentation explicitly warns that secrets paged out can remain accessible on persistent swap long after RAM cleanup and process termination. It also warns that suspend implementations can save RAM to disk regardless of memory locks. Memory locks are not inherited across `fork`.
- Inference: locking selected buffers is useful engineering but is neither timed erasure nor proof against every persistent memory image or other unlocked copy. It cannot retroactively erase previously persisted bytes.

### C3 — .NET strings and conversions create additional lifetime obligations

- Source: [SecureString class, .NET 10 API documentation](https://learn.microsoft.com/en-us/dotnet/api/system.security.securestring?view=net-10.0), publisher: Microsoft; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct runtime documentation.
- Microsoft describes ordinary strings as immutable, with unpredictable garbage-collection lifetime. Operations and garbage-collector movement can produce additional copies. `SecureString` does not encrypt its internal storage on non-Windows platforms.
- Interop conversion methods make unmanaged plaintext strings that the developer must separately zero and free. Microsoft also identifies raw-memory access, process dumps and user-visible swap as exposure paths, and recommends against `SecureString` for new .NET development.
- Inference: a carefully allocated buffer cannot by itself demonstrate a global guarantee covering plaintext already materialized as strings or conversion copies. The source does not certify the absence of other copies in a particular application.

### C4 — A stopped process cannot supply its own deadline cleanup

- Source: [signal(7)](https://man7.org/linux/man-pages/man7/signal.7.html), publisher: Linux man-pages project; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct OS interface documentation plus inference.
- Linux specifies `SIGSTOP` as stopping the process, and states that `SIGSTOP` and `SIGKILL` cannot be caught, blocked or ignored. Signal handlers run through return to userspace execution.
- **Counterexample inference:** stop the holder after it has plaintext but before its deadline, keep it stopped past that deadline, then resume it. Its own timer/handler/`finally` cannot erase its buffer while the process is stopped. A post-resume check may deny use or trigger cleanup then; that does not establish destruction at the earlier exclusive deadline. An independently scheduled supervisor might terminate the process but is a different mechanism and still needs erasure evidence.

### C5 — Core controls address one copy path

- Source: [core(5)](https://man7.org/linux/man-pages/man5/core.5.html), publisher: Linux man-pages project; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct OS interface documentation.
- A core file can contain an image of process memory at termination. The manual documents core limits, dumpability-related controls, and exclusions of address ranges using `MADV_DONTDUMP`.
- It also warns that `RLIMIT_CORE` is ignored when the system pipes core dumps to a program.
- Inference: configured exclusion or suppression of process cores reduces an identified persistence path. That is not evidence for deleting a previously captured core, excluding a whole-system/hypervisor snapshot, or erasing another plaintext allocation. Kernel and VM crash-dump guarantees require their own platform evidence; none has been verified here.

### C6 — Suspend and hibernation directly defeat userspace timing assumptions

- Source: [System Sleep States](https://docs.kernel.org/admin-guide/pm/sleep-states.html), publisher: Linux kernel documentation, author Rafael J. Wysocki; pub_date: undated (2017 copyright observed); accessed: 2026-10-25; confidence: high; evidence class: direct kernel documentation.
- Linux sleep states do not execute userspace. Suspend-to-RAM puts memory in self-refresh to retain its contents. Hibernation writes a memory snapshot to persistent storage, can power the machine down, and restores the pre-hibernation state before userspace runs again.
- **Counterexample inference:** suspend across the deadline retains a holder's bytes without permitting its cleanup to run. A hibernation image can retain them across power-off; later buffer zeroing is not erasure of that earlier image. Power loss is not an evidenced timed sanitization mechanism, and this source establishes no physical DRAM-remanence bound.

### C7 — Runtime cleanup is not unconditional or deadline scheduled

- Source: [Object.Finalize](https://learn.microsoft.com/en-us/dotnet/api/system.object.finalize?view=net-10.0), publisher: Microsoft; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct runtime documentation.
- Finalization is nondeterministic; calling `Dispose` is the documented route to immediate resource release while code is running.
- Inference: a finalizer is not a deadline erasure mechanism. A crash before executing explicit cleanup leaves that cleanup unperformed. The precise documented conditions that prevent finalizer invocation will be checked before treating any particular exit path as covered; no finalizer reliability beyond the retrieved documentation is assumed.

### C8 — Expiration notification and absolute-time checks are separate from erasure

- Source: [timerfd_create(2)](https://man7.org/linux/man-pages/man2/timerfd_create.2.html), publisher: Linux man-pages project; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct OS interface documentation plus inference.
- `CLOCK_MONOTONIC` excludes suspended time; `CLOCK_BOOTTIME` includes it. Realtime is settable. Alarm variants can wake suspended systems subject to capability requirements. Timer descriptors notify expiry; reads report how many expirations occurred.
- Inference: selecting a suspend-aware clock and testing `now >= original_deadline` can support rejection on the next executing path. The original deadline must not be reset on resume or restart. Neither the clock nor its notification writes zeros into a stopped holder's memory, and a wakeup facility is not a documented absolute cleanup-latency guarantee.

## Engineering possibilities versus verified guarantees

Engineering controls supported within the above limits include: minimize plaintext exposure and copies; lock buffers before placing secrets in them; suppress relevant core paths; explicitly clear every controlled buffer; and recheck a persisted original deadline before releasing or using a secret. These are proposed defenses informed by C2–C8, not evidence that any implementation already meets them. A claim that every plaintext copy becomes irrecoverable exactly at the original deadline needs an execution and erasure mechanism outside the paused/crashed process, a complete storage/copy boundary, and a stated fault model. No such end-to-end guarantee has been verified in round 1.

## Written confirmation and physical verification needed

Requests below are research recommendations only; no contact or test was performed.

1. Obtain an exact vendor statement defining what an exclusive deadline means, which clock governs it, maximum lateness, and whether the erasure trigger executes autonomously through host stop, machine sleep, crash and power loss. Require it to cover plaintext and all DEKs, not merely future unwrap denial.
2. Require explicit coverage of CPU registers/caches, controller/device memory, all replicas, paging, process/kernel/VM dumps, hibernation images, backups and restoration/rollback. Ask what retained copies and debug/service modes are excluded. C1–C6 identify concrete reasons this inventory matters.
3. On the exact proposed hardware/OS/runtime configuration, fault-inject `SIGSTOP`, host suspend, crash-before-cleanup, restore of old memory images, and abrupt power removal around the deadline. Attempt recovery from every enabled persistence path and resume computation after the deadline. A finite successful test set can falsify unsafe claims or support a bounded assurance case; it cannot by itself prove universal destruction. These are proposed tests, not observed outcomes.

## Round 2 claims: runtime operation boundaries and hardware containment

### C9 — `ZeroMemory` clears one supplied buffer when execution reaches it

- Source: [CryptographicOperations.ZeroMemory(Span<Byte>)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0), publisher: Microsoft; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct runtime API contract plus inference.
- The documented operation fills the provided `Span<byte>` with zeros. Microsoft explains that this dedicated API guards against possible future optimization that could remove otherwise unused writes.
- Inference: this is a useful executed erasure primitive for that span. Its contract does not clear other allocations, prior persisted images, strings or other already exported copies. It does not schedule itself and cannot run in a stopped process. An HSM deletion call and a zeroing call have different targets; the former is not evidence that the latter occurred on each independent process buffer.

### C10 — A documented .NET termination path bypasses `finally` and finalizers

- Source: [Environment.FailFast](https://learn.microsoft.com/en-us/dotnet/api/system.environment.failfast?view=net-10.0), publisher: Microsoft; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct runtime documentation.
- Microsoft explicitly states that `FailFast` terminates without executing active `try`/`finally` blocks or finalizers. Its examples demonstrate an unexecuted `finally`. The documented Windows behavior also creates an application dump; Unix-like behavior writes a message and stack trace to standard error.
- **Counterexample inference:** if zeroing lives in a skipped cleanup block, termination before it does not establish its execution. This proves a real cleanup-bypass path, not that every kind of crash leaves identical artifacts or that released RAM remains forever recoverable. A universal crash-before-cleanup guarantee needs an independent mechanism rather than an assumption that `Dispose`/`finally`/a finalizer always runs.

### C11 — SGX isolation is a concrete containment candidate, not a timed-erasure contract

- Source: [Intel SGX SDK for Linux OS overview](https://www.intel.com/content/www/us/en/developer/tools/software-guard-extensions/linux-overview.html), publisher: Intel; pub_date: undated; accessed: 2026-10-25; confidence: high for documented scope; evidence class: direct hardware vendor documentation.
- Intel describes protected enclave execution, prevention of outside reads/writes, CPU-contained memory keys, and memory encryption. Production and debug enclaves have different debugger access. The page states that the memory key changes across power cycles and leaves side-channel defenses to developers.
- Inference: these properties could reduce exposure if plaintext and DEKs never leave the enclave. They do not establish an autonomous wall-clock expiry interrupt, a bounded zeroization deadline during stalled execution, or erasure of plaintext previously exported to ordinary application memory. This inspected source supplies no such guarantee.

### C12 — Genuine hardware key-loss behavior exists, but with a different trigger and scope

- Source: [Power Transitions in Intel SGX Applications for Windows](https://cdrdv2-public.intel.com/671295/powertransition-in-intel-sgx.pdf), publisher: Intel; pub_date: undated (references a 2016 SDK guide); accessed: 2026-10-25; confidence: high for the paper's stated scope, unverified for any proposed platform; evidence class: direct vendor technical paper, pp. 1–2.
- This Windows-scoped paper says S0/S1-to-S2–S5 transitions destroy the protected memory-encryption key, rendering the enclave unreadable and requiring recreation on resume. Preserving secrets requires saving state externally. The OS does not guarantee enough time to save all enclave state before a transition.
- Inference: this is meaningful power-state-triggered cryptographic loss of enclave access, rather than merely access isolation. It does **not** document an arbitrary original deadline trigger while a system remains powered and an application is paused, or revocation of plaintext exported outside the enclave. Applicability to an exact current CPU, firmware, OS and sleep state requires written confirmation and physical tests.

### C13 — A trusted-time API is not a retained-plaintext revocation engine

- Source: [Trusted Time and Monotonic Counters with Intel SGX Platform Services](https://cdrdv2-public.intel.com/671564/intel-sgx-platform-services.pdf), publisher: Intel, authors Shanwei Cen and Bo Zhang; pub_date: undated (2017 copyright observed); accessed: 2026-10-25; confidence: high for paper scope; evidence class: direct vendor technical paper, pp. 1, 5–6.
- The paper covers SDK 1.8 and sixth/seventh-generation platforms. It says SGX itself does not provide trusted timer/counter services. Its separate CSME-backed implementation supplies coarse seconds, a discontinuity epoch, and elapsed time rather than trusted calendar time. Applications must handle epoch changes. A sample detects expiration when enforcing a time-based policy.
- Inference: detection and policy evaluation are not a documented hardware deadline callback that erases a paused enclave's retained plaintext. These old, version-specific services are not presumed available on a proposed current server. They cannot be promoted into a verified current timed-destruction guarantee.

### C14 — Linux SGX documents actual lifecycle operations, not an expiry service

- Source: [Software Guard eXtensions (SGX)](https://docs.kernel.org/arch/x86/sgx.html), publisher: Linux kernel documentation; pub_date: undated; accessed: 2026-10-25; confidence: high; evidence class: direct kernel documentation.
- The kernel documents enclave-private pages, power-transition loss of the ephemeral encryption key, and reinitialization of enclave pages at boot using `EREMOVE`, including after a crash/`kexec` cycle. It documents enclave entry/resume and marshaling parameters into and out of the enclave. It also describes a page reclaimer for overcommitted EPC memory.
- Inference: enclave privacy and eventual page sanitization during lifecycle transitions have documented mechanisms. This source does not document autonomous original-deadline destruction of retained plaintext. Combining isolation with application deadline checks therefore remains an engineering proposal until exact independent enforcement and failure coverage are established.

## Hardware finding and enforcement boundary

**No examined source verifies the complete requested hardware-contained alternative.** The search did identify a real hardware loss-of-memory-key mechanism (C12, corroborated in C14), but its stated trigger is a power/lifecycle transition, not the arbitrary original exclusive deadline. C11–C14 should be used as leads for a narrowly specified hardware assurance case, never as a blanket TEE erasure claim. Confidence: high as a statement about this bounded search; evidence class: research result/absence of a verified guarantee, not proof of nonexistence.

For faults allowed to pause the whole VM, enforcement inside that VM has the same execution problem as the application. An exact-deadline destruction design needs an enforcing component outside every paused/crashed domain that can retain plaintext, or hardware whose verified autonomous behavior removes access to all retained state under those faults. This is a design inference from C4/C6/C9–C14. Even independent termination is not automatically physical erasure; it must be tied to the actual memory/key lifecycle. No such component or full copy inventory was verified here.

The physical-test and written-confirmation requirements above apply to any enclave or secure coprocessor proposal. Add explicit tests for host/VM pause without power transition, production/debug mode, stalled clocks, deadline-crossing resume, sealed-state rollback, exported copies and loss of power. Require a vendor distinction between permanent loss of retained plaintext access and refusal to perform the next decryption.
