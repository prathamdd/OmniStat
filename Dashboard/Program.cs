using Dashboard; 
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. ADD SERVICES (The Ingredients)
builder.Services.AddSignalR();
// This tells .NET to run your Redis listener in the background
builder.Services.AddHostedService<RedisSubscriberService>();

// Setup Redis Connection for the /scores route
var redis = ConnectionMultiplexer.Connect("127.0.0.1:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

var app = builder.Build();

// 2. CONFIGURE ROUTES (The Addresses)

// Old Reliable: Manual refresh route
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

// New Hotness: SignalR Hub address
app.MapHub<ScoreHub>("/scoreHub");

// Health Check
app.MapGet("/ping", () => new { status = "SignalR and Background Service are live!" });

app.Run();