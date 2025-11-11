using SnowKingdomBackendAPI.ApiService.Services;
using SnowKingdomBackendAPI.RGS.Controllers;
using SnowKingdomBackendAPI.RGS.Services;

namespace SnowKingdomBackendAPI.RGS;

public class BackendOptions
{
    public string Url { get; set; } = "http://localhost:5001/play";
}

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Add controllers with explicit configuration
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(GameController).Assembly);

        // Add SessionService
        builder.Services.AddSingleton<SessionService>();

        // Add HttpClient for backend communication
        builder.Services.AddHttpClient();

        // Add RNG service
        builder.Services.AddHttpClient<RngService>();

        // Add configuration for backend URL
        builder.Services.Configure<BackendOptions>(builder.Configuration.GetSection("Backend"));

        // Add HTTP logging middleware
        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
            options.RequestBodyLogLimit = 64 * 1024;
            options.ResponseBodyLogLimit = 64 * 1024;
        });

        // Add CORS policy to allow the frontend to call the RGS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(
                        "http://localhost:3000", // Lobby
                        "http://localhost:3001", // FrontEnd (Game 1)
                        "http://localhost:3002", // FrontEnd5x3 (Game 2)
                        "http://localhost:3003", // frontEndDamian (Inferno-Empress)
                        "http://localhost:3004", // FrontEndRicky (Reign Of Thunder)
                        "http://127.0.0.1:3000",
                        "http://127.0.0.1:3001",
                        "http://127.0.0.1:3002",
                        "http://127.0.0.1:3003",
                        "http://127.0.0.1:3004"
                    )
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        // Enable HTTP logging
        app.UseHttpLogging();

        // Enable CORS
        app.UseCors();

        // Map controllers (MapControllers automatically handles routing)
        app.MapControllers();

        // Map default endpoints (health checks, etc.)
        app.MapDefaultEndpoints();

        // Add health check endpoint
        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
           .WithName("HealthCheck");

        app.Run();
    }
}