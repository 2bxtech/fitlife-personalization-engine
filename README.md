# FitLife Personalization Engine

> A proof-of-concept gym class recommendation system demonstrating modern microservices architecture, event-driven design, and personalized user experiences for Life Time Fitness members.

## 🎯 Project Overview

FitLife is a full-stack personalization platform that recommends gym classes, workouts, and content to members based on their preferences, behavior patterns, and fitness goals. This project showcases enterprise-level software engineering practices suitable for rebuilding Life Time's personalization and Pega systems.

### Key Features

- ✅ **User Profile Management** - Comprehensive fitness profiles with goals and preferences
- ✅ **Smart Recommendations** - Rule-based scoring engine with segment-aware personalization
- ✅ **Real-Time Event Tracking** - Capture user interactions via Kafka event streaming
- ✅ **Class Catalog** - Browse, filter, and search gym classes with advanced filtering
- ✅ **Admin Dashboard** - Manage classes and view analytics
- ✅ **Responsive Frontend** - Modern Vue.js SPA with TypeScript and Tailwind CSS
- ✅ **Scalable Architecture** - Microservices ready for Kubernetes deployment

## 🏗️ Tech Stack

### Backend
- **.NET Core 8** - Web API with C# 12
- **Entity Framework Core** - ORM with SQL Server
- **Redis** - Caching layer for recommendations
- **Apache Kafka** - Event streaming and async processing
- **JWT Authentication** - Secure API access

### Frontend
- **Vue.js 3** - Composition API with TypeScript
- **Pinia** - State management
- **Tailwind CSS** - Utility-first styling
- **Axios** - HTTP client with interceptors
- **Chart.js** - Data visualization

### Infrastructure
- **Docker** - Containerization
- **Kubernetes** - Orchestration and scaling
- **Azure SQL Database** - Production data storage
- **Azure Event Hubs** - Kafka-compatible event streaming
- **GitHub Actions** - CI/CD automation

## 🚀 Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/)

### Local Development with Docker Compose

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/fitlife-app.git
   cd fitlife-app
   ```

2. **Start all services**
   ```bash
   docker-compose up -d
   ```

3. **Run database migrations**
   ```bash
   cd FitLife.Api
   dotnet ef database update
   dotnet run --seed  # Seed sample data
   ```

4. **Start the frontend** (separate terminal)
   ```bash
   cd fitlife-web
   npm install
   npm run dev
   ```

5. **Access the application**
   - Frontend: http://localhost:3000
   - API: http://localhost:8080
   - Swagger UI: http://localhost:8080/swagger

### Running Tests

```bash
# Backend tests
cd FitLife.Api
dotnet test

# Frontend tests
cd fitlife-web
npm run test
```

## 📁 Project Structure

```
fitlife-app/
├── FitLife.Api/              # .NET Core Web API
│   ├── Controllers/          # API endpoints
│   ├── Services/             # Business logic
│   ├── Models/               # Domain models
│   ├── Data/                 # EF Core DbContext & repositories
│   ├── Infrastructure/       # Kafka, Redis, JWT services
│   └── BackgroundServices/   # Event consumers & batch jobs
├── fitlife-web/              # Vue.js frontend
│   ├── src/
│   │   ├── components/       # Reusable UI components
│   │   ├── views/            # Page-level components
│   │   ├── stores/           # Pinia state management
│   │   ├── services/         # API client layer
│   │   └── router/           # Vue Router configuration
├── docker-compose.yml        # Local development stack
├── k8s/                      # Kubernetes manifests
├── .github/workflows/        # CI/CD pipelines
└── docs/                     # Additional documentation
```

## 🎨 Architecture Highlights

### Event-Driven Architecture
- User interactions publish events to Kafka topics
- Background workers consume events asynchronously
- Decouples API layer from data processing

### Caching Strategy
- Redis caches recommendations (10-minute TTL)
- Cache-aside pattern with automatic refresh
- Reduces database load by 90%+

### Recommendation Engine
- Multi-factor scoring algorithm
- User segmentation (YogaEnthusiast, HighlyActive, etc.)
- Real-time personalization based on behavior

See [ARCHITECTURE.md](./docs/ARCHITECTURE.md) for detailed system design.

## 🔐 Authentication

The API uses JWT bearer tokens. To authenticate:

1. **Register a new user**
   ```bash
   POST /api/auth/register
   {
     "email": "user@example.com",
     "password": "SecurePass123!",
     "firstName": "John",
     "lastName": "Doe",
     "fitnessLevel": "Beginner"
   }
   ```

2. **Login to get token**
   ```bash
   POST /api/auth/login
   {
     "email": "user@example.com",
     "password": "SecurePass123!"
   }
   ```

3. **Use token in requests**
   ```
   Authorization: Bearer <your-jwt-token>
   ```

## 📊 Key Metrics

Performance targets for production:
- **API Latency**: P50 < 100ms, P95 < 200ms
- **Cache Hit Rate**: > 90%
- **Recommendation CTR**: > 15%
- **System Uptime**: 99.9%

## 🧪 Testing Strategy

- **Unit Tests**: Service layer and business logic
- **Integration Tests**: API endpoints with test database
- **E2E Tests**: Frontend user flows with Playwright
- **Load Tests**: API stress testing with k6

## 🚢 Deployment

### Docker
```bash
# Build images
docker build -t fitlife-api:latest ./FitLife.Api
docker build -t fitlife-web:latest ./fitlife-web

# Push to registry
docker push yourregistry/fitlife-api:latest
docker push yourregistry/fitlife-web:latest
```

### Kubernetes
```bash
# Apply manifests
kubectl apply -f k8s/

# Check deployment status
kubectl get pods -n fitlife

# Access application
kubectl port-forward svc/fitlife-web 3000:80
```

See [DEPLOYMENT.md](./docs/DEPLOYMENT.md) for detailed deployment instructions.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

See [DEVELOPMENT.md](./docs/DEVELOPMENT.md) for coding standards and workflow.

## 📚 Documentation

- [Architecture Overview](./docs/ARCHITECTURE.md)
- [API Specification](./docs/API.md)
- [Database Schema](./docs/DATABASE.md)
- [Recommendation Algorithm](./docs/RECOMMENDATIONS.md)
- [Development Guide](./docs/DEVELOPMENT.md)
- [Deployment Guide](./docs/DEPLOYMENT.md)

## 🎯 Interview Demo Checklist

Before presenting this project:

- [ ] Application runs locally via Docker Compose
- [ ] All API endpoints tested in Postman/Swagger
- [ ] Frontend displays data correctly with smooth UX
- [ ] User can register, login, browse, and book classes
- [ ] Recommendations update after user interactions
- [ ] Events flow through Kafka to background workers
- [ ] Code is clean, well-commented, and follows best practices
- [ ] Can explain trade-offs and scaling strategies
- [ ] Demo video recorded (5-10 minutes)

## 📝 License

This project is created as a demonstration/portfolio piece for interview purposes.

## 👤 Contact

**Your Name**
- Email: your.email@example.com
- GitHub: [@yourusername](https://github.com/yourusername)
- LinkedIn: [Your Name](https://linkedin.com/in/yourprofile)

---

**Built with ❤️ to showcase modern full-stack engineering for Life Time Fitness**
