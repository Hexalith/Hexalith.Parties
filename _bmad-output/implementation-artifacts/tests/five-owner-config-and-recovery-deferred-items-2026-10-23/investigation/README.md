# Planning evidence

A disposable authenticated Redis 7.4.11 ACL user and daprd 1.18.7 prove that the
mapping prefix can permit persistence while the ingestion prefix rejects it.
Granting the ingestion prefix repairs the same backend. The attached sanitized
receipt records HTTP results, verified authenticated username and owned cleanup.

This is backend qualification only. The implementation must exercise registered
C# SDK stores and actual health aggregation before closing the ledger item.
No production code has changed. The initial empty-password attempt was rejected
as evidence because it could use the default user; it is not a passing control.

Reproduction used isolated state.redis metadata redisUsername/redisPassword,
a generated nonempty password, and ACL key grants for
*parties:memory-mapping:* followed by *parties:memory-ingestion:*.
No credential or raw runtime log is retained.
