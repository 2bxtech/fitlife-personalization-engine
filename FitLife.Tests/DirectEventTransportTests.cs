using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Api.Configuration;
using FitLife.Api.Events;
using FitLife.Api.Observability;
using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Kafka;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Hosting;
using Moq;

namespace FitLife.Tests;

/// <summary>Kafka-free minimal-demo transport: accepted events are persisted in-request.</summary>
public class DirectEventTransportTests : IClassFixture<DirectEventTransportTests.DirectTransportFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DirectTransportFactory _factory;

    public DirectEventTransportTests(DirectTransportFactory factory) => _factory = factory;

    [Fact]
    public void DirectMode_RegistersInProcessPublisherAndNoKafkaClient()
    {
        _factory.Services.GetRequiredService<IEventPublisher>()
            .Should().BeOfType<DirectEventPublisher>();
        _factory.Services.GetService<KafkaProducer>().Should().BeNull();
        _factory.Services.GetService<IDeadLetterPublisher>().Should().BeNull();
    }

    [Fact]
    public async Task AcceptedEvent_IsStoredBeforeResponse_AndRetryWithSameEventIdIsIgnored()
    {
        using var published = new MetricCollector<long>(
            _factory.Services.GetRequiredService<FitLifeMetrics>().Meter, "fitlife.events.published");
        var (client, userId) = await AuthenticatedClientAsync();
        var eventId = Guid.NewGuid().ToString();
        var dto = new EventDto
        {
            EventId = eventId,
            UserId = userId,
            ItemId = "class-direct-1",
            ItemType = "Class",
            EventType = EventTypes.View
        };

        var first = await client.PostAsJsonAsync("/api/events", dto);
        var retry = await client.PostAsJsonAsync("/api/events", dto);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        (await db.Interactions.CountAsync(i => i.EventId == eventId)).Should().Be(1);
        published.GetMeasurementSnapshot().Select(m => $"{m.Tags["transport"]}:{m.Tags["outcome"]}")
            .Should().Equal("direct:success", "direct:success");
    }

    [Fact]
    public void ConsumerRole_WithDirectTransport_FailsClosed()
    {
        var validate = () => ProcessTopology.Validate(
            Config(("Events:Transport", "Direct")), ProcessRole.Consumer);
        validate.Should().Throw<InvalidOperationException>().WithMessage("*Events:Transport Kafka*");
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("InProcess")]
    [InlineData("")]
    public void UnknownTransport_FailsClosed(string value)
    {
        var read = () => EventTransport.Read(Config(("Events:Transport", value)));
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ProductionApi_WithDirectTransport_DoesNotRequireBroker()
    {
        var validate = () => ProductionConfigurationValidator.Validate(
            ProductionApiConfig(("Events:Transport", "Direct")), Production(), ProcessRole.Api);
        validate.Should().NotThrow();
    }

    [Fact]
    public void ProductionApi_WithKafkaTransport_StillRequiresBroker()
    {
        var validate = () => ProductionConfigurationValidator.Validate(
            ProductionApiConfig(), Production(), ProcessRole.Api);
        validate.Should().Throw<InvalidOperationException>().WithMessage("*Kafka:BootstrapServers*");
    }

    private async Task<(HttpClient Client, string UserId)> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = $"direct_{Guid.NewGuid():N}@example.com",
            Password = $"Pw-{Guid.NewGuid():N}-A1",
            FirstName = "Direct",
            LastName = "Transport",
            FitnessLevel = "Beginner"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return (client, body.Data.User!.Id);
    }

    private static IConfiguration ProductionApiConfig(params (string Key, string? Value)[] extra) =>
        Config(new (string, string?)[]
        {
            ("Jwt:Secret", new string('k', 48)),
            ("ConnectionStrings:DefaultConnection",
                "Server=tcp:fitlife.database.windows.net;Database=FitLifeDb;Integrated Security=true"),
            ("Redis:ConnectionString", "fitlife.redis.cache.windows.net:6380,ssl=True")
        }.Concat(extra).ToArray());

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static IHostEnvironment Production()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");
        return environment.Object;
    }

    public sealed class DirectTransportFactory : FitLifeWebApplicationFactory
    {
        protected override IReadOnlyDictionary<string, string> StartupSettings =>
            new Dictionary<string, string> { ["Events:Transport"] = "Direct" };

        protected override bool UseRecordingEventPublisher => false;
    }
}
