# FitLife Deployment Guide

## Table of Contents
1. [Deployment Overview](#deployment-overview)
2. [Docker Deployment](#docker-deployment)
3. [Kubernetes Deployment](#kubernetes-deployment)
4. [Azure Deployment](#azure-deployment)
5. [CI/CD Pipeline](#cicd-pipeline)
6. [Environment Configuration](#environment-configuration)
7. [Monitoring & Observability](#monitoring--observability)
8. [Rollback Procedures](#rollback-procedures)

---

## Deployment Overview

### Environments

| Environment | Purpose | URL | Deployment Trigger |
|-------------|---------|-----|-------------------|
| **Local** | Development | localhost | Manual |
| **Staging** | Pre-production testing | staging.fitlife.com | Push to `develop` |
| **Production** | Live user traffic | app.fitlife.com | Manual approval after staging |

### Deployment Architecture

```
GitHub Repository
      │
      ├─► [Push to main/develop]
      │
      ▼
GitHub Actions CI/CD
      │
      ├─► Build Docker Images
      ├─► Run Tests
      ├─► Security Scan
      │
      ▼
Azure Container Registry
      │
      ▼
Kubernetes Cluster (AKS)
      │
      ├─► API Pods (3 replicas)
      ├─► Web Pods (2 replicas)
      ├─► Worker Pods (2 replicas)
      │
      ▼
Azure Load Balancer → Users
```

---

## Docker Deployment

### Build Docker Images

#### Backend API
```dockerfile
# FitLife.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FitLife.Api/FitLife.Api.csproj", "FitLife.Api/"]
RUN dotnet restore "FitLife.Api/FitLife.Api.csproj"
COPY . .
WORKDIR "/src/FitLife.Api"
RUN dotnet build "FitLife.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FitLife.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser
USER appuser

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FitLife.Api.dll"]
```

**Build and push**:
```bash
cd FitLife.Api
docker build -t fitlife-api:latest .
docker tag fitlife-api:latest yourregistry.azurecr.io/fitlife-api:latest
docker push yourregistry.azurecr.io/fitlife-api:latest
```

#### Frontend Web
```dockerfile
# fitlife-web/Dockerfile
# Build stage
FROM node:18-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Create non-root user
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chmod -R 755 /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/health || exit 1

USER nginx
CMD ["nginx", "-g", "daemon off;"]
```

**nginx.conf**:
```nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /health {
        return 200 "healthy\n";
        add_header Content-Type text/plain;
    }

    location /api {
        proxy_pass http://fitlife-api-service:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;
}
```

### Docker Compose (Local Development)

```yaml
# docker-compose.yml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: fitlife-sqlserver
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SQL_SA_PASSWORD}
      - MSSQL_PID=Express
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_SA_PASSWORD} -Q "SELECT 1"
      interval: 30s
      timeout: 10s
      retries: 5
    networks:
      - fitlife-network

  redis:
    image: redis:7-alpine
    container_name: fitlife-redis
    command: redis-server --appendonly yes --requirepass ${REDIS_PASSWORD}
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "--raw", "incr", "ping"]
      interval: 10s
      timeout: 5s
      retries: 3
    networks:
      - fitlife-network

  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    container_name: fitlife-zookeeper
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"
    networks:
      - fitlife-network

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    container_name: fitlife-kafka
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
      - "9093:9093"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9093,PLAINTEXT_HOST://localhost:9092
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"
    healthcheck:
      test: kafka-topics --bootstrap-server localhost:9093 --list
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - fitlife-network

  api:
    build:
      context: ./FitLife.Api
      dockerfile: Dockerfile
    container_name: fitlife-api
    depends_on:
      - sqlserver
      - redis
      - kafka
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Development}
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=FitLifeDB;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True
      - Redis__ConnectionString=redis:6379,password=${REDIS_PASSWORD}
      - Kafka__BootstrapServers=kafka:9093
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__Issuer=FitLifeApi
      - Jwt__Audience=FitLifeClient
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    networks:
      - fitlife-network

  web:
    build:
      context: ./fitlife-web
      dockerfile: Dockerfile
    container_name: fitlife-web
    depends_on:
      - api
    ports:
      - "3000:80"
    environment:
      - VITE_API_BASE_URL=http://localhost:8080/api
    networks:
      - fitlife-network

volumes:
  sqlserver-data:
  redis-data:

networks:
  fitlife-network:
    driver: bridge
```

**Start services**:
```bash
docker-compose up -d
```

**View logs**:
```bash
docker-compose logs -f api
```

**Stop services**:
```bash
docker-compose down
```

---

## Kubernetes Deployment

### Prerequisites

1. **Install kubectl**
   ```bash
   # Windows (chocolatey)
   choco install kubernetes-cli
   
   # macOS
   brew install kubectl
   
   # Linux
   curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
   ```

2. **Install Helm** (optional, for easier deployments)
   ```bash
   choco install kubernetes-helm  # Windows
   brew install helm              # macOS
   ```

3. **Configure kubectl to connect to your cluster**
   ```bash
   az aks get-credentials --resource-group fitlife-rg --name fitlife-aks
   ```

### Kubernetes Manifests

#### Namespace
```yaml
# k8s/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: fitlife
  labels:
    name: fitlife
```

#### ConfigMap
```yaml
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: fitlife-config
  namespace: fitlife
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  Jwt__Issuer: "FitLifeApi"
  Jwt__Audience: "FitLifeClient"
  Redis__ConnectionString: "fitlife-redis:6379"
  Kafka__BootstrapServers: "fitlife-kafka:9093"
```

#### Secrets
```yaml
# k8s/secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: fitlife-secrets
  namespace: fitlife
type: Opaque
data:
  sql-connection-string: <base64-encoded-connection-string>
  jwt-secret: <base64-encoded-secret>
  redis-password: <base64-encoded-password>
```

**Create secrets**:
```bash
kubectl create secret generic fitlife-secrets \
  --from-literal=sql-connection-string='Server=...' \
  --from-literal=jwt-secret='YourSuperSecret...' \
  --from-literal=redis-password='YourRedisPass' \
  -n fitlife
```

#### API Deployment
```yaml
# k8s/api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fitlife-api
  namespace: fitlife
  labels:
    app: fitlife-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: fitlife-api
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 1
  template:
    metadata:
      labels:
        app: fitlife-api
    spec:
      containers:
      - name: api
        image: yourregistry.azurecr.io/fitlife-api:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: fitlife-config
              key: ASPNETCORE_ENVIRONMENT
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: fitlife-secrets
              key: sql-connection-string
        - name: Jwt__Secret
          valueFrom:
            secretKeyRef:
              name: fitlife-secrets
              key: jwt-secret
        - name: Redis__ConnectionString
          valueFrom:
            configMapKeyRef:
              name: fitlife-config
              key: Redis__ConnectionString
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 20
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 2
      imagePullSecrets:
      - name: acr-secret
---
apiVersion: v1
kind: Service
metadata:
  name: fitlife-api-service
  namespace: fitlife
spec:
  selector:
    app: fitlife-api
  ports:
  - protocol: TCP
    port: 8080
    targetPort: 8080
  type: ClusterIP
```

#### Web Deployment
```yaml
# k8s/web-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fitlife-web
  namespace: fitlife
  labels:
    app: fitlife-web
spec:
  replicas: 2
  selector:
    matchLabels:
      app: fitlife-web
  template:
    metadata:
      labels:
        app: fitlife-web
    spec:
      containers:
      - name: web
        image: yourregistry.azurecr.io/fitlife-web:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 80
          name: http
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: fitlife-web-service
  namespace: fitlife
spec:
  selector:
    app: fitlife-web
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer
```

#### Ingress
```yaml
# k8s/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: fitlife-ingress
  namespace: fitlife
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
  - hosts:
    - app.fitlife.com
    secretName: fitlife-tls
  rules:
  - host: app.fitlife.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: fitlife-api-service
            port:
              number: 8080
      - path: /
        pathType: Prefix
        backend:
          service:
            name: fitlife-web-service
            port:
              number: 80
```

#### Horizontal Pod Autoscaler
```yaml
# k8s/hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: fitlife-api-hpa
  namespace: fitlife
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: fitlife-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### Deploy to Kubernetes

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Apply configurations
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml

# Deploy applications
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/web-deployment.yaml

# Configure ingress
kubectl apply -f k8s/ingress.yaml

# Enable autoscaling
kubectl apply -f k8s/hpa.yaml

# Verify deployments
kubectl get pods -n fitlife
kubectl get services -n fitlife
kubectl get ingress -n fitlife
```

---

## Azure Deployment

### Azure Resources Setup

#### 1. Create Resource Group
```bash
az group create \
  --name fitlife-rg \
  --location eastus
```

#### 2. Create Azure Container Registry
```bash
az acr create \
  --resource-group fitlife-rg \
  --name fitlifeacr \
  --sku Standard

# Login to ACR
az acr login --name fitlifeacr
```

#### 3. Create Azure Kubernetes Service
```bash
az aks create \
  --resource-group fitlife-rg \
  --name fitlife-aks \
  --node-count 3 \
  --node-vm-size Standard_DS2_v2 \
  --enable-addons monitoring \
  --generate-ssh-keys \
  --attach-acr fitlifeacr

# Get credentials
az aks get-credentials --resource-group fitlife-rg --name fitlife-aks
```

#### 4. Create Azure SQL Database
```bash
az sql server create \
  --name fitlife-sql-server \
  --resource-group fitlife-rg \
  --location eastus \
  --admin-user sqladmin \
  --admin-password 'YourStrong@Password123'

az sql db create \
  --resource-group fitlife-rg \
  --server fitlife-sql-server \
  --name FitLifeDB \
  --service-objective S1 \
  --backup-storage-redundancy Zone
```

#### 5. Create Azure Cache for Redis
```bash
az redis create \
  --resource-group fitlife-rg \
  --name fitlife-redis \
  --location eastus \
  --sku Standard \
  --vm-size c1
```

#### 6. Create Azure Event Hubs (Kafka-compatible)
```bash
az eventhubs namespace create \
  --resource-group fitlife-rg \
  --name fitlife-eventhub \
  --location eastus \
  --sku Standard

az eventhubs eventhub create \
  --resource-group fitlife-rg \
  --namespace-name fitlife-eventhub \
  --name user-events \
  --partition-count 4
```

---

## CI/CD Pipeline

### GitHub Actions Workflow

```yaml
# .github/workflows/deploy.yml
name: Build and Deploy to AKS

on:
  push:
    branches:
      - main
      - develop
  pull_request:
    branches:
      - main

env:
  AZURE_CONTAINER_REGISTRY: fitlifeacr.azurecr.io
  API_IMAGE_NAME: fitlife-api
  WEB_IMAGE_NAME: fitlife-web
  AKS_RESOURCE_GROUP: fitlife-rg
  AKS_CLUSTER_NAME: fitlife-aks
  NAMESPACE: fitlife

jobs:
  build-and-test-backend:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore FitLife.Api/FitLife.Api.csproj
    
    - name: Build
      run: dotnet build FitLife.Api/FitLife.Api.csproj --no-restore
    
    - name: Run tests
      run: dotnet test FitLife.Api.Tests/FitLife.Api.Tests.csproj --no-build --verbosity normal
    
    - name: Code coverage
      run: dotnet test FitLife.Api.Tests/FitLife.Api.Tests.csproj --collect:"XPlat Code Coverage"

  build-and-test-frontend:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: 18
        cache: 'npm'
        cache-dependency-path: fitlife-web/package-lock.json
    
    - name: Install dependencies
      run: cd fitlife-web && npm ci
    
    - name: Lint
      run: cd fitlife-web && npm run lint
    
    - name: Type check
      run: cd fitlife-web && npm run type-check
    
    - name: Run tests
      run: cd fitlife-web && npm run test
    
    - name: Build
      run: cd fitlife-web && npm run build

  build-and-push-images:
    needs: [build-and-test-backend, build-and-test-frontend]
    if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop'
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Login to ACR
      run: az acr login --name fitlifeacr
    
    - name: Build and push API image
      run: |
        docker build -t ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.API_IMAGE_NAME }}:${{ github.sha }} \
          -t ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.API_IMAGE_NAME }}:latest \
          -f FitLife.Api/Dockerfile .
        docker push ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.API_IMAGE_NAME }}:${{ github.sha }}
        docker push ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.API_IMAGE_NAME }}:latest
    
    - name: Build and push Web image
      run: |
        docker build -t ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.WEB_IMAGE_NAME }}:${{ github.sha }} \
          -t ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.WEB_IMAGE_NAME }}:latest \
          -f fitlife-web/Dockerfile fitlife-web/
        docker push ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.WEB_IMAGE_NAME }}:${{ github.sha }}
        docker push ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.WEB_IMAGE_NAME }}:latest

  deploy-staging:
    needs: build-and-push-images
    if: github.ref == 'refs/heads/develop'
    runs-on: ubuntu-latest
    environment:
      name: staging
      url: https://staging.fitlife.com
    steps:
    - uses: actions/checkout@v3
    
    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Get AKS credentials
      run: az aks get-credentials --resource-group ${{ env.AKS_RESOURCE_GROUP }} --name ${{ env.AKS_CLUSTER_NAME }}
    
    - name: Deploy to AKS
      run: |
        kubectl set image deployment/fitlife-api fitlife-api=${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.API_IMAGE_NAME }}:${{ github.sha }} -n ${{ env.NAMESPACE }}
        kubectl set image deployment/fitlife-web fitlife-web=${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.WEB_IMAGE_NAME }}:${{ github.sha }} -n ${{ env.NAMESPACE }}
        kubectl rollout status deployment/fitlife-api -n ${{ env.NAMESPACE }}
        kubectl rollout status deployment/fitlife-web -n ${{ env.NAMESPACE }}
    
    - name: Run smoke tests
      run: |
        sleep 30
        curl -f https://staging.fitlife.com/health || exit 1

  deploy-production:
    needs: build-and-push-images
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment:
      name: production
      url: https://app.fitlife.com
    steps:
    - uses: actions/checkout@v3
    
    - name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Get AKS credentials
      run: az aks get-credentials --resource-group ${{ env.AKS_RESOURCE_GROUP }} --name ${{ env.AKS_CLUSTER_NAME }}
    
    - name: Deploy to AKS
      run: |
        kubectl set image deployment/fitlife-api fitlife-api=${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.API_IMAGE_NAME }}:${{ github.sha }} -n ${{ env.NAMESPACE }}
        kubectl set image deployment/fitlife-web fitlife-web=${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.WEB_IMAGE_NAME }}:${{ github.sha }} -n ${{ env.NAMESPACE }}
        kubectl rollout status deployment/fitlife-api -n ${{ env.NAMESPACE }} --timeout=5m
        kubectl rollout status deployment/fitlife-web -n ${{ env.NAMESPACE }} --timeout=5m
    
    - name: Verify deployment
      run: |
        sleep 30
        curl -f https://app.fitlife.com/health || exit 1
        curl -f https://app.fitlife.com/api/health || exit 1
    
    - name: Notify team
      if: success()
      run: echo "Deployment successful! 🚀"
```

---

## Environment Configuration

### Local (.env file)
```env
SQL_SA_PASSWORD=YourStrong@Passw0rd
REDIS_PASSWORD=RedisPass123
JWT_SECRET=YourSuperSecretKeyThatIsAtLeast32CharactersLong!
ASPNETCORE_ENVIRONMENT=Development
```

### Staging (Kubernetes secrets)
```bash
kubectl create secret generic fitlife-secrets \
  --from-literal=sql-connection-string='Server=fitlife-sql-staging.database.windows.net;...' \
  --from-literal=jwt-secret='StagingSecret...' \
  -n fitlife-staging
```

### Production (Azure Key Vault)
```bash
# Store secrets in Key Vault
az keyvault secret set --vault-name fitlife-keyvault --name sql-connection-string --value "Server=..."
az keyvault secret set --vault-name fitlife-keyvault --name jwt-secret --value "..."

# Configure AKS to access Key Vault
# (use Azure AD Pod Identity or workload identity)
```

---

## Monitoring & Observability

### Application Insights
```bash
# Create Application Insights
az monitor app-insights component create \
  --app fitlife-insights \
  --location eastus \
  --resource-group fitlife-rg

# Get instrumentation key
az monitor app-insights component show \
  --app fitlife-insights \
  --resource-group fitlife-rg \
  --query instrumentationKey -o tsv
```

**Add to appsettings.json**:
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "<your-key>"
  }
}
```

### Prometheus & Grafana
```bash
# Install Prometheus
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack -n monitoring --create-namespace

# Access Grafana
kubectl port-forward svc/prometheus-grafana 3000:80 -n monitoring
# Default credentials: admin / prom-operator
```

---

## Rollback Procedures

### Kubernetes Rollback
```bash
# View rollout history
kubectl rollout history deployment/fitlife-api -n fitlife

# Rollback to previous version
kubectl rollout undo deployment/fitlife-api -n fitlife

# Rollback to specific revision
kubectl rollout undo deployment/fitlife-api --to-revision=3 -n fitlife

# Verify rollback
kubectl rollout status deployment/fitlife-api -n fitlife
```

### Database Rollback
```bash
# Rollback migration
dotnet ef database update PreviousMigrationName --project FitLife.Api

# Or restore from backup
az sql db restore \
  --resource-group fitlife-rg \
  --server fitlife-sql-server \
  --name FitLifeDB \
  --dest-name FitLifeDB-Restored \
  --time "2025-10-30T12:00:00Z"
```

---

## Troubleshooting

### Check pod logs
```bash
kubectl logs -f deployment/fitlife-api -n fitlife
kubectl logs -f deployment/fitlife-api -n fitlife --previous  # Previous crashed pod
```

### Check pod status
```bash
kubectl describe pod <pod-name> -n fitlife
kubectl get events -n fitlife --sort-by='.lastTimestamp'
```

### Test connectivity
```bash
kubectl run -it --rm debug --image=busybox --restart=Never -- sh
# Inside pod:
wget -O- http://fitlife-api-service:8080/health
```

---

This deployment guide provides everything needed to deploy FitLife to production on Azure with Kubernetes! 🚀
