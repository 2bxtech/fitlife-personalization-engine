using System.Diagnostics;
using System.Diagnostics.Metrics;
using FitLife.Api.Configuration;

namespace FitLife.Api.Observability;

/// <summary>
/// Operational counters for event ingestion and scheduled work, published on the
/// "FitLife" meter. Any System.Diagnostics.Metrics listener can collect them
/// (dotnet-counters, OpenTelemetry); no exporter is wired here.
/// </summary>
public sealed class FitLifeMetrics
{
    public const string MeterName = "FitLife";

    /// <summary>Identifies this process in worker logs; one scheduler owner per database.</summary>
    public static readonly string ProcessOwner = $"{Environment.MachineName}/{Environment.ProcessId}";

    private readonly string _transport;
    private readonly Counter<long> _eventsPublished;
    private readonly Counter<long> _eventsRecorded;
    private readonly Counter<long> _eventRetries;
    private readonly Counter<long> _eventsDeadLettered;
    private readonly Counter<long> _workerRuns;
    private readonly Histogram<double> _workerRunDuration;
    private readonly Counter<long> _workerItems;

    public FitLifeMetrics(IMeterFactory meterFactory, IConfiguration configuration)
    {
        Meter = meterFactory.Create(MeterName);
        _transport = EventTransport.Read(configuration) == EventTransportMode.Direct ? "direct" : "kafka";
        _eventsPublished = Meter.CreateCounter<long>("fitlife.events.published",
            description: "Events accepted by the API, by transport and outcome.");
        _eventsRecorded = Meter.CreateCounter<long>("fitlife.events.recorded",
            description: "Valid events persisted (stored) or ignored as duplicates.");
        _eventRetries = Meter.CreateCounter<long>("fitlife.events.retries",
            description: "Consumer processing attempts that failed and were retried.");
        _eventsDeadLettered = Meter.CreateCounter<long>("fitlife.events.dead_lettered",
            description: "Consumer records sent to the dead-letter topic, by disposition.");
        _workerRuns = Meter.CreateCounter<long>("fitlife.worker.runs",
            description: "Scheduled worker batch runs, by worker and outcome.");
        _workerRunDuration = Meter.CreateHistogram<double>("fitlife.worker.run.duration", unit: "s",
            description: "Scheduled worker batch duration.");
        _workerItems = Meter.CreateCounter<long>("fitlife.worker.users",
            description: "Users processed by scheduled workers, by worker and outcome.");
    }

    public Meter Meter { get; }

    public void EventPublished(bool success) =>
        _eventsPublished.Add(1, new("transport", _transport), new("outcome", success ? "success" : "failure"));

    public void EventRecorded(bool stored) =>
        _eventsRecorded.Add(1, new KeyValuePair<string, object?>("outcome", stored ? "stored" : "duplicate"));

    public void EventRetried() => _eventRetries.Add(1);

    public void EventDeadLettered(string disposition) =>
        _eventsDeadLettered.Add(1, new KeyValuePair<string, object?>("disposition", disposition));

    /// <param name="outcome">success, failure, or cancelled (shutdown interrupted the batch).</param>
    public void WorkerRunCompleted(string worker, string outcome, TimeSpan duration)
    {
        var tags = new TagList { { "worker", worker }, { "outcome", outcome } };
        _workerRuns.Add(1, tags);
        _workerRunDuration.Record(duration.TotalSeconds, tags);
    }

    public void WorkerUsersProcessed(string worker, int succeeded, int failed)
    {
        if (succeeded > 0)
            _workerItems.Add(succeeded, new("worker", worker), new("outcome", "success"));
        if (failed > 0)
            _workerItems.Add(failed, new("worker", worker), new("outcome", "failure"));
    }
}
