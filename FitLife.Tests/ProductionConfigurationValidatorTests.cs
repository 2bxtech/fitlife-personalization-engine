using FitLife.Api.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

namespace FitLife.Tests;

public class ProductionConfigurationValidatorTests
{
    [Fact]
    public void Startup_ProductionWithTrackedDefaults_FailsClosed()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var action = () => factory.CreateClient();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Production configuration is invalid*");
    }

    [Fact]
    public void Validate_ProductionDefaults_RejectsEveryDemoDependency()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "your-256-bit-secret-key-here-change-in-production-minimum-32-characters",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost,1433;Database=FitLifeDb;User Id=sa;Password=YourStrong@Passw0rd;",
            ["Redis:ConnectionString"] = "localhost:6380",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["DemoAuthorization:OperatorEmails:0"] = "operator@example.com"
        });

        var action = () => ProductionConfigurationValidator.Validate(
            configuration,
            Environment("Production"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Secret*")
            .WithMessage("*ConnectionStrings:DefaultConnection*")
            .WithMessage("*Redis:ConnectionString*")
            .WithMessage("*Kafka:BootstrapServers*")
            .WithMessage("*DemoAuthorization:OperatorEmails*");
    }

    [Fact]
    public void Validate_ProductionManagedConfiguration_Passes()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "a-production-secret-that-is-at-least-32-characters",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=tcp:fitlife.database.windows.net,1433;Database=FitLifeDb;Authentication=Active Directory Default;",
            ["Redis:ConnectionString"] = "fitlife.redis.cache.windows.net:6380,ssl=True",
            ["Kafka:BootstrapServers"] = "fitlife.servicebus.windows.net:9093"
        });

        var action = () => ProductionConfigurationValidator.Validate(
            configuration,
            Environment("Production"));

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_NonProductionDemoConfiguration_Passes(string environmentName)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var action = () => ProductionConfigurationValidator.Validate(
            configuration,
            Environment(environmentName));

        action.Should().NotThrow();
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(name);
        return environment.Object;
    }
}
