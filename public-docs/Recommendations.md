# Recommendation model

FitLife ranks upcoming classes with a **deterministic, rule-based scorer**. There
is no trained model and no collaborative filtering: every point in a score comes
from one of nine named rules, and the explanation a member sees is generated from
those same rules. **Verified** statements below are exercised by tests in
`FitLife.Tests/Services`.

## Pipeline

```
GET /api/recommendations/{userId}
  -> Redis  rec:{userId}            (10 min TTL; cache errors fall through)
  -> SQL    Recommendations rows    (< 10 min old; persisted with their factors)
  -> generate:
       user profile + 90 days of interactions
       + the classes those interactions refer to   -> ScoringHistory
       upcoming classes (up to 100)                -> ScoringEngine.Explain() each
       rank by total, then start time, then id     -> top N
       reason = RecommendationReasons.Compose(factors)
       persist rows (score, reason, factors JSON) and cache
```

The scheduler role regenerates recommendations for recently active users every
10 minutes. Booking, cancelling, and Book/Cancel/Complete/Rate events invalidate
the user's cache entry (best-effort; see [Worker topology](Worker-Topology.md)).

## The nine factors

`ScoringEngine.Explain(user, class, history)` returns a `ScoreBreakdown`: nine
factors, each with a stable key, points, and a plain-language detail. The ranking
score is their sum, floored at zero. **Verified:** the factors are unique, and
their sum equals the score used for ranking.

| Key | Points | Rule |
|---|---|---|
| `fitness_level` | 0–10 | 10 for "All Levels" or an exact match; 5 for one level easier; 3 for two levels easier or one level harder; 0 for an Advanced class and a Beginner member |
| `class_type` | 15 | The class type is in the member's chosen preferred types |
| `instructor` | 20 | The member has completed 2 or more classes with this instructor |
| `time_of_day` | 0, 4, 8 | The class starts in the same UTC hour (8), or within an hour across midnight (4), as classes the member booked |
| `rating` | rating × 2 | Average member rating (0–10 points) |
| `availability` | −5 to +3 | −5 when under 20% of spots are open; +3 when over 80% are open |
| `activity_profile` | 0–12 | A fixed rule keyed on the member's segment (below) |
| `starts_soon` | 0, 3, 5 | Within 24 hours (5) or 3 days (3) |
| `popularity` | 0, 4, 8 | More than 20 (4) or 50 (8) bookings across all members this week |

### History comes from classes, not from clicks

`ScoringHistory` is built from the classes a member's interactions refer to:

- **Instructor affinity** counts `Complete` events by the completed class's
  instructor.
- **Time of day** uses the start hour of each booked class.

An earlier version used the timestamp of the Book click, so booking an evening
class at 3 AM counted as a 3 AM preference. **Verified** by
`TimePreference_UsesBookedClassStartTime_NotWhenTheBookingWasMade` and
`InstructorAffinity_ComesFromCompletedClasses_WithoutMetadata`. When a referenced
class no longer exists, the event's own timestamp and any `instructorId` in its
metadata are used instead.

### Activity profile (segments)

The profiler assigns a segment from the member's last 30 days of completed
classes:

1. fewer than 5 completions: `Beginner`;
2. an average of 5 or more per week: `HighlyActive`;
3. more than 60% of completions of one type: `YogaEnthusiast`, `StrengthTrainer`
   (HIIT or Strength), or `CardioLover` (Spin, Running, or Cardio);
4. more than 80% on weekends: `WeekendWarrior`;
5. otherwise `General`.

The `activity_profile` factor then applies fixed boosts: 12 when a specialist's
type matches, 5 for `HighlyActive`, and 10 for weekend classes for
`WeekendWarrior`. This describes the member's **own** history. It is not a
measurement of what similar members book, and explanations do not claim that.

## Explanations

`RecommendationReasons.Compose` builds the one-line reason from the breakdown:

- It considers only factors that scored. Rating counts only at 4.7 or above, and
  popularity only at the top tier.
- Personal factors come first (preferred type, instructor, activity profile, time
  of day), followed by class qualities.
- There are at most two clauses. With no qualifying factor, it says so: "Ranked
  on level fit, rating, and schedule; no personal signals yet."

The API returns the reason and the full `factors` array with every
recommendation, so the interface can show the complete breakdown. **Verified:** a
yoga-focused member shown a Spin class gets no profile, preference, or instructor
claim. The previous generator said "popular among YogaEnthusiast members like you"
for every class.

If scoring throws, the service falls back to popular classes with the reason
"Popular this week. Personalized scoring was unavailable." and no factors.

## Performance

The generation path loads one user, their interactions, the classes those
interactions refer to (one batched query), and up to 100 upcoming classes. It
then scores in memory. No latency figures are published: none has been
**measured** with a retained, reproducible method.

## Deliberate limits

- **Weights are product rules, not learned parameters.** Changing one is a code
  change with a test, which keeps every ranking reproducible and explainable.
- Hours are compared in UTC, so a member whose bookings span a DST change can
  shift by an hour.
- Candidate generation considers the next 100 upcoming active classes.
- A learned model (for example, matrix factorization over completions) would
  need real interaction volume to beat these rules. It is a **target**, not
  current behavior.
