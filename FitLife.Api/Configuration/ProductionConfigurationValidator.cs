using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FitLife.Api.Configuration;

public static class ProductionConfigurationValidator
{
    private const string DemoJwtSecret =
        "your-256-bit-secret-key-here-change-in-production-minimum-32-characters";

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var invalidKeys = new List<string>();
        var jwtSecret = configuration["Jwt:Secret"];
        var sqlConnection = configuration.GetConnectionString("DefaultConnection");
        var redisConnection = configuration["Redis:ConnectionString"];
        var kafkaBootstrapServers = configuration["Kafka:BootstrapServers"];

        if (string.IsNullOrWhiteSpace(jwtSecret)
            || jwtSecret.Length < 32
            || string.Equals(jwtSecret, DemoJwtSecret, StringComparison.Ordinal))
        {
            invalidKeys.Add("Jwt:Secret");
        }

        if (IsMissingOrLocal(sqlConnection)
            || sqlConnection?.Contains(
                "YourStrong@Passw0rd",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            invalidKeys.Add("ConnectionStrings:DefaultConnection");
        }

        if (IsMissingOrLocal(redisConnection))
        {
            invalidKeys.Add("Redis:ConnectionString");
        }

        if (IsMissingOrLocal(kafkaBootstrapServers))
        {
            invalidKeys.Add("Kafka:BootstrapServers");
        }

        if (configuration.GetSection("DemoAuthorization:OperatorEmails").GetChildren().Any())
        {
            invalidKeys.Add("DemoAuthorization:OperatorEmails");
        }

        if (invalidKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Production configuration is invalid for: {string.Join(", ", invalidKeys)}.");
        }
    }

    private static bool IsMissingOrLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
               || value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || value.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
               || value.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }
}
