using Microsoft.EntityFrameworkCore;
using SnowKingdomBackendAPI.ApiService.Data;
using SnowKingdomBackendAPI.ApiService.Game;
using SnowKingdomBackendAPI.ApiService.Models;
using SnowKingdomBackendAPI.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<GameConfigService>();
builder.Services.AddSingleton<SessionService>(sp => new SessionService(sp));

// Add optional RGS service (non-blocking)
builder.Services.AddHttpClient<OptionalRgsService>();
builder.Services.AddSingleton<OptionalRgsService>();

// Database configuration for slot game data persistence
// Database is stored at C:\OnlineGameData\onlinegame.db
var dbPath = @"C:\OnlineGameData\onlinegame.db";
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register GameDataService for persistence
builder.Services.AddScoped<GameDataService>();

// Configure JSON options to use camelCase for API responses
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Add HTTP logging middleware
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    options.RequestBodyLogLimit = 64 * 1024;
    options.ResponseBodyLogLimit = 64 * 1024;
});

// Add CORS policy to allow frontend communication
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
                "http://localhost:3005", // FrontEndBookOfRa
                "https://localhost:3000",
                "https://localhost:3001",
                "https://localhost:3002",
                "https://localhost:3003",
                "https://localhost:3004",
                "https://localhost:3005",
                "http://localhost:9003",
                "https://localhost:9003",
                "http://localhost:5073",
                "https://localhost:5073",
                "http://localhost:59775",
                "https://localhost:59775",
                "http://localhost:52436",
                "https://localhost:52436",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:3001",
                "http://127.0.0.1:3002",
                "http://127.0.0.1:3003",
                "http://127.0.0.1:3004",
                "http://127.0.0.1:3005",
                "http://127.0.0.1:9003",
                "http://127.0.0.1:5073",
                "http://127.0.0.1:59775",
                "http://127.0.0.1:52436"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Enable HTTP logging
app.UseHttpLogging();

// Enable CORS
app.UseCors();

