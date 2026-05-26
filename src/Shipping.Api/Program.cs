using Shipping.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<ShipmentStore>();
builder.Services.AddHostedService<ShippingConsumerService>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();
