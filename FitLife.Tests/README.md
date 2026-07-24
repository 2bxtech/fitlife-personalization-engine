# FitLife.Tests

Automated unit and API integration tests for the FitLife Personalization Engine.

## Current coverage

- Nine-factor scoring behavior and boundaries.
- Recommendation and infrastructure behavior.
- Authentication and controller API flows.
- Class browsing and booking API behavior.
- Selected cross-user profile authorization behavior.

The suite currently contains 50 tests. Treat that number as point-in-time
evidence; run the suite for the current count.

## Run

From the repository root:

```bash
dotnet test FitLife.sln --configuration Release
```

Run a focused class:

```bash
dotnet test FitLife.Tests/FitLife.Tests.csproj \
  --filter "FullyQualifiedName~ScoringEngineTests"
```

Run the complete repository verification on PowerShell:

```powershell
./scripts/verify.ps1
```

## Known gaps

- Kafka consumer behavior is not integration-tested against a broker.
- Redis failure and cache-invalidation behavior need deeper integration
  coverage.
- Booking uniqueness and concurrent last-seat behavior are not enforced yet.
- SQL Server-specific invariants require coverage beyond the EF in-memory test
  provider.

These gaps are tracked in the private phased implementation plan.
