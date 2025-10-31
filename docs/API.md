# FitLife API Documentation

## Base URL
```
Development: http://localhost:8080/api
Production:  https://api.fitlife.com/api
```

## Authentication

All protected endpoints require a JWT bearer token in the `Authorization` header:
```
Authorization: Bearer <your-jwt-token>
```

### Token Lifecycle
- **Expiration**: 24 hours
- **Refresh**: Not implemented in v1 (re-login required)
- **Format**: JWT with HS256 signature

---

## API Endpoints

### Authentication

#### Register New User
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "john.doe@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe",
  "fitnessLevel": "Beginner"
}
```

**Response** `201 Created`
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "fitnessLevel": "Beginner",
    "segment": "Beginner",
    "createdAt": "2025-10-30T12:00:00Z"
  },
  "expiresAt": "2025-10-31T12:00:00Z"
}
```

**Error Responses**
- `400 Bad Request`: Invalid email format or weak password
- `409 Conflict`: Email already registered

---

#### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "john.doe@example.com",
  "password": "SecurePass123!"
}
```

**Response** `200 OK`
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": { ... },
  "expiresAt": "2025-10-31T12:00:00Z"
}
```

**Error Responses**
- `401 Unauthorized`: Invalid credentials

---

#### Get Current User
```http
GET /api/auth/me
Authorization: Bearer <token>
```

**Response** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "fitnessLevel": "Intermediate",
  "goals": ["Weight Loss", "Build Strength"],
  "preferredClassTypes": ["Yoga", "HIIT"],
  "favoriteInstructors": ["inst_sarah", "inst_mike"],
  "segment": "HighlyActive",
  "createdAt": "2025-09-01T12:00:00Z",
  "updatedAt": "2025-10-30T08:15:00Z"
}
```

---

### Users

#### Get User Profile
```http
GET /api/users/{userId}
Authorization: Bearer <token>
```

**Response** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "fitnessLevel": "Intermediate",
  "segment": "HighlyActive",
  "stats": {
    "totalClassesCompleted": 42,
    "currentStreak": 3,
    "favoriteClassType": "Yoga"
  }
}
```

**Authorization**: Users can only access their own profile unless admin.

---

#### Update User Profile
```http
PUT /api/users/{userId}
Authorization: Bearer <token>
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Doe",
  "fitnessLevel": "Advanced"
}
```

**Response** `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "firstName": "John",
  "lastName": "Doe",
  "fitnessLevel": "Advanced",
  "updatedAt": "2025-10-30T14:30:00Z"
}
```

---

#### Get User Preferences
```http
GET /api/users/{userId}/preferences
Authorization: Bearer <token>
```

**Response** `200 OK`
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "goals": ["Weight Loss", "Build Strength", "Flexibility"],
  "preferredClassTypes": ["Yoga", "HIIT", "Pilates"],
  "favoriteInstructors": ["inst_sarah", "inst_mike"],
  "preferredTimes": ["Morning", "Evening"],
  "preferredDays": ["Monday", "Wednesday", "Friday"],
  "notifications": {
    "email": true,
    "push": true,
    "reminderMinutes": 60
  }
}
```

---

#### Update User Preferences
```http
PUT /api/users/{userId}/preferences
Authorization: Bearer <token>
Content-Type: application/json

{
  "goals": ["Weight Loss", "Cardio Endurance"],
  "preferredClassTypes": ["Spin", "HIIT"],
  "favoriteInstructors": ["inst_lisa"]
}
```

**Response** `200 OK`

---

#### Get User Activity
```http
GET /api/users/{userId}/activity?limit=20
Authorization: Bearer <token>
```

**Response** `200 OK`
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "activities": [
    {
      "id": "int_001",
      "eventType": "Book",
      "itemType": "Class",
      "itemId": "class_001",
      "itemName": "Vinyasa Flow Yoga",
      "timestamp": "2025-10-30T10:15:00Z"
    },
    {
      "id": "int_002",
      "eventType": "View",
      "itemType": "Class",
      "itemId": "class_003",
      "itemName": "Spin Revolution",
      "timestamp": "2025-10-30T09:45:00Z"
    }
  ],
  "pagination": {
    "total": 156,
    "limit": 20,
    "offset": 0
  }
}
```

---

