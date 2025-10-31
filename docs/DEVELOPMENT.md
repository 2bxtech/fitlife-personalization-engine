# FitLife Development Guide

## Table of Contents
1. [Getting Started](#getting-started)
2. [Development Environment Setup](#development-environment-setup)
3. [Project Structure](#project-structure)
4. [Coding Standards](#coding-standards)
5. [Testing Strategy](#testing-strategy)
6. [Git Workflow](#git-workflow)
7. [Debugging](#debugging)
8. [Common Tasks](#common-tasks)

---

## Getting Started

### Prerequisites

Install the following tools:

- **.NET 8 SDK** - https://dotnet.microsoft.com/download/dotnet/8.0
- **Node.js 18+** (LTS) - https://nodejs.org/
- **Docker Desktop** - https://www.docker.com/products/docker-desktop
- **Git** - https://git-scm.com/
- **Visual Studio 2022** or **VS Code** with C# extension
- **Azure CLI** (optional) - https://docs.microsoft.com/en-us/cli/azure/install-azure-cli

### Clone Repository

```bash
git clone https://github.com/yourusername/fitlife-app.git
cd fitlife-app
```

---

## Development Environment Setup

### Backend Setup (.NET Core)

1. **Navigate to API project**
   ```bash
   cd FitLife.Api
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Update appsettings.Development.json**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost,1433;Database=FitLifeDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
     },
     "Redis": {
       "ConnectionString": "localhost:6379"
     },
     "Kafka": {
       "BootstrapServers": "localhost:9092"
     },
     "Jwt": {
       "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
       "Issuer": "FitLifeApi",
       "Audience": "FitLifeClient",
       "ExpirationHours": 24
     }
   }
   ```

4. **Start dependencies with Docker Compose**
   ```bash
   cd ..
   docker-compose up -d sqlserver redis kafka zookeeper
   ```

5. **Run database migrations**
   ```bash
   cd FitLife.Api
   dotnet ef database update
   ```

6. **Seed sample data**
   ```bash
   dotnet run --seed
   ```

7. **Run the API**
   ```bash
   dotnet run
   ```

   API will be available at: http://localhost:8080
   Swagger UI: http://localhost:8080/swagger

### Frontend Setup (Vue.js)

1. **Navigate to frontend project**
   ```bash
   cd fitlife-web
   ```

2. **Install dependencies**
   ```bash
   npm install
   ```

3. **Create .env.development**
   ```env
   VITE_API_BASE_URL=http://localhost:8080/api
   VITE_APP_NAME=FitLife
   ```

4. **Run development server**
   ```bash
   npm run dev
   ```

   Frontend will be available at: http://localhost:3000

---

## Project Structure

### Backend Structure

```
FitLife.Api/
├── Controllers/              # API endpoints
│   ├── AuthController.cs
│   ├── UsersController.cs
│   ├── ClassesController.cs
│   ├── RecommendationsController.cs
│   └── EventsController.cs
├── Services/                 # Business logic
│   ├── IUserService.cs
│   ├── UserService.cs
│   ├── IRecommendationService.cs
│   ├── RecommendationService.cs
│   └── ScoringEngine.cs
├── Models/                   # Domain models & DTOs
│   ├── User.cs
│   ├── Class.cs
│   ├── Interaction.cs
│   ├── Recommendation.cs
│   └── DTOs/
├── Data/                     # Data access layer
│   ├── FitLifeDbContext.cs
│   ├── Repositories/
│   └── Migrations/
├── Infrastructure/           # External services
│   ├── Kafka/
│   ├── Redis/
│   └── Authentication/
├── BackgroundServices/       # Background workers
│   ├── EventConsumerService.cs
│   ├── RecommendationGeneratorService.cs
│   └── UserProfilerService.cs
├── Program.cs               # Application entry point
└── appsettings.json         # Configuration
```

### Frontend Structure

```
fitlife-web/
├── src/
│   ├── components/          # Reusable UI components
│   │   ├── layout/
│   │   ├── classes/
│   │   ├── recommendations/
│   │   └── common/
│   ├── views/              # Page components
│   │   ├── DashboardView.vue
│   │   ├── ClassesView.vue
│   │   └── ProfileView.vue
│   ├── stores/             # Pinia state management
│   │   ├── auth.ts
│   │   ├── classes.ts
│   │   └── recommendations.ts
│   ├── services/           # API client layer
│   │   ├── api.ts
│   │   ├── authService.ts
│   │   └── classService.ts
│   ├── router/             # Vue Router config
│   │   └── index.ts
│   ├── types/              # TypeScript interfaces
│   ├── utils/              # Helper functions
│   ├── App.vue
│   └── main.ts
├── package.json
├── vite.config.ts
└── tailwind.config.js
```

---

## Coding Standards

### C# Backend Standards

#### Naming Conventions
- **Classes/Interfaces**: PascalCase (`UserService`, `IUserRepository`)
- **Methods**: PascalCase (`GetUserById`, `CreateRecommendations`)
- **Variables/Parameters**: camelCase (`userId`, `classFilter`)
- **Private Fields**: _camelCase (`_context`, `_logger`)
- **Constants**: PascalCase (`MaxRetryAttempts`)

#### Code Style
```csharp
// Good: Use explicit types when not obvious
User user = await _userRepository.GetByIdAsync(userId);

// Good: Use var when type is obvious
var users = new List<User>();

// Good: Async suffix for async methods
public async Task<User> GetUserByIdAsync(string userId)
{
    return await _context.Users.FindAsync(userId);
}

// Good: Guard clauses early
public IActionResult GetUser(string id)
{
    if (string.IsNullOrEmpty(id))
        return BadRequest("User ID is required");
        
    var user = _userService.GetById(id);
    if (user == null)
        return NotFound();
        
    return Ok(user);
}

// Good: Dependency injection in constructor
public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;
    
    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}
```

#### XML Documentation
```csharp
/// <summary>
/// Generates personalized class recommendations for a user.
/// </summary>
/// <param name="userId">The unique identifier of the user.</param>
/// <param name="limit">Maximum number of recommendations to return.</param>
/// <returns>List of recommended classes with scores and reasons.</returns>
/// <exception cref="ArgumentException">Thrown when userId is null or empty.</exception>
public async Task<List<RecommendationDto>> GenerateRecommendationsAsync(string userId, int limit = 10)
```

### TypeScript Frontend Standards

#### Naming Conventions
- **Components**: PascalCase (`ClassCard.vue`, `RecommendationFeed.vue`)
- **Composables**: camelCase with `use` prefix (`useAuth`, `useClasses`)
- **Constants**: UPPER_SNAKE_CASE (`API_BASE_URL`, `MAX_RESULTS`)
- **Types/Interfaces**: PascalCase (`User`, `ClassResponse`)

#### Code Style
```typescript
// Good: Use Composition API with <script setup>
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { Class } from '@/types/Class'

const props = defineProps<{
  classId: string
}>()

const emit = defineEmits<{
  book: [classId: string]
  cancel: [classId: string]
}>()

const classData = ref<Class | null>(null)
const isLoading = ref(false)

const availableSpots = computed(() => {
  if (!classData.value) return 0
  return classData.value.capacity - classData.value.currentEnrollment
})

onMounted(async () => {
  await fetchClass()
})

async function fetchClass() {
  isLoading.value = true
  try {
    classData.value = await classService.getClass(props.classId)
  } catch (error) {
    console.error('Failed to fetch class:', error)
  } finally {
    isLoading.value = false
  }
}
</script>
```

#### Type Definitions
```typescript
// types/User.ts
export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  fitnessLevel: FitnessLevel
  segment: UserSegment
  createdAt: string
  updatedAt: string
}

export type FitnessLevel = 'Beginner' | 'Intermediate' | 'Advanced'
export type UserSegment = 'General' | 'YogaEnthusiast' | 'HighlyActive' | 'StrengthTrainer'
```

### Code Formatting

#### Backend (.NET)
Use `.editorconfig`:
```ini
[*.cs]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# Use var when type is obvious
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion

# Prefer pattern matching
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
```

**Format code**:
```bash
dotnet format
```

#### Frontend (Vue/TypeScript)
Use **ESLint** and **Prettier**:

```bash
# Install dev dependencies
npm install -D eslint prettier @vue/eslint-config-typescript

# Format code
npm run lint
npm run format
```

---

## Testing Strategy

### Backend Testing

#### Unit Tests (xUnit)

**Example: Service Layer Test**
```csharp
public class RecommendationServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IClassRepository> _classRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly RecommendationService _service;
    
    public RecommendationServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _classRepositoryMock = new Mock<IClassRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _service = new RecommendationService(
            _userRepositoryMock.Object,
            _classRepositoryMock.Object,
            _cacheServiceMock.Object
        );
    }
    
    [Fact]
    public async Task GenerateRecommendations_ReturnsTopTenByScore()
    {
        // Arrange
        var userId = "user_001";
        var user = new User { Id = userId, PreferredClassTypes = "[\"Yoga\"]" };
        var classes = TestDataHelper.GetSampleClasses(20);
        
        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _classRepositoryMock.Setup(x => x.GetUpcomingClassesAsync()).ReturnsAsync(classes);
        
        // Act
        var result = await _service.GenerateRecommendationsAsync(userId, 10);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Count);
        Assert.True(result[0].Score >= result[1].Score); // Ordered by score
    }
}
```

**Run tests**:
```bash
dotnet test
```

#### Integration Tests

**Example: API Controller Test**
```csharp
public class ClassesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public ClassesControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real database with in-memory database
                services.RemoveAll<DbContextOptions<FitLifeDbContext>>();
                services.AddDbContext<FitLifeDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });
            });
        }).CreateClient();
    }
    
    [Fact]
    public async Task GetClasses_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/classes");
        
        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
```

### Frontend Testing

#### Unit Tests (Vitest)

**Example: Component Test**
```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import ClassCard from '@/components/classes/ClassCard.vue'

describe('ClassCard', () => {
  const mockClass = {
    id: 'class_001',
    name: 'Vinyasa Flow Yoga',
    instructorName: 'Sarah Johnson',
    startTime: '2025-10-31T18:00:00Z',
    durationMinutes: 60,
    capacity: 25,
    currentEnrollment: 12,
    averageRating: 4.8
  }
  
  it('renders class information correctly', () => {
    const wrapper = mount(ClassCard, {
      props: { class: mockClass }
    })
    
    expect(wrapper.text()).toContain('Vinyasa Flow Yoga')
    expect(wrapper.text()).toContain('Sarah Johnson')
  })
  
  it('emits book event when button clicked', async () => {
    const wrapper = mount(ClassCard, {
      props: { class: mockClass }
    })
    
    await wrapper.find('[data-testid="book-button"]').trigger('click')
    
    expect(wrapper.emitted('book')).toBeTruthy()
    expect(wrapper.emitted('book')?.[0]).toEqual(['class_001'])
  })
})
```

**Run tests**:
```bash
npm run test
```

#### E2E Tests (Playwright)

```typescript
import { test, expect } from '@playwright/test'

test('user can browse and book classes', async ({ page }) => {
  // Login
  await page.goto('http://localhost:3000/login')
  await page.fill('[data-testid="email-input"]', 'test@example.com')
  await page.fill('[data-testid="password-input"]', 'password123')
  await page.click('[data-testid="login-button"]')
  
  // Navigate to classes
  await page.click('[data-testid="nav-classes"]')
  await expect(page).toHaveURL(/.*classes/)
  
  // Filter by type
  await page.selectOption('[data-testid="type-filter"]', 'Yoga')
  
  // Click on first class
  await page.click('[data-testid="class-card"]:first-child')
  
  // Book class
  await page.click('[data-testid="book-button"]')
  
  // Verify success message
  await expect(page.locator('[data-testid="success-toast"]')).toBeVisible()
})
```

---

## Git Workflow

### Branch Naming

- `main` - Production-ready code
- `develop` - Integration branch
- `feature/feature-name` - New features
- `bugfix/bug-description` - Bug fixes
- `hotfix/critical-fix` - Production hotfixes

### Commit Messages

Follow **Conventional Commits**:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting)
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Build/tooling changes

**Examples**:
```
feat(recommendations): add user segment-based scoring

- Implement user profiler service
- Add segment field to User model
- Update scoring algorithm to consider segments

Closes #123
```

```
fix(api): prevent duplicate event tracking

Users were accidentally sending duplicate events when
clicking rapidly. Added debouncing to event tracking.

Fixes #456
```

### Pull Request Process

1. **Create feature branch**
   ```bash
   git checkout -b feature/add-favorites
   ```

2. **Make changes and commit**
   ```bash
   git add .
   git commit -m "feat(classes): add favorites functionality"
   ```

3. **Push to remote**
   ```bash
   git push origin feature/add-favorites
   ```

4. **Open Pull Request on GitHub**
   - Fill out PR template
   - Link related issues
   - Add reviewers
   - Wait for CI to pass

5. **Address review comments**

6. **Merge when approved**
   - Squash and merge (preferred)
   - Delete branch after merge

---

## Debugging

### Backend Debugging

#### Visual Studio 2022
1. Open `FitLife.sln`
2. Set `FitLife.Api` as startup project
3. Press F5 to start debugging
4. Set breakpoints in code

#### VS Code
1. Open folder in VS Code
2. Install C# extension
3. Create `.vscode/launch.json`:
   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "name": ".NET Core Launch (web)",
         "type": "coreclr",
         "request": "launch",
         "preLaunchTask": "build",
         "program": "${workspaceFolder}/FitLife.Api/bin/Debug/net8.0/FitLife.Api.dll",
         "args": [],
         "cwd": "${workspaceFolder}/FitLife.Api",
         "env": {
           "ASPNETCORE_ENVIRONMENT": "Development"
         }
       }
     ]
   }
   ```
4. Press F5 to start debugging

#### Logging
```csharp
_logger.LogInformation("Generating recommendations for user {UserId}", userId);
_logger.LogWarning("Cache miss for user {UserId}, generating fresh recommendations", userId);
_logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
```

View logs in console or Application Insights.

### Frontend Debugging

#### Browser DevTools
1. Open Chrome DevTools (F12)
2. Go to Sources tab
3. Set breakpoints in TypeScript files
4. Inspect network requests in Network tab

#### Vue DevTools
1. Install Vue DevTools browser extension
2. Open DevTools and select Vue tab
3. Inspect component state, events, and props

---

## Common Tasks

### Add New API Endpoint

1. **Create DTO**
   ```csharp
   // Models/DTOs/CreateClassRequest.cs
   public class CreateClassRequest
   {
       [Required]
       public string Name { get; set; }
       
       [Required]
       public string Type { get; set; }
       
       [Required]
       public DateTime StartTime { get; set; }
   }
   ```

2. **Add Controller Action**
   ```csharp
   [HttpPost]
   [Authorize(Roles = "Admin")]
   public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request)
   {
       var classEntity = _mapper.Map<Class>(request);
       await _classService.CreateAsync(classEntity);
       return CreatedAtAction(nameof(GetClass), new { id = classEntity.Id }, classEntity);
   }
   ```

3. **Update Swagger documentation** (automatic with XML comments)

4. **Test with Postman**

### Add New Migration

1. **Modify model**
   ```csharp
   public class User
   {
       // ... existing properties
       public string PhoneNumber { get; set; } // New property
   }
   ```

2. **Create migration**
   ```bash
   dotnet ef migrations add AddPhoneNumberToUser
   ```

3. **Review generated migration**

4. **Apply migration**
   ```bash
   dotnet ef database update
   ```

### Add New Vue Component

1. **Create component file**
   ```vue
   <!-- components/classes/ClassFilter.vue -->
   <script setup lang="ts">
   import { ref } from 'vue'
   
   const selectedType = ref<string>('')
   const emit = defineEmits<{
     filterChange: [type: string]
   }>()
   
   function handleFilterChange() {
     emit('filterChange', selectedType.value)
   }
   </script>
   
   <template>
     <div class="filter-container">
       <select v-model="selectedType" @change="handleFilterChange">
         <option value="">All Types</option>
         <option value="Yoga">Yoga</option>
         <option value="HIIT">HIIT</option>
       </select>
     </div>
   </template>
   ```

2. **Use in parent component**
   ```vue
   <template>
     <ClassFilter @filter-change="handleFilter" />
   </template>
   ```

### Update Dependencies

**Backend**:
```bash
dotnet list package --outdated
dotnet add package PackageName --version x.y.z
```

**Frontend**:
```bash
npm outdated
npm update
# or for specific package
npm install package@latest
```

---

## Troubleshooting

### Common Issues

#### "Cannot connect to SQL Server"
- Ensure Docker containers are running: `docker-compose ps`
- Check connection string in appsettings.json
- Verify SQL Server is accepting connections on port 1433

#### "Kafka connection refused"
- Ensure Kafka and Zookeeper are running
- Check bootstrap servers config: `localhost:9092`
- View Kafka logs: `docker-compose logs kafka`

#### "CORS policy error"
- Verify CORS is configured in Program.cs
- Check frontend API base URL matches backend URL
- Ensure credentials are included in requests

#### "Migration already applied"
- Check migration history: `dotnet ef migrations list`
- Rollback if needed: `dotnet ef database update <PreviousMigration>`
- Remove migration: `dotnet ef migrations remove`

---

## Resources

- [.NET Core Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Vue.js Guide](https://vuejs.org/guide/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [Tailwind CSS](https://tailwindcss.com/docs)
- [Docker Documentation](https://docs.docker.com/)

---

Happy coding! 🚀
