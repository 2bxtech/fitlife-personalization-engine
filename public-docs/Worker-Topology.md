# Process and worker topology

**Verified in source/tests:** one executable and container image support three
mutually exclusive roles. **Configured:** Compose and Kubernetes assign these
roles to separate processes. No cloud deployment or cluster scaling is claimed.

| `Process:Role` | Responsibility | Ownership |
|---|---|---|
| `Api` (default) | HTTP routes, on-demand recommendations, booking | No background workers |
| `Consumer` | Kafka ingestion and dead-letter handling | Kafka consumer group owns partitions |
| `Scheduler` | Recommendation batches and user profiling | One scheduled process per database |

Worker roles use a generic host with no HTTP listener. They never apply migrations
or seed data. Initialize the database through the existing API/seed path before
starting workers. Controlled production migrations remain deployment work.

`BackgroundWorkers:<name>:Enabled=false` can disable an owned worker. Explicitly
enabling a worker in the wrong role fails startup, including legacy configurations
that enable all three workers. Omitting flags enables only the role's own workers.
Role names are case-sensitive; unknown values fail closed.

## Local operation

The default `dotnet run --project FitLife.Api` serves HTTP only. After starting the
local SQL, Redis, and Kafka services and initializing the database, run these in
separate terminals from the repository root:

```powershell
dotnet run --project FitLife.Api -- --Process:Role=Consumer
dotnet run --project FitLife.Api -- --Process:Role=Scheduler
```

Use the existing Development launch profile for local configuration. Stop each
process with Ctrl+C. Do not run a local scheduler against a database already owned
by a Compose or Kubernetes scheduler.

The root `.dockerignore` restricts the API image build context to .NET source
and project files, excluding private workspace data and local build output.

`docker compose up -d --build` configures API, consumer, and scheduler containers.
Workers wait for API readiness, which follows local startup migrations. The image
HTTP health check is disabled for worker containers because they have no HTTP
listener. Process exit status, logs, and the metrics below are their operational
signals.

## Kafka-free minimal demo

**Verified in tests and a local Compose smoke:** `Events:Transport=Direct`
removes the broker from the runtime. The API persists each accepted event
inside the request through the same idempotent recorder the consumer uses
(EventId lookup plus the unique EventId index), then requests invalidation of
the user's recommendation cache for Book, Cancel, Complete, and Rate events.
Invalidation is best-effort: the Redis client logs and swallows cache errors.

```powershell
docker compose -f docker-compose.yml -f docker-compose.minimal.yml up -d --build api scheduler web
```

Trade-offs compared with the Kafka transport:

- A `200` response means the interaction is committed to SQL, not that a broker
  acknowledged it.
- There is no consumer retry loop, dead-letter topic, or replay. A failed write
  returns an error to the caller. A retry is deduplicated only when the client
  supplied the `EventId`; otherwise the API generates a new one per request, so
  a retry after an ambiguous failure can store the interaction twice. The web
  client does not currently supply `EventId`.
- `POST /api/events/batch` records events one at a time. A failure partway
  through returns an error after earlier events in the batch are already stored.
- Event writes add SQL latency to the request path.
- `Process:Role=Consumer` with `Events:Transport=Direct` fails startup, and
  unknown transport values fail closed. In Production, the broker setting is only
  required when the transport is `Kafka` (the default).

## Singleton boundary

The supported scheduled topology has exactly one owner:

- Compose uses the fixed `fitlife-scheduler` container name, preventing scaling
  that service to multiple containers in one Compose deployment.
- `k8s/worker-deployments.yaml` configures one scheduler replica with `Recreate`,
  avoiding rolling-update surge. No HPA targets the scheduler.
- API or consumer replica counts do not create scheduled workers.

This is a deployment constraint, not a distributed lock or exactly-once guarantee.
Do not increase scheduler replicas, create a second scheduler deployment, or force
replacement while the old process may still run. Independent deployments and node
partition scenarios are not fenced by this design. A distributed ownership protocol
is required before supporting those scenarios. Repeat runs after restart are possible.

The consumer can scale only to the available Kafka partition count. The local
single-broker, auto-created topic normally offers one effective consumer. Consumer
group ownership is separate from scheduled ownership.

## Failure and shutdown

The consumer stops polling on shutdown, finishes or cancels in-flight processing,
and closes its Kafka client only after the loop exits. Its producer is flushed by
DI disposal after workers stop. If processing and dead-letter publication fail,
the worker exits instead of polling a later record whose offset could skip the
unacknowledged record. Restart replays from the committed offset.

Scheduled workers stop during startup delays, interval waits, or error backoff.
Repository operations already in flight may finish during shutdown. Worker hosts
allow 60 seconds for stopping; configured containers allow 90 seconds. Longer runs
can still be terminated by orchestration and retried on restart.

The profiler saves changed segments before invalidating recommendations. A failed
save does not invalidate the cache. Cache invalidation recovery after a successful
save remains a separate reliability concern.

## Operational signals

**Verified in tests:** every role publishes counters on the `FitLife` meter
(`System.Diagnostics.Metrics`). Tests observe per-test meter instances and cover
publish (both transports), recorded, retry, dead-letter, and the recommendation
generator's run, duration, and user counts; they fail when emission is removed.
The user profiler uses the same `WorkerRun` wrapper but has no dedicated metric test. **Not configured:** no exporter is wired, so
nothing collects these values until deployment adds OpenTelemetry or
`dotnet-counters` is attached.

| Instrument | Tags | Emitted by |
|---|---|---|
| `fitlife.events.published` | `transport` (kafka, direct), `outcome` | API event endpoints |
| `fitlife.events.recorded` | `outcome` (stored, duplicate) | Consumer and Direct transport |
| `fitlife.events.retries` | none | Consumer, per failed attempt that is retried |
| `fitlife.events.dead_lettered` | `disposition` | Consumer, after the DLQ publish succeeds |
| `fitlife.worker.runs` | `worker`, `outcome` (success, failure, cancelled) | Scheduler batches |
| `fitlife.worker.run.duration` (s) | `worker`, `outcome` | Scheduler batches |
| `fitlife.worker.users` | `worker`, `outcome` | Users processed per batch |

Scheduler logs record `Worker run started/finished/failed` with the owning
`host/process-id`, which identifies the process that owns scheduled work.
Consumer lag is not measured; broker-side tooling (`kafka-consumer-groups`)
remains the source for it.

## Deployment status

Kubernetes files remain configured architecture evidence. Initialize schema before
applying worker deployments and substitute the same API image revision for all
roles. The disabled legacy Azure workflow has not been made deployable by this
change; migration orchestration, worker rollout wiring, telemetry, and live smoke
tests belong to deployment preparation. The default portfolio target remains Azure
Container Apps with bounded cost; AKS is not required.
