# FitLife Database Schema Documentation

## Overview

FitLife uses **Azure SQL Database** (SQL Server compatible) for production with Entity Framework Core as the ORM. The schema is designed for:
- **Strong consistency** for user profiles and bookings
- **Efficient querying** with strategic indexes
- **Audit trail** with timestamp tracking
- **Scalability** through proper normalization

## Database Configuration

### Connection String Format
```
Server=<server>.database.windows.net;Database=FitLifeDB;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False
```

### Local Development
```
Server=localhost,1433;Database=FitLifeDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
```

---

## Entity Relationship Diagram

```
┌─────────────────────┐
│       Users         │
│─────────────────────│
│ Id (PK)             │
│ Email (UQ)          │
│ PasswordHash        │
│ FirstName           │
│ LastName            │
│ FitnessLevel        │
│ Goals (JSON)        │
│ PreferredClassTypes │
│ FavoriteInstructors │
│ Segment             │
│ CreatedAt           │
│ UpdatedAt           │
└──────────┬──────────┘
           │
           │ 1:N
           │
    ┌──────┴──────────────────────┐
    │                             │
    ▼                             ▼
┌─────────────────┐    ┌──────────────────────┐
│  Interactions   │    │   Recommendations    │
│─────────────────│    │──────────────────────│
│ Id (PK)         │    │ UserId (PK, FK)      │
│ UserId (FK)     │    │ ItemId (PK, FK)      │
│ ItemId          │    │ ItemType             │
│ ItemType        │    │ Score                │
│ EventType       │    │ Rank                 │
│ Timestamp       │    │ Reason               │
│ Metadata (JSON) │    │ GeneratedAt          │
└─────────────────┘    └──────────┬───────────┘
                                  │
                                  │ N:1
                                  │
                       ┌──────────┴───────────┐
                       │      Classes         │
                       │──────────────────────│
                       │ Id (PK)              │
                       │ Name                 │
                       │ Type                 │
                       │ Description          │
                       │ InstructorId         │
                       │ InstructorName       │
                       │ LocationId           │
                       │ LocationName         │
                       │ StartTime            │
                       │ DurationMinutes      │
                       │ Capacity             │
                       │ CurrentEnrollment    │
                       │ AverageRating        │
                       │ DifficultyLevel      │
                       │ ImageUrl             │
                       │ IsActive             │
                       │ CreatedAt            │
                       └──────────────────────┘
```

---

## Table Definitions

### Users

Stores user profiles, preferences, and authentication credentials.

```sql
CREATE TABLE Users (
    Id NVARCHAR(50) PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    FitnessLevel NVARCHAR(20) NOT NULL DEFAULT 'Beginner',
    Goals NVARCHAR(MAX) NULL,                    -- JSON: ["Weight Loss", "Strength"]
    PreferredClassTypes NVARCHAR(MAX) NULL,      -- JSON: ["Yoga", "HIIT"]
    FavoriteInstructors NVARCHAR(MAX) NULL,      -- JSON: ["inst_001", "inst_002"]
    Segment NVARCHAR(50) NOT NULL DEFAULT 'General',
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_FitnessLevel CHECK (FitnessLevel IN ('Beginner', 'Intermediate', 'Advanced'))
);
```

**Indexes**
```sql
CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_Segment ON Users(Segment);
CREATE INDEX IX_Users_CreatedAt ON Users(CreatedAt DESC);
```

**Sample Data**
```sql
INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, FitnessLevel, Goals, PreferredClassTypes, Segment)
VALUES 
('user_001', 'john.doe@example.com', '$2a$12$...', 'John', 'Doe', 'Intermediate', 
 '["Weight Loss", "Build Strength"]', '["Yoga", "HIIT"]', 'HighlyActive'),
('user_002', 'jane.smith@example.com', '$2a$12$...', 'Jane', 'Smith', 'Beginner',
 '["Flexibility", "Stress Relief"]', '["Yoga", "Pilates"]', 'YogaEnthusiast');
```

---

### Classes

Stores gym class schedule, instructor info, and enrollment data.