app.MapPost("/play", async (PlayRequest request, GameConfigService configService, SessionService sessionService, GameDataService dataService, OptionalRgsService? optionalRgsService) =>
{
    try
    {
        // Get or create session
        var currentState = sessionService.GetOrCreateSession(request.SessionId);

        // Get gameId: prefer from request (RGS route), then from session, then default
        var session = await sessionService.GetSessionAsync(request.SessionId);
        var gameId = request.GameId ?? session?.GameId ?? "SnowKingdom";
        
        // Update session's GameId if it was provided in the request and differs
        if (!string.IsNullOrEmpty(request.GameId) && session != null && session.GameId != request.GameId)
        {
            session.GameId = request.GameId;
            await sessionService.UpdateSessionAsync(session);
        }

        // Load game configuration
        var gameConfig = configService.LoadGameConfig(gameId);

        // Create game engine instance with loaded config
        var gameEngine = new GameEngine(gameConfig);

        // Check if this is an action game spin (no balance deduction)
        var isActionGameSpin = request.ActionGameSpins > 0;
        var isFreeSpin = currentState.FreeSpinsRemaining > 0;

        // Calculate bet amount: if numPaylines and betPerPayline are provided, use that; otherwise use BetAmount
        // For bet-specific payouts, we need the TOTAL bet amount (R1, R2, R3, or R5)
        // Prefer BetAmount (total bet) if provided, otherwise calculate from numPaylines * betPerPayline
        // During free spins, we need to use the original bet amount from the session if available
        var totalBet = request.BetAmount > 0 
            ? request.BetAmount 
            : (request.NumPaylines > 0 && request.BetPerPayline > 0
                ? request.NumPaylines * request.BetPerPayline
                : 0);
        
        // If in free spins and totalBet is 0 or invalid, try to get it from the last response in the session
        // This ensures we use the correct bet amount for payout calculations during free spins
        if (isFreeSpin && totalBet <= 0 && currentState.Results != null)
        {
            // Try to infer from the last response - but we don't store it, so fall back to a default
            // For now, use the first bet amount from config as a fallback
            if (gameConfig.BetAmounts != null && gameConfig.BetAmounts.Length > 0)
            {
                totalBet = gameConfig.BetAmounts[0]; // Default to R1 if we can't determine
                Console.WriteLine($"[BET DEBUG] Free spin with invalid totalBet, using fallback: {totalBet}");
            }
        }
        
        // Normalize totalBet to match one of the configured bet amounts (R1, R2, R3, R5)
        // This ensures the bet key lookup will work correctly
        if (totalBet > 0 && gameConfig.BetAmounts != null && gameConfig.BetAmounts.Length > 0)
        {
            // Find the closest matching bet amount
            var closestBet = gameConfig.BetAmounts.OrderBy(b => Math.Abs(b - totalBet)).First();
            // Only use it if it's very close (within 0.01 to handle floating point issues)
            if (Math.Abs(closestBet - totalBet) < 0.01m)
            {
                totalBet = closestBet;
                Console.WriteLine($"[BET DEBUG] Normalized totalBet to configured bet amount: {totalBet}");
            }
            else
            {
                Console.WriteLine($"[BET DEBUG] Warning: totalBet {totalBet} does not match any configured bet amount. Closest: {closestBet}");
            }
        }
        
        // Ensure totalBet is set correctly - it should be one of the configured bet amounts
        if (totalBet <= 0)
        {
            return Results.BadRequest(new { Error = "Invalid bet amount" });
        }
        
        Console.WriteLine($"[BET DEBUG] TotalBet: {totalBet}, BetAmount: {request.BetAmount}, NumPaylines: {request.NumPaylines}, BetPerPayline: {request.BetPerPayline}, IsFreeSpin: {isFreeSpin}");

        // Check if player has enough balance (unless action game or free spin)
        if (!isActionGameSpin && !isFreeSpin && currentState.Balance < totalBet)
        {
            return Results.BadRequest(new
            {
                Error = "Insufficient balance",
                CurrentBalance = currentState.Balance,
                FreeSpinsRemaining = currentState.FreeSpinsRemaining,
                ActionGameSpins = currentState.ActionGameSpins
            });
        }

        // Get feature symbol from session if in free spins
        var featureSymbol = currentState.FeatureSymbol;
        if (isFreeSpin && string.IsNullOrEmpty(featureSymbol))
        {
            // Should have been set when free spins started, but set it now if missing
            featureSymbol = gameEngine.SelectFeatureSymbol();
        }

        // Generate new grid
        var grid = gameEngine.GenerateGrid();

        // Evaluate the spin
        // Note: betAmount parameter is per-payline for legacy compatibility, but totalBet is used for bet-specific payout lookup
        var numPaylines = request.NumPaylines > 0 ? request.NumPaylines : 0; // 0 means use all paylines
        var betPerPayline = request.NumPaylines > 0 && request.BetPerPayline > 0 ? request.BetPerPayline : (totalBet / (numPaylines > 0 ? numPaylines : gameConfig.MaxPaylines));
        var spinResult = gameEngine.EvaluateSpin(grid, betPerPayline, numPaylines, isFreeSpin, featureSymbol, totalBet);

        // Update player state
        var newBalance = currentState.Balance;
        var newFreeSpins = currentState.FreeSpinsRemaining;
        var newActionGameSpins = currentState.ActionGameSpins;

        if (isActionGameSpin)
        {
            // Action game spin: deduct from action game spins, not balance
            newActionGameSpins = Math.Max(0, request.ActionGameSpins - 1);
        }
        else if (isFreeSpin)
        {
            // Using free spin - decrement FIRST before checking for retrigger
            newFreeSpins = Math.Max(0, currentState.FreeSpinsRemaining - 1);
            Console.WriteLine($"[FREE SPINS DEBUG] Decremented free spins: {currentState.FreeSpinsRemaining} -> {newFreeSpins}");
        }
        else
        {
            // Deduct bet amount
            newBalance = currentState.Balance - totalBet;
        }

        // Add winnings
        // Base game wins (excluding feature symbol wins in free spins)
        newBalance += spinResult.TotalWin;
        Console.WriteLine($"[WIN CALCULATION] Base game win: R{spinResult.TotalWin}");
        
        // Feature game wins (only in free spins when 3+ reels expand)
        if (spinResult.ExpandedWin > 0)
        {
            newBalance += spinResult.ExpandedWin;
            Console.WriteLine($"[WIN CALCULATION] Feature game win: R{spinResult.ExpandedWin}");
        }
        
        Console.WriteLine($"[WIN CALCULATION] Total win: R{spinResult.TotalWin + spinResult.ExpandedWin}");

        // Add free spins if triggered
        // Can trigger in base game OR retrigger during free spins (if feature symbol is NOT scatter)
        var freeSpinsAwarded = 0;
        string? selectedFeatureSymbol = null;
        if (spinResult.ScatterWin.TriggeredFreeSpins)
        {
            freeSpinsAwarded = gameConfig.FreeSpinsAwarded;
            newFreeSpins += freeSpinsAwarded;
            Console.WriteLine($"[FREE SPINS DEBUG] Retriggered! Added {freeSpinsAwarded} free spins. New total: {newFreeSpins}");
            
            // Only select new feature symbol if starting free spins (not retriggering)
            if (!isFreeSpin)
            {
                // Select feature symbol for free spins
                selectedFeatureSymbol = gameEngine.SelectFeatureSymbol();
                spinResult.FeatureSymbol = selectedFeatureSymbol;
            }
            // If retriggering during free spins, keep the existing feature symbol
        }

        // Add action game spins if triggered
        if (spinResult.ActionGameTriggered)
        {
            newActionGameSpins += spinResult.ActionGameSpins;
        }

        // Update session state
        // When retriggering free spins, keep the existing feature symbol
        var finalFeatureSymbol = selectedFeatureSymbol ?? featureSymbol ?? "";
        var newState = new GameState
        {
            Balance = newBalance,
            FreeSpinsRemaining = newFreeSpins,
            LastWin = spinResult.TotalWin + spinResult.ExpandedWin,
            Results = spinResult,
            ActionGameSpins = newActionGameSpins,
            FeatureSymbol = finalFeatureSymbol
        };
        
        // Update spinResult with the final feature symbol
        if (!string.IsNullOrEmpty(finalFeatureSymbol))
        {
            spinResult.FeatureSymbol = finalFeatureSymbol;
        }

        sessionService.UpdateSession(request.SessionId, newState);

        // Log spin transaction to database
        try
        {
            await dataService.SaveSpinTransactionAsync(
                request.SessionId,
                gameId,
                totalBet,
                spinResult,
                isFreeSpin || isActionGameSpin,
                freeSpinsAwarded);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the request
            Console.WriteLine($"[ERROR] Failed to save spin transaction: {ex.Message}");
        }

        // Prepare response
        var response = new PlayResponse
        {
            SessionId = request.SessionId,
            Player = newState,
            Game = newState,
            FreeSpins = newFreeSpins,
            ActionGameSpins = newActionGameSpins,
            FeatureSymbol = newState.FeatureSymbol
        };

        // Attempt to send to RGS (non-blocking, fire-and-forget)
        if (optionalRgsService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await optionalRgsService.SendSpinDataAsync(request.SessionId, gameId, response);
                }
                catch
                {
                    // Already logged in OptionalRgsService, ignore here
                }
            });
        }

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing spin: {ex.Message}");
    }
})
.WithName("PlayGame");

