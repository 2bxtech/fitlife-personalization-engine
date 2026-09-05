## Design Decisions

**Implementation-level decisions and their rationale.** For high-level architectural choices (technology selection, system structure), see [Architecture.md](Architecture.md).

These describe what the code does today and why. Where a decision leaves a gap, the gap is stated rather than smoothed over.

### Why await Kafka acknowledgement before responding to `/api/events`?

The endpoint publishes the versioned envelope and awaits the broker acknowledgement before returning. Returning before the acknowledgement would let the API report success for an event that never reached the log, which makes the response meaningless as a signal.

The endpoint returns **200 OK**, not 202 Accepted. The distinction matters less than it looks: what the response actually asserts is that the broker accepted the record, not that a consumer processed it or that an `Interaction` row exists. If the response body is ever extended to advertise processing status, 202 with a status resource would be the more accurate contract.

The cost is that broker latency and availability are on the request path. That is the intended trade for tracked interactions. Non-critical UI telemetry should not block a member's primary action on the same dependency.

### Why does `acks=all` not settle the durability question?

The producer runs with `Acks = All` and `EnableIdempotence = true` (which requires `acks=all`), `MaxInFlight = 5`, and a 30-second message timeout.

On the local Compose stack there is one broker and replication factor 1, so the in-sync replica set has a single member and `acks=all` is equivalent to `acks=1`. The acknowledgement means one broker wrote the record to its log. It does not mean the record survives the loss of that broker. Replicated durability needs a multi-broker cluster with `min.insync.replicas` ≥ 2, which is not part of the current setup.

Documenting the configuration without this caveat would overstate what the local system proves.

### Why is producer idempotence not enough?

`EnableIdempotence` deduplicates producer retries within a producer session and partition, and preserves ordering with more than one in-flight request. That is a narrow guarantee. It does nothing about:

- an HTTP client retrying after a lost response, and
- a consumer reprocessing a message after a rebalance or an uncommitted offset.

Those need separate mechanisms. The second is handled by `EventId` deduplication in SQL. The first is only handled if the caller participates — see below.

### Why can `EventId` come from the caller?

`EventDto.EventId` is optional. If supplied it must parse as a GUID; if omitted the server generates one.

This is what makes an ambiguous HTTP retry tractable. If the broker acknowledges an event and the response is then lost, the caller's retry is the *same logical event only if the caller supplied `EventId` and reuses the same value*. When the field is omitted, each request gets a fresh server-generated GUID, so a retry is a genuinely distinct event and consumer deduplication will not collapse it — correctly, because the system has no way to know the two requests were meant to be one.

The practical rule: **callers that need retry safety must generate and retain their own `EventId`.** The `Idempotency-Key` header serves the same purpose for booking, but it is scoped to booking and is not read by `/api/events`.

### Why both a pre-check and a unique constraint for deduplication?

The consumer calls `ExistsByEventIdAsync` before inserting, then catches SQL error 2601/2627 on `IX_Interactions_EventId` if the insert collides anyway.

The pre-check alone would be a check-then-act race: two consumers, or one consumer either side of a rebalance, could both read "not present" and both insert. The unique filtered index is the final concurrency boundary. The pre-check is an optimization that avoids a failed insert in the common case, not the safety mechanism.

Cache invalidation runs on the duplicate path as well as the insert path. That is deliberate: if a previous attempt stored the interaction and then failed before invalidating, redelivery recovers the invalidation.

### Why is the retry policy not described as retrying "transient" failures?

The retry loop catches every exception except cancellation, retries up to `MaxAttempts` (default 3, clamped 1–10) with linear backoff (`RetryDelayMilliseconds × attempt`, default 250ms), and dead-letters when attempts are exhausted.

There is no transient-versus-fatal classification. A sustained SQL Server outage exhausts attempts and dead-letters messages exactly as a deterministic bug would. Calling these retries "transient-failure handling" would describe an intent the code does not implement.

The bounded retry is short — roughly 750ms across three attempts — so it stays well clear of `MaxPollIntervalMs` (5 minutes) and does not risk a rebalance mid-retry. Classifying exceptions, so that a dependency outage backs off instead of draining the topic into the DLQ, is the obvious next improvement.

### Why do some events skip retries entirely?

Deserialization failures and contract violations go straight to the dead-letter topic with no retry. Retrying is pointless when the message cannot become valid: malformed JSON, an unsupported schema version, a non-GUID `EventId`, missing or oversized fields, an `OccurredAt` outside the accepted window, or metadata over 8 KiB.

