# FitLife Recommendation Algorithm Documentation

## Table of Contents
1. [Algorithm Overview](#algorithm-overview)
2. [Scoring Components](#scoring-components)
3. [User Segmentation](#user-segmentation)
4. [Implementation Details](#implementation-details)
5. [Performance Optimization](#performance-optimization)
6. [Evaluation Metrics](#evaluation-metrics)
7. [Future Enhancements](#future-enhancements)

---

## Algorithm Overview

FitLife uses a **rule-based collaborative filtering** approach to generate personalized class recommendations. The system scores each candidate class based on multiple factors and ranks them by relevance to the user.

### Key Principles

1. **Personalization**: Recommendations adapt to individual preferences and behavior
2. **Freshness**: Algorithm considers upcoming classes (not past events)
3. **Diversity**: Balances familiar preferences with exploration
4. **Explainability**: Every recommendation includes a human-readable reason
5. **Performance**: Results are cached for fast retrieval (<200ms)

### Recommendation Flow

```
User requests recommendations
    ↓
Check Redis cache (10-min TTL)
    ↓ (Cache Miss)
Fetch user profile & segment
    ↓
Get candidate classes (upcoming, active, not full)
    ↓
For each class:
    Calculate multi-factor score
    ↓
Sort by score descending
    ↓
Take top N (default: 10)
    ↓
Generate explanation for each
    ↓
Save to database & cache in Redis
    ↓
Return to user
```

---

## Scoring Components

### Multi-Factor Scoring Formula

```
Total Score = Σ (Factor Weight × Factor Value)

Total Score = 
    (FitnessLevelMatch × 10) +
    (PreferredClassType × 15) +
    (FavoriteInstructor × 20) +
    (TimePreference × 8) +
    (ClassRating × 2) +
    (AvailabilityBonus) +
    (SegmentBoost) +
    (RecencyBonus) +
    (PopularityBonus)
```

### Factor Details

#### 1. Fitness Level Match (Weight: 10)
**Purpose**: Ensure class difficulty aligns with user's fitness level

**Logic**:
```csharp
if (classLevel == "All Levels") 
    return 10;  // Always matches

if (userLevel == classLevel)
    return 10;  // Perfect match

if (userLevel == "Intermediate" && classLevel == "Beginner")
    return 5;   // Partial match (user can handle it)

if (userLevel == "Beginner" && classLevel == "Advanced")
    return 0;   // No match (too difficult)

return 3;  // Default partial match
```

**Example**:
- User: Intermediate
- Class: All Levels → **10 points**
- Class: Intermediate → **10 points**
- Class: Beginner → **5 points**
- Class: Advanced → **3 points**

---

#### 2. Preferred Class Type (Weight: 15)
**Purpose**: Favor class types the user explicitly prefers

**Logic**:
```csharp
var preferredTypes = JsonSerializer.Deserialize<List<string>>(user.PreferredClassTypes);

if (preferredTypes.Contains(classType))
    return 15;

return 0;
```

**Example**:
- User prefers: ["Yoga", "Pilates"]
- Class type: Yoga → **15 points**
- Class type: HIIT → **0 points**

---

#### 3. Favorite Instructor (Weight: 20)
**Purpose**: Prioritize classes taught by user's favorite instructors

**Logic**:
```csharp
var favoriteInstructors = JsonSerializer.Deserialize<List<string>>(user.FavoriteInstructors);

if (favoriteInstructors.Contains(classInstructorId))
    return 20;

return 0;
```

**Example**:
- User favorites: ["inst_sarah", "inst_mike"]
- Class instructor: inst_sarah → **20 points**
- Class instructor: inst_lisa → **0 points**

**Note**: This is the highest weighted factor because instructor quality is a primary driver of user satisfaction.

---

#### 4. Time Preference (Weight: 8)
**Purpose**: Recommend classes at times the user typically attends

**Note**: In the actual implementation, `bookingHours` is fetched once before the scoring loop (passed via user interactions), not per-class.

**Logic**:
```csharp
// Historical booking patterns analyzed from user interactions
var bookingHours = userInteractions
    .Where(i => i.EventType == "Book")
    .Select(i => i.Timestamp.Hour)
    .Distinct()
    .ToList();

var classHour = classStartTime.Hour;

if (bookingHours.Contains(classHour))
    return 8;

// Check if within 1 hour of typical time
if (bookingHours.Any(h => Math.Abs(h - classHour) <= 1))
    return 4;

return 0;
```

**Example**:
- User typically books: 6-7 AM, 5-7 PM
- Class starts: 6:00 PM → **8 points**
- Class starts: 8:00 PM → **4 points** (close to 7 PM)
- Class starts: 12:00 PM → **0 points**

---

#### 5. Class Rating (Weight: 2× rating)
**Purpose**: Surface high-quality classes

**Logic**:
```csharp
return classAverageRating * 2;
```

**Example**:
- Class rating: 4.8 → **9.6 points**
- Class rating: 3.5 → **7.0 points**
- Class rating: 5.0 → **10.0 points**

---

#### 6. Availability Bonus/Penalty
**Purpose**: Penalize nearly-full classes to avoid booking failures

**Logic**:
```csharp
var availabilityRatio = (double)(capacity - currentEnrollment) / capacity;

if (availabilityRatio < 0.2)  // Less than 20% spots left
    return -5;

if (availabilityRatio > 0.8)  // More than 80% spots available
    return 3;  // Bonus for ample space

return 0;
```

**Example**:
- Class: 25/30 enrolled (83% full, 17% available) → **-5 points**
- Class: 5/30 enrolled (17% full, 83% available) → **+3 points**
- Class: 15/30 enrolled (50% full) → **0 points**

---

#### 7. Segment Boost (Weight: up to 12)
**Purpose**: Apply behavior-based personalization

**Logic**:
```csharp
return segment switch
{
    "YogaEnthusiast" when classType == "Yoga" => 12,
    "StrengthTrainer" when classType == "HIIT" || classType == "Strength" => 12,
    "CardioLover" when classType == "Spin" || classType == "Running" => 12,
    "HighlyActive" => 5,  // General boost for all types
    "WeekendWarrior" when isWeekend => 10,
    _ => 0
};
```

**Example**:
- User segment: YogaEnthusiast
- Class type: Yoga → **12 points**
- Class type: HIIT → **0 points**

---

#### 8. Recency Bonus
**Purpose**: Slightly favor classes happening sooner

**Logic**:
```csharp
var daysUntilClass = (classStartTime - DateTime.UtcNow).TotalDays;

if (daysUntilClass <= 1)
    return 5;  // Happening within 24 hours

if (daysUntilClass <= 3)
    return 3;  // Happening within 3 days

return 0;
```

---

#### 9. Popularity Bonus
**Purpose**: Surface trending classes

**Logic**:
```csharp
var bookingCountLast7Days = GetRecentBookingCount(classId, days: 7);

if (bookingCountLast7Days > 50)
    return 8;

if (bookingCountLast7Days > 20)
    return 4;

return 0;
```

---

## User Segmentation

### Segmentation Algorithm

User segments are calculated every 30 minutes by the `UserProfilerService` based on interaction history (last 30 days).

```csharp
public string CalculateUserSegment(string userId)
{
    var interactions = GetRecentInteractions(userId, days: 30);
    var completedClasses = interactions.Where(i => i.EventType == "Complete").ToList();
    
    if (completedClasses.Count < 5)
        return "Beginner";
    
    var avgClassesPerWeek = completedClasses.Count / 4.0;
    
    if (avgClassesPerWeek >= 5)
        return "HighlyActive";
    
    // Analyze class type preferences
    var classTypeDistribution = completedClasses
        .GroupBy(i => GetClassType(i.ItemId))
        .ToDictionary(g => g.Key, g => g.Count());
    
    var totalCompleted = completedClasses.Count;
    
    foreach (var (type, count) in classTypeDistribution)
    {
        var percentage = (double)count / totalCompleted;
        
        if (percentage > 0.6)  // >60% of classes are this type
        {
            return type switch
            {
                "Yoga" => "YogaEnthusiast",
                "HIIT" or "Strength" => "StrengthTrainer",
                "Spin" or "Running" => "CardioLover",
                _ => "General"
            };
        }
    }
    
    // Check weekend pattern
    var weekendClasses = completedClasses.Count(i => IsWeekend(i.Timestamp));
    if (weekendClasses > totalCompleted * 0.8)
        return "WeekendWarrior";
    
    return "General";
}
```

### Segment Descriptions

| Segment | Criteria | Behavior Patterns |
|---------|----------|-------------------|
| **Beginner** | <5 completed classes | Exploring options, needs encouragement |
| **HighlyActive** | 5+ classes/week | Committed, seeks variety and challenges |
| **YogaEnthusiast** | >60% yoga classes | Prefers mindfulness and flexibility |
| **StrengthTrainer** | >60% HIIT/Strength | Focused on building muscle |
| **CardioLover** | >60% cardio classes | Enjoys high-energy workouts |
| **WeekendWarrior** | >80% weekend bookings | Limited weekday availability |
| **General** | Default | Balanced interests |

---

## Implementation Details

### Core Service: RecommendationService

```csharp
public class RecommendationService : IRecommendationService
{
    private readonly IUserRepository _userRepository;
    private readonly IClassRepository _classRepository;
    private readonly ICacheService _cacheService;
    private readonly ScoringEngine _scoringEngine;
    private readonly ILogger<RecommendationService> _logger;
    
    public async Task<List<RecommendationDto>> GenerateRecommendationsAsync(
        string userId, 
        int limit = 10)
    {
        // Check cache first
        var cacheKey = $"rec:{userId}";
        var cached = await _cacheService.GetAsync<List<RecommendationDto>>(cacheKey);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for user {UserId}", userId);
            return cached;
        }
        
        _logger.LogInformation("Generating fresh recommendations for user {UserId}", userId);
        
        // Fetch user profile
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new NotFoundException($"User {userId} not found");
        
        // Get candidate classes
        var candidates = await _classRepository.GetUpcomingClassesAsync();
        candidates = candidates
            .Where(c => c.IsActive && c.CurrentEnrollment < c.Capacity)
            .ToList();
        
        // Score each candidate
        var scoredClasses = new List<(Class Class, double Score)>();
        foreach (var classItem in candidates)
        {
            var score = _scoringEngine.CalculateScore(user, classItem);
            scoredClasses.Add((classItem, score));
        }
        
        // Sort by score and take top N
        var topRecommendations = scoredClasses
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToList();
        
        // Generate explanations
        var recommendations = new List<RecommendationDto>();
        for (int i = 0; i < topRecommendations.Count; i++)
        {
            var (classItem, score) = topRecommendations[i];
            var reason = GenerateExplanation(user, classItem, score);
            
            recommendations.Add(new RecommendationDto
            {
                Rank = i + 1,
                Score = score,
                Reason = reason,
                Class = _mapper.Map<ClassDto>(classItem)
            });
        }
        
        // Cache for 10 minutes
        await _cacheService.SetAsync(cacheKey, recommendations, TimeSpan.FromMinutes(10));
        
        // Save to database for persistence
        await SaveRecommendationsToDatabaseAsync(userId, recommendations);
        
        return recommendations;
    }
    
    private string GenerateExplanation(User user, Class classItem, double score)
    {
        var reasons = new List<string>();
        
        // Check which factors contributed most
        var preferredTypes = JsonSerializer.Deserialize<List<string>>(user.PreferredClassTypes);
        if (preferredTypes?.Contains(classItem.Type) == true)
            reasons.Add($"you love {classItem.Type} classes");
        
        var favoriteInstructors = JsonSerializer.Deserialize<List<string>>(user.FavoriteInstructors);
        if (favoriteInstructors?.Contains(classItem.InstructorId) == true)
            reasons.Add($"you enjoy classes with {classItem.InstructorName}");
        
        if (classItem.AverageRating >= 4.7)
            reasons.Add("this class has excellent reviews");
        
        if (user.Segment != "General")
            reasons.Add($"popular among {user.Segment} members like you");
        
        if (!reasons.Any())
            return "Recommended based on your activity";
        
        return $"Because {string.Join(" and ", reasons)}";
    }
}
```

### Scoring Engine

```csharp
public class ScoringEngine
{
    public double CalculateScore(User user, Class classItem)
    {
        double score = 0;
        
        // Factor 1: Fitness level match (weight: 10)
        score += GetFitnessLevelScore(user.FitnessLevel, classItem.DifficultyLevel);
        
        // Factor 2: Preferred class type (weight: 15)
        score += GetClassTypeScore(user.PreferredClassTypes, classItem.Type);
        
        // Factor 3: Favorite instructor (weight: 20)
        score += GetInstructorScore(user.FavoriteInstructors, classItem.InstructorId);
        
        // Factor 4: Time preference (weight: 8)
        score += GetTimePreferenceScore(user.Id, classItem.StartTime);
        
        // Factor 5: Class rating (weight: rating × 2)
        score += (double)classItem.AverageRating * 2;
        
        // Factor 6: Availability bonus/penalty
        score += GetAvailabilityScore(classItem.Capacity, classItem.CurrentEnrollment);
        
        // Factor 7: Segment boost (weight: up to 12)
        score += GetSegmentBoost(user.Segment, classItem.Type);
        
        // Factor 8: Recency bonus
        score += GetRecencyBonus(classItem.StartTime);
        
        // Factor 9: Popularity bonus
        score += GetPopularityBonus(classItem.Id);
        
        return Math.Max(0, score);  // Never return negative score
    }
    
    private double GetFitnessLevelScore(string userLevel, string classLevel)
    {
        if (classLevel == "All Levels") return 10;
        if (userLevel == classLevel) return 10;
        if (userLevel == "Intermediate" && classLevel == "Beginner") return 5;
        if (userLevel == "Advanced" && classLevel != "Advanced") return 3;
        return 3;
    }
    
    // ... other scoring methods
}
```

---

## Performance Optimization

### Caching Strategy

1. **Redis Cache Layer**
   - Key: `rec:{userId}`
   - TTL: 10 minutes
   - Cache hit rate target: >90%

2. **Database Persistence**
   - Save recommendations to `Recommendations` table
   - Query if cache miss and generated recently (< 10 min ago)
   - Reduces duplicate computation

3. **Batch Generation**
   - Background service generates recommendations for active users
   - Runs every 10 minutes
   - Processes 1000 users per batch

### Query Optimization

```csharp
// Efficient candidate fetching
var candidates = await _context.Classes
    .AsNoTracking()  // Read-only, faster
    .Where(c => c.IsActive && c.StartTime > DateTime.UtcNow)
    .Where(c => c.CurrentEnrollment < c.Capacity)
    .OrderBy(c => c.StartTime)
    .Take(100)  // Limit candidates to reduce processing time
    .ToListAsync();
```

### Parallel Processing

**Note**: Parallelization provides benefit with larger candidate sets (500+). For this POC with ~100 candidates, sequential iteration is sufficient and simpler. The trade-off is thread overhead vs. throughput - at current scale, single-threaded scoring completes in <2 seconds.

```csharp
// Optional: Score classes in parallel for large candidate sets (500+)
var scoredClasses = candidates
    .AsParallel()
    .WithDegreeOfParallelism(4)
    .Select(c => new { Class = c, Score = _scoringEngine.CalculateScore(user, c) })
    .OrderByDescending(x => x.Score)
    .Take(limit)
    .ToList();
```

---

## Evaluation Metrics

**Note**: These are target metrics for production deployment. This POC focuses on algorithm implementation and architecture; measuring these would require A/B testing infrastructure and production traffic.

### Recommendation Quality Metrics

1. **Click-Through Rate (CTR)**
   ```
   CTR = (Clicks on Recommendations) / (Total Recommendation Views)
   Target: >15%
   ```

2. **Conversion Rate**
   ```
   Conversion = (Bookings from Recommendations) / (Clicks on Recommendations)
   Target: >5%
   ```

3. **Average Recommendation Score**
   ```
   Avg Score of Booked Classes
   Target: >70
   ```

4. **Diversity Score**
   ```
   Diversity = Unique Class Types in Top 10 Recommendations
   Target: 4-6 types
   ```

5. **Freshness**
   ```
   % of Recommendations for Classes within 7 days
   Target: >60%
   ```

### A/B Testing Framework

```csharp
public class RecommendationExperiment
{
    public string UserId { get; set; }
    public string Variant { get; set; }  // "control" or "test"
    public List<string> RecommendedClassIds { get; set; }
    public DateTime Timestamp { get; set; }
}

// Split users into control (current algorithm) and test (new algorithm)
var variant = GetUserVariant(userId);  // 50/50 split based on userId hash

if (variant == "test")
{
    // Use experimental scoring weights
    recommendations = await _experimentalRecommendationService.GenerateAsync(userId);
}
else
{
    // Use current algorithm
    recommendations = await _recommendationService.GenerateAsync(userId);
}

// Track experiment results
await _analyticsService.TrackExperiment(new RecommendationExperiment
{
    UserId = userId,
    Variant = variant,
    RecommendedClassIds = recommendations.Select(r => r.Class.Id).ToList(),
    Timestamp = DateTime.UtcNow
});
```

---

## Future Enhancements

### 1. Machine Learning Model
**Approach**: Collaborative Filtering with Matrix Factorization

**Benefits**:
- Learn latent factors automatically
- Better handle cold start problem
- More accurate predictions

**Implementation**:
```python
from surprise import SVD, Dataset, Reader
from surprise.model_selection import cross_validate

# Load interaction data
reader = Reader(rating_scale=(0, 1))
data = Dataset.load_from_df(interactions_df[['userId', 'classId', 'interaction']], reader)

# Train SVD model
model = SVD(n_factors=50, n_epochs=20, lr_all=0.005, reg_all=0.02)
model.fit(data.build_full_trainset())

# Generate predictions
for class_id in candidate_classes:
    predicted_score = model.predict(user_id, class_id).est
```

### 2. Real-Time Personalization
**Approach**: Update recommendations immediately after user interactions

**Implementation**:
```csharp
// Event consumer triggers recommendation refresh
public async Task ConsumeAsync(InteractionEvent interactionEvent)
{
    await _eventRepository.SaveAsync(interactionEvent);
    
    // If high-value interaction (Book, Complete), refresh recommendations
    if (interactionEvent.EventType == "Book" || interactionEvent.EventType == "Complete")
    {
        await _recommendationService.RefreshRecommendationsAsync(interactionEvent.UserId);
        
        // Invalidate cache
        await _cacheService.DeleteAsync($"rec:{interactionEvent.UserId}");
    }
}
```

### 3. Contextual Bandits
**Approach**: Balance exploration vs exploitation

**Benefits**:
- Discover new preferences
- Avoid filter bubble
- Continuously optimize

**Implementation**:
```csharp
public List<Class> ApplyEpsilonGreedy(List<Class> recommendations, double epsilon = 0.1)
{
    var random = new Random();
    
    // With probability epsilon, inject a random class
    if (random.NextDouble() < epsilon)
    {
        var randomClass = _classRepository.GetRandom();
        recommendations[random.Next(recommendations.Count)] = randomClass;
    }
    
    return recommendations;
}
```

### 4. Social Recommendations
**Approach**: "Friends who also booked this class"

**Implementation**:
```sql
SELECT c.*, COUNT(DISTINCT i.UserId) as FriendsBooked
FROM Classes c
JOIN Interactions i ON c.Id = i.ItemId
WHERE i.UserId IN (SELECT FriendId FROM Friendships WHERE UserId = @UserId)
  AND i.EventType = 'Book'
GROUP BY c.Id
ORDER BY FriendsBooked DESC
```

### 5. Multi-Objective Optimization
**Approach**: Optimize for multiple goals simultaneously

**Goals**:
- User satisfaction (CTR, bookings)
- Business metrics (revenue, capacity utilization)
- Long-term engagement (retention)

**Implementation**:
```csharp
public double CalculateMultiObjectiveScore(Class classItem, User user)
{
    var userSatisfactionScore = _scoringEngine.CalculateScore(user, classItem);
    var revenueScore = classItem.Price * 0.1;  // Higher priced classes generate more revenue
    var capacityScore = (classItem.Capacity - classItem.CurrentEnrollment) * 0.5;  // Fill underutilized classes
    
    // Weighted combination
    return (userSatisfactionScore * 0.7) + (revenueScore * 0.2) + (capacityScore * 0.1);
}
```

---

## Summary

The FitLife recommendation algorithm uses a **transparent, rule-based approach** that:
- ✅ Provides explainable recommendations
- ✅ Adapts to user preferences and behavior
- ✅ Performs well with limited data
- ✅ Scales efficiently with caching
- ✅ Can evolve into ML-based system

**Next Steps**: Collect interaction data for 3 months, then train ML model using historical data for comparison with rule-based approach.