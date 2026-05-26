output "cluster_name" {
  description = "The K3d cluster name."
  value       = var.cluster_name
}

output "kubectl_context" {
  description = "The kubectl context configured for the local cluster."
  value       = "k3d-${var.cluster_name}"
}

output "orders_url" {
  description = "Orders API ingress URL."
  value       = "http://orders.localtest.me:8080"
}

output "shipping_url" {
  description = "Shipping API ingress URL."
  value       = "http://shipping.localtest.me:8080"
}

output "rabbitmq_management_url" {
  description = "RabbitMQ management UI URL."
  value       = "http://rabbitmq.localtest.me:8080"
}