```sql
CREATE TABLE Classes (
    Id NVARCHAR(50) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    InstructorId NVARCHAR(50) NOT NULL,
    InstructorName NVARCHAR(255) NOT NULL,
    LocationId NVARCHAR(50) NOT NULL DEFAULT 'default',
    LocationName NVARCHAR(255) NULL,
    StartTime DATETIME2 NOT NULL,
    DurationMinutes INT NOT NULL,
    Capacity INT NOT NULL,
    CurrentEnrollment INT NOT NULL DEFAULT 0,
    AverageRating DECIMAL(3,2) NOT NULL DEFAULT 0.00,
    DifficultyLevel NVARCHAR(50) NOT NULL DEFAULT 'All Levels',
    ImageUrl NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT CK_Classes_Capacity CHECK (Capacity > 0),
    CONSTRAINT CK_Classes_Enrollment CHECK (CurrentEnrollment >= 0 AND CurrentEnrollment <= Capacity),
    CONSTRAINT CK_Classes_Rating CHECK (AverageRating >= 0 AND AverageRating <= 5),
    CONSTRAINT CK_Classes_Duration CHECK (DurationMinutes > 0)
);
```

**Indexes**
```sql
-- Most common queries: filter by start time, type, active status
CREATE INDEX IX_Classes_StartTime ON Classes(StartTime) WHERE IsActive = 1;
CREATE INDEX IX_Classes_Type ON Classes(Type) WHERE IsActive = 1;
CREATE INDEX IX_Classes_InstructorId ON Classes(InstructorId) WHERE IsActive = 1;

-- Composite index for filtered searches
CREATE INDEX IX_Classes_Type_StartTime_Active 
    ON Classes(Type, StartTime) 
    WHERE IsActive = 1;

-- Covering index for list queries (includes commonly selected columns)
CREATE INDEX IX_Classes_List 
    ON Classes(StartTime, Type, IsActive) 
    INCLUDE (Name, InstructorName, Capacity, CurrentEnrollment, AverageRating);
```

**Sample Data**
```sql
INSERT INTO Classes (Id, Name, Type, Description, InstructorId, InstructorName, LocationId, LocationName, 
    StartTime, DurationMinutes, Capacity, CurrentEnrollment, AverageRating, DifficultyLevel, ImageUrl)
VALUES 
('class_001', 'Vinyasa Flow Yoga', 'Yoga', 'Dynamic yoga practice connecting breath with movement', 
 'inst_sarah', 'Sarah Johnson', 'loc_uptown', 'Minneapolis - Uptown', 
 '2025-10-31 18:00:00', 60, 25, 12, 4.8, 'All Levels', '/images/yoga.jpg'),
 
('class_002', 'HIIT Blast', 'HIIT', 'High-intensity interval training for maximum calorie burn', 
 'inst_mike', 'Mike Thompson', 'loc_uptown', 'Minneapolis - Uptown', 
 '2025-10-31 06:00:00', 45, 30, 28, 4.9, 'Intermediate', '/images/hiit.jpg');
```

---

### Interactions

Event store for all user interactions (views, clicks, bookings, completions).

```sql
CREATE TABLE Interactions (
    Id NVARCHAR(50) PRIMARY KEY,
    UserId NVARCHAR(50) NOT NULL,
    ItemId NVARCHAR(50) NOT NULL,
    ItemType NVARCHAR(20) NOT NULL DEFAULT 'Class',
    EventType NVARCHAR(20) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Metadata NVARCHAR(MAX) NULL,  -- JSON: {"source": "recommendation_feed", "position": 1}
    
    CONSTRAINT FK_Interactions_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Interactions_EventType CHECK (EventType IN ('View', 'Click', 'Book', 'Complete', 'Cancel', 'Rate'))
);
```

**Indexes**
```sql
-- Most common: get user's recent interactions
CREATE INDEX IX_Interactions_UserId_Timestamp 
    ON Interactions(UserId, Timestamp DESC);

-- Analytics: count events by type
CREATE INDEX IX_Interactions_EventType_Timestamp 
    ON Interactions(EventType, Timestamp DESC);

-- Find interactions for specific item
CREATE INDEX IX_Interactions_ItemId_EventType 
    ON Interactions(ItemId, EventType);

-- Composite for user behavior analysis
CREATE INDEX IX_Interactions_UserId_ItemType_EventType 
    ON Interactions(UserId, ItemType, EventType) 
    INCLUDE (ItemId, Timestamp);
```

