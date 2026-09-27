using FitLife.Api.Observability;
using FitLife.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FitLife.Api.BackgroundServices;

/// <summary>
/// Background service that generates recommendations for active users in batches
/// Runs every 10 minutes (configurable) to pre-compute recommendations
/// </summary>
public class RecommendationGeneratorService : BackgroundService
{
    private readonly ILogger<RecommendationGeneratorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public RecommendationGeneratorService(
        ILogger<RecommendationGeneratorService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if worker is enabled
        var enabled = _configuration.GetValue<bool>("BackgroundWorkers:RecommendationGenerator:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("RecommendationGeneratorService is disabled in configuration");
            return;
        }

        var intervalMinutes = _configuration.GetValue<int>("BackgroundWorkers:RecommendationGenerator:IntervalMinutes", 10);
        var batchSize = _configuration.GetValue<int>("BackgroundWorkers:RecommendationGenerator:BatchSize", 1000);
        var processActiveOnly = _configuration.GetValue<bool>("BackgroundWorkers:RecommendationGenerator:ProcessActiveUsersOnly", true);

        _logger.LogInformation(
            "RecommendationGeneratorService started - Running every {Interval} minutes, batch size: {BatchSize}",
            intervalMinutes, batchSize);

        // Wait a bit before first run to let other services initialize
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RecommendationGeneratorService cancelled during startup delay");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBatchAsync(batchSize, processActiveOnly, stoppingToken);

                // Wait for next interval
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("RecommendationGeneratorService is shutting down");
                break;
            }
            catch (Exception)
            {
                // WorkerRun has already logged and counted the failure.
                // Back off on errors
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("RecommendationGeneratorService stopped");
    }

    internal Task RunBatchAsync(int batchSize, bool processActiveOnly, CancellationToken cancellationToken) =>
        WorkerRun.ExecuteAsync("recommendation-generator", _logger,
            _serviceProvider.GetService<FitLifeMetrics>(),
            () => GenerateRecommendationsBatchAsync(batchSize, processActiveOnly, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Generates recommendations for a batch of active users
    /// </summary>
    private async Task<(int Succeeded, int Failed)> GenerateRecommendationsBatchAsync(
        int batchSize, 
        bool processActiveOnly, 
        CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var interactionRepository = scope.ServiceProvider.GetRequiredService<IInteractionRepository>();
            var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();

            // Get users to process
            List<string> userIds;
            
            if (processActiveOnly)
            {
                // Get users with interactions in the last 7 days
                var cutoffDate = DateTime.UtcNow.AddDays(-7);
                
                // For now, get all users and filter - in production, add a specialized repository method
                var allUsers = await userRepository.GetAllAsync();
                var activeUserIds = new HashSet<string>();

                foreach (var user in allUsers.Take(batchSize))
                {
                    var recentInteractions = await interactionRepository.GetRecentByUserIdAsync(user.Id, days: 7);
                    if (recentInteractions.Any())
                    {
                        activeUserIds.Add(user.Id);
                    }
                }

                userIds = activeUserIds.Take(batchSize).ToList();
                
                _logger.LogInformation("Found {Count} active users (with interactions in last 7 days)", 
                    userIds.Count);
            }
            else
            {
                // Process all users
                var allUsers = await userRepository.GetAllAsync();
                userIds = allUsers.Take(batchSize).Select(u => u.Id).ToList();
                
                _logger.LogInformation("Processing {Count} users (all users mode)", userIds.Count);
            }

            if (!userIds.Any())
            {
                _logger.LogInformation("No users to process in this batch");
                return (0, 0);
            }

            // Generate recommendations for each user
            var successCount = 0;
            var failureCount = 0;

            foreach (var userId in userIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await recommendationService.GenerateRecommendationsAsync(userId, limit: 10);
                    successCount++;

                    if (successCount % 100 == 0)
                    {
                        _logger.LogInformation("Generated recommendations for {Count} users so far...", 
                            successCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
                    failureCount++;
                }
            }

            _logger.LogInformation(
                "Batch complete: {Success} successful, {Failures} failed out of {Total} users",
                successCount, failureCount, userIds.Count);
            return (successCount, failureCount);
        }
    }
}
