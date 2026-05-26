download_dir = "${get_env("TEMP", "C:/Temp")}/rabbitmq-demo-terragrunt-cache"

inputs = {
  namespace           = "rabbitmq-demo"
  orders_image_name   = "rabbitmq-demo/orders-api"
  orders_image_tag    = "dev"
  shipping_image_name = "rabbitmq-demo/shipping-api"
  shipping_image_tag  = "dev"
}
