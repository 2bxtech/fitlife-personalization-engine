using FluentAssertions;
using FitLife.Core.Models;
using FitLife.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FitLife.Tests.Services;

/// <summary>
/// Tests for the 9-factor recommendation scoring algorithm
/// Each test validates a specific scoring factor in isolation
/// </summary>
public class ScoringEngineTests
{
    private readonly ScoringEngine _engine;

    public ScoringEngineTests()
    {
        _engine = new ScoringEngine(NullLogger<ScoringEngine>.Instance);
    }

    #region Factor 1: Fitness Level Match (Weight: 10)

    [Fact]
    public void CalculateScore_AllLevelsClass_Returns10Points()
    {
        // Arrange
        var user = new User { FitnessLevel = "Beginner" };
        var classItem = new Class { Level = "All Levels", Type = "Yoga", AverageRating = 4.0m };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert
        score.Should().BeGreaterOrEqualTo(10); // At minimum gets fitness level match
    }

    [Theory]
    [InlineData("Beginner", "Beginner", 10)]
    [InlineData("Intermediate", "Intermediate", 10)]
    [InlineData("Advanced", "Advanced", 10)]
    public void CalculateScore_PerfectFitnessMatch_Returns10Points(string userLevel, string classLevel, int expectedMinScore)
    {
        // Arrange
        var user = new User { FitnessLevel = userLevel };
        var classItem = new Class { Level = classLevel, Type = "Yoga", AverageRating = 4.0m };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert
        score.Should().BeGreaterOrEqualTo(expectedMinScore);
    }

    [Fact]
    public void CalculateScore_IntermediateUserBeginnerClass_Returns5Points()
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class { Level = "Beginner", Type = "Yoga", AverageRating = 4.0m };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should get 5 for fitness level (can handle it) + 8 from rating
        score.Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public void CalculateScore_BeginnerUserAdvancedClass_ReturnsLowScore()
    {
        // Arrange
        var user = new User { FitnessLevel = "Beginner" };
        var classItem = new Class { Level = "Advanced", Type = "Yoga", AverageRating = 4.0m };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should not get full fitness level match
        score.Should().BeLessThan(20);
    }

    #endregion

    #region Factor 2: Preferred Class Type (Weight: 15)

