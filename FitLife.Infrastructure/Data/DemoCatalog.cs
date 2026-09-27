using System.Text.Json;
using FitLife.Core.Models;

namespace FitLife.Infrastructure.Data;

/// <summary>A synthetic member a visitor can explore with one click.</summary>
/// <param name="Id">Stable persona id used in URLs.</param>
/// <param name="UserId">Seeded user the persona signs in as.</param>
/// <param name="Headline">One-line summary shown on the persona card.</param>
/// <param name="Summary">What the visitor should expect to see.</param>
public sealed record DemoPersona(string Id, string UserId, string Headline, string Summary);

/// <summary>
/// Canonical synthetic demo data. Schedules are relative to a supplied clock, and
/// histories are explicit (no randomness), so every reset produces the same
/// recommendations. Each history is chosen so the scheduled profiler assigns the
/// same segment the persona is seeded with.
/// </summary>
public static class DemoCatalog
{
    public static readonly IReadOnlyList<DemoPersona> Personas = new[]
    {
        new DemoPersona("sarah", "user_001", "Yoga regular, trains in the morning",
            "Six recent yoga and Pilates classes with one instructor, so morning yoga with Sarah Martinez ranks first."),
        new DemoPersona("mike", "user_002", "Strength and HIIT, trains in the evening",
            "Advanced member with evening HIIT and strength history, so evening high-intensity classes rank first."),
        new DemoPersona("emily", "user_003", "New member, still exploring",
            "Only one completed class, so rankings lean on level fit, stated preferences, and class quality.")
    };

    /// <summary>Enrollment from members outside the demo; active demo bookings are added on top.</summary>
    public static int BaselineEnrollment(string classId) =>
        ClassSpecs.Single(spec => spec.Id == classId).BaselineEnrollment;

    public static List<User> Users(DateTime now) => UserSpecs.Select(spec => spec.ToUser(now)).ToList();

    public static User User(string userId, DateTime now) =>
        UserSpecs.Single(spec => spec.Id == userId).ToUser(now);

    /// <summary>Display facts for a persona card, without building a user or hashing.</summary>
    public static (string FirstName, string FitnessLevel, string[] PreferredTypes) Profile(string userId)
    {
        var spec = UserSpecs.Single(s => s.Id == userId);
        return (spec.FirstName, spec.FitnessLevel, spec.PreferredTypes);
    }

    // Hashed once per process: anonymous demo routes must not pay for BCrypt per request.
    private static readonly Lazy<string> DemoPasswordHash =
        new(() => BCrypt.Net.BCrypt.HashPassword("Demo123!", workFactor: 10));

    public static List<Class> Classes(DateTime now)
    {
        var tomorrow = now.Date.AddDays(1);
        var saturday = NextOnOrAfter(tomorrow, DayOfWeek.Saturday);
        var sunday = NextOnOrAfter(tomorrow, DayOfWeek.Sunday);
        return ClassSpecs.Select(spec =>
        {
            var day = spec.Day switch
            {
                ScheduleDay.Saturday => saturday,
                ScheduleDay.Sunday => sunday,
                _ => tomorrow.AddDays(spec.DaysAfterTomorrow)
            };
            return new Class
            {
                Id = spec.Id,
                Name = spec.Name,
                Type = spec.Type,
                InstructorId = spec.InstructorId,
                InstructorName = spec.InstructorName,
                Level = spec.Level,
                Description = spec.Description,
                StartTime = day.Add(spec.StartUtc),
                DurationMinutes = spec.DurationMinutes,
                Capacity = spec.Capacity,
                CurrentEnrollment = spec.BaselineEnrollment,
                AverageRating = spec.Rating,
                TotalRatings = spec.TotalRatings,
                WeeklyBookings = spec.WeeklyBookings,
                IsActive = true,
                CreatedAt = now.AddDays(-60),
                UpdatedAt = now
            };
        }).ToList();
    }

