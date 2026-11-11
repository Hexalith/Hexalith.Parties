Owned runtime counterexample, 2026-11-11.

Run command and working directory: run-command.json. Compilation arguments: compile-command.json.
Compiler exited 0 (one CS1701 framework-unification warning); runtime exited 0.
Server binary and relevant source identities: identities.json. All source and binaries are copied from the proposed working implementation; shared files were not modified.

The probe creates actual source-A historical and survivor workflow chunks using IndexSemanticChunksActivity, completes and purges the target workflow, reconciles independently empty source B with the existing fenced protocol, and schedules the same identity using the real DaprIngestionWorkflowScheduler and IngestionWorkflow. The real RecordSourceInventoryCandidateActivity grants modern provenance. The workflow's subsequent counter activity is held to prevent unrelated later writes during observation.

SemanticKeyDiscovery omits retained chunk 7. The real DeleteMemoryUnitProjectionActivity returns true, sets a tombstone, and leaves the historical chunk hash and independent survivor hash unchanged. Exact persisted observations and hashes are in result.json; full process output is in runtime.log. No full modern ingestion-completion claim is made.

Both owned provider containers and the owned Dapr process were disposed and independently confirmed absent in cleanup.json. Existing placement/scheduler services were used and left running.
