using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FitLife.Api.BackgroundServices;

/// <summary>
/// Background service that updates user segments based on interaction history
/// Runs every 30 minutes (configurable) to recalculate user behavior segments
/// Segments: Beginner, HighlyActive, YogaEnthusiast, StrengthTrainer, CardioLover, WeekendWarrior, General
/// </summary>
public class UserProfilerService : BackgroundService
{
    private readonly ILogger<UserProfilerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public UserProfilerService(
        ILogger<UserProfilerService> logger,
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
        var enabled = _configuration.GetValue<bool>("BackgroundWorkers:UserProfiler:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("UserProfilerService is disabled in configuration");
            return;
        }

        var intervalMinutes = _configuration.GetValue<int>("BackgroundWorkers:UserProfiler:IntervalMinutes", 30);
        var lookbackDays = _configuration.GetValue<int>("BackgroundWorkers:UserProfiler:LookbackDays", 30);

        _logger.LogInformation(
            "UserProfilerService started - Running every {Interval} minutes, lookback: {Lookback} days",
            intervalMinutes, lookbackDays);

        // Wait before first run to let other services initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                _logger.LogInformation("Starting user profiling batch");

                await ProfileUsersBatchAsync(lookbackDays, stoppingToken);

                var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                _logger.LogInformation("Completed user profiling batch in {Duration}s", elapsed);

                // Wait for next interval
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("UserProfilerService is shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UserProfilerService batch");
                
                // Back off on errors
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        _logger.LogInformation("UserProfilerService stopped");
    }

    /// <summary>
    /// Processes all users and updates their segments based on interaction history
    /// </summary>
    private async Task ProfileUsersBatchAsync(int lookbackDays, CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var interactionRepository = scope.ServiceProvider.GetRequiredService<IInteractionRepository>();
            var classRepository = scope.ServiceProvider.GetRequiredService<IClassRepository>();
            var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();

            var allUsers = await userRepository.GetAllAsync();
            
            _logger.LogInformation("Profiling {Count} users", allUsers.Count());

            var segmentChanges = 0;
            var processedCount = 0;

            foreach (var user in allUsers)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var oldSegment = user.Segment;
                    var newSegment = await CalculateUserSegmentAsync(
                        user, 
                        interactionRepository, 
                        classRepository, 
                        lookbackDays);

                    if (oldSegment != newSegment)
                    {
                        user.Segment = newSegment;
                        user.UpdatedAt = DateTime.UtcNow;
                        await userRepository.UpdateAsync(user);

                        _logger.LogInformation(
                            "Updated user {UserId} segment: {OldSegment} → {NewSegment}",
                            user.Id, oldSegment ?? "none", newSegment);

                        // Invalidate recommendations cache since segment changed
                        await recommendationService.InvalidateCacheAsync(user.Id);

                        segmentChanges++;
                    }

                    processedCount++;

                    if (processedCount % 100 == 0)
                    {
                        _logger.LogInformation("Profiled {Count} users so far...", processedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error profiling user {UserId}", user.Id);
                }
            }

            _logger.LogInformation(
                "Profiling complete: {Processed} users processed, {Changes} segment changes",
                processedCount, segmentChanges);
        }
    }

    /// <summary>
    /// Calculates user segment based on interaction history and behavior patterns
    /// Segments: Beginner, HighlyActive, YogaEnthusiast, StrengthTrainer, CardioLover, WeekendWarrior, General
    /// </summary>
    private async Task<string> CalculateUserSegmentAsync(
        User user,
        IInteractionRepository interactionRepository,
        IClassRepository classRepository,
        int lookbackDays)
    {
        var interactions = await interactionRepository.GetRecentByUserIdAsync(user.Id, lookbackDays);
        var completedClasses = interactions.Where(i => i.EventType == "Complete").ToList();

        // Rule 1: Less than 5 completed classes = Beginner
        if (completedClasses.Count < 5)
        {
            return "Beginner";
        }

        // Calculate average classes per week
        var avgClassesPerWeek = completedClasses.Count / (lookbackDays / 7.0);

        // Rule 2: 5+ classes per week = HighlyActive
        if (avgClassesPerWeek >= 5)
        {
            return "HighlyActive";
        }

        // Analyze class type distribution
        var classTypeCounts = new Dictionary<string, int>();

        foreach (var interaction in completedClasses)
        {
            try
            {
                var classItem = await classRepository.GetByIdAsync(interaction.ItemId);
                if (classItem != null)
                {
                    if (!classTypeCounts.ContainsKey(classItem.Type))
                    {
                        classTypeCounts[classItem.Type] = 0;
                    }
                    classTypeCounts[classItem.Type]++;
                }
            }
            catch
            {
                // Skip if class not found
            }
        }

        var totalCompleted = completedClasses.Count;

        // Rule 3: >60% of one class type = Specialist segment
        foreach (var (classType, count) in classTypeCounts)
        {
            var percentage = (double)count / totalCompleted;

            if (percentage > 0.6)
            {
                return classType switch
                {
                    "Yoga" => "YogaEnthusiast",
                    "HIIT" => "StrengthTrainer",
                    "Strength" => "StrengthTrainer",
                    "Spin" => "CardioLover",
                    "Running" => "CardioLover",
                    "Cardio" => "CardioLover",
                    _ => "General"
                };
            }
        }

        // Rule 4: >80% weekend bookings = WeekendWarrior
        var weekendClasses = completedClasses.Count(i =>
        {
            var dayOfWeek = i.Timestamp.DayOfWeek;
            return dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
        });

        if (totalCompleted > 0 && weekendClasses > totalCompleted * 0.8)
        {
            return "WeekendWarrior";
        }

        // Default: General (balanced interests)
        return "General";
    }
}