**Sample Data**
```sql
INSERT INTO Interactions (Id, UserId, ItemId, ItemType, EventType, Timestamp, Metadata)
VALUES 
('int_001', 'user_001', 'class_001', 'Class', 'View', '2025-10-30 10:00:00', 
 '{"source": "browse", "device": "mobile"}'),
 
('int_002', 'user_001', 'class_001', 'Class', 'Book', '2025-10-30 10:05:00', 
 '{"source": "class_detail", "sessionId": "sess_xyz"}');
```

---

### Recommendations

Pre-computed personalized recommendations with scores and explanations.

```sql
CREATE TABLE Recommendations (
    UserId NVARCHAR(50) NOT NULL,
    ItemId NVARCHAR(50) NOT NULL,
    ItemType NVARCHAR(20) NOT NULL DEFAULT 'Class',
    Score FLOAT NOT NULL,
    Rank INT NOT NULL,
    Reason NVARCHAR(500) NULL,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT PK_Recommendations PRIMARY KEY (UserId, ItemId),
    CONSTRAINT FK_Recommendations_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Recommendations_Score CHECK (Score >= 0),
    CONSTRAINT CK_Recommendations_Rank CHECK (Rank > 0)
);
```

**Indexes**
```sql
-- Get top N recommendations for user (most common query)
CREATE INDEX IX_Recommendations_UserId_Rank 
    ON Recommendations(UserId, Rank) 
    INCLUDE (ItemId, Score, Reason);

-- Find recommendations by generation time (for cache invalidation)
CREATE INDEX IX_Recommendations_GeneratedAt 
    ON Recommendations(GeneratedAt);

-- Analytics: which items are most recommended
CREATE INDEX IX_Recommendations_ItemId_Score 
    ON Recommendations(ItemId, Score DESC);
```

**Sample Data**
```sql
INSERT INTO Recommendations (UserId, ItemId, ItemType, Score, Rank, Reason, GeneratedAt)
VALUES 
('user_001', 'class_006', 'Class', 87.5, 1, 
 'Because you love yoga classes with Sarah Johnson', '2025-10-30 14:25:00'),
 
('user_001', 'class_002', 'Class', 82.0, 2, 
 'Popular among HighlyActive members like you', '2025-10-30 14:25:00');
```

---

## Views

### vw_UserActivity
Aggregated user statistics for dashboard display.

```sql
CREATE VIEW vw_UserActivity AS
SELECT 
    u.Id AS UserId,
    u.FirstName,
    u.LastName,
    u.Segment,
    COUNT(DISTINCT CASE WHEN i.EventType = 'Complete' THEN i.ItemId END) AS TotalClassesCompleted,
    COUNT(DISTINCT CASE WHEN i.EventType = 'Book' THEN i.ItemId END) AS TotalClassesBooked,
    MAX(CASE WHEN i.EventType = 'Complete' THEN i.Timestamp END) AS LastClassCompleted,
    (SELECT TOP 1 c.Type 
     FROM Interactions i2 
     JOIN Classes c ON i2.ItemId = c.Id 
     WHERE i2.UserId = u.Id AND i2.EventType = 'Complete'
     GROUP BY c.Type 
     ORDER BY COUNT(*) DESC) AS FavoriteClassType
FROM Users u
LEFT JOIN Interactions i ON u.Id = i.UserId
GROUP BY u.Id, u.FirstName, u.LastName, u.Segment;
```

### vw_ClassPopularity
Class performance metrics for analytics.

