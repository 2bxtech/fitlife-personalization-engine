## Design Decisions

**Implementation-level decisions and their rationale.** For high-level architectural choices (technology selection, system structure), see [Architecture.md](Architecture.md).

Key implementation patterns and the reasoning behind them:

### Why await Kafka acknowledgement for tracked events?
The event endpoint returns success only after Kafka acknowledges the versioned
event envelope. This gives clients a stable event ID they can safely reuse and
prevents the API from claiming an event was accepted when delivery failed.
Producer idempotence and SQL-enforced consumer deduplication then make retries
safe under at-least-once delivery. The tradeoff is that broker latency and
outages are visible to callers; non-critical UI tracking handles those failures
without blocking the member's primary action.

### Why batch recommendation generation (every 10 min) instead of on-demand?
On-demand scoring for every request would hammer the database and create unpredictable latency spikes. Batch generation lets us amortize the cost across all users, pre-compute during low-traffic periods, and serve from cache. 10 minutes balances freshness against compute cost — recommendations don't go stale that quickly for gym classes.

### Why persist recommendations to both Redis AND the database?
Redis is fast but volatile. If Redis restarts or evicts keys under memory pressure, we'd lose all recommendations and face a cold-start stampede. The database serves as durable storage; Redis is the hot read path. On cache miss, we check the DB before regenerating — this prevents thundering herd on Redis failures.

### Why is instructor preference weighted highest (20 points)?
Domain insight: gym-goers are often loyal to specific instructors, not just class types. Someone who loves Sarah's yoga class will follow her to a different time slot before trying a different instructor's class. This mirrors how Life Time members actually behave — the instructor relationship drives retention.

### Why 10-minute cache TTL?
Short enough that booking a class or updating preferences feels responsive (cache invalidates on those events anyway). Long enough that we're not regenerating constantly. The TTL is a fallback — explicit invalidation handles the important state changes.

### Why IHostedService for background workers instead of a separate service?
For a single-instance demo, co-locating workers with the API simplifies local
deployment and monitoring. Each API process starts an event consumer,
recommendation generator, and user profiler. The event consumers can share work
through a Kafka consumer group, but the scheduled generator and profiler do not
currently have leader election or a distributed lease. They must be disabled,
coordinated, or extracted into separately scaled workers before running multiple
API replicas. The Kubernetes manifests are therefore configured architecture
assets, not evidence that the current worker topology is safe at horizontal
scale.

### Why validate EventType against a static class instead of accepting any string?
Defense against garbage data. If the frontend sends `eventType: "clck"` (typo), we reject it immediately rather than polluting the interactions table with unprocessable events. The `EventTypes` static class acts as a schema contract between frontend and backend.

### Why segment users into behavioral cohorts (YogaEnthusiast, WeekendWarrior, etc.)?
Explicit preferences only tell part of the story. A user might say they like "Strength" but consistently book yoga classes. Behavioral segmentation captures revealed preferences, not just stated ones. Recalculating every 30 minutes keeps segments reasonably fresh without excessive computation.
