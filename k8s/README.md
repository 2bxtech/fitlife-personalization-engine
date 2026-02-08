# Kubernetes Deployment Guide

## Prerequisites

The FitLife application depends on external backing services that must be provisioned **before** deploying the application pods.

### Required Backing Services

| Service     | K8s Resource                   | Notes                                               |
| ----------- | ------------------------------ | --------------------------------------------------- |
| SQL Server  | StatefulSet or Azure SQL       | Connection string in `fitlife-secrets`               |
| Redis       | StatefulSet or Azure Cache     | Connection string in `fitlife-config` ConfigMap      |
| Kafka       | StatefulSet or Azure Event Hub | Bootstrap servers in `fitlife-config` ConfigMap      |

### Recommended Approach (Azure Managed)

For production, use Azure managed services instead of self-hosted StatefulSets:

- **Azure SQL Database** — Fully managed, automatic backups, geo-replication
- **Azure Cache for Redis** — Managed Redis with TLS, clustering support
- **Azure Event Hubs** — Kafka-compatible managed event streaming

## Deployment Order

```bash
# 1. Create namespace
kubectl apply -f namespace.yaml

# 2. Create secrets (copy template first!)
cp secrets.yaml.template secrets.yaml
# Edit secrets.yaml with real values
kubectl apply -f secrets.yaml

# 3. Create config
kubectl apply -f configmap.yaml

# 4. Deploy application
kubectl apply -f api-deployment.yaml
kubectl apply -f web-deployment.yaml

# 5. Configure networking
kubectl apply -f ingress.yaml

# 6. Enable autoscaling
kubectl apply -f hpa.yaml
```

## Secrets Setup

```bash
# NEVER commit secrets.yaml — it's in .gitignore
cp secrets.yaml.template secrets.yaml

# Replace placeholder values:
#   <REPLACE_WITH_SQL_CONNECTION_STRING>   → Your SQL Server connection string
#   <REPLACE_WITH_256_BIT_SECRET_KEY>      → openssl rand -base64 32
#   <REPLACE_WITH_REDIS_PASSWORD>          → Your Redis password

kubectl apply -f secrets.yaml -n fitlife
```

## Database Migrations

Before deploying a new API version, run EF Core migrations:

```bash
# Option 1: Init container (recommended for CI/CD)
# Add to api-deployment.yaml:
#   initContainers:
#   - name: migrate
#     image: <ACR_REGISTRY>/fitlife-api:latest
#     command: ["dotnet", "ef", "database", "update"]

# Option 2: Manual migration
kubectl run migrate --rm -it --restart=Never \
  --image=<ACR_REGISTRY>/fitlife-api:latest \
  -n fitlife \
  --env="ConnectionStrings__DefaultConnection=<CONNECTION_STRING>" \
  -- dotnet ef database update
```

## Health Checks

- **API**: `GET /health` on port 8080 (checks database + Redis connectivity)
- **Web**: `GET /health` on port 80 (nginx returns 200 "healthy")

## Troubleshooting

```bash
# Check pod status
kubectl get pods -n fitlife

# View API logs
kubectl logs -l app=fitlife-api -n fitlife --tail=50

# Check events for failed pods
kubectl describe pod <pod-name> -n fitlife

# Test API inside cluster
kubectl run curl --rm -it --restart=Never --image=curlimages/curl \
  -n fitlife -- curl http://fitlife-api-service/health
```
