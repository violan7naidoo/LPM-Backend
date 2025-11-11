using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure pipeline
app.UseCors();
app.MapControllers();

// Simple RNG endpoints for local development
app.MapGet("/rng", (int? min, int? max, string? gameId, string? roundId) =>
{
    var random = new Random();
    var minValue = min ?? 0;
    var maxValue = max ?? int.MaxValue;
    var result = random.Next(minValue, maxValue);
    
    return Results.Ok(new { random = result });
})
.WithName("GetRandomNumber");

app.MapPost("/pools", (PoolsRequest request) =>
{
    var random = new Random();
    var pools = new Dictionary<string, List<int>>();
    
    foreach (var pool in request.Pools)
    {
        var numbers = new List<int>();
        for (int i = 0; i < pool.Value.Size; i++)
        {
            numbers.Add(random.Next(pool.Value.Min, pool.Value.Max + 1));
        }
        pools[pool.Key] = numbers;
    }
    
    return Results.Ok(new { pools });
})
.WithName("GetRandomPools");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck");

app.Run();

// Models
public class PoolsRequest
{
    public Dictionary<string, PoolRequest> Pools { get; set; } = new();
    public string? GameId { get; set; }
    public string? RoundId { get; set; }
}

public class PoolRequest
{
    public int Min { get; set; }
    public int Max { get; set; }
    public int Size { get; set; } = 1;
}
