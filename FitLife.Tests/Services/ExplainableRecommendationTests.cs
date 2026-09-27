using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FitLife.Tests.Services;

/// <summary>
/// The explanation a member sees must be derived from the factors that produced
/// the score, and history facts must come from the classes themselves.
/// </summary>
public class ExplainableRecommendationTests
{
    private readonly ScoringEngine _engine = new(NullLogger<ScoringEngine>.Instance);

    [Fact]
    public void Breakdown_HasNineUniqueFactors_ThatSumToTheRankingScore()
    {
        var user = new User { FitnessLevel = "Intermediate", PreferredClassTypes = "[\"Yoga\"]", Segment = "YogaEnthusiast" };
        var yoga = ClassAt("c1", "Yoga", hour: 18, instructor: "inst_a");

        var breakdown = _engine.Explain(user, yoga, ScoringHistory.Empty);

        breakdown.Factors.Select(f => f.Key).Should().OnlyHaveUniqueItems().And.HaveCount(9);
        breakdown.Total.Should().BeApproximately(breakdown.Factors.Sum(f => f.Points), 1e-9);
        breakdown.Total.Should().Be(_engine.CalculateScore(user, yoga, new List<Interaction>()));
    }

    [Fact]
    public void TimePreference_UsesBookedClassStartTime_NotWhenTheBookingWasMade()
    {
        var booked = ClassAt("booked", "Spin", hour: 18, instructor: "inst_a");
        // Booked at 03:00 for a class that starts at 18:00.
        var interactions = new List<Interaction>
        {
            new() { ItemId = "booked", EventType = EventTypes.Book, Timestamp = DateTime.UtcNow.Date.AddDays(-2).AddHours(3) }
        };
        var history = ScoringHistory.From(interactions, new Dictionary<string, Class> { ["booked"] = booked });
        var user = new User { FitnessLevel = "Intermediate", PreferredClassTypes = "[]" };

        Factor(_engine.Explain(user, ClassAt("evening", "Spin", 18, "inst_b"), history), ScoreFactorKeys.TimeOfDay)
            .Points.Should().Be(8);
        Factor(_engine.Explain(user, ClassAt("night", "Spin", 3, "inst_b"), history), ScoreFactorKeys.TimeOfDay)
            .Points.Should().Be(0);
    }

    [Fact]
    public void TimePreference_TreatsMidnightAsAdjacentHours()
    {
        var bookedAtMidnight = ClassAt("late", "Spin", 0, "inst_a");
        var history = ScoringHistory.From(
            new[] { new Interaction { ItemId = "late", EventType = EventTypes.Book } },
            new Dictionary<string, Class> { ["late"] = bookedAtMidnight });
        var user = new User { FitnessLevel = "Intermediate", PreferredClassTypes = "[]" };

        Factor(_engine.Explain(user, ClassAt("eleven", "Spin", 23, "inst_b"), history), ScoreFactorKeys.TimeOfDay)
            .Points.Should().Be(4);
    }

    [Fact]
    public void UnratedClass_IsDescribedAsUnrated()
    {
        var unrated = ClassAt("new", "Yoga", 9, "inst_a");
        unrated.AverageRating = 0;
        var user = new User { FitnessLevel = "Intermediate", PreferredClassTypes = "[]" };

        Factor(_engine.Explain(user, unrated, ScoringHistory.Empty), ScoreFactorKeys.Rating)
            .Detail.Should().Be("No ratings yet");
    }

    [Fact]
    public void InstructorAffinity_ComesFromCompletedClasses_WithoutMetadata()
    {
        var classes = new Dictionary<string, Class>
        {
            ["done1"] = ClassAt("done1", "Yoga", 7, "inst_sarah"),
            ["done2"] = ClassAt("done2", "Yoga", 8, "inst_sarah")
        };
        var interactions = classes.Keys
            .Select(id => new Interaction { ItemId = id, EventType = EventTypes.Complete, Metadata = "{}" })
            .ToList();
        var user = new User { FitnessLevel = "Intermediate", PreferredClassTypes = "[]" };

        var factor = Factor(
            _engine.Explain(user, ClassAt("next", "Yoga", 9, "inst_sarah"), ScoringHistory.From(interactions, classes)),
            ScoreFactorKeys.Instructor);

        factor.Points.Should().Be(20);
        factor.Detail.Should().Be("You've completed 2 classes with Instructor inst_sarah");
    }

    [Fact]
    public async Task Reasons_OnlyClaimFactorsThatScored_AndFactorsTravelWithTheResult()
    {
        // A yoga-focused member being shown a Spin class: the old explanation claimed
        // "popular among YogaEnthusiast members like you" for every class.
        var user = new User
        {
            Id = "u1", FitnessLevel = "Intermediate", PreferredClassTypes = "[]", Segment = "YogaEnthusiast"
        };
        var spin = ClassAt("spin", "Spin", 18, "inst_lisa");
        spin.AverageRating = 4.0m;
        spin.WeeklyBookings = 10;
        var service = Service(user, new[] { spin });

        var result = (await service.GenerateRecommendationsAsync("u1")).Single();

        result.Reason.Should().NotContainAny("popular among", "profile", "preferred", "instructor");
        result.Factors.Should().HaveCount(9);
        result.Score.Should().BeApproximately(result.Factors.Sum(f => f.Points), 0.01);
        Factor(result.Factors, ScoreFactorKeys.ActivityProfile).Points.Should().Be(0);
    }

    [Fact]
    public void Compose_UsesPersonalFactorsFirst_AndCapsAtTwoClauses()
    {
        var reason = RecommendationReasons.Compose(new[]
        {
            new ScoreFactor(ScoreFactorKeys.Popularity, "Popularity", 8, "112 bookings this week"),
            new ScoreFactor(ScoreFactorKeys.Rating, "Rating", 9.8, "Rated 4.9 out of 5"),
            new ScoreFactor(ScoreFactorKeys.ClassType, "Preferred type", 15, "Yoga is one of your preferred class types"),
            new ScoreFactor(ScoreFactorKeys.ActivityProfile, "Activity profile", 0, "No profile rule applies")
        });

        reason.Should().Be("Yoga is one of your preferred class types. Rated 4.9 out of 5.");
    }

    [Fact]
    public void Compose_WithNoPersonalOrStandoutSignals_SaysSoPlainly()
    {
        RecommendationReasons.Compose(new[]
        {
            new ScoreFactor(ScoreFactorKeys.Rating, "Rating", 8, "Rated 4.0 out of 5"),
            new ScoreFactor(ScoreFactorKeys.FitnessLevel, "Fitness level", 10, "Open to all levels")
        }).Should().Be("Ranked on level fit, rating, and schedule; no personal signals yet.");
    }

    private static ScoreFactor Factor(ScoreBreakdown breakdown, string key) => Factor(breakdown.Factors, key);

    private static ScoreFactor Factor(IEnumerable<ScoreFactor> factors, string key) =>
        factors.Single(factor => factor.Key == key);

    private static Class ClassAt(string id, string type, int hour, string instructor) => new()
    {
        Id = id,
        Name = $"{type} {id}",
        Type = type,
        Level = "All Levels",
        InstructorId = instructor,
        InstructorName = $"Instructor {instructor}",
        StartTime = DateTime.UtcNow.Date.AddDays(5).AddHours(hour),
        Capacity = 20,
        CurrentEnrollment = 10,
        AverageRating = 4.5m,
        WeeklyBookings = 30
    };

    private RecommendationService Service(User user, IEnumerable<Class> candidates)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        var classes = new Mock<IClassRepository>();
        classes.Setup(r => r.GetUpcomingClassesAsync(It.IsAny<int>())).ReturnsAsync(candidates);
        classes.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(Array.Empty<Class>());
        var interactions = new Mock<IInteractionRepository>();
        interactions.Setup(r => r.GetRecentByUserIdAsync(user.Id, It.IsAny<int>())).ReturnsAsync(new List<Interaction>());
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.GetActiveClassIdsAsync(user.Id, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        return new RecommendationService(
            users.Object, classes.Object, interactions.Object,
            Mock.Of<IRecommendationRepository>(), Mock.Of<ICacheService>(), _engine, bookings.Object,
            NullLogger<RecommendationService>.Instance);
    }
}