```sql
CREATE VIEW vw_ClassPopularity AS
SELECT 
    c.Id,
    c.Name,
    c.Type,
    c.InstructorName,
    COUNT(DISTINCT CASE WHEN i.EventType = 'View' THEN i.UserId END) AS TotalViews,
    COUNT(DISTINCT CASE WHEN i.EventType = 'Book' THEN i.UserId END) AS TotalBookings,
    CAST(COUNT(DISTINCT CASE WHEN i.EventType = 'Book' THEN i.UserId END) AS FLOAT) / 
        NULLIF(COUNT(DISTINCT CASE WHEN i.EventType = 'View' THEN i.UserId END), 0) AS ConversionRate,
    c.AverageRating
FROM Classes c
LEFT JOIN Interactions i ON c.Id = i.ItemId
WHERE c.IsActive = 1
GROUP BY c.Id, c.Name, c.Type, c.InstructorName, c.AverageRating;
```

---

## Stored Procedures

### sp_GenerateUserRecommendations
Batch generate recommendations for a user.

```sql
CREATE PROCEDURE sp_GenerateUserRecommendations
    @UserId NVARCHAR(50),
    @Limit INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Delete old recommendations
    DELETE FROM Recommendations WHERE UserId = @UserId;
    
    -- Insert new recommendations (simplified scoring logic)
    INSERT INTO Recommendations (UserId, ItemId, ItemType, Score, Rank, Reason, GeneratedAt)
    SELECT TOP (@Limit)
        @UserId,
        c.Id,
        'Class',
        -- Simple scoring: rating * 10 + availability bonus
        (c.AverageRating * 10) + ((c.Capacity - c.CurrentEnrollment) * 0.5) AS Score,
        ROW_NUMBER() OVER (ORDER BY (c.AverageRating * 10) + ((c.Capacity - c.CurrentEnrollment) * 0.5) DESC) AS Rank,
        'Recommended based on class popularity and availability',
        GETUTCDATE()
    FROM Classes c
    WHERE c.IsActive = 1
        AND c.StartTime > GETUTCDATE()
        AND c.CurrentEnrollment < c.Capacity
    ORDER BY Score DESC;
END;
```

### sp_GetUserInteractionStats
Get user interaction statistics for segmentation.

```sql
CREATE PROCEDURE sp_GetUserInteractionStats
    @UserId NVARCHAR(50),
    @DaysBack INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StartDate DATETIME2 = DATEADD(DAY, -@DaysBack, GETUTCDATE());
    
    SELECT 
        COUNT(CASE WHEN EventType = 'View' THEN 1 END) AS TotalViews,
        COUNT(CASE WHEN EventType = 'Book' THEN 1 END) AS TotalBookings,
        COUNT(CASE WHEN EventType = 'Complete' THEN 1 END) AS TotalCompleted,
        COUNT(CASE WHEN EventType = 'Cancel' THEN 1 END) AS TotalCancelled,
        (SELECT TOP 1 c.Type 
         FROM Interactions i2 
         JOIN Classes c ON i2.ItemId = c.Id 
         WHERE i2.UserId = @UserId AND i2.EventType = 'Complete' AND i2.Timestamp >= @StartDate
         GROUP BY c.Type 
         ORDER BY COUNT(*) DESC) AS MostFrequentClassType,
        CAST(COUNT(CASE WHEN EventType = 'Complete' THEN 1 END) AS FLOAT) / 
            NULLIF(COUNT(CASE WHEN EventType = 'Book' THEN 1 END), 0) AS CompletionRate
    FROM Interactions
    WHERE UserId = @UserId AND Timestamp >= @StartDate;
END;
```

---

## Migration Strategy

### Entity Framework Core Migrations

**Create Initial Migration**
```bash
dotnet ef migrations add InitialCreate --project FitLife.Api
```

**Update Database**
```bash
dotnet ef database update --project FitLife.Api
```

**Generate SQL Script** (for production deployment)
```bash
dotnet ef migrations script --project FitLife.Api --output migration.sql
```

### Migration Files Structure
```
FitLife.Api/Data/Migrations/
├── 20251030_InitialCreate.cs
├── 20251101_AddUserSegment.cs
├── 20251105_AddRecommendationsTable.cs
└── FitLifeDbContextModelSnapshot.cs
```

### Best Practices
1. **Never modify existing migrations** - Create new ones for changes
2. **Test migrations on staging** before production
3. **Backup database** before running migrations
4. **Use transactions** for complex migrations
5. **Keep migrations small** and focused

