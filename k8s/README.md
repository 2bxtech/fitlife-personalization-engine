# Kubernetes Deployment Reference

These manifests are configured architecture assets and have not been verified
in a live AKS environment. The portfolio demo's default hosted target is a
simpler managed application platform; use this directory to evaluate or adapt
the Kubernetes design, not as evidence of a current deployment.

## Prerequisites

The application depends on external backing services provisioned before the
application workloads.

| Service | Kubernetes or managed option | Configuration |
|---|---|---|
| SQL Server | StatefulSet or Azure SQL | `fitlife-secrets` |
| Redis | StatefulSet or Azure Managed Redis | `fitlife-config` |
| Kafka | StatefulSet or Azure Event Hubs Kafka endpoint | `fitlife-config` |

For a live Azure environment, prefer managed services over stateful workloads
inside the application cluster.

## Illustrative deployment order

```bash
kubectl apply -f namespace.yaml

cp secrets.yaml.template secrets.yaml
# Replace every placeholder; never commit secrets.yaml.
kubectl apply -f secrets.yaml

kubectl apply -f configmap.yaml
kubectl apply -f api-deployment.yaml
kubectl apply -f web-deployment.yaml
kubectl apply -f ingress.yaml
kubectl apply -f hpa.yaml
```

Replace `<ACR_REGISTRY>`, example hosts, service endpoints, and secret
placeholders before applying the manifests.

## Secrets

`secrets.yaml` is ignored and must never be committed. For a real environment,
prefer workload identity and an external secret store over long-lived values in
a local manifest.

Required secret inputs currently include:

- SQL connection string;
- JWT signing secret;
- any managed Redis credentials required by the selected service.

## Database migrations

The API currently applies EF Core migrations at startup. That behavior is
convenient locally but is not the intended production topology when several API
replicas start concurrently.

Before using these manifests in a live environment:

1. move migration execution to one controlled deployment job or release step;
2. use an immutable image containing the required migration mechanism;
3. back up the database and document schema rollback limitations;
4. start or route traffic to API replicas only after migration succeeds.

## Health checks

- API liveness: `GET /health/live` on port 8080 checks only that the process is
  running.
- API readiness: `GET /health/ready` on port 8080 checks database and Redis
  connectivity.
- API compatibility: `GET /health` remains an alias for readiness.
- Web: `GET /health` on port 80 returns a static nginx health response.

The configured liveness probe is dependency-independent, so a transient
database or Redis failure removes the API from service through readiness
without restarting a healthy process.

## Troubleshooting

```bash
kubectl get pods -n fitlife
kubectl logs -l app=fitlife-api -n fitlife --tail=50
kubectl describe pod <pod-name> -n fitlife
kubectl run curl --rm -it --restart=Never --image=curlimages/curl \
  -n fitlife -- curl http://fitlife-api-service/health
```
