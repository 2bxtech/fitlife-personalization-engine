using FitLife.Core.Models;

namespace FitLife.Core.Services;

/// <summary>
/// Builds the one-line recommendation reason from the factors that actually scored.
/// It never adds a claim that a factor did not produce points for.
/// </summary>
public static class RecommendationReasons
{
    private const int MaxClauses = 2;

    // Personal factors explain "why you"; class qualities follow.
    private static readonly string[] Priority =
    {
        ScoreFactorKeys.ClassType,
        ScoreFactorKeys.Instructor,
        ScoreFactorKeys.ActivityProfile,
        ScoreFactorKeys.TimeOfDay,
        ScoreFactorKeys.Rating,
        ScoreFactorKeys.Popularity,
        ScoreFactorKeys.StartsSoon
    };

    /// <summary>A rating only counts as a reason when it is excellent (4.7 or higher).</summary>
    private const double ExcellentRatingPoints = 9.4;

    public static string Compose(IEnumerable<ScoreFactor> factors)
    {
        var byKey = factors.ToDictionary(factor => factor.Key);
        var clauses = Priority
            .Where(byKey.ContainsKey)
            .Select(key => byKey[key])
            .Where(IsReason)
            .Take(MaxClauses)
            .Select(factor => factor.Detail)
            .ToList();

        return clauses.Count == 0
            ? "Ranked on level fit, rating, and schedule; no personal signals yet."
            : string.Join(". ", clauses) + ".";
    }

    private static bool IsReason(ScoreFactor factor) => factor.Key switch
    {
        ScoreFactorKeys.Rating => factor.Points >= ExcellentRatingPoints,
        ScoreFactorKeys.Popularity => factor.Points >= 8,
        _ => factor.Points > 0
    };
}