### Classes

#### List All Classes
```http
GET /api/classes?page=1&pageSize=20&sortBy=startTime&order=asc
Authorization: Bearer <token> (optional for browsing)
```

**Query Parameters**
- `page` (default: 1): Page number
- `pageSize` (default: 20): Items per page
- `sortBy` (default: startTime): Field to sort by (startTime, rating, name)
- `order` (default: asc): Sort order (asc, desc)

**Response** `200 OK`
```json
{
  "classes": [
    {
      "id": "class_001",
      "name": "Vinyasa Flow Yoga",
      "type": "Yoga",
      "description": "Dynamic yoga practice connecting breath with movement",
      "instructorId": "inst_sarah",
      "instructorName": "Sarah Johnson",
      "locationId": "loc_uptown",
      "locationName": "Minneapolis - Uptown",
      "startTime": "2025-10-31T18:00:00Z",
      "durationMinutes": 60,
      "capacity": 25,
      "currentEnrollment": 12,
      "availableSpots": 13,
      "averageRating": 4.8,
      "difficultyLevel": "All Levels",
      "imageUrl": "https://cdn.fitlife.com/images/yoga.jpg",
      "isActive": true
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalPages": 5,
    "totalCount": 94
  }
}
```

---

#### Search Classes
```http
GET /api/classes/search?query=yoga&type=Yoga&date=2025-10-31&instructor=Sarah&difficulty=All+Levels
```

**Query Parameters**
- `query`: Search in name and description
- `type`: Filter by class type (Yoga, HIIT, Spin, etc.)
- `date`: Filter by date (YYYY-MM-DD)
- `startTime`: Filter by time range (e.g., "morning", "evening")
- `instructor`: Filter by instructor name (partial match)
- `difficulty`: Filter by difficulty level
- `locationId`: Filter by location

**Response** `200 OK` (same structure as List All Classes)

---

#### Get Single Class
```http
GET /api/classes/{classId}
```

**Response** `200 OK`
```json
{
  "id": "class_001",
  "name": "Vinyasa Flow Yoga",
  "type": "Yoga",
  "description": "Dynamic yoga practice connecting breath with movement. Suitable for all levels.",
  "instructorId": "inst_sarah",
  "instructorName": "Sarah Johnson",
  "instructorBio": "Certified yoga instructor with 10 years of experience...",
  "locationId": "loc_uptown",
  "locationName": "Minneapolis - Uptown",
  "startTime": "2025-10-31T18:00:00Z",
  "endTime": "2025-10-31T19:00:00Z",
  "durationMinutes": 60,
  "capacity": 25,
  "currentEnrollment": 12,
  "availableSpots": 13,
  "averageRating": 4.8,
  "totalReviews": 156,
  "difficultyLevel": "All Levels",
  "imageUrl": "https://cdn.fitlife.com/images/yoga.jpg",
  "tags": ["Vinyasa", "Flow", "Flexibility", "Mind-Body"],
  "equipment": ["Yoga Mat", "Blocks (optional)"],
  "isActive": true,
  "createdAt": "2025-09-15T10:00:00Z"
}
```

---

#### Get Upcoming Classes
```http
GET /api/classes/upcoming?days=7&userId={userId}
Authorization: Bearer <token>
```

**Query Parameters**
- `days` (default: 7): Number of days to look ahead
- `userId` (optional): Personalize based on user preferences

**Response** `200 OK`
```json
{
  "classes": [ ... ],
  "count": 47
}
```

---

#### Create Class (Admin Only)
```http
POST /api/classes
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "name": "Sunrise Meditation",
  "type": "Yoga",
  "description": "Peaceful meditation session to start your day",
  "instructorId": "inst_sarah",
  "locationId": "loc_uptown",
  "startTime": "2025-11-01T06:00:00Z",
  "durationMinutes": 45,
  "capacity": 20,
  "difficultyLevel": "Beginner",
  "imageUrl": "https://cdn.fitlife.com/images/meditation.jpg"
}
```

**Response** `201 Created`

---

#### Update Class (Admin Only)
```http
PUT /api/classes/{classId}
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "capacity": 30,
  "description": "Updated description"
}
```

**Response** `200 OK`

---

#### Delete Class (Admin Only)
```http
DELETE /api/classes/{classId}
Authorization: Bearer <admin-token>
```

