var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<LamportClock>();
var app = builder.Build();
app.MapControllers();
app.Run();