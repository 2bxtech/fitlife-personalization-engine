namespace FitLife.Api.Configuration;

public static class WorkerApplication
{
    public static HostApplicationBuilder CreateBuilder(IConfiguration configuration,
        IHostEnvironment environment, ProcessRole role)
    {
        if (role == ProcessRole.Api)
            throw new InvalidOperationException("WorkerApplication requires Consumer or Scheduler role.");

        ProductionConfigurationValidator.Validate(configuration, environment, role);
        ProcessTopology.Validate(configuration, role);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Reuse the entry point's resolved settings, including command-line overrides.
            DisableDefaults = true,
            EnvironmentName = environment.EnvironmentName,
            ContentRootPath = environment.ContentRootPath
        });
        builder.Configuration.AddConfiguration(configuration);
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsole();
        builder.Services.AddFitLifeRuntime(builder.Configuration, builder.Environment, role);
        builder.Services.AddProcessWorkers(builder.Configuration, role);
        return builder;
    }
}