app.MapGet("/session/{sessionId}", (string sessionId, SessionService sessionService) =>
{
    var session = sessionService.GetOrCreateSession(sessionId);
    return Results.Ok(new PlayResponse
    {
        SessionId = sessionId,
        Player = session,
        Game = session,
        FreeSpins = session.FreeSpinsRemaining,
        ActionGameSpins = session.ActionGameSpins,
        FeatureSymbol = session.FeatureSymbol
    });
})
.WithName("GetSession");

app.MapPost("/session/{sessionId}/reset", (string sessionId, SessionService sessionService) =>
{
    sessionService.ResetSession(sessionId);
    return Results.Ok(new { Message = "Session reset successfully" });
})
.WithName("ResetSession");

// API endpoints for game data
app.MapGet("/game/sessions/{sessionId}/history", async (string sessionId, GameDataService dataService, int? limit = 50) =>
{
    try
    {
        var history = await dataService.GetSpinHistoryAsync(sessionId, limit);
        return Results.Ok(history);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving spin history: {ex.Message}");
    }
})
.WithName("GetSpinHistory");

app.MapGet("/game/sessions/{sessionId}", async (string sessionId, GameDataService dataService) =>
{
    try
    {
        var session = await dataService.GetSessionAsync(sessionId);
        if (session == null)
        {
            return Results.NotFound(new { Error = "Session not found" });
        }
        return Results.Ok(session);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving session: {ex.Message}");
    }
})
.WithName("GetSessionDetails");

app.MapGet("/game/stats", async (GameDataService dataService) =>
{
    try
    {
        var stats = await dataService.GetGameStatsAsync();
        return Results.Ok(stats);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving game statistics: {ex.Message}");
    }
})
.WithName("GetGameStats");

// Action game wheel spin endpoint
app.MapPost("/action-game/spin", async (ActionGameSpinRequest request, GameConfigService configService, SessionService sessionService, OptionalRgsService? optionalRgsService) =>
{
    try
    {
        // Get session
        var currentState = sessionService.GetOrCreateSession(request.SessionId);
        var session = await sessionService.GetSessionAsync(request.SessionId);
        
        if (session == null)
        {
            return Results.BadRequest(new { Error = "Session not found" });
        }

        if (currentState.ActionGameSpins <= 0)
        {
            return Results.BadRequest(new { Error = "No action game spins remaining" });
        }

        // Get game config
        var gameId = session.GameId ?? "SnowKingdom";
        var gameConfig = configService.LoadGameConfig(gameId);
        var gameEngine = new GameEngine(gameConfig);

        // Spin the wheel
        var wheelResult = gameEngine.SpinActionGameWheel();

        // Update balance if won
        var newBalance = currentState.Balance + wheelResult.Win;

        // Update action game spins (deduct 1, add any additional spins)
        var newActionGameSpins = currentState.ActionGameSpins - 1 + wheelResult.AdditionalSpins;

        // Update session state
        var newState = new GameState
        {
            Balance = newBalance,
            FreeSpinsRemaining = currentState.FreeSpinsRemaining,
            LastWin = wheelResult.Win,
            Results = currentState.Results,
            ActionGameSpins = newActionGameSpins,
            FeatureSymbol = currentState.FeatureSymbol
        };

        sessionService.UpdateSession(request.SessionId, newState);

        var response = new ActionGameSpinResponse
        {
            SessionId = request.SessionId,
            Result = wheelResult,
            RemainingSpins = newActionGameSpins
        };

        // Attempt to send to RGS (non-blocking, fire-and-forget)
        if (optionalRgsService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await optionalRgsService.SendActionGameSpinDataAsync(request.SessionId, response);
                }
                catch
                {
                    // Already logged in OptionalRgsService, ignore here
                }
            });
        }

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing action game spin: {ex.Message}");
    }
})
.WithName("ActionGameSpin");

// Ensure database is created on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
