# rabbitmq-demo

Demonstrator project featuring two ASP.NET Core 10 services that exchange messages through RabbitMQ. The repository now supports two local demo modes:

- a minimal `docker compose` setup that builds both APIs locally and runs everything on one Docker host
- a `k3d` Kubernetes demo bootstrapped with `terraform` and `terragrunt`

## Outcome

After setup, the system behaves like this:

1. `Orders.Api` accepts a REST call on `POST /api/orders`.
2. `Orders.Api` publishes an `OrderSubmittedMessage` to RabbitMQ exchange `orders.exchange`.
3. RabbitMQ routes the message to queue `shipping.orders`.
4. `Shipping.Api` consumes the message in a background worker.
5. `Shipping.Api` exposes the processed result through `GET /api/shipments` and `GET /api/shipments/{orderId}`.

## Architecture

- `Orders.Api`
  Accepts HTTP requests and publishes messages to RabbitMQ.
- `Shipping.Api`
  Consumes RabbitMQ messages and exposes the processed state over HTTP.
- `RabbitMQ`
  Hosts the durable exchange and queue used by both services.
- `Demo.SmokeHarness`
  Posts a sample order and polls `Shipping.Api` until the queued message has been processed.

Message topology:

- Exchange: `orders.exchange`
- Queue: `shipping.orders`
- Routing key: `orders.submitted`

## Repository Layout

- `compose.yaml`
  Minimal Docker Compose demo.
- `src/Orders.Api`
  Publisher API.
- `src/Shipping.Api`
  Consumer API.
- `tests/Demo.SmokeHarness`
  End-to-end verification harness.
- `infra/k3d/rabbitmq-demo.yaml`
  Local cluster definition.
- `infra/terraform/modules/k3d-demo`
  Terraform module that builds images, creates the K3d cluster, imports the images, and deploys the manifests.
- `infra/live/dev/terragrunt.hcl`
  Terragrunt entrypoint for the local development environment.
- `kubernetes/base`
  Base Kubernetes manifests.
- `kubernetes/overlays/local`
  Local image overlay for the demo cluster.

## Prerequisites

### For the compose demo

- .NET SDK `10.0.300` or newer in the `10.0.x` feature band
- Docker Desktop or another local Docker engine with Compose support

### For the k3d demo

- .NET SDK `10.0.300` or newer in the `10.0.x` feature band
- Docker Desktop or another local Docker engine usable by `k3d`
- `kubectl`
- `k3d`
- `terraform`
- `terragrunt`

## Local Build

Build the projects individually from the repository root:

```text
dotnet restore src/Orders.Api/Orders.Api.csproj
dotnet restore src/Shipping.Api/Shipping.Api.csproj
dotnet restore tests/Demo.SmokeHarness/Demo.SmokeHarness.csproj

dotnet build src/Orders.Api/Orders.Api.csproj
dotnet build src/Shipping.Api/Shipping.Api.csproj
dotnet build tests/Demo.SmokeHarness/Demo.SmokeHarness.csproj
```

## Compose Demo

Start RabbitMQ plus both APIs with local image builds:

```text
docker compose up --build -d
```

This starts:

- `rabbitmq` on AMQP port `5672`
- RabbitMQ management UI on `http://localhost:15672`
- `Orders.Api` on `http://localhost:8081`
- `Shipping.Api` on `http://localhost:8082`

### Verify the compose flow

Run the smoke harness manually against the compose endpoints:

```text
dotnet run --project tests/Demo.SmokeHarness/Demo.SmokeHarness.csproj -- ^
  --orders-url http://localhost:8081 ^
  --shipping-url http://localhost:8082
```

Expected behavior:

- the harness posts a new order to `Orders.Api`
- RabbitMQ accepts and routes the message
- `Shipping.Api` eventually returns the processed shipment

You can also verify manually:

```text
curl -X POST http://localhost:8081/api/orders ^
  -H "Content-Type: application/json" ^
  -d "{\"customerId\":\"demo-customer\",\"sku\":\"bike-helmet\",\"quantity\":2}"

curl http://localhost:8082/api/shipments
```

RabbitMQ management UI:

- URL: `http://localhost:15672`
- User: `demo`
- Password: `demo-password`

Inside RabbitMQ you should see:

- exchange `orders.exchange`
- queue `shipping.orders`
- binding with routing key `orders.submitted`

### Compose maintenance

Rebuild and restart after code changes:

```text
docker compose up --build -d
```

Inspect runtime state:

```text
docker compose ps
docker compose logs rabbitmq
docker compose logs orders-api
docker compose logs shipping-api
```

Stop the compose demo:

```text
docker compose down
```

Remove the compose demo including persisted RabbitMQ data:

```text
docker compose down -v
```

## k3d Demo

The Kubernetes demo is now managed by Terragrunt on top of Terraform. A single apply will:

- build the local `orders-api` and `shipping-api` Docker images
- create the K3d cluster defined in `infra/k3d/rabbitmq-demo.yaml`
- import the images into that cluster
- apply `kubernetes/overlays/local` and wait for all three deployments to roll out

Create or update the dev environment:

```text
terragrunt apply --working-dir infra/live/dev
```

The cluster maps host port `8080` to the in-cluster ingress controller.

Ingress endpoints:

- `http://orders.localtest.me:8080`
- `http://shipping.localtest.me:8080`
- `http://rabbitmq.localtest.me:8080`

`localtest.me` resolves to `127.0.0.1`, so no hosts file changes are required.

Run the smoke harness:

```text
dotnet run --project tests/Demo.SmokeHarness/Demo.SmokeHarness.csproj
```

RabbitMQ management UI:

- URL: `http://rabbitmq.localtest.me:8080`
- User: `demo`
- Password: `demo-password`

### k3d maintenance

Reconcile the environment after application or manifest changes:

```text
terragrunt apply --working-dir infra/live/dev
```

Useful day-to-day commands:

```text
kubectl get all -n rabbitmq-demo
kubectl logs deployment/orders-api -n rabbitmq-demo
kubectl logs deployment/shipping-api -n rabbitmq-demo
kubectl logs deployment/rabbitmq -n rabbitmq-demo
kubectl describe ingress rabbitmq-demo -n rabbitmq-demo
terragrunt output --working-dir infra/live/dev
```

Remove the workload and cluster:

```text
terragrunt destroy --working-dir infra/live/dev
```

## Known Limits

- `Shipping.Api` stores processed shipments in memory, so a restart clears the HTTP-visible history.
- The demo uses plain credentials for clarity.
- This repository is optimized for local demonstration and operator understanding, not production hardening.
