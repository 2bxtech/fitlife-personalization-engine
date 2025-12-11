using FitLife.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FitLife.Infrastructure.Data;

/// <summary>
/// Seeds the database with sample data for demo purposes
/// </summary>
public class DbSeeder
{
    private readonly FitLifeDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(FitLifeDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Check if already seeded
            if (await _context.Users.AnyAsync())
            {
                _logger.LogInformation("Database already seeded, skipping...");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            // Seed Users
            var users = CreateSampleUsers();
            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} users", users.Count);

            // Seed Classes
            var classes = CreateSampleClasses();
            await _context.Classes.AddRangeAsync(classes);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} classes", classes.Count);

            // Seed Interactions (user activity)
            var interactions = CreateSampleInteractions(users, classes);
            await _context.Interactions.AddRangeAsync(interactions);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} interactions", interactions.Count);

            _logger.LogInformation("Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }

    private List<User> CreateSampleUsers()
    {
        return new List<User>
        {
            new User
            {
                Id = "user_001",
                Email = "sarah.johnson@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!", workFactor: 12),
                FirstName = "Sarah",
                LastName = "Johnson",
                FitnessLevel = "Intermediate",
                Goals = JsonSerializer.Serialize(new[] { "Weight Loss", "Flexibility" }),
                PreferredClassTypes = JsonSerializer.Serialize(new[] { "Yoga", "Pilates" }),
                Segment = "YogaEnthusiast",
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            },
            new User
            {
                Id = "user_002",
                Email = "mike.chen@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!", workFactor: 12),
                FirstName = "Mike",
                LastName = "Chen",
                FitnessLevel = "Advanced",
                Goals = JsonSerializer.Serialize(new[] { "Muscle Building", "Endurance" }),
                PreferredClassTypes = JsonSerializer.Serialize(new[] { "HIIT", "Strength", "Spin" }),
                Segment = "HighlyActive",
                CreatedAt = DateTime.UtcNow.AddDays(-120)
            },
            new User
            {
                Id = "user_003",
                Email = "emily.rodriguez@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!", workFactor: 12),
                FirstName = "Emily",
                LastName = "Rodriguez",
                FitnessLevel = "Beginner",
                Goals = JsonSerializer.Serialize(new[] { "General Fitness", "Stress Relief" }),
                PreferredClassTypes = JsonSerializer.Serialize(new[] { "Yoga", "Walking" }),
                Segment = "Beginner",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new User
            {
                Id = "user_004",
                Email = "david.kim@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!", workFactor: 12),
                FirstName = "David",
                LastName = "Kim",
                FitnessLevel = "Intermediate",
                Goals = JsonSerializer.Serialize(new[] { "Cardio Health", "Weight Loss" }),
                PreferredClassTypes = JsonSerializer.Serialize(new[] { "Spin", "Running", "Cardio" }),
                Segment = "CardioLover",
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            new User
            {
                Id = "user_005",
                Email = "jessica.taylor@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!", workFactor: 12),
                FirstName = "Jessica",
                LastName = "Taylor",
                FitnessLevel = "Advanced",
                Goals = JsonSerializer.Serialize(new[] { "Strength", "Muscle Definition" }),
                PreferredClassTypes = JsonSerializer.Serialize(new[] { "Strength", "HIIT" }),
                Segment = "StrengthTrainer",
                CreatedAt = DateTime.UtcNow.AddDays(-180)
            }
        };
    }

    private List<Class> CreateSampleClasses()
    {
        var baseTime = DateTime.UtcNow.Date.AddDays(1).AddHours(6); // Tomorrow at 6 AM

        return new List<Class>
        {
            // Morning Yoga Classes
            new Class
            {
                Id = "class_001",
                Name = "Morning Vinyasa Flow",
                Type = "Yoga",
                InstructorId = "inst_sarah",
                InstructorName = "Sarah Martinez",
                Level = "All Levels",
                Description = "Start your day with energizing flow sequences",
                StartTime = baseTime,
                DurationMinutes = 60,
                Capacity = 30,
                CurrentEnrollment = 18,
                AverageRating = 4.8m,
                WeeklyBookings = 85,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            new Class
            {
                Id = "class_002",
                Name = "Gentle Yoga",
                Type = "Yoga",
                InstructorId = "inst_sarah",
                InstructorName = "Sarah Martinez",
                Level = "Beginner",
                Description = "Relaxing stretches and breathing exercises",
                StartTime = baseTime.AddHours(1.5),
                DurationMinutes = 45,
                Capacity = 25,
                CurrentEnrollment = 12,
                AverageRating = 4.9m,
                WeeklyBookings = 62,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            // HIIT Classes
            new Class
            {
                Id = "class_003",
                Name = "Intense HIIT Workout",
                Type = "HIIT",
                InstructorId = "inst_marcus",
                InstructorName = "Marcus Thompson",
                Level = "Advanced",
                Description = "High-intensity intervals to maximize calorie burn",
                StartTime = baseTime.AddHours(12), // 6 PM
                DurationMinutes = 45,
                Capacity = 20,
                CurrentEnrollment = 19,
                AverageRating = 4.7m,
                WeeklyBookings = 94,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-45)
            },
            new Class
            {
                Id = "class_004",
                Name = "HIIT for Beginners",
                Type = "HIIT",
                InstructorId = "inst_marcus",
                InstructorName = "Marcus Thompson",
                Level = "Beginner",
                Description = "Introduction to high-intensity training",
                StartTime = baseTime.AddHours(13), // 7 PM
                DurationMinutes = 30,
                Capacity = 25,
                CurrentEnrollment = 8,
                AverageRating = 4.6m,
                WeeklyBookings = 48,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-45)
            },
            // Spin Classes
            new Class
            {
                Id = "class_005",
                Name = "Power Spin",
                Type = "Spin",
                InstructorId = "inst_lisa",
                InstructorName = "Lisa Chen",
                Level = "Intermediate",
                Description = "Intense cycling with energizing music",
                StartTime = baseTime.AddHours(11.5), // 5:30 PM
                DurationMinutes = 45,
                Capacity = 35,
                CurrentEnrollment = 32,
                AverageRating = 4.9m,
                WeeklyBookings = 112,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            },
            new Class
            {
                Id = "class_006",
                Name = "Morning Spin & Strength",
                Type = "Spin",
                InstructorId = "inst_lisa",
                InstructorName = "Lisa Chen",
                Level = "All Levels",
                Description = "Cardio cycling plus bodyweight exercises",
                StartTime = baseTime.AddHours(2), // 8 AM
                DurationMinutes = 60,
                Capacity = 30,
                CurrentEnrollment = 15,
                AverageRating = 4.8m,
                WeeklyBookings = 67,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            },
            // Strength Training
            new Class
            {
                Id = "class_007",
                Name = "Total Body Strength",
                Type = "Strength",
                InstructorId = "inst_jake",
                InstructorName = "Jake Williams",
                Level = "Intermediate",
                Description = "Full-body resistance training",
                StartTime = baseTime.AddHours(12.5), // 6:30 PM
                DurationMinutes = 60,
                Capacity = 20,
                CurrentEnrollment = 16,
                AverageRating = 4.7m,
                WeeklyBookings = 78,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            // Pilates
            new Class
            {
                Id = "class_008",
                Name = "Core Pilates",
                Type = "Pilates",
                InstructorId = "inst_sarah",
                InstructorName = "Sarah Martinez",
                Level = "All Levels",
                Description = "Core strengthening with controlled movements",
                StartTime = baseTime.AddHours(3), // 9 AM
                DurationMinutes = 50,
                Capacity = 22,
                CurrentEnrollment = 10,
                AverageRating = 4.8m,
                WeeklyBookings = 54,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            // Weekend Classes
            new Class
            {
                Id = "class_009",
                Name = "Saturday Morning Yoga Flow",
                Type = "Yoga",
                InstructorId = "inst_sarah",
                InstructorName = "Sarah Martinez",
                Level = "All Levels",
                Description = "Weekend energizing flow",
                StartTime = baseTime.AddDays(5).AddHours(2), // Next Saturday 8 AM
                DurationMinutes = 75,
                Capacity = 35,
                CurrentEnrollment = 22,
                AverageRating = 4.9m,
                WeeklyBookings = 89,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            },
            new Class
            {
                Id = "class_010",
                Name = "Sunday Stretch & Restore",
                Type = "Yoga",
                InstructorId = "inst_sarah",
                InstructorName = "Sarah Martinez",
                Level = "Beginner",
                Description = "Gentle restorative yoga for recovery",
                StartTime = baseTime.AddDays(6).AddHours(3), // Next Sunday 9 AM
                DurationMinutes = 60,
                Capacity = 25,
                CurrentEnrollment = 8,
                AverageRating = 5.0m,
                WeeklyBookings = 72,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            }
        };
    }

    private List<Interaction> CreateSampleInteractions(List<User> users, List<Class> classes)
    {
        var interactions = new List<Interaction>();
        var random = new Random(42); // Fixed seed for reproducibility

        // Sarah (user_001) - Yoga Enthusiast
        var sarah = users[0];
        AddUserInteractions(interactions, sarah.Id, new[] { "class_001", "class_002", "class_008", "class_009" }, 
            random, completionRate: 0.9);

        // Mike (user_002) - Highly Active
        var mike = users[1];
        AddUserInteractions(interactions, mike.Id, new[] { "class_003", "class_005", "class_007", "class_004" }, 
            random, completionRate: 0.95);

        // Emily (user_003) - Beginner
        var emily = users[2];
        AddUserInteractions(interactions, emily.Id, new[] { "class_002", "class_004", "class_010" }, 
            random, completionRate: 0.7);

        // David (user_004) - Cardio Lover
        var david = users[3];
        AddUserInteractions(interactions, david.Id, new[] { "class_005", "class_006", "class_003" }, 
            random, completionRate: 0.85);

        // Jessica (user_005) - Strength Trainer
        var jessica = users[4];
        AddUserInteractions(interactions, jessica.Id, new[] { "class_007", "class_003", "class_004" }, 
            random, completionRate: 0.9);

        return interactions;
    }

    private void AddUserInteractions(List<Interaction> interactions, string userId, string[] classIds, 
        Random random, double completionRate)
    {
        var now = DateTime.UtcNow;

        foreach (var classId in classIds)
        {
            // View event (all classes)
            interactions.Add(new Interaction
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                ItemId = classId,
                ItemType = "Class",
                EventType = "View",
                Timestamp = now.AddDays(-random.Next(7, 30)),
                Metadata = JsonSerializer.Serialize(new { source = "browse" })
            });

            // Book event (80% of viewed)
            if (random.NextDouble() < 0.8)
            {
                interactions.Add(new Interaction
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    ItemId = classId,
                    ItemType = "Class",
                    EventType = "Book",
                    Timestamp = now.AddDays(-random.Next(5, 28)),
                    Metadata = "{}"
                });

                // Complete event (based on completion rate)
                if (random.NextDouble() < completionRate)
                {
                    interactions.Add(new Interaction
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        ItemId = classId,
                        ItemType = "Class",
                        EventType = "Complete",
                        Timestamp = now.AddDays(-random.Next(4, 27)),
                        Metadata = "{}"
                    });

                    // Rate event (70% of completed)
                    if (random.NextDouble() < 0.7)
                    {
                        interactions.Add(new Interaction
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserId = userId,
                            ItemId = classId,
                            ItemType = "Class",
                            EventType = "Rate",
                            Timestamp = now.AddDays(-random.Next(3, 26)),
                            Metadata = JsonSerializer.Serialize(new { rating = random.Next(4, 6) })
                        });
                    }
                }
            }
        }
    }
}
