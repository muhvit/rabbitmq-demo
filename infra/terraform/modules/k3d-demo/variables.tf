variable "cluster_name" {
  description = "Name of the local K3d cluster."
  type        = string
  default     = "rabbitmq-demo"
}

variable "namespace" {
  description = "Kubernetes namespace that hosts the demo workload."
  type        = string
  default     = "rabbitmq-demo"
}

variable "repo_root" {
  description = "Absolute path to the repository root."
  type        = string
}

variable "k3d_config_path" {
  description = "Absolute path to the K3d cluster configuration file."
  type        = string
}

variable "kubernetes_overlay_path" {
  description = "Absolute path to the Kubernetes overlay applied to the cluster."
  type        = string
}

variable "orders_image_name" {
  description = "Docker image name for Orders API."
  type        = string
  default     = "rabbitmq-demo/orders-api"
}

variable "orders_image_tag" {
  description = "Docker image tag for Orders API."
  type        = string
  default     = "dev"
}

variable "shipping_image_name" {
  description = "Docker image name for Shipping API."
  type        = string
  default     = "rabbitmq-demo/shipping-api"
}

variable "shipping_image_tag" {
  description = "Docker image tag for Shipping API."
  type        = string
  default     = "dev"
}
