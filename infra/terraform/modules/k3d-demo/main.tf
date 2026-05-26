terraform {
  required_version = ">= 1.5.0"
}

locals {
  orders_image   = "${var.orders_image_name}:${var.orders_image_tag}"
  shipping_image = "${var.shipping_image_name}:${var.shipping_image_tag}"

  script_dir = abspath("${var.repo_root}/infra/scripts")

  build_script_path   = "${local.script_dir}/build-demo-images.ps1"
  cluster_script_path = "${local.script_dir}/create-k3d-cluster.ps1"
  import_script_path  = "${local.script_dir}/import-k3d-images.ps1"
  deploy_script_path  = "${local.script_dir}/deploy-k8s-demo.ps1"
  destroy_script_path = "${local.script_dir}/destroy-k3d-demo.ps1"

  build_script_hash   = filesha1(local.build_script_path)
  cluster_script_hash = filesha1(local.cluster_script_path)
  import_script_hash  = filesha1(local.import_script_path)
  deploy_script_hash  = filesha1(local.deploy_script_path)
  destroy_script_hash = filesha1(local.destroy_script_path)

  shared_build_files = [
    "Directory.Packages.props",
    "NuGet.Config",
    "global.json",
  ]

  orders_source_files = sort(concat(
    local.shared_build_files,
    tolist(fileset(var.repo_root, "src/Orders.Api/**")),
  ))

  shipping_source_files = sort(concat(
    local.shared_build_files,
    tolist(fileset(var.repo_root, "src/Shipping.Api/**")),
  ))

  kubernetes_manifest_files = sort(concat(
    tolist(fileset(var.repo_root, "kubernetes/base/**")),
    tolist(fileset(var.repo_root, "kubernetes/overlays/local/**")),
  ))

  orders_source_hash = sha1(join("", [
    for relative_path in local.orders_source_files :
    filesha1("${var.repo_root}/${relative_path}")
  ]))

  shipping_source_hash = sha1(join("", [
    for relative_path in local.shipping_source_files :
    filesha1("${var.repo_root}/${relative_path}")
  ]))

  kubernetes_manifest_hash = sha1(join("", [
    for relative_path in local.kubernetes_manifest_files :
    filesha1("${var.repo_root}/${relative_path}")
  ]))

  k3d_config_hash = filesha1(var.k3d_config_path)
}

resource "terraform_data" "build_images" {
  triggers_replace = [
    local.orders_image,
    local.shipping_image,
    local.orders_source_hash,
    local.shipping_source_hash,
    local.build_script_hash,
    var.repo_root,
  ]

  input = {
    orders_image         = local.orders_image
    shipping_image       = local.shipping_image
    orders_source_hash   = local.orders_source_hash
    shipping_source_hash = local.shipping_source_hash
    repo_root            = var.repo_root
    build_script_hash    = local.build_script_hash
  }

  provisioner "local-exec" {
    interpreter = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command = join(" ", [
      "& '${local.build_script_path}'",
      "-RepoRoot '${var.repo_root}'",
      "-OrdersImage '${local.orders_image}'",
      "-ShippingImage '${local.shipping_image}'",
    ])
  }
}

resource "terraform_data" "k3d_cluster" {
  triggers_replace = [
    var.cluster_name,
    var.k3d_config_path,
    local.k3d_config_hash,
    local.cluster_script_hash,
    local.destroy_script_hash,
    var.namespace,
    var.kubernetes_overlay_path,
  ]

  input = {
    cluster_name        = var.cluster_name
    config_path         = var.k3d_config_path
    config_hash         = local.k3d_config_hash
    destroy_script_path = local.destroy_script_path
    destroy_script_hash = local.destroy_script_hash
    cluster_script_hash = local.cluster_script_hash
    namespace           = var.namespace
    overlay_path        = var.kubernetes_overlay_path
  }

  provisioner "local-exec" {
    interpreter = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command = join(" ", [
      "& '${local.cluster_script_path}'",
      "-ClusterName '${var.cluster_name}'",
      "-ConfigPath '${var.k3d_config_path}'",
    ])
  }

  provisioner "local-exec" {
    when        = destroy
    interpreter = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command = join(" ", [
      "& '${self.input.destroy_script_path}'",
      "-ClusterName '${self.input.cluster_name}'",
      "-OverlayPath '${self.input.overlay_path}'",
      "-Namespace '${self.input.namespace}'",
    ])
  }
}

resource "terraform_data" "import_images" {
  depends_on = [
    terraform_data.build_images,
    terraform_data.k3d_cluster,
  ]

  triggers_replace = [
    terraform_data.build_images.id,
    terraform_data.k3d_cluster.id,
    local.import_script_hash,
    local.orders_image,
    local.shipping_image,
    var.cluster_name,
  ]

  input = {
    cluster_name       = var.cluster_name
    orders_image       = local.orders_image
    shipping_image     = local.shipping_image
    build_images_id    = terraform_data.build_images.id
    k3d_cluster_id     = terraform_data.k3d_cluster.id
    import_script_hash = local.import_script_hash
  }

  provisioner "local-exec" {
    interpreter = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command = join(" ", [
      "& '${local.import_script_path}'",
      "-ClusterName '${var.cluster_name}'",
      "-OrdersImage '${local.orders_image}'",
      "-ShippingImage '${local.shipping_image}'",
    ])
  }
}

resource "terraform_data" "deploy_demo" {
  depends_on = [
    terraform_data.import_images,
  ]

  triggers_replace = [
    terraform_data.import_images.id,
    local.kubernetes_manifest_hash,
    local.deploy_script_hash,
    var.cluster_name,
    var.namespace,
    var.kubernetes_overlay_path,
  ]

  input = {
    cluster_name             = var.cluster_name
    namespace                = var.namespace
    kubernetes_overlay_path  = var.kubernetes_overlay_path
    kubernetes_manifest_hash = local.kubernetes_manifest_hash
    import_images_id         = terraform_data.import_images.id
    deploy_script_hash       = local.deploy_script_hash
  }

  provisioner "local-exec" {
    interpreter = ["PowerShell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command = join(" ", [
      "& '${local.deploy_script_path}'",
      "-ClusterName '${var.cluster_name}'",
      "-OverlayPath '${var.kubernetes_overlay_path}'",
      "-Namespace '${var.namespace}'",
    ])
  }
}