---

## Performance Considerations

### Connection Pooling
```csharp
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Max Pool Size=100;Min Pool Size=10;..."
  }
}
```

### Query Optimization Tips

1. **Use projections** to select only needed columns
```csharp
var classes = await _context.Classes
    .Where(c => c.IsActive)
    .Select(c => new ClassDto { Name = c.Name, Type = c.Type })
    .ToListAsync();
```

2. **Avoid N+1 queries** with eager loading
```csharp
var users = await _context.Users
    .Include(u => u.Interactions)
    .ToListAsync();
```

3. **Use AsNoTracking** for read-only queries
```csharp
var classes = await _context.Classes
    .AsNoTracking()
    .ToListAsync();
```

4. **Batch operations** for bulk inserts
```csharp
_context.Recommendations.AddRange(recommendations);
await _context.SaveChangesAsync();
```

### Index Monitoring

```sql
-- Find missing indexes (run periodically)
SELECT 
    migs.avg_total_user_cost * (migs.avg_user_impact / 100.0) * (migs.user_seeks + migs.user_scans) AS improvement_measure,
    'CREATE INDEX IX_' + OBJECT_NAME(mid.object_id) + '_' + REPLACE(REPLACE(REPLACE(mid.equality_columns, '[', ''), ']', ''), ',', '_') 
    + ' ON ' + mid.statement + ' (' + ISNULL(mid.equality_columns, '') + ISNULL(mid.inequality_columns, '') + ')'
    + ISNULL(' INCLUDE (' + mid.included_columns + ')', '') AS create_index_statement
FROM sys.dm_db_missing_index_groups mig
INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
WHERE migs.avg_total_user_cost * (migs.avg_user_impact / 100.0) * (migs.user_seeks + migs.user_scans) > 10
ORDER BY improvement_measure DESC;
```

---

## Backup & Recovery

### Automated Backups (Azure SQL)
- **Full backup**: Daily at 2 AM UTC
- **Transaction log backup**: Every 10 minutes
- **Retention**: 30 days

### Manual Backup
```sql
BACKUP DATABASE FitLifeDB 
TO DISK = 'C:\Backups\FitLifeDB_20251030.bak'
WITH FORMAT, COMPRESSION;
```

### Point-in-Time Restore
```sql
RESTORE DATABASE FitLifeDB_Restore
FROM DISK = 'C:\Backups\FitLifeDB_20251030.bak'
WITH STOPAT = '2025-10-30 12:00:00';
```

---

## Security

### Row-Level Security (Future Enhancement)
```sql
CREATE FUNCTION fn_SecurityPredicate(@UserId NVARCHAR(50))
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS result
WHERE @UserId = CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(50));

CREATE SECURITY POLICY UserFilter
ADD FILTER PREDICATE dbo.fn_SecurityPredicate(UserId)
ON dbo.Interactions
WITH (STATE = ON);
```

### Encryption
- **Transparent Data Encryption (TDE)**: Enabled in Azure SQL
- **Always Encrypted**: For sensitive fields (future consideration)
- **Connection encryption**: TLS 1.3

---

## Monitoring Queries

### Active Connections
```sql
SELECT 
    DB_NAME(dbid) as DatabaseName,
    COUNT(dbid) as NumberOfConnections,
    loginame as LoginName
FROM sys.sysprocesses
WHERE dbid > 0
GROUP BY dbid, loginame;
```

### Long-Running Queries
```sql
SELECT 
    r.session_id,
    r.start_time,
    r.status,
    r.command,
    SUBSTRING(t.text, (r.statement_start_offset/2)+1,
        ((CASE r.statement_end_offset
            WHEN -1 THEN DATALENGTH(t.text)
            ELSE r.statement_end_offset
        END - r.statement_start_offset)/2) + 1) AS query_text,
    r.wait_type,
    r.wait_time,
    r.cpu_time
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id > 50
ORDER BY r.cpu_time DESC;
```

---

This database schema provides a solid foundation for the FitLife application with room for growth and optimization as the system scales.