Dispositions recorded: `null-payload`, `malformed-json`, `unsupported-schema`, `invalid-event-id`, `invalid-contract`, `invalid-occurred-at`, `metadata-too-large`, and `retries-exhausted` for the bounded-retry path.

### Why is the dead-letter record metadata-only?

`DeadLetterEvent` carries `DeadLetterId`, `SchemaVersion`, `FailedAt`, source topic/partition/offset, message key, `EventId`, `Disposition`, and `Attempts`. It does not carry the original payload.

This keeps dead-letter records small and avoids copying user metadata into a second topic. The consequence is that **the DLQ supports investigation, not replay**. Source partition and offset locate the original record while it remains within the source topic's retention window (168 hours locally), but once it ages out the payload is gone. Retaining the payload is a deliberate future change, not an oversight.

### Why publish to the DLQ before committing the source offset?

Committing the offset first would let a crash between the two steps lose the record of a failure entirely: the source message would be skipped and nothing would say why. Publishing first means the abandonment is recorded before the system moves past the input.

The ordering narrows the gap but does not close it. **The dead-letter publish and the offset commit are separate, non-transactional operations.** A crash after the DLQ acknowledgement and before the commit causes the message to be redelivered, re-fail, and be dead-lettered again, so the DLQ can hold duplicate records for one source message. Anything reading the DLQ should deduplicate on source partition and offset. Closing this properly would require Kafka transactions with read-process-write semantics, which is not implemented.

### Why manual offset commits?

`EnableAutoCommit = false`. Auto-commit advances offsets on a timer regardless of processing progress, which can commit past a message that was never stored — silent loss. Manual commit after processing or dead-letter handling makes redelivery the failure mode instead, which the `EventId` unique index already absorbs.

If the commit itself throws, the error is logged and the loop continues. Redelivery may occur after restart or reassignment, and is deduplicated; a later successful commit can also advance past already processed records. If processing and dead-letter publication fail, the worker stops before polling another record, preventing a later commit from skipping an unacknowledged record.

### Why one image with separate process roles?

The HTTP API, Kafka consumer, and scheduled workers share libraries but run as
separate processes. `Process:Role` selects Api (default), Consumer, or Scheduler;
incompatible worker flags fail startup. Worker hosts run no HTTP listener,
migrations, or seeding.

Consumers coordinate through the Kafka consumer group, bounded by topic partitions.
The scheduler is configured as one Compose container or one Kubernetes replica
with `Recreate` and no HPA. API scaling cannot multiply scheduled work. There is
no distributed lease: a second scheduler against the same database is unsupported.
See [Worker Topology](Worker-Topology.md) for the precise ownership boundary.

### Why validate `EventType` against a static class instead of accepting any string?

Defense against garbage data. A typo like `eventType: "clck"` is rejected rather than stored as an unprocessable interaction. `EventTypes` acts as a schema contract between frontend and backend.

Validation runs twice, at the API boundary and again in the consumer. The duplication is intentional: the topic can receive records from something other than this API, and the consumer should not trust that every message passed the same checks.

### Why batch recommendation generation (every 10 min) instead of on-demand?

On-demand scoring for every request would hammer the database and create unpredictable latency spikes. Batch generation amortizes the cost across users and serves from cache. Ten minutes balances freshness against compute cost; gym class recommendations do not go stale faster than that.

### Why persist recommendations to both Redis and the database?

Redis is fast but volatile. If it restarts or evicts under memory pressure, recommendations would be lost and every user would trigger regeneration at once. The database is durable storage and Redis is the hot read path. On a cache miss the database is checked before regenerating, which limits the stampede.

### Why is instructor preference weighted highest (20 points)?

Domain insight: gym-goers are often loyal to specific instructors rather than class types. Someone who likes a particular instructor's yoga class will follow them to a different time slot before trying another instructor. The instructor relationship drives retention, so it outweighs class type (15) and fitness level (10).

### Why a 10-minute cache TTL?

Short enough that booking a class or updating preferences feels responsive, and those paths invalidate explicitly anyway. Long enough to avoid constant regeneration. The TTL is a fallback; explicit invalidation on `Book`, `Cancel`, `Complete`, and `Rate` handles the state changes that matter.

### Why segment users into behavioral cohorts?

Stated preferences only tell part of the story. A user might say they prefer strength training and consistently book yoga. Behavioral segmentation captures revealed preferences. Recalculating every 30 minutes keeps segments reasonably fresh without excessive computation.
