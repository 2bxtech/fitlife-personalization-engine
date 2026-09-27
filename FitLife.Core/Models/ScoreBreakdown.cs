using System.Text.Json;

namespace FitLife.Core.Models;

/// <summary>Stable identifiers for the nine scoring factors.</summary>
public static class ScoreFactorKeys
{
    public const string FitnessLevel = "fitness_level";
    public const string ClassType = "class_type";
    public const string Instructor = "instructor";
    public const string TimeOfDay = "time_of_day";
    public const string Rating = "rating";
    public const string Availability = "availability";
    public const string ActivityProfile = "activity_profile";
    public const string StartsSoon = "starts_soon";
    public const string Popularity = "popularity";
}

/// <summary>One factor's contribution to a recommendation score.</summary>
/// <param name="Key">Stable identifier from <see cref="ScoreFactorKeys"/>.</param>
/// <param name="Label">Short display name.</param>
/// <param name="Points">Points contributed (may be negative).</param>
/// <param name="Detail">Plain-language statement of the fact that produced the points.</param>
public sealed record ScoreFactor(string Key, string Label, double Points, string Detail);

/// <summary>The auditable result of scoring one class for one user.</summary>
public sealed class ScoreBreakdown
{
    public ScoreBreakdown(IReadOnlyList<ScoreFactor> factors)
    {
        Factors = factors;
        // Mirrors the engine's historical floor: a score never goes below zero.
        Total = Math.Max(0, factors.Sum(factor => factor.Points));
    }

    public IReadOnlyList<ScoreFactor> Factors { get; }

    public double Total { get; }

    public static string Serialize(IEnumerable<ScoreFactor> factors) =>
        JsonSerializer.Serialize(factors);

    public static IReadOnlyList<ScoreFactor> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ScoreFactor>();
        try
        {
            return JsonSerializer.Deserialize<List<ScoreFactor>>(json) ?? new List<ScoreFactor>();
        }
        catch (JsonException)
        {
            return Array.Empty<ScoreFactor>();
        }
    }
}

/// <summary>
/// Facts derived from a user's interaction history that the scorer consumes.
/// Built from the classes the user actually booked or completed, so time-of-day
/// preference reflects class start times rather than when a button was clicked.
/// </summary>
public sealed class ScoringHistory
{
    public static readonly ScoringHistory Empty = new(
        new Dictionary<string, int>(), new HashSet<int>());

    public ScoringHistory(
        IReadOnlyDictionary<string, int> completionsByInstructor,
        IReadOnlySet<int> bookedStartHoursUtc)
    {
        CompletionsByInstructor = completionsByInstructor;
        BookedStartHoursUtc = bookedStartHoursUtc;
    }

    /// <summary>Completed-class count per instructor id.</summary>
    public IReadOnlyDictionary<string, int> CompletionsByInstructor { get; }

    /// <summary>UTC start hours of classes the user has booked.</summary>
    public IReadOnlySet<int> BookedStartHoursUtc { get; }

    /// <summary>
    /// Derives history using class facts where the class is known. When a class is
    /// not in <paramref name="classesById"/>, falls back to an "instructorId" field in
    /// Complete metadata and to the Book event's own timestamp.
    /// </summary>
    public static ScoringHistory From(
        IEnumerable<Interaction> interactions,
        IReadOnlyDictionary<string, Class>? classesById = null)
    {
        var completions = new Dictionary<string, int>();
        var hours = new HashSet<int>();
        foreach (var interaction in interactions)
        {
            Class? classItem = null;
            classesById?.TryGetValue(interaction.ItemId, out classItem);
            if (interaction.EventType == EventTypes.Complete)
            {
                var instructorId = classItem?.InstructorId ?? InstructorFromMetadata(interaction.Metadata);
                if (!string.IsNullOrEmpty(instructorId))
                    completions[instructorId] = completions.GetValueOrDefault(instructorId) + 1;
            }
            else if (interaction.EventType == EventTypes.Book)
            {
                hours.Add((classItem?.StartTime ?? interaction.Timestamp).Hour);
            }
        }

        return new ScoringHistory(completions, hours);
    }

    private static string? InstructorFromMetadata(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata) || metadata == "{}")
            return null;
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadata);
            return values != null
                   && values.TryGetValue("instructorId", out var element)
                   && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