**Response** `204 No Content`

**Note**: This is a soft delete (sets `isActive = false`)

---

### Recommendations

#### Get Personalized Recommendations
```http
GET /api/recommendations/{userId}?type=class&limit=10
Authorization: Bearer <token>
```

**Query Parameters**
- `type` (default: class): Type of recommendations (class, workout, article)
- `limit` (default: 10): Number of recommendations
- `refresh` (default: false): Force regeneration

**Response** `200 OK`
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "recommendations": [
    {
      "rank": 1,
      "score": 87.5,
      "reason": "Because you love yoga classes with Sarah Johnson",
      "class": {
        "id": "class_006",
        "name": "Restorative Yoga",
        "type": "Yoga",
        "instructorName": "Sarah Johnson",
        "startTime": "2025-11-02T10:00:00Z",
        "durationMinutes": 75,
        "availableSpots": 15,
        "averageRating": 4.9,
        "imageUrl": "https://cdn.fitlife.com/images/restorative.jpg"
      }
    },
    {
      "rank": 2,
      "score": 82.0,
      "reason": "Popular among HighlyActive members like you",
      "class": { ... }
    }
  ],
  "generatedAt": "2025-10-30T14:25:00Z",
  "expiresAt": "2025-10-30T14:35:00Z"
}
```

**Response Headers**
- `X-Cache-Status`: HIT or MISS
- `X-Generation-Time-Ms`: Time to generate (if cache miss)

---

#### Refresh Recommendations
```http
POST /api/recommendations/refresh/{userId}
Authorization: Bearer <token>
```

**Response** `200 OK`
```json
{
  "message": "Recommendations refreshed successfully",
  "recommendations": [ ... ],
  "generatedAt": "2025-10-30T14:30:00Z"
}
```

---

#### Explain Recommendation
```http
GET /api/recommendations/{userId}/explain/{itemId}
Authorization: Bearer <token>
```

**Response** `200 OK`
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "itemId": "class_006",
  "score": 87.5,
  "explanation": {
    "summary": "This class is recommended because you love yoga classes with Sarah Johnson",
    "factors": [
      {
        "factor": "Preferred Instructor",
        "weight": 20,
        "description": "Sarah Johnson is one of your favorite instructors"
      },
      {
        "factor": "Preferred Class Type",
        "weight": 15,
        "description": "You frequently book Yoga classes"
      },
      {
        "factor": "Fitness Level Match",
        "weight": 10,
        "description": "Class difficulty matches your level (Intermediate)"
      },
      {
        "factor": "High Rating",
        "weight": 9.8,
        "description": "This class has excellent reviews (4.9/5)"
      },
      {
        "factor": "Segment Boost",
        "weight": 12,
        "description": "Popular among HighlyActive members"
      }
    ]
  }
}
```

---

### Events

#### Track Event
```http
POST /api/events
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "itemId": "class_001",
  "itemType": "Class",
  "eventType": "View",
  "metadata": {
    "source": "recommendation_feed",
    "position": 1,
    "sessionId": "sess_xyz"
  }
}
```

**Event Types**
- `View`: User viewed item details
- `Click`: User clicked on item
- `Book`: User booked a class
- `Complete`: User completed a class
- `Cancel`: User cancelled a booking
- `Rate`: User rated a class

**Response** `202 Accepted`
```json
{
  "eventId": "evt_12345",
  "status": "queued",
  "message": "Event queued for processing"
}
```

**Note**: Event processing is asynchronous via Kafka.

---

#### Get User Events
```http
GET /api/events/{userId}?limit=50&eventType=Book
Authorization: Bearer <token>
```

**Query Parameters**
- `limit` (default: 50): Number of events
- `eventType` (optional): Filter by event type
- `startDate` (optional): Filter by date range (YYYY-MM-DD)
- `endDate` (optional): Filter by date range

