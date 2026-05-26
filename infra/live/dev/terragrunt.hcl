include "root" {
  path = find_in_parent_folders("root.hcl")
}

terraform {
  source = "../../terraform/modules/k3d-demo"
}

inputs = {
  cluster_name            = "rabbitmq-demo"
  k3d_config_path         = "${get_parent_terragrunt_dir()}/k3d/rabbitmq-demo.yaml"
  kubernetes_overlay_path = "${get_parent_terragrunt_dir()}/../kubernetes/overlays/local"
  repo_root               = "${get_parent_terragrunt_dir()}/.."
}
