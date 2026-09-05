using FitLife.Api.BackgroundServices;

namespace FitLife.Api.Configuration;

public enum ProcessRole { Api, Consumer, Scheduler }

/// <summary>One executable/image, with mutually exclusive process responsibilities.</summary>
public static class ProcessTopology
{
    public static ProcessRole ReadRole(IConfiguration configuration) =>
        configuration["Process:Role"] switch
        {
            null or "Api" => ProcessRole.Api,
            "Consumer" => ProcessRole.Consumer,
            "Scheduler" => ProcessRole.Scheduler,
            _ => throw new InvalidOperationException("Process:Role must be Api, Consumer, or Scheduler.")
        };

    public static void Validate(IConfiguration configuration, ProcessRole role)
    {
        Check("EventConsumer", role == ProcessRole.Consumer);
        Check("RecommendationGenerator", role == ProcessRole.Scheduler);
        Check("UserProfiler", role == ProcessRole.Scheduler);

        void Check(string worker, bool allowed)
        {
            if (!allowed && configuration.GetValue<bool>($"BackgroundWorkers:{worker}:Enabled"))
                throw new InvalidOperationException($"BackgroundWorkers:{worker}:Enabled is incompatible with Process:Role {role}.");
        }
    }

    public static void AddProcessWorkers(this IServiceCollection services,
        IConfiguration configuration, ProcessRole role)
    {
        Validate(configuration, role);
        // Stop a worker process if its background task fails; orchestration can restart it.
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
            options.ShutdownTimeout = TimeSpan.FromSeconds(60);
        });
        if (role == ProcessRole.Consumer && Enabled("EventConsumer"))
            services.AddHostedService<EventConsumerService>();
        if (role == ProcessRole.Scheduler)
        {
            if (Enabled("RecommendationGenerator"))
                services.AddHostedService<RecommendationGeneratorService>();
            if (Enabled("UserProfiler"))
                services.AddHostedService<UserProfilerService>();
        }

        bool Enabled(string worker) => configuration.GetValue($"BackgroundWorkers:{worker}:Enabled", true);
    }
}