**Response** `200 OK`
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "events": [
    {
      "id": "evt_12345",
      "itemId": "class_001",
      "itemType": "Class",
      "eventType": "Book",
      "timestamp": "2025-10-30T10:15:00Z",
      "metadata": { ... }
    }
  ],
  "pagination": {
    "total": 234,
    "limit": 50,
    "offset": 0
  }
}
```

---

#### Get Event Analytics (Admin Only)
```http
GET /api/events/analytics?startDate=2025-10-01&endDate=2025-10-30
Authorization: Bearer <admin-token>
```

**Response** `200 OK`
```json
{
  "dateRange": {
    "start": "2025-10-01T00:00:00Z",
    "end": "2025-10-30T23:59:59Z"
  },
  "eventCounts": {
    "View": 15678,
    "Click": 3456,
    "Book": 892,
    "Complete": 756,
    "Cancel": 45
  },
  "conversionRates": {
    "viewToClick": 0.22,
    "clickToBook": 0.26,
    "bookToComplete": 0.85
  },
  "topClasses": [
    {
      "classId": "class_002",
      "name": "HIIT Blast",
      "bookings": 156
    }
  ]
}
```

---

### Health & Monitoring

#### Health Check
```http
GET /health
```

**Response** `200 OK`
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-30T14:30:00Z",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "kafka": "Healthy"
  }
}
```

**Status Codes**
- `200`: All systems healthy
- `503`: One or more systems degraded

---

#### Readiness Check
```http
GET /health/ready
```

**Response** `200 OK` or `503 Service Unavailable`

---

#### Metrics
```http
GET /metrics
```

**Response** `200 OK` (Prometheus format)
```
# HELP api_requests_total Total number of API requests
# TYPE api_requests_total counter
api_requests_total{method="GET",endpoint="/api/classes",status="200"} 12456

# HELP api_request_duration_seconds API request duration
# TYPE api_request_duration_seconds histogram
api_request_duration_seconds_bucket{le="0.1"} 10234
api_request_duration_seconds_bucket{le="0.5"} 12000
...
```

---

## Error Responses

All error responses follow this format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid email format",
    "details": [
      {
        "field": "email",
        "issue": "Must be a valid email address"
      }
    ],
    "timestamp": "2025-10-30T14:30:00Z",
    "path": "/api/auth/register",
    "traceId": "trace_xyz123"
  }
}
```

### Common Error Codes

| HTTP Status | Code                  | Description                          |
|-------------|-----------------------|--------------------------------------|
| 400         | VALIDATION_ERROR      | Invalid request data                 |
| 401         | UNAUTHORIZED          | Missing or invalid authentication    |
| 403         | FORBIDDEN             | Insufficient permissions             |
| 404         | NOT_FOUND             | Resource not found                   |
| 409         | CONFLICT              | Resource already exists              |
| 429         | RATE_LIMIT_EXCEEDED   | Too many requests                    |
| 500         | INTERNAL_SERVER_ERROR | Unexpected server error              |
| 503         | SERVICE_UNAVAILABLE   | System temporarily unavailable       |

---

## Rate Limiting

- **Per User**: 100 requests per minute
- **Per IP**: 1000 requests per minute

**Rate Limit Headers**
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 75
X-RateLimit-Reset: 1698700000
```

**Response when exceeded** `429 Too Many Requests`
```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Rate limit exceeded. Try again in 45 seconds.",
    "retryAfter": 45
  }
}
```

---

## Pagination

List endpoints support cursor-based pagination for optimal performance.

**Request**
```http
GET /api/classes?page=2&pageSize=20
```

**Response**
```json
{
  "data": [ ... ],
  "pagination": {
    "currentPage": 2,
    "pageSize": 20,
    "totalPages": 5,
    "totalCount": 94,
    "hasNext": true,
    "hasPrevious": true
  }
}
```

---

## Filtering & Sorting

Most list endpoints support filtering and sorting via query parameters.

**Example**
```http
GET /api/classes?type=Yoga&difficulty=Beginner&sortBy=rating&order=desc
```

**Supported Operators**
- Equality: `field=value`
- Range: `startDate=2025-10-01&endDate=2025-10-31`
- Multiple values: `type=Yoga,Pilates` (OR logic)

---

## Postman Collection

Import the Postman collection for easy API testing:
```
./postman/FitLife-API.postman_collection.json
```

Includes:
- Pre-configured environments (local, staging, production)
- All endpoints with example requests
- Automated token management
- Test scripts for validation

---

## API Versioning

Current version: **v1**

Future versions will be accessible via:
```
/api/v2/...
```

Breaking changes will always bump the major version.
