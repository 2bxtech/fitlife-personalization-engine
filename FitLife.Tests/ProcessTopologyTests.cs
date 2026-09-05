using FitLife.Api.BackgroundServices;
using FitLife.Api.Configuration;
using FitLife.Core.Interfaces;
using FitLife.Infrastructure.Kafka;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace FitLife.Tests;

public class ProcessTopologyTests
{
    [Theory]
    [InlineData(null, ProcessRole.Api)]
    [InlineData("Api", ProcessRole.Api)]
    [InlineData("Consumer", ProcessRole.Consumer)]
    [InlineData("Scheduler", ProcessRole.Scheduler)]
    public void Roles_RegisterOnlyTheirOwnedWork(string? name, ProcessRole expected)
    {
        var config = Config(("Process:Role", name));
        var role = ProcessTopology.ReadRole(config);
        role.Should().Be(expected);
        var services = new ServiceCollection();
        services.AddProcessWorkers(config, role);
        var workers = services.Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType).ToArray();
        workers.Should().BeEquivalentTo(expected switch
        {
            ProcessRole.Api => Array.Empty<Type>(),
            ProcessRole.Consumer => new[] { typeof(EventConsumerService) },
            _ => new[] { typeof(RecommendationGeneratorService), typeof(UserProfilerService) }
        });
    }

    [Theory]
    [InlineData("api")]
    [InlineData("Api,Consumer")]
    [InlineData("0")]
    [InlineData("")]
    public void UnknownRole_FailsClosed(string name)
    {
        var read = () => ProcessTopology.ReadRole(Config(("Process:Role", name)));
        read.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(ProcessRole.Api, "EventConsumer")]
    [InlineData(ProcessRole.Api, "RecommendationGenerator")]
    [InlineData(ProcessRole.Api, "UserProfiler")]
    [InlineData(ProcessRole.Consumer, "UserProfiler")]
    [InlineData(ProcessRole.Consumer, "RecommendationGenerator")]
    [InlineData(ProcessRole.Scheduler, "EventConsumer")]
    public void IncompatibleLegacyEnableFlags_FailClosed(ProcessRole role, string worker)
    {
        var services = new ServiceCollection();
        var register = () => services.AddProcessWorkers(
            Config(($"BackgroundWorkers:{worker}:Enabled", "true")), role);
        register.Should().Throw<InvalidOperationException>().WithMessage("*incompatible*");
        services.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcessRole.Consumer)]
    [InlineData(ProcessRole.Scheduler)]
    public async Task DisabledWorkerHost_StartsAndStopsWithoutHttpOrDependencyConnections(ProcessRole role)
    {
        var config = Config(
            ("BackgroundWorkers:EventConsumer:Enabled", "false"),
            ("BackgroundWorkers:RecommendationGenerator:Enabled", "false"),
            ("BackgroundWorkers:UserProfiler:Enabled", "false"));
        var builder = WorkerApplication.CreateBuilder(config, Environment(), role);
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        host.Services.GetService<IServer>().Should().BeNull();
        host.Services.GetServices<IHostedService>().Should().BeEmpty();
        await host.StopAsync(timeout.Token);
    }

    [Fact]
    public void Scheduler_DoesNotRegisterKafkaOrRequireJwtOrBrokerInProduction()
    {
        var config = Config(
            ("ConnectionStrings:DefaultConnection", "Server=tcp:fitlife.database.windows.net;Database=FitLifeDb;Integrated Security=true"),
            ("Redis:ConnectionString", "fitlife.redis.cache.windows.net:6380,ssl=True"));
        var builder = WorkerApplication.CreateBuilder(config, Environment("Production"), ProcessRole.Scheduler);
        builder.Services.Should().NotContain(d => d.ServiceType == typeof(KafkaProducer)
            || d.ServiceType == typeof(IEventPublisher) || d.ServiceType == typeof(IDeadLetterPublisher));
    }

    [Fact]
    public void Consumer_StillRequiresBrokerInProduction()
    {
        var config = Config(
            ("ConnectionStrings:DefaultConnection", "Server=tcp:fitlife.database.windows.net;Database=FitLifeDb;Integrated Security=true"),
            ("Redis:ConnectionString", "fitlife.redis.cache.windows.net:6380,ssl=True"));
        var create = () => WorkerApplication.CreateBuilder(config, Environment("Production"), ProcessRole.Consumer);
        create.Should().Throw<InvalidOperationException>().WithMessage("*Kafka:BootstrapServers*");
    }

    [Fact]
    public async Task ScheduledServices_StopDuringStartupDelayWithoutAccessingDatabase()
    {
        var builder = WorkerApplication.CreateBuilder(Config(), Environment(), ProcessRole.Scheduler);
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
        host.Services.GetServices<IHostedService>().OfType<BackgroundService>()
            .Should().OnlyContain(service => service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public void TwoApiHosts_RunNoConsumerOrScheduledWork()
    {
        using var first = new FitLifeWebApplicationFactory();
        using var second = new FitLifeWebApplicationFactory();
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        foreach (var host in new[] { first, second })
            host.Services.GetServices<IHostedService>().Should().NotContain(service =>
                service is EventConsumerService || service is RecommendationGeneratorService
                || service is UserProfilerService);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    private static IHostEnvironment Environment(string name = "Testing")
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
        return environment.Object;
    }
}