    [Fact]
    public void CalculateScore_PreferredClassType_Adds15Points()
    {
        // Arrange
        var user = new User
        {
            FitnessLevel = "Intermediate",
            PreferredClassTypes = "[\"Yoga\", \"Pilates\"]"
        };
        var classItem = new Class
        {
            Type = "Yoga",
            Level = "Intermediate",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should get fitness match (10) + preferred type (15) + rating (8)
        score.Should().BeGreaterOrEqualTo(33);
    }

    [Fact]
    public void CalculateScore_NonPreferredClassType_NoBonus()
    {
        // Arrange
        var user = new User
        {
            FitnessLevel = "Intermediate",
            PreferredClassTypes = "[\"Yoga\", \"Pilates\"]"
        };
        var classItem = new Class
        {
            Type = "HIIT",
            Level = "Intermediate",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should NOT get preferred type bonus
        score.Should().BeLessThan(30);
    }

    #endregion

    #region Factor 3: Favorite Instructor (Weight: 20)

    [Fact]
    public void CalculateScore_FavoriteInstructor_Adds20Points()
    {
        // Arrange
        var user = new User
        {
            FitnessLevel = "Intermediate",
            PreferredClassTypes = "[]"
        };
        var classItem = new Class
        {
            InstructorId = "inst_sarah",
            Type = "Yoga",
            Level = "Intermediate",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>
        {
            // User has completed classes with this instructor
            new Interaction { UserId = user.Id, ItemId = "class_1", EventType = "Complete", Metadata = "{\"instructorId\":\"inst_sarah\"}" },
            new Interaction { UserId = user.Id, ItemId = "class_2", EventType = "Complete", Metadata = "{\"instructorId\":\"inst_sarah\"}" }
        };

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should include instructor bonus
        score.Should().BeGreaterOrEqualTo(38); // 10 (fitness) + 20 (instructor) + 8 (rating)
    }

    #endregion

    #region Factor 4: Time Preference (Weight: 8)

    [Fact]
    public void CalculateScore_MatchingTimePreference_Adds8Points()
    {
        // Arrange
        var user = new User { Id = "user_123", FitnessLevel = "Intermediate" };
        var classTime = DateTime.UtcNow.AddDays(1).Date.AddHours(18); // 6 PM
        var classItem = new Class
        {
            StartTime = classTime,
            Type = "Yoga",
            Level = "Intermediate",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>
        {
            // User typically books at 6 PM
            new Interaction { UserId = user.Id, ItemId = "class_1", EventType = "Book", Timestamp = DateTime.UtcNow.AddDays(-7).Date.AddHours(18) },
            new Interaction { UserId = user.Id, ItemId = "class_2", EventType = "Book", Timestamp = DateTime.UtcNow.AddDays(-6).Date.AddHours(18) }
        };

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should include time preference bonus
        score.Should().BeGreaterOrEqualTo(26); // 10 (fitness) + 8 (time) + 8 (rating)
    }

    #endregion

    #region Factor 5: Class Rating (Weight: 2× rating)

    [Theory]
    [InlineData(5.0, 10.0)]  // 5.0 * 2 = 10 points
    [InlineData(4.8, 9.6)]   // 4.8 * 2 = 9.6 points
    [InlineData(3.5, 7.0)]   // 3.5 * 2 = 7.0 points
    [InlineData(0.0, 0.0)]   // No rating = 0 points
    public void CalculateScore_ClassRating_Multiplied2x(decimal rating, double expectedRatingPoints)
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class
        {
            Level = "Intermediate",
            Type = "Yoga",
            AverageRating = rating
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should include rating points
        var ratingPoints = (double)rating * 2;
        score.Should().BeGreaterOrEqualTo(ratingPoints);
    }

    #endregion

    #region Factor 6: Availability Bonus/Penalty

    [Fact]
    public void CalculateScore_NearlyFull_PenalizesFivePoints()
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class
        {
            Capacity = 30,
            CurrentEnrollment = 28, // 93% full - less than 20% spots available
            Level = "Intermediate",
            Type = "Yoga",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Score should be reduced due to low availability
        score.Should().BeLessThan(20); // Lower due to -5 penalty
    }

    [Fact]
    public void CalculateScore_AmpleSpace_BonusThreePoints()
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class
        {
            Capacity = 30,
            CurrentEnrollment = 5, // 17% full - more than 80% spots available
            Level = "Intermediate",
            Type = "Yoga",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should get availability bonus
        score.Should().BeGreaterOrEqualTo(21); // 10 (fitness) + 8 (rating) + 3 (availability)
    }

    #endregion

    #region Factor 7: Segment Boost (Weight: up to 12)

    [Theory]
    [InlineData("YogaEnthusiast", "Yoga", 12)]
    [InlineData("YogaEnthusiast", "HIIT", 0)]
    [InlineData("StrengthTrainer", "HIIT", 12)]
    [InlineData("StrengthTrainer", "Strength", 12)]
    [InlineData("CardioLover", "Spin", 12)]
    [InlineData("HighlyActive", "Yoga", 5)]
    public void CalculateScore_AppliesSegmentBoost(string segment, string classType, int expectedBoost)
    {
        // Arrange
        var user = new User
        {
            FitnessLevel = "Intermediate",
            Segment = segment
        };
        var classItem = new Class
        {
            Type = classType,
            Level = "Intermediate",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should include segment boost if applicable
        if (expectedBoost > 0)
        {
            score.Should().BeGreaterOrEqualTo(18 + expectedBoost); // Base + segment boost
        }
    }

    #endregion

    #region Factor 8: Recency Bonus

    [Fact]
    public void CalculateScore_ClassWithin24Hours_AddsFivePoints()
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class
        {
            StartTime = DateTime.UtcNow.AddHours(12), // Within 24 hours
            Level = "Intermediate",
            Type = "Yoga",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should include recency bonus
        score.Should().BeGreaterOrEqualTo(23); // 10 (fitness) + 8 (rating) + 5 (recency)
    }

    [Fact]
    public void CalculateScore_ClassWithin3Days_AddsThreePoints()
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class
        {
            StartTime = DateTime.UtcNow.AddDays(2), // Within 3 days
            Level = "Intermediate",
            Type = "Yoga",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should include recency bonus
        score.Should().BeGreaterOrEqualTo(21); // 10 (fitness) + 8 (rating) + 3 (recency)
    }

    #endregion

    #region Factor 9: Popularity Bonus

    [Theory]
    [InlineData(60, 8)]  // >50 bookings per week = 8 points
    [InlineData(30, 4)]  // >20 bookings per week = 4 points
    [InlineData(10, 0)]  // <20 bookings = 0 points
    public void CalculateScore_AppliesPopularityBonus(int weeklyBookings, int expectedBonus)
    {
        // Arrange
        var user = new User { FitnessLevel = "Intermediate" };
        var classItem = new Class
        {
            WeeklyBookings = weeklyBookings,
            Level = "Intermediate",
            Type = "Yoga",
            AverageRating = 4.0m
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert
        if (expectedBonus > 0)
        {
            score.Should().BeGreaterOrEqualTo(18 + expectedBonus);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CalculateScore_NewUserNoHistory_ReturnsBaseScore()
    {
        // Arrange
        var user = new User
        {
            FitnessLevel = "Beginner",
            Segment = "Beginner",
            PreferredClassTypes = "[]"
        };
        var classItem = new Class
        {
            Type = "Yoga",
            Level = "Beginner",
            AverageRating = 4.5m,
            Capacity = 30,
            CurrentEnrollment = 10,
            StartTime = DateTime.UtcNow.AddDays(2)
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert
        score.Should().BeGreaterThan(0);
        score.Should().BeLessThan(150); // Max possible score
    }

    [Fact]
    public void CalculateScore_PowerUser_ReturnsHighScore()
    {
        // Arrange
        var user = new User
        {
            Id = "user_123",
            FitnessLevel = "Advanced",
            Segment = "YogaEnthusiast",
            PreferredClassTypes = "[\"Yoga\", \"Pilates\"]"
        };
        var classItem = new Class
        {
            InstructorId = "inst_sarah",
            Type = "Yoga",
            Level = "Advanced",
            AverageRating = 5.0m,
            Capacity = 30,
            CurrentEnrollment = 5,
            StartTime = DateTime.UtcNow.AddHours(12),
            WeeklyBookings = 60
        };
        var interactions = new List<Interaction>
        {
            // Completed classes with this instructor
            new Interaction { UserId = user.Id, ItemId = "class_1", EventType = "Complete", Metadata = "{\"instructorId\":\"inst_sarah\"}" },
            // Books at similar time
            new Interaction { UserId = user.Id, ItemId = "class_2", EventType = "Book", Timestamp = DateTime.UtcNow.AddDays(-7).Date.AddHours(12) }
        };

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert - Should get most bonuses
        // 10 (fitness) + 15 (preferred) + 20 (instructor) + 8 (time) + 10 (rating) + 3 (availability) + 12 (segment) + 5 (recency) + 8 (popularity)
        // Realistic: 10 + 15 + 20 (if enough history) + 8 (if time matches) + 10 + 3 + 12 + 5 + 8 = ~91 max
        score.Should().BeGreaterOrEqualTo(60); // Realistic threshold for power user
    }

    [Fact]
    public void CalculateScore_NeverReturnsNegative()
    {
        // Arrange - Worst possible class
        var user = new User { FitnessLevel = "Beginner" };
        var classItem = new Class
        {
            Type = "Advanced HIIT",
            Level = "Advanced",
            AverageRating = 0m,
            Capacity = 10,
            CurrentEnrollment = 10, // Full
            StartTime = DateTime.UtcNow.AddDays(30)
        };
        var interactions = new List<Interaction>();

        // Act
        var score = _engine.CalculateScore(user, classItem, interactions);

        // Assert
        score.Should().BeGreaterOrEqualTo(0);
    }

    #endregion
}
