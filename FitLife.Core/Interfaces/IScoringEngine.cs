using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

/// <summary>
/// Interface for the recommendation scoring engine
/// Calculates multi-factor scores to rank class recommendations
/// </summary>
public interface IScoringEngine
{
    /// <summary>
    /// Calculates a personalized score for a class based on user profile and interaction history
    /// </summary>
    /// <param name="user">User requesting recommendations</param>
    /// <param name="classItem">Class to score</param>
    /// <param name="userInteractions">User's interaction history for pattern analysis</param>
    /// <returns>Score between 0 and ~150 (higher is better)</returns>
    double CalculateScore(User user, Class classItem, List<Interaction> userInteractions);
}
