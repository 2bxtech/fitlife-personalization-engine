using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using System.Text.Json;

namespace FitLife.Core.Services;

/// <summary>
/// Core recommendation scoring engine implementing the 9-factor algorithm
/// Calculates personalized scores to rank class recommendations
/// </summary>
public class ScoringEngine : IScoringEngine
{
    /// <summary>
    /// Calculates a personalized score for a class based on user profile and interaction history
    /// </summary>
    /// <param name="user">User requesting recommendations</param>
    /// <param name="classItem">Class to score</param>
    /// <param name="userInteractions">User's interaction history for pattern analysis</param>
    /// <returns>Score between 0 and ~150 (higher is better)</returns>
    public double CalculateScore(User user, Class classItem, List<Interaction> userInteractions)
    {
        double score = 0;

        // Factor 1: Fitness level match (weight: 10)
        score += GetFitnessLevelScore(user.FitnessLevel, classItem.Level);

        // Factor 2: Preferred class type (weight: 15)
        score += GetClassTypeScore(user.PreferredClassTypes, classItem.Type);

        // Factor 3: Favorite instructor (weight: 20)
        score += GetInstructorScore(classItem.InstructorId, userInteractions);

        // Factor 4: Time preference (weight: 8)
        score += GetTimePreferenceScore(classItem.StartTime, userInteractions);

        // Factor 5: Class rating (weight: rating × 2)
        score += (double)classItem.AverageRating * 2;

        // Factor 6: Availability bonus/penalty
        score += GetAvailabilityScore(classItem.Capacity, classItem.CurrentEnrollment);

        // Factor 7: Segment boost (weight: up to 12)
        score += GetSegmentBoost(user.Segment, classItem.Type, classItem.StartTime);

        // Factor 8: Recency bonus
        score += GetRecencyBonus(classItem.StartTime);

        // Factor 9: Popularity bonus
        score += GetPopularityBonus(classItem.WeeklyBookings);

        return Math.Max(0, score); // Never return negative score
    }

    /// <summary>
    /// Factor 1: Fitness Level Match (Weight: 10)
    /// Ensures class difficulty aligns with user's fitness level
    /// </summary>
    private double GetFitnessLevelScore(string userLevel, string classLevel)
    {
        if (classLevel == "All Levels")
            return 10; // Always matches

        if (userLevel == classLevel)
            return 10; // Perfect match

        if (userLevel == "Intermediate" && classLevel == "Beginner")
            return 5; // Partial match (user can handle it)

        if (userLevel == "Advanced" && classLevel == "Beginner")
            return 3; // User can handle but may be too easy

        if (userLevel == "Advanced" && classLevel == "Intermediate")
            return 5;

        if (userLevel == "Beginner" && classLevel == "Advanced")
            return 0; // Too difficult

        return 3; // Default partial match
    }

    /// <summary>
    /// Factor 2: Preferred Class Type (Weight: 15)
    /// Favors class types the user explicitly prefers
    /// </summary>
    private double GetClassTypeScore(string preferredTypesJson, string classType)
    {
        try
        {
            var preferredTypes = JsonSerializer.Deserialize<List<string>>(preferredTypesJson);
            if (preferredTypes != null && preferredTypes.Contains(classType))
                return 15;
        }
        catch
        {
            // Invalid JSON, return 0
        }

        return 0;
    }

    /// <summary>
    /// Factor 3: Favorite Instructor (Weight: 20)
    /// Prioritizes classes taught by instructors the user has completed classes with
    /// Highest weight because instructor quality is a primary driver of satisfaction
    /// </summary>
    private double GetInstructorScore(string instructorId, List<Interaction> userInteractions)
    {
        // Check if user has completed classes with this instructor
        var completedWithInstructor = userInteractions
            .Where(i => i.EventType == "Complete")
            .Count(i =>
            {
                try
                {
                    if (string.IsNullOrEmpty(i.Metadata) || i.Metadata == "{}")
                        return false;

                    var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(i.Metadata);
                    if (metadata != null && metadata.TryGetValue("instructorId", out var instructorIdElement))
                    {
                        return instructorIdElement.GetString() == instructorId;
                    }
                }
                catch
                {
                    // Invalid metadata
                }
                return false;
            });

        // If user has completed 2+ classes with this instructor, consider them a favorite
        if (completedWithInstructor >= 2)
            return 20;

        return 0;
    }

    /// <summary>
    /// Factor 4: Time Preference (Weight: 8)
    /// Recommends classes at times the user typically attends
    /// </summary>
    private double GetTimePreferenceScore(DateTime classStartTime, List<Interaction> userInteractions)
    {
        // Analyze user's historical booking patterns
        var bookingHours = userInteractions
            .Where(i => i.EventType == "Book")
            .Select(i => i.Timestamp.Hour)
            .Distinct()
            .ToList();

        if (!bookingHours.Any())
            return 0; // No booking history

        var classHour = classStartTime.Hour;

        // Exact match
        if (bookingHours.Contains(classHour))
            return 8;

        // Within 1 hour of typical booking time
        if (bookingHours.Any(h => Math.Abs(h - classHour) <= 1))
            return 4;

        return 0;
    }

    /// <summary>
    /// Factor 6: Availability Bonus/Penalty
    /// Penalizes nearly-full classes to avoid booking failures
    /// </summary>
    private double GetAvailabilityScore(int capacity, int currentEnrollment)
    {
        if (capacity == 0)
            return 0;

        var availableSpots = capacity - currentEnrollment;
        var availabilityRatio = (double)availableSpots / capacity;

        if (availabilityRatio < 0.2) // Less than 20% spots available
            return -5;

        if (availabilityRatio > 0.8) // More than 80% spots available
            return 3;

        return 0; // Normal availability
    }

    /// <summary>
    /// Factor 7: Segment Boost (Weight: up to 12)
    /// Applies behavior-based personalization
    /// </summary>
    private double GetSegmentBoost(string? segment, string classType, DateTime classStartTime)
    {
        if (string.IsNullOrEmpty(segment))
            return 0;

        var isWeekend = classStartTime.DayOfWeek == DayOfWeek.Saturday ||
                       classStartTime.DayOfWeek == DayOfWeek.Sunday;

        return segment switch
        {
            "YogaEnthusiast" when classType == "Yoga" => 12,
            "StrengthTrainer" when classType == "HIIT" || classType == "Strength" => 12,
            "CardioLover" when classType == "Spin" || classType == "Running" || classType == "Cardio" => 12,
            "HighlyActive" => 5, // General boost for all types
            "WeekendWarrior" when isWeekend => 10,
            _ => 0
        };
    }

    /// <summary>
    /// Factor 8: Recency Bonus
    /// Slightly favors classes happening sooner
    /// </summary>
    private double GetRecencyBonus(DateTime classStartTime)
    {
        var daysUntilClass = (classStartTime - DateTime.UtcNow).TotalDays;

        if (daysUntilClass <= 1)
            return 5; // Happening within 24 hours

        if (daysUntilClass <= 3)
            return 3; // Happening within 3 days

        return 0;
    }

    /// <summary>
    /// Factor 9: Popularity Bonus
    /// Surfaces trending classes
    /// </summary>
    private double GetPopularityBonus(int weeklyBookings)
    {
        if (weeklyBookings > 50)
            return 8;

        if (weeklyBookings > 20)
            return 4;

        return 0;
    }
}
