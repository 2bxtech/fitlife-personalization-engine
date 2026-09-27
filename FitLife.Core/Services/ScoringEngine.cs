using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace FitLife.Core.Services;

/// <summary>
/// Deterministic nine-factor scoring engine. Every point in a score comes from a
/// named factor with a plain-language detail, so explanations are derived from the
/// computation itself rather than re-inferred afterwards.
/// </summary>
public class ScoringEngine : IScoringEngine
{
    private readonly ILogger<ScoringEngine> _logger;

    public ScoringEngine(ILogger<ScoringEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public double CalculateScore(User user, Class classItem, List<Interaction> userInteractions) =>
        Explain(user, classItem, ScoringHistory.From(userInteractions)).Total;

    /// <inheritdoc />
    public ScoreBreakdown Explain(User user, Class classItem, ScoringHistory history) =>
        new(new[]
        {
            FitnessLevel(user.FitnessLevel, classItem.Level),
            ClassType(user.PreferredClassTypes, classItem.Type),
            Instructor(classItem, history),
            TimeOfDay(classItem.StartTime, history),
            Rating(classItem.AverageRating),
            Availability(classItem.Capacity, classItem.CurrentEnrollment),
            ActivityProfile(user.Segment, classItem.Type, classItem.StartTime),
            StartsSoon(classItem.StartTime),
            Popularity(classItem.WeeklyBookings)
        });

    /// <summary>Factor 1 (up to 10): class difficulty versus the user's stated level.</summary>
    private static ScoreFactor FitnessLevel(string userLevel, string classLevel)
    {
        var points = classLevel switch
        {
            "All Levels" => 10,
            _ when userLevel == classLevel => 10,
            "Beginner" when userLevel == "Intermediate" => 5,
            "Beginner" when userLevel == "Advanced" => 3,
            "Intermediate" when userLevel == "Advanced" => 5,
            "Advanced" when userLevel == "Beginner" => 0,
            _ => 3
        };
        var detail = points == 10
            ? classLevel == "All Levels"
                ? "Open to all levels"
                : $"{classLevel} class matches your {userLevel} level"
            : $"{classLevel} class for your {userLevel} level";
        return new(ScoreFactorKeys.FitnessLevel, "Fitness level", points, detail);
    }

    /// <summary>Factor 2 (15): the class type is one the user chose as a preference.</summary>
    private ScoreFactor ClassType(string preferredTypesJson, string classType)
    {
        var preferred = false;
        try
        {
            preferred = JsonSerializer.Deserialize<List<string>>(preferredTypesJson ?? "[]")
                ?.Contains(classType) == true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse PreferredClassTypes JSON for scoring");
        }

        return preferred
            ? new(ScoreFactorKeys.ClassType, "Preferred type", 15, $"{classType} is one of your preferred class types")
            : new(ScoreFactorKeys.ClassType, "Preferred type", 0, $"{classType} is not in your preferred types");
    }

    /// <summary>Factor 3 (20): the user has completed two or more classes with this instructor.</summary>
    private static ScoreFactor Instructor(Class classItem, ScoringHistory history)
    {
        var completed = history.CompletionsByInstructor.GetValueOrDefault(classItem.InstructorId);
        return completed >= 2
            ? new(ScoreFactorKeys.Instructor, "Instructor", 20,
                $"You've completed {completed} classes with {classItem.InstructorName}")
            : new(ScoreFactorKeys.Instructor, "Instructor", 0,
                completed == 1
                    ? $"You've completed 1 class with {classItem.InstructorName}"
                    : $"No completed classes with {classItem.InstructorName} yet");
    }

    /// <summary>Factor 4 (up to 8): start time versus start times of classes the user booked.</summary>
    private static ScoreFactor TimeOfDay(DateTime classStartTime, ScoringHistory history)
    {
        // Details avoid clock times: the API compares UTC hours, while visitors read
        // local times, so a relative statement is the one that stays true.
        var hour = classStartTime.Hour;
        if (history.BookedStartHoursUtc.Count == 0)
            return new(ScoreFactorKeys.TimeOfDay, "Time of day", 0, "No booking history to compare times");
        if (history.BookedStartHoursUtc.Contains(hour))
            return new(ScoreFactorKeys.TimeOfDay, "Time of day", 8, "Starts in the same hour as classes you've booked");
        // Hours wrap at midnight: 23:00 and 00:00 are one hour apart.
        if (history.BookedStartHoursUtc.Any(booked => Math.Min(Math.Abs(booked - hour), 24 - Math.Abs(booked - hour)) <= 1))
            return new(ScoreFactorKeys.TimeOfDay, "Time of day", 4, "Starts within an hour of classes you've booked");
        return new(ScoreFactorKeys.TimeOfDay, "Time of day", 0, "Starts at a different time from classes you've booked");
    }

    /// <summary>Factor 5 (rating × 2): average member rating.</summary>
    private static ScoreFactor Rating(decimal averageRating) =>
        new(ScoreFactorKeys.Rating, "Rating", (double)averageRating * 2,
            averageRating <= 0
                ? "No ratings yet"
                : $"Rated {averageRating.ToString("0.0", CultureInfo.InvariantCulture)} out of 5");

    /// <summary>Factor 6 (−5 to +3): steer away from nearly-full classes.</summary>
    private static ScoreFactor Availability(int capacity, int currentEnrollment)
    {
        if (capacity == 0)
            return new(ScoreFactorKeys.Availability, "Availability", 0, "Capacity not set");
        var open = capacity - currentEnrollment;
        var ratio = (double)open / capacity;
        var points = ratio < 0.2 ? -5 : ratio > 0.8 ? 3 : 0;
        var detail = open <= 0 ? "Class is full" : $"{open} of {capacity} spots open";
        return new(ScoreFactorKeys.Availability, "Availability", points, detail);
    }

    /// <summary>
    /// Factor 7 (up to 12): a rule keyed on the user's activity segment. Segments are
    /// assigned by the profiler from the user's own history; this is a fixed rule,
    /// not a measurement of what similar members book.
    /// </summary>
    private static ScoreFactor ActivityProfile(string? segment, string classType, DateTime classStartTime)
    {
        var isWeekend = classStartTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var (points, description) = segment switch
        {
            "YogaEnthusiast" when classType == "Yoga" => (12, "yoga-focused"),
            "StrengthTrainer" when classType is "HIIT" or "Strength" => (12, "strength-focused"),
            "CardioLover" when classType is "Spin" or "Running" or "Cardio" => (12, "cardio-focused"),
            "HighlyActive" => (5, "highly active"),
            "WeekendWarrior" when isWeekend => (10, "weekend"),
            _ => (0, null as string)
        };
        return description == null
            ? new(ScoreFactorKeys.ActivityProfile, "Activity profile", 0,
                string.IsNullOrEmpty(segment) ? "No activity profile yet" : "No profile rule applies")
            : new(ScoreFactorKeys.ActivityProfile, "Activity profile", points,
                $"Fits your {description} activity profile");
    }

    /// <summary>Factor 8 (up to 5): slightly favour classes happening sooner.</summary>
    private static ScoreFactor StartsSoon(DateTime classStartTime)
    {
        var days = (classStartTime - DateTime.UtcNow).TotalDays;
        return days <= 1
            ? new(ScoreFactorKeys.StartsSoon, "Starts soon", 5, "Starts within 24 hours")
            : days <= 3
                ? new(ScoreFactorKeys.StartsSoon, "Starts soon", 3, "Starts within 3 days")
                : new(ScoreFactorKeys.StartsSoon, "Starts soon", 0, "More than 3 days away");
    }

    /// <summary>Factor 9 (up to 8): bookings across all members this week.</summary>
    private static ScoreFactor Popularity(int weeklyBookings)
    {
        var points = weeklyBookings > 50 ? 8 : weeklyBookings > 20 ? 4 : 0;
        return new(ScoreFactorKeys.Popularity, "Popularity", points, $"{weeklyBookings} bookings this week");
    }
}
