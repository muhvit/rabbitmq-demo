# rabbitmq-demo

Demonstrator project featuring two ASP.NET Core 10 Web APIs that exchange messages through RabbitMQ inside the same `k3d` Kubernetes cluster.

## Outcome

After setup, the system behaves like this:

1. `Orders.Api` accepts a REST call on `POST /api/orders`.
2. `Orders.Api` publishes an `OrderSubmittedMessage` to RabbitMQ exchange `orders.exchange`.
3. RabbitMQ routes the message to queue `shipping.orders`.
4. `Shipping.Api` consumes the message in a background worker.
5. `Shipping.Api` exposes the processed result through `GET /api/shipments` and `GET /api/shipments/{orderId}`.

Everything runs in one local Kubernetes cluster managed by `k3d`, with ingress routes exposed on port `8080`.

## Architecture

- `Orders.Api`
  Accepts HTTP requests and publishes messages to RabbitMQ.
- `Shipping.Api`
  Consumes RabbitMQ messages and exposes the processed state over HTTP.
- `RabbitMQ`
  Runs in-cluster and hosts the durable exchange and queue used by both services.
- `Demo.SmokeHarness`
  Posts a sample order and polls `Shipping.Api` until the queued message has been processed.

Message topology:

- Exchange: `orders.exchange`
- Queue: `shipping.orders`
- Routing key: `orders.submitted`

Ingress endpoints:

- `http://orders.localtest.me:8080`
- `http://shipping.localtest.me:8080`
- `http://rabbitmq.localtest.me:8080`

`localtest.me` resolves to `127.0.0.1`, so no hosts file changes are required.

## Repository Layout

- `src/Orders.Api`
  Publisher API.
- `src/Shipping.Api`
  Consumer API.
- `tests/Demo.SmokeHarness`
  End-to-end verification harness.
- `infra/k3d/rabbitmq-demo.yaml`
  Local cluster definition.
- `kubernetes/base`
  Base Kubernetes manifests.
- `kubernetes/overlays/local`
  Local image overlay for the demo cluster.

## Prerequisites

- .NET SDK `10.0.300` or newer in the `10.0.x` feature band
- Docker Desktop or another local Docker engine usable by `k3d`
- `kubectl`
- `k3d`

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

## Build Container Images

Build both web APIs as local Docker images:

```text
docker build -t rabbitmq-demo/orders-api:dev -f src/Orders.Api/Dockerfile .
docker build -t rabbitmq-demo/shipping-api:dev -f src/Shipping.Api/Dockerfile .
```

## Create the k3d Cluster

Create the local Kubernetes cluster defined in `infra/k3d/rabbitmq-demo.yaml`:

```text
k3d cluster create --config infra/k3d/rabbitmq-demo.yaml
kubectl cluster-info
kubectl config current-context
```

The cluster maps host port `8080` to the in-cluster ingress controller.

## Import Images into k3d

Load the locally built images into the cluster:

```text
k3d image import rabbitmq-demo/orders-api:dev -c rabbitmq-demo
k3d image import rabbitmq-demo/shipping-api:dev -c rabbitmq-demo
```

## Deploy RabbitMQ and Both APIs

Apply the local overlay:

```text
kubectl apply -k kubernetes/overlays/local
kubectl rollout status deployment/rabbitmq -n rabbitmq-demo
kubectl rollout status deployment/orders-api -n rabbitmq-demo
kubectl rollout status deployment/shipping-api -n rabbitmq-demo
kubectl get pods,svc,ingress -n rabbitmq-demo
```

This deploys:

- `rabbitmq` with the management plugin enabled
- `orders-api`
- `shipping-api`
- an ingress with three hostnames

## Verify the End-to-End Flow

Run the smoke harness:

```text
dotnet run --project tests/Demo.SmokeHarness/Demo.SmokeHarness.csproj
```

Expected behavior:

- the harness posts a new order to `Orders.Api`
- RabbitMQ accepts and routes the message
- `Shipping.Api` eventually returns the processed shipment

You can also verify manually:

```text
curl -X POST http://orders.localtest.me:8080/api/orders ^
  -H "Content-Type: application/json" ^
  -d "{\"customerId\":\"demo-customer\",\"sku\":\"bike-helmet\",\"quantity\":2}"

curl http://shipping.localtest.me:8080/api/shipments
```

RabbitMQ management UI:

- URL: `http://rabbitmq.localtest.me:8080`
- User: `demo`
- Password: `demo-password`

Inside RabbitMQ you should see:

- exchange `orders.exchange`
- queue `shipping.orders`
- binding with routing key `orders.submitted`

## Maintenance Notes

### Rebuild after code changes

When either API changes:

```text
docker build -t rabbitmq-demo/orders-api:dev -f src/Orders.Api/Dockerfile .
docker build -t rabbitmq-demo/shipping-api:dev -f src/Shipping.Api/Dockerfile .

k3d image import rabbitmq-demo/orders-api:dev -c rabbitmq-demo
k3d image import rabbitmq-demo/shipping-api:dev -c rabbitmq-demo

kubectl rollout restart deployment/orders-api -n rabbitmq-demo
kubectl rollout restart deployment/shipping-api -n rabbitmq-demo
kubectl rollout status deployment/orders-api -n rabbitmq-demo
kubectl rollout status deployment/shipping-api -n rabbitmq-demo
```

### Inspect runtime state

Useful day-to-day commands:

```text
kubectl get all -n rabbitmq-demo
kubectl logs deployment/orders-api -n rabbitmq-demo
kubectl logs deployment/shipping-api -n rabbitmq-demo
kubectl logs deployment/rabbitmq -n rabbitmq-demo
kubectl describe ingress rabbitmq-demo -n rabbitmq-demo
```

### Rotate demo credentials

Credentials live in `kubernetes/base/secret.yaml`. After updating them:

1. Apply the manifests again with `kubectl apply -k kubernetes/overlays/local`.
2. Restart `rabbitmq`, `orders-api`, and `shipping-api`.
3. Confirm both APIs reconnect successfully.

### Storage and durability

- RabbitMQ uses a persistent volume claim named `rabbitmq-data`.
- The queue is durable.
- `Shipping.Api` keeps its read model in memory only.

The in-memory shipment store is acceptable for a demonstrator, but if you need durable consumer-side state or multiple `Shipping.Api` replicas with a shared view, add a database or another persistent store.

## Cleanup

Remove the workload:

```text
kubectl delete -k kubernetes/overlays/local
k3d cluster delete rabbitmq-demo
```

## Known Limits

- `Shipping.Api` stores processed shipments in memory, so a pod restart clears the HTTP-visible history.
- The manifests use plain demo credentials for clarity.
- This repository is optimized for local demonstration and operator understanding, not production hardening.