    /// <summary>The fixed interaction history for one seeded user, timed relative to <paramref name="now"/>.</summary>
    public static List<Interaction> History(string userId, DateTime now)
    {
        var classes = ClassSpecs.ToDictionary(spec => spec.Id);
        var interactions = new List<Interaction>();
        foreach (var (classId, daysAgo) in Histories.GetValueOrDefault(userId, Array.Empty<(string, int)>()))
        {
            var spec = classes[classId];
            // The session the member attended, at the series' usual start time.
            var attended = now.Date.AddDays(-daysAgo).Add(spec.StartUtc);
            interactions.Add(Event(userId, classId, EventTypes.View, attended.AddDays(-2), new { source = "browse" }));
            interactions.Add(Event(userId, classId, EventTypes.Book, attended.AddDays(-1), null));
            interactions.Add(Event(userId, classId, EventTypes.Complete, attended.AddMinutes(spec.DurationMinutes), null));
        }

        foreach (var (classId, daysAgo) in Browsing.GetValueOrDefault(userId, Array.Empty<(string, int)>()))
            interactions.Add(Event(userId, classId, EventTypes.View, now.Date.AddDays(-daysAgo).AddHours(12), new { source = "browse" }));

        return interactions;
    }

    private static Interaction Event(string userId, string classId, string type, DateTime at, object? metadata) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserId = userId,
        ItemId = classId,
        ItemType = "Class",
        EventType = type,
        Timestamp = at,
        Metadata = metadata == null ? "{}" : JsonSerializer.Serialize(metadata)
    };

    private static DateTime NextOnOrAfter(DateTime date, DayOfWeek day) =>
        date.AddDays(((int)day - (int)date.DayOfWeek + 7) % 7);

    // Completed sessions as (series id, days ago). All fall inside the profiler's
    // 30-day window and keep weekly frequency below the HighlyActive threshold.
    private static readonly Dictionary<string, (string ClassId, int DaysAgo)[]> Histories = new()
    {
        // 5 of 6 Yoga (83%) -> YogaEnthusiast; all with Sarah Martinez; morning starts.
        ["user_001"] = new[]
        {
            ("class_001", 3), ("class_001", 10), ("class_002", 5), ("class_002", 12),
            ("class_009", 8), ("class_008", 15)
        },
        // 4 of 6 HIIT (67%) -> StrengthTrainer; evening starts.
        ["user_002"] = new[]
        {
            ("class_003", 2), ("class_003", 9), ("class_004", 4), ("class_004", 11),
            ("class_007", 6), ("class_007", 13)
        },
        // Fewer than 5 completions -> Beginner.
        ["user_003"] = new[] { ("class_002", 6) },
        // 5 of 6 Spin (83%) -> CardioLover.
        ["user_004"] = new[]
        {
            ("class_005", 2), ("class_005", 9), ("class_005", 16), ("class_006", 4),
            ("class_006", 11), ("class_003", 7)
        },
        // 4 of 6 Strength (67%) -> StrengthTrainer.
        ["user_005"] = new[]
        {
            ("class_007", 3), ("class_007", 10), ("class_012", 5), ("class_012", 12),
            ("class_003", 7), ("class_003", 14)
        }
    };

    private static readonly Dictionary<string, (string ClassId, int DaysAgo)[]> Browsing = new()
    {
        ["user_003"] = new[] { ("class_011", 3), ("class_010", 2) }
    };

    private enum ScheduleDay { Relative, Saturday, Sunday }

    private sealed record ClassSpec(
        string Id, string Name, string Type, string InstructorId, string InstructorName,
        string Level, string Description, int DaysAfterTomorrow, TimeSpan StartUtc,
        int DurationMinutes, int Capacity, int BaselineEnrollment, decimal Rating,
        int TotalRatings, int WeeklyBookings, ScheduleDay Day = ScheduleDay.Relative);

    private static readonly ClassSpec[] ClassSpecs =
    {
        new("class_001", "Morning Vinyasa Flow", "Yoga", "inst_sarah", "Sarah Martinez", "All Levels",
            "Start your day with energizing flow sequences.", 0, TimeSpan.FromHours(6), 60, 30, 18, 4.8m, 124, 85),
        new("class_002", "Gentle Yoga", "Yoga", "inst_sarah", "Sarah Martinez", "Beginner",
            "Relaxing stretches and breathing exercises.", 0, TimeSpan.FromHours(7.5), 45, 25, 12, 4.9m, 98, 62),
        new("class_003", "Intense HIIT Workout", "HIIT", "inst_marcus", "Marcus Thompson", "Advanced",
            "High-intensity intervals to maximize calorie burn.", 0, TimeSpan.FromHours(18), 45, 20, 14, 4.7m, 156, 94),
        new("class_004", "HIIT for Beginners", "HIIT", "inst_marcus", "Marcus Thompson", "Beginner",
            "An introduction to interval training with scaled options.", 0, TimeSpan.FromHours(19), 45, 25, 8, 4.6m, 67, 48),
        new("class_005", "Power Spin", "Spin", "inst_lisa", "Lisa Chen", "Intermediate",
            "Intense cycling intervals set to music.", 0, TimeSpan.FromHours(17.5), 45, 35, 27, 4.9m, 201, 112),
        new("class_006", "Morning Spin & Strength", "Spin", "inst_lisa", "Lisa Chen", "All Levels",
            "Cardio cycling plus bodyweight exercises.", 0, TimeSpan.FromHours(8), 50, 30, 15, 4.8m, 88, 67),
        new("class_007", "Total Body Strength", "Strength", "inst_jake", "Jake Williams", "Intermediate",
            "Full-body resistance training with coached form.", 0, TimeSpan.FromHours(18.5), 60, 20, 13, 4.7m, 76, 78),
        new("class_008", "Core Pilates", "Pilates", "inst_sarah", "Sarah Martinez", "All Levels",
            "Core strengthening with controlled movements.", 0, TimeSpan.FromHours(9), 45, 22, 10, 4.8m, 64, 54),
        new("class_009", "Saturday Morning Yoga Flow", "Yoga", "inst_sarah", "Sarah Martinez", "All Levels",
            "A longer weekend flow for all levels.", 0, TimeSpan.FromHours(8), 75, 35, 22, 4.9m, 143, 89,
            ScheduleDay.Saturday),
        new("class_010", "Sunday Stretch & Restore", "Yoga", "inst_sarah", "Sarah Martinez", "Beginner",
            "Gentle restorative yoga for recovery.", 0, TimeSpan.FromHours(9), 60, 25, 8, 5.0m, 51, 72,
            ScheduleDay.Sunday),
        new("class_011", "Beginner Walking Club", "Walking", "inst_nina", "Nina Patel", "Beginner",
            "A brisk, social outdoor walk at a conversational pace.", 1, TimeSpan.FromHours(12), 45, 30, 9, 4.6m, 32, 24),
        new("class_012", "Advanced Strength Circuit", "Strength", "inst_jake", "Jake Williams", "Advanced",
            "Heavy compound lifts in a timed circuit.", 1, TimeSpan.FromHours(19), 60, 16, 9, 4.8m, 58, 41)
    };

    private sealed record UserSpec(
        string Id, string Email, string FirstName, string LastName, string FitnessLevel,
        string[] Goals, string[] PreferredTypes, string Segment, int MemberForDays)
    {
        public User ToUser(DateTime now) => new()
        {
            Id = Id,
            Email = Email,
            // Personas sign in through the demo session endpoint; this login is a
            // local convenience only and is documented in DEMO_SETUP.md.
            PasswordHash = DemoPasswordHash.Value,
            FirstName = FirstName,
            LastName = LastName,
            FitnessLevel = FitnessLevel,
            Goals = JsonSerializer.Serialize(Goals),
            PreferredClassTypes = JsonSerializer.Serialize(PreferredTypes),
            Segment = Segment,
            CreatedAt = now.AddDays(-MemberForDays),
            UpdatedAt = now
        };
    }

    private static readonly UserSpec[] UserSpecs =
    {
        new("user_001", "sarah.johnson@example.com", "Sarah", "Johnson", "Intermediate",
            new[] { "Flexibility", "Stress Relief" }, new[] { "Yoga", "Pilates" }, "YogaEnthusiast", 90),
        new("user_002", "mike.chen@example.com", "Mike", "Chen", "Advanced",
            new[] { "Muscle Building", "Endurance" }, new[] { "HIIT", "Strength", "Spin" }, "StrengthTrainer", 120),
        new("user_003", "emily.rodriguez@example.com", "Emily", "Rodriguez", "Beginner",
            new[] { "General Fitness", "Stress Relief" }, new[] { "Yoga", "Walking" }, "Beginner", 14),
        new("user_004", "david.kim@example.com", "David", "Kim", "Intermediate",
            new[] { "Cardio Health" }, new[] { "Spin", "Running", "Cardio" }, "CardioLover", 60),
        new("user_005", "jessica.taylor@example.com", "Jessica", "Taylor", "Advanced",
            new[] { "Strength" }, new[] { "Strength", "HIIT" }, "StrengthTrainer", 180)
    };
}
