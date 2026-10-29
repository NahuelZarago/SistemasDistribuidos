var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<LamportClock>();
builder.Services.AddHttpClient("inventory", client =>
    client.BaseAddress = new Uri(builder.Configuration["InventoryUrl"] ?? "http://inventory-service:8080"));
builder.Services.AddHttpClient("payments", client =>
    client.BaseAddress = new Uri(builder.Configuration["PaymentsUrl"] ?? "http://payments-service:8080"));
var app = builder.Build();
app.MapControllers();
app.Run();