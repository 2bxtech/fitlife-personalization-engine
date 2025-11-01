# FitLife.Tests

Test project for the FitLife Personalization Engine.

## Purpose

This test project will contain unit and integration tests for the core recommendation logic.

## Test Categories

### Phase 3 Tests (To Be Implemented)
- **ScoringEngineTests** - Test the 9-factor recommendation algorithm
- **RecommendationServiceTests** - Test caching and recommendation generation
- **UserEventTests** - Test event validation and domain logic

### Phase 6 Tests (Integration)
- End-to-end recommendation flow
- Kafka event processing
- Cache invalidation scenarios

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ScoringEngineTests"

# Run with coverage (after adding coverlet.collector)
dotnet test --collect:"XPlat Code Coverage"
```

## Test Dependencies

- **xUnit** - Testing framework
- **Moq** - Mocking library for dependencies
- **FluentAssertions** - Readable assertions

## Notes

- Tests for ScoringEngine will be critical as this contains core business logic
- Integration tests will be added in Phase 6 before production deployment
- Current placeholder test verifies infrastructure is working
