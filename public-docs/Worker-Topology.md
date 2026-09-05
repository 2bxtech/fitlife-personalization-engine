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
listener. Process exit status and logs are their current operational signals.

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

## Deployment status

Kubernetes files remain configured architecture evidence. Initialize schema before
applying worker deployments and substitute the same API image revision for all
roles. The disabled legacy Azure workflow has not been made deployable by this
change; migration orchestration, worker rollout wiring, telemetry, and live smoke
tests belong to deployment preparation. The default portfolio target remains Azure
Container Apps with bounded cost; AKS is not required.
