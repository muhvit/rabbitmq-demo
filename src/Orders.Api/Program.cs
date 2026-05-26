using Orders.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IOrderPublisher, RabbitMqPublisher>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();
