using ClockNode;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<SimulatedClock>();

var app = builder.Build();
app.MapControllers();
app.Run();