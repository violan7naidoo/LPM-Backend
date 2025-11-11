var builder = DistributedApplication.CreateBuilder(args);

// Aspire automatically reads the ports from the C# projects' launchSettings.json
var gameEngine = builder.AddProject<Projects.SnowKingdomBackendAPI_ApiService>("apiservice");
var rgsService = builder.AddProject<Projects.SnowKingdomBackendAPI_RGS>("rgsservice")
                        .WithReference(gameEngine);

// Assign the Frontend to a fixed, stable port: 3000
builder.AddNpmApp("frontend", "../../../FrontEnd", "dev")
       .WithReference(rgsService)
       .WithHttpEndpoint(port: 3000, env: "PORT"); 

builder.Build().Run();