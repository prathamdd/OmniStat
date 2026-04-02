using Dashboard; 
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.WithOrigins("null", "http://localhost:5500", "http://127.0.0.1:5500") // Covers Live Server
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// 1. ADD SERVICES (The Ingredients)
builder.Services.AddSignalR();
builder.Services.AddControllers();

var host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect($"{host}:6379"));

builder.Services.AddHostedService<RedisSubscriberService>();

var app = builder.Build();

// 2. CONFIGURE ROUTES (The Addresses)

app.MapGet("/scores", async (IConnectionMultiplexer redisConn) => {
    var db = redisConn.GetDatabase();
    try 
    {
        string? scoreData = await db.StringGetAsync("latest_nba_score");
        return string.IsNullOrEmpty(scoreData) 
            ? Results.Json(new { message = "Redis key is empty." }) 
            : Results.Content(scoreData, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = "Connection Error", detail = ex.Message });
    }
});

app.UseCors();
// New Hotness: SignalR Hub address
app.MapHub<ScoreHub>("/scoreHub");
app.MapControllers();

// Health Check
app.MapGet("/ping", () => new { status = "SignalR and Background Service are live!" });

app.Run();