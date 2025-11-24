using Microsoft.EntityFrameworkCore;
using SnowKingdomBackendAPI.ApiService.Data;
using SnowKingdomBackendAPI.ApiService.Game;
using SnowKingdomBackendAPI.ApiService.Models;
using SnowKingdomBackendAPI.ApiService.Services;
using System.Linq;

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
                "http://localhost:3006", // FrontEndInfernoEmpress
                "http://localhost:3007", // FrontEndReignOfThunder
                "https://localhost:3000",
                "https://localhost:3001",
                "https://localhost:3002",
                "https://localhost:3003",
                "https://localhost:3004",
                "https://localhost:3005",
                "https://localhost:3006",
                "https://localhost:3007",
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
                "http://127.0.0.1:3006",
                "http://127.0.0.1:3007",
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

        // Determine if we are in a feature game
        string? activeFeatureSymbol = null;
        // Logic: If free spins are active, grab the feature symbol from the stored state
        if (isFreeSpin && !string.IsNullOrEmpty(featureSymbol))
        {
            activeFeatureSymbol = featureSymbol;
        }

        // Generate new grid
        var grid = gameEngine.GenerateGrid(activeFeatureSymbol);

        // Evaluate the spin
        // Note: betAmount parameter is per-payline for legacy compatibility, but totalBet is used for bet-specific payout lookup
        var numPaylines = request.NumPaylines > 0 ? request.NumPaylines : 0; // 0 means use all paylines
        var betPerPayline = request.NumPaylines > 0 && request.BetPerPayline > 0 ? request.BetPerPayline : (totalBet / (numPaylines > 0 ? numPaylines : gameConfig.MaxPaylines));
        var spinResult = gameEngine.EvaluateSpin(grid, betPerPayline, numPaylines, isFreeSpin, featureSymbol, totalBet);

        // Update player state
        var newBalance = currentState.Balance;
        var newFreeSpins = currentState.FreeSpinsRemaining;
        var newActionGameSpins = currentState.ActionGameSpins;
        var wasInFreeSpinsMode = currentState.FreeSpinsRemaining > 0;
        var accumulatedActionWin = currentState.AccumulatedActionGameWin;
        
        // Track penny game and action game bets
        var accumulatedPennyGameBets = currentState.AccumulatedPennyGameBets;
        var accumulatedActionGameBets = currentState.AccumulatedActionGameBets;
        var losingSpinsAfterFeature = currentState.LosingSpinsAfterFeature;
        var lastFeatureExitType = currentState.LastFeatureExitType;
        var mysteryPrizeTrigger = currentState.MysteryPrizeTrigger; // Get stored trigger value
        const decimal pennyGameBetAmount = 0.10m;

        if (isActionGameSpin)
        {
            // Action game spin: deduct from action game spins, not balance
            newActionGameSpins = Math.Max(0, request.ActionGameSpins - 1);
        }
        else if (isFreeSpin)
        {
            // Check if player has enough balance for R0.10 penny game bet
            if (currentState.Balance < pennyGameBetAmount)
            {
                return Results.BadRequest(new
                {
                    Error = "Insufficient balance for penny game spin",
                    CurrentBalance = currentState.Balance,
                    Required = pennyGameBetAmount
                });
            }
            
            // Deduct R0.10 for penny game (free spin)
            newBalance = currentState.Balance - pennyGameBetAmount;
            accumulatedPennyGameBets += pennyGameBetAmount;
            Console.WriteLine($"[PENNY GAME] Deducted R{pennyGameBetAmount} for free spin. Total accumulated: R{accumulatedPennyGameBets}");
            
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
        // IMPORTANT: In free spins, payouts happen in this order:
        // 1. Base game wins (all symbols EXCEPT feature symbol wins that will expand)
        // 2. Feature game wins (after expansion, feature symbol wins on all paylines)
        // 3. Action game wins (accumulated during free spins, paid after free spins complete)
        
        // Base game wins (excluding feature symbol wins in free spins that will expand)
        // During free spins, don't add ActionGameWin to balance - accumulate it instead
        var baseWin = spinResult.TotalWin;
        if (isFreeSpin && spinResult.ActionGameTriggered)
        {
            // During free spins: accumulate action game win, don't add to balance
            accumulatedActionWin += spinResult.ActionGameWin;
            baseWin -= spinResult.ActionGameWin; // Remove from base win since we're accumulating it
            Console.WriteLine($"[ACTION GAME] Accumulating win during free spins: R{spinResult.ActionGameWin}, Total accumulated: R{accumulatedActionWin}");
        }
        else if (!isFreeSpin && !isActionGameSpin && spinResult.ActionGameTriggered)
        {
            // Base game: add action game win to balance immediately (no free spins to wait for)
            baseWin += spinResult.ActionGameWin;
        }
        
        // STEP 1: Add base game wins FIRST (before expansion)
        newBalance += baseWin;
        Console.WriteLine($"[WIN CALCULATION] Base game win: R{baseWin}");
        
        // STEP 2: Add feature game wins AFTER expansion (only in free spins when reels expand)
        if (spinResult.ExpandedWin > 0)
        {
            newBalance += spinResult.ExpandedWin;
            Console.WriteLine($"[WIN CALCULATION] Feature game win (after expansion): R{spinResult.ExpandedWin}");
        }
        
        var totalWin = baseWin + spinResult.ExpandedWin;
        Console.WriteLine($"[WIN CALCULATION] Total win: R{totalWin} (Base: R{baseWin}, Expanded: R{spinResult.ExpandedWin})");

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

        // Track when free spins end (transition from > 0 to 0)
        var freeSpinsJustEnded = wasInFreeSpinsMode && newFreeSpins == 0;
        if (freeSpinsJustEnded)
        {
            lastFeatureExitType = "freeSpins";
            losingSpinsAfterFeature = 0;
            mysteryPrizeTrigger = null; // Reset trigger when feature ends
            Console.WriteLine($"[MYSTERY PRIZE] Free spins ended. Starting mystery prize tracking.");
        }

        // Track when action games end (if we're in base game and action games just became 0)
        var actionGamesJustEnded = !isFreeSpin && !isActionGameSpin && currentState.ActionGameSpins > 0 && newActionGameSpins == 0;
        if (actionGamesJustEnded)
        {
            lastFeatureExitType = "actionGames";
            losingSpinsAfterFeature = 0;
            mysteryPrizeTrigger = null; // Reset trigger when feature ends
            Console.WriteLine($"[MYSTERY PRIZE] Action games ended. Starting mystery prize tracking.");
        }

        // Mystery prize logic - only in base game (not free spins or action game spins)
        var mysteryPrizeAwarded = 0m;
        if (!isFreeSpin && !isActionGameSpin && !string.IsNullOrEmpty(lastFeatureExitType))
        {
            // Use the totalWin already calculated above (baseWin + ExpandedWin)
            // This represents the actual win amount added to balance
            
            if (totalWin == 0)
            {
                // Losing spin - increment counter
                losingSpinsAfterFeature++;
                Console.WriteLine($"[MYSTERY PRIZE] Losing spin #{losingSpinsAfterFeature} after {lastFeatureExitType}");
                
                // Check if we should award mystery prize (between 2-5 losing spins)
                if (losingSpinsAfterFeature >= 2)
                {
                    // Generate random trigger ONCE when we first reach 2 losing spins
                    if (mysteryPrizeTrigger == null)
                    {
                        var random = new Random();
                        mysteryPrizeTrigger = random.Next(2, 6); // 2, 3, 4, or 5
                        Console.WriteLine($"[MYSTERY PRIZE] Generated trigger: {mysteryPrizeTrigger} (will award on losing spin #{mysteryPrizeTrigger})");
                    }
                    
                    // Check if current losing spin count matches the trigger
                    if (losingSpinsAfterFeature == mysteryPrizeTrigger)
                    {
                        // Award mystery prize
                        var totalAccumulatedBets = accumulatedPennyGameBets + accumulatedActionGameBets;
                        if (totalAccumulatedBets > 0)
                        {
                            mysteryPrizeAwarded = totalAccumulatedBets;
                            newBalance += mysteryPrizeAwarded;
                            Console.WriteLine($"[MYSTERY PRIZE] Awarded R{mysteryPrizeAwarded} (Penny: R{accumulatedPennyGameBets}, Action: R{accumulatedActionGameBets})");
                            
                            // Update totalWin to include mystery prize for display purposes
                            // This ensures the win animation shows the correct amount
                            totalWin = mysteryPrizeAwarded;
                            
                            // Reset pools and tracking
                            accumulatedPennyGameBets = 0;
                            accumulatedActionGameBets = 0;
                            losingSpinsAfterFeature = 0;
                            lastFeatureExitType = null;
                            mysteryPrizeTrigger = null; // Reset trigger after awarding
                        }
                    }
                }
            }
            else
            {
                // Winning spin - reset losing spin counter and trigger but KEEP accumulated bets and lastFeatureExitType
                // This allows mystery prize to still be awarded if more losing spins occur
                // Only reset everything when mystery prize is actually awarded
                losingSpinsAfterFeature = 0;
                mysteryPrizeTrigger = null; // Reset trigger on winning spin (will be regenerated on next losing spin)
                var totalPending = accumulatedPennyGameBets + accumulatedActionGameBets;
                if (totalPending > 0)
                {
                    Console.WriteLine($"[MYSTERY PRIZE] Winning spin - resetting losing spin counter and trigger (accumulated bets: R{totalPending} still pending, will continue tracking)");
                }
                else
                {
                    // No accumulated bets, safe to reset everything
                    lastFeatureExitType = null;
                    Console.WriteLine($"[MYSTERY PRIZE] Winning spin - no accumulated bets, resetting all tracking");
                }
            }
        }

        // Update session state
        // When retriggering free spins, keep the existing feature symbol
        var finalFeatureSymbol = selectedFeatureSymbol ?? featureSymbol ?? "";
        
        // Update spinResult.TotalWin if mystery prize was awarded (for display purposes)
        if (mysteryPrizeAwarded > 0)
        {
            spinResult.TotalWin = mysteryPrizeAwarded;
        }
        
        var newState = new GameState
        {
            Balance = newBalance,
            FreeSpinsRemaining = newFreeSpins,
            LastWin = mysteryPrizeAwarded > 0 ? mysteryPrizeAwarded : (spinResult.TotalWin + spinResult.ExpandedWin),
            Results = spinResult,
            ActionGameSpins = newActionGameSpins,
            FeatureSymbol = finalFeatureSymbol,
            AccumulatedActionGameWin = accumulatedActionWin,
            AccumulatedPennyGameBets = accumulatedPennyGameBets,
            AccumulatedActionGameBets = accumulatedActionGameBets,
            LosingSpinsAfterFeature = losingSpinsAfterFeature,
            LastFeatureExitType = lastFeatureExitType,
            MysteryPrizeTrigger = mysteryPrizeTrigger // Store the trigger value
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
            FeatureSymbol = newState.FeatureSymbol,
            MysteryPrizeAwarded = mysteryPrizeAwarded,
            AccumulatedPennyGameBets = accumulatedPennyGameBets,
            AccumulatedActionGameBets = accumulatedActionGameBets
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

        // Check if player has enough balance for R0.10 action game bet
        const decimal actionGameBetAmount = 0.10m;
        if (session.Balance < actionGameBetAmount)
        {
            return Results.BadRequest(new
            {
                Error = "Insufficient balance for action game spin",
                CurrentBalance = session.Balance,
                Required = actionGameBetAmount
            });
        }

        // Get game config
        var gameId = session.GameId ?? "SnowKingdom";
        var gameConfig = configService.LoadGameConfig(gameId);
        var gameEngine = new GameEngine(gameConfig);

        // Deduct R0.10 for action game spin
        var currentBalance = session.Balance;
        var balanceAfterDeduction = currentBalance - actionGameBetAmount;
        var accumulatedActionGameBets = (session.LastResponse?.AccumulatedActionGameBets ?? 0) + actionGameBetAmount;
        Console.WriteLine($"[ACTION GAME] Deducted R{actionGameBetAmount} for action game spin. Total accumulated: R{accumulatedActionGameBets}");

        // Spin the wheel
        var wheelResult = gameEngine.SpinActionGameWheel();

        // Add wheel win to balance immediately after each spin
        var newBalance = balanceAfterDeduction + wheelResult.Win;
        Console.WriteLine($"[ACTION GAME] Current balance: R{currentBalance}, Deducted: R{actionGameBetAmount}, Wheel win: R{wheelResult.Win}, New balance: R{newBalance}");

        // Update action game spins (deduct 1, add any additional spins)
        var newActionGameSpins = currentState.ActionGameSpins - 1 + wheelResult.AdditionalSpins;
        
        // Track when action games end (transition from > 0 to 0)
        var actionGamesJustEnded = currentState.ActionGameSpins > 0 && newActionGameSpins == 0;
        var losingSpinsAfterFeature = 0;
        string? lastFeatureExitType = null;
        int? mysteryPrizeTrigger = null;
        if (actionGamesJustEnded)
        {
            lastFeatureExitType = "actionGames";
            losingSpinsAfterFeature = 0;
            mysteryPrizeTrigger = null; // Reset trigger when feature ends
            Console.WriteLine($"[MYSTERY PRIZE] Action games ended. Starting mystery prize tracking.");
        }
        else
        {
            // Preserve existing mystery prize tracking state
            losingSpinsAfterFeature = currentState.LosingSpinsAfterFeature;
            lastFeatureExitType = currentState.LastFeatureExitType;
            mysteryPrizeTrigger = currentState.MysteryPrizeTrigger; // Preserve trigger
        }

        // Load accumulated win from database session (LastResponse) to ensure we have the latest value
        // This is critical for accumulating wins across multiple action game sessions
        var currentAccumulatedWin = session.LastResponse?.AccumulatedActionGameWin ?? 0;
        var accumulatedWin = currentAccumulatedWin + wheelResult.Win;
        Console.WriteLine($"[ACTION GAME] Current accumulated win: R{currentAccumulatedWin}, Wheel win: R{wheelResult.Win}, New accumulated win: R{accumulatedWin}");

        // Preserve penny game bets
        var accumulatedPennyGameBets = currentState.AccumulatedPennyGameBets;

        // Update session state
        var newState = new GameState
        {
            Balance = newBalance,
            FreeSpinsRemaining = currentState.FreeSpinsRemaining,
            LastWin = wheelResult.Win,
            Results = currentState.Results,
            ActionGameSpins = newActionGameSpins,
            FeatureSymbol = currentState.FeatureSymbol,
            AccumulatedActionGameWin = accumulatedWin,
            AccumulatedPennyGameBets = accumulatedPennyGameBets,
            AccumulatedActionGameBets = accumulatedActionGameBets,
            LosingSpinsAfterFeature = losingSpinsAfterFeature,
            LastFeatureExitType = lastFeatureExitType,
            MysteryPrizeTrigger = mysteryPrizeTrigger // Preserve or reset trigger
        };

        // Update session state (this updates both in-memory cache and database)
        sessionService.UpdateSession(request.SessionId, newState);
        
        // Verify the update by checking the session balance
        var updatedSession = await sessionService.GetSessionAsync(request.SessionId);
        Console.WriteLine($"[ACTION GAME] After update - Session balance: R{updatedSession?.Balance}, Expected: R{newBalance}");

        var response = new ActionGameSpinResponse
        {
            SessionId = request.SessionId,
            Result = wheelResult,
            RemainingSpins = newActionGameSpins,
            AccumulatedWin = accumulatedWin,
            TotalActionSpins = currentState.ActionGameSpins,
            Balance = newBalance // Use the calculated newBalance, not from session
        };
        
        Console.WriteLine($"[ACTION GAME] Response balance: R{response.Balance}");

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

// Dev mode endpoint: Force trigger free spins (only in development)
if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/trigger-free-spins", async (DevTriggerFreeSpinsRequest request, GameConfigService configService, SessionService sessionService, GameDataService dataService, OptionalRgsService? optionalRgsService) =>
    {
        try
        {
            // Get or create session
            var currentState = sessionService.GetOrCreateSession(request.SessionId);
            var session = await sessionService.GetSessionAsync(request.SessionId);
            
            if (session == null)
            {
                return Results.BadRequest(new { Error = "Session not found" });
            }

            // Get gameId
            var gameId = request.GameId ?? session.GameId ?? "SnowKingdom";
            
            // Load game configuration
            var gameConfig = configService.LoadGameConfig(gameId);
            var gameEngine = new GameEngine(gameConfig);
            
            var scatterSymbol = gameConfig.GetScatterSymbol();
            var reelStrips = gameConfig.ReelStrips;
            var numReels = reelStrips.Count;
            var numRows = reelStrips[0]?.Count > 0 ? reelStrips[0].Count : 3;

            // Force generate a grid with 3+ scatter symbols on different reels
            var forcedGrid = new List<List<string>>();
            var scatterPositions = new List<(int reel, int row)>();
            
            // Place scatter symbols on reels 0, 1, and 2 (guaranteed 3 scatters)
            for (int reel = 0; reel < Math.Min(3, numReels); reel++)
            {
                var reelStrip = reelStrips[reel] ?? new List<string>();
                var reelColumn = new List<string>();
                
                for (int row = 0; row < numRows; row++)
                {
                    if (row == 1) // Place scatter in middle row
                    {
                        reelColumn.Add(scatterSymbol);
                        scatterPositions.Add((reel, row));
                    }
                    else
                    {
                        // Get random symbol from reel strip (excluding scatter to avoid consecutive)
                        var availableSymbols = reelStrip.Where(s => s != scatterSymbol).ToList();
                        if (availableSymbols.Count == 0) availableSymbols = reelStrip.ToList();
                        var randomSymbol = availableSymbols[new Random().Next(availableSymbols.Count)];
                        reelColumn.Add(randomSymbol);
                    }
                }
                forcedGrid.Add(reelColumn);
            }
            
            // Fill remaining reels with random symbols
            for (int reel = 3; reel < numReels; reel++)
            {
                var reelStrip = reelStrips[reel] ?? new List<string>();
                var reelColumn = new List<string>();
                
                for (int row = 0; row < numRows; row++)
                {
                    var randomSymbol = reelStrip[new Random().Next(reelStrip.Count)];
                    reelColumn.Add(randomSymbol);
                }
                forcedGrid.Add(reelColumn);
            }

            // Now process this grid through normal play logic
            // We'll create a play request and process it
            var isFreeSpin = currentState.FreeSpinsRemaining > 0;
            var totalBet = request.BetAmount > 0 
                ? request.BetAmount 
                : (request.NumPaylines > 0 && request.BetPerPayline > 0
                    ? request.NumPaylines * request.BetPerPayline
                    : 1.00m);
            var numPaylines = request.NumPaylines > 0 ? request.NumPaylines : gameConfig.MaxPaylines;
            var betPerPayline = totalBet / numPaylines;
            
            // Get feature symbol if in free spins
            string? featureSymbol = null;
            if (isFreeSpin && !string.IsNullOrEmpty(currentState.FeatureSymbol))
            {
                featureSymbol = currentState.FeatureSymbol;
            }

            // Evaluate the forced spin
            var spinResult = gameEngine.EvaluateSpin(forcedGrid, betPerPayline, numPaylines, isFreeSpin, featureSymbol, totalBet);
            
            // Process the result through normal play logic (balance updates, free spins, etc.)
            // Calculate wins - baseWin starts as TotalWin, then adjusted
            var baseWin = spinResult.TotalWin;
            var accumulatedActionWin = currentState.AccumulatedActionGameWin;
            
            // Handle action game wins during free spins
            if (isFreeSpin && spinResult.ActionGameTriggered)
            {
                accumulatedActionWin += spinResult.ActionGameWin;
                baseWin -= spinResult.ActionGameWin;
            }
            else if (!isFreeSpin && spinResult.ActionGameTriggered)
            {
                baseWin += spinResult.ActionGameWin;
            }
            
            var totalWin = baseWin + spinResult.ExpandedWin;
            
            // Update balance
            var newBalance = session.Balance;
            if (!isFreeSpin)
            {
                newBalance = newBalance - totalBet + totalWin;
            }
            else
            {
                // During free spins, deduct R0.10 and add to accumulated bets
                const decimal pennyGameBetAmount = 0.10m;
                newBalance = newBalance - pennyGameBetAmount + totalWin;
                currentState.AccumulatedPennyGameBets = currentState.AccumulatedPennyGameBets + pennyGameBetAmount;
            }
            
            // Add free spins if triggered
            var newFreeSpins = currentState.FreeSpinsRemaining;
            string? selectedFeatureSymbol = null;
            if (spinResult.ScatterWin.TriggeredFreeSpins)
            {
                var freeSpinsAwarded = gameConfig.FreeSpinsAwarded;
                newFreeSpins += freeSpinsAwarded;
                
                if (!isFreeSpin)
                {
                    selectedFeatureSymbol = gameEngine.SelectFeatureSymbol();
                    spinResult.FeatureSymbol = selectedFeatureSymbol;
                }
            }
            
            // Update state
            currentState.Balance = newBalance;
            currentState.FreeSpinsRemaining = newFreeSpins;
            if (!string.IsNullOrEmpty(selectedFeatureSymbol))
            {
                currentState.FeatureSymbol = selectedFeatureSymbol;
            }
            currentState.LastWin = totalWin;
            
            // Track free spins ending
            var wasInFreeSpinsMode = currentState.FreeSpinsRemaining > 0;
            var freeSpinsJustEnded = wasInFreeSpinsMode && newFreeSpins == 0;
            if (freeSpinsJustEnded)
            {
                currentState.LastFeatureExitType = "freeSpins";
                currentState.LosingSpinsAfterFeature = 0;
                currentState.MysteryPrizeTrigger = null; // Reset trigger when feature ends
                Console.WriteLine($"[MYSTERY PRIZE] Free spins ended. Starting mystery prize tracking.");
            }
            
            // Track action games ending
            var wasInActionGamesMode = currentState.ActionGameSpins > 0;
            var actionGamesJustEnded = wasInActionGamesMode && currentState.ActionGameSpins == 0;
            if (actionGamesJustEnded && !isFreeSpin)
            {
                currentState.LastFeatureExitType = "actionGames";
                currentState.LosingSpinsAfterFeature = 0;
                currentState.MysteryPrizeTrigger = null; // Reset trigger when feature ends
                Console.WriteLine($"[MYSTERY PRIZE] Action games ended. Starting mystery prize tracking.");
            }
            
            // Mystery prize logic - only in base game (not free spins or action game spins)
            var mysteryPrizeAwarded = 0m;
            var accumulatedPennyGameBets = currentState.AccumulatedPennyGameBets;
            var accumulatedActionGameBets = currentState.AccumulatedActionGameBets;
            var losingSpinsAfterFeature = currentState.LosingSpinsAfterFeature;
            var lastFeatureExitType = currentState.LastFeatureExitType;
            var mysteryPrizeTrigger = currentState.MysteryPrizeTrigger; // Get stored trigger value
            
            if (!isFreeSpin && !string.IsNullOrEmpty(lastFeatureExitType))
            {
                if (totalWin == 0)
                {
                    // Losing spin - increment counter
                    losingSpinsAfterFeature++;
                    Console.WriteLine($"[MYSTERY PRIZE] Losing spin #{losingSpinsAfterFeature} after {lastFeatureExitType}");
                    
                    // Check if we should award mystery prize (between 2-5 losing spins)
                    if (losingSpinsAfterFeature >= 2)
                    {
                        // Generate random trigger ONCE when we first reach 2 losing spins
                        if (mysteryPrizeTrigger == null)
                        {
                            var random = new Random();
                            mysteryPrizeTrigger = random.Next(2, 6); // 2, 3, 4, or 5
                            Console.WriteLine($"[MYSTERY PRIZE] Generated trigger: {mysteryPrizeTrigger} (will award on losing spin #{mysteryPrizeTrigger})");
                        }
                        
                        // Check if current losing spin count matches the trigger
                        if (losingSpinsAfterFeature == mysteryPrizeTrigger)
                        {
                            // Award mystery prize
                            var totalAccumulatedBets = accumulatedPennyGameBets + accumulatedActionGameBets;
                            if (totalAccumulatedBets > 0)
                            {
                                mysteryPrizeAwarded = totalAccumulatedBets;
                                newBalance += mysteryPrizeAwarded;
                                Console.WriteLine($"[MYSTERY PRIZE] Awarded R{mysteryPrizeAwarded} (Penny: R{accumulatedPennyGameBets}, Action: R{accumulatedActionGameBets})");
                                
                                // Update totalWin to include mystery prize for display purposes
                                // This ensures the win animation shows the correct amount
                                totalWin = mysteryPrizeAwarded;
                                
                                // Reset pools and tracking
                                accumulatedPennyGameBets = 0;
                                accumulatedActionGameBets = 0;
                                losingSpinsAfterFeature = 0;
                                lastFeatureExitType = null;
                                mysteryPrizeTrigger = null; // Reset trigger after awarding
                            }
                        }
                    }
                }
                else
                {
                    // Winning spin - reset losing spin counter and trigger but KEEP accumulated bets and lastFeatureExitType
                    losingSpinsAfterFeature = 0;
                    mysteryPrizeTrigger = null; // Reset trigger on winning spin (will be regenerated on next losing spin)
                    var totalPending = accumulatedPennyGameBets + accumulatedActionGameBets;
                    if (totalPending > 0)
                    {
                        Console.WriteLine($"[MYSTERY PRIZE] Winning spin - resetting losing spin counter and trigger (accumulated bets: R{totalPending} still pending, will continue tracking)");
                    }
                    else
                    {
                        // No accumulated bets, safe to reset everything
                        lastFeatureExitType = null;
                        Console.WriteLine($"[MYSTERY PRIZE] Winning spin - no accumulated bets, resetting all tracking");
                    }
                }
            }
            
            // Update state with mystery prize tracking
            currentState.AccumulatedPennyGameBets = accumulatedPennyGameBets;
            currentState.AccumulatedActionGameBets = accumulatedActionGameBets;
            currentState.LosingSpinsAfterFeature = losingSpinsAfterFeature;
            currentState.LastFeatureExitType = lastFeatureExitType;
            currentState.MysteryPrizeTrigger = mysteryPrizeTrigger; // Store trigger value
            
            // Update session
            session.Balance = newBalance;
            session.FreeSpinsRemaining = newFreeSpins;
            session.LastWin = totalWin;
            
            // Update LastResponse with new state (which includes FeatureSymbol)
            if (session.LastResponse == null)
            {
                session.LastResponse = new GameState();
            }
            session.LastResponse.Balance = newBalance;
            session.LastResponse.FreeSpinsRemaining = newFreeSpins;
            session.LastResponse.LastWin = totalWin;
            session.LastResponse.Results = spinResult;
            if (!string.IsNullOrEmpty(selectedFeatureSymbol))
            {
                session.LastResponse.FeatureSymbol = selectedFeatureSymbol;
            }
            session.LastResponse.AccumulatedActionGameWin = accumulatedActionWin;
            session.LastResponse.AccumulatedPennyGameBets = accumulatedPennyGameBets;
            session.LastResponse.AccumulatedActionGameBets = accumulatedActionGameBets;
            session.LastResponse.LosingSpinsAfterFeature = losingSpinsAfterFeature;
            session.LastResponse.LastFeatureExitType = lastFeatureExitType;
            
            await sessionService.UpdateSessionAsync(session);
            
            // Update session service state
            sessionService.UpdateSession(request.SessionId, currentState);
            
            // Update spinResult with final values
            spinResult.TotalWin = totalWin;
            if (!string.IsNullOrEmpty(selectedFeatureSymbol))
            {
                spinResult.FeatureSymbol = selectedFeatureSymbol;
            }
            
            // Build response matching PlayResponse structure
            var newState = new GameState
            {
                Balance = newBalance,
                FreeSpinsRemaining = newFreeSpins,
                LastWin = totalWin,
                Results = spinResult,
                ActionGameSpins = currentState.ActionGameSpins,
                FeatureSymbol = selectedFeatureSymbol ?? currentState.FeatureSymbol ?? "",
                AccumulatedActionGameWin = accumulatedActionWin,
                AccumulatedPennyGameBets = accumulatedPennyGameBets,
                AccumulatedActionGameBets = accumulatedActionGameBets,
                LosingSpinsAfterFeature = losingSpinsAfterFeature,
                LastFeatureExitType = lastFeatureExitType,
                MysteryPrizeTrigger = mysteryPrizeTrigger // Store the trigger value
            };
            
            var response = new PlayResponse
            {
                SessionId = request.SessionId,
                Player = newState,
                Game = newState,
                FreeSpins = newFreeSpins,
                ActionGameSpins = currentState.ActionGameSpins,
                FeatureSymbol = selectedFeatureSymbol ?? currentState.FeatureSymbol ?? "",
                MysteryPrizeAwarded = mysteryPrizeAwarded,
                AccumulatedPennyGameBets = accumulatedPennyGameBets,
                AccumulatedActionGameBets = accumulatedActionGameBets
            };
            
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEV MODE] Error triggering free spins: {ex.Message}");
            return Results.Problem($"Error: {ex.Message}");
        }
    });

    // Dev mode endpoint: Force trigger action games (only in development)
    app.MapPost("/dev/trigger-action-games", async (DevTriggerActionGamesRequest request, GameConfigService configService, SessionService sessionService, GameDataService dataService, OptionalRgsService? optionalRgsService) =>
    {
        try
        {
            // Get or create session
            var currentState = sessionService.GetOrCreateSession(request.SessionId);
            var session = await sessionService.GetSessionAsync(request.SessionId);
            
            if (session == null)
            {
                return Results.BadRequest(new { Error = "Session not found" });
            }

            // Get gameId
            var gameId = request.GameId ?? session.GameId ?? "SnowKingdom";
            var gameConfig = configService.LoadGameConfig(gameId);
            var gameEngine = new GameEngine(gameConfig);

            // Calculate total bet
            var totalBet = request.BetAmount > 0 ? request.BetAmount : (request.NumPaylines > 0 && request.BetPerPayline > 0 ? request.NumPaylines * request.BetPerPayline : 1.00m);
            
            // Normalize to configured bet amount
            var normalizedBet = gameConfig.BetAmounts.OrderBy(b => Math.Abs(b - totalBet)).First();
            totalBet = normalizedBet;

            // Generate a grid that will trigger action games
            // We'll force a grid with enough symbols to trigger action games
            var grid = new List<List<string>>();
            var numReels = gameConfig.NumReels;
            var numRows = gameConfig.NumRows;
            
            // Find a symbol that triggers action games
            string? actionGameSymbol = null;
            int requiredCount = 0;
            int actionSpinsAwarded = 10; // Default dev action spins
            
            // Look for action game triggers in config
            if (gameConfig.Symbols != null)
            {
                var betKey = totalBet.ToString("F2");
                foreach (var symbolEntry in gameConfig.Symbols)
                {
                    var symbol = symbolEntry.Value;
                    if (symbol.ActionGamesByBet != null && symbol.ActionGamesByBet.TryGetValue(betKey, out var betActionGames))
                    {
                        // Find the highest count that awards action games
                        foreach (var countEntry in betActionGames.OrderByDescending(x => x.Key))
                        {
                            if (countEntry.Value > 0)
                            {
                                actionGameSymbol = symbolEntry.Key;
                                requiredCount = countEntry.Key;
                                actionSpinsAwarded = countEntry.Value;
                                break;
                            }
                        }
                        if (actionGameSymbol != null) break;
                    }
                }
            }
            
            // If no action game trigger found, use a default symbol (e.g., "Queen")
            if (actionGameSymbol == null)
            {
                actionGameSymbol = "Queen";
                requiredCount = 5;
            }
            
            // Generate grid with enough symbols to trigger action games
            // Fill grid with the action game symbol in required positions
            for (int reel = 0; reel < numReels; reel++)
            {
                var reelStrip = new List<string>();
                for (int row = 0; row < numRows; row++)
                {
                    // Place action game symbol in first few positions to ensure trigger
                    if (reel * numRows + row < requiredCount)
                    {
                        reelStrip.Add(actionGameSymbol);
                    }
                    else
                    {
                        // Fill rest with random symbols
                        if (gameConfig.Symbols != null && gameConfig.Symbols.Count > 0)
                        {
                            var allSymbols = gameConfig.Symbols.Keys.ToList();
                            reelStrip.Add(allSymbols[new Random().Next(allSymbols.Count)]);
                        }
                        else
                        {
                            // Fallback if no symbols available
                            reelStrip.Add("A");
                        }
                    }
                }
                grid.Add(reelStrip);
            }

            // Evaluate the spin with the forced grid
            var numPaylines = request.NumPaylines > 0 ? request.NumPaylines : gameConfig.MaxPaylines;
            var betPerPayline = request.NumPaylines > 0 && request.BetPerPayline > 0 ? request.BetPerPayline : (totalBet / numPaylines);
            var spinResult = gameEngine.EvaluateSpin(grid, betPerPayline, numPaylines, false, null, totalBet);
            
            // Force action games to be triggered
            spinResult.ActionGameTriggered = true;
            spinResult.ActionGameSpins = actionSpinsAwarded;
            
            // Calculate base win (excluding action game win)
            var baseWin = spinResult.TotalWin;
            
            // Update balance (deduct bet for base game spin)
            var newBalance = currentState.Balance - totalBet + baseWin;
            
            // Update action game spins
            var newActionGameSpins = currentState.ActionGameSpins + actionSpinsAwarded;
            
            // Track action games starting
            var losingSpinsAfterFeature = currentState.LosingSpinsAfterFeature;
            var lastFeatureExitType = currentState.LastFeatureExitType;
            var mysteryPrizeTrigger = currentState.MysteryPrizeTrigger;
            
            // Preserve accumulated values
            var accumulatedPennyGameBets = currentState.AccumulatedPennyGameBets;
            var accumulatedActionGameBets = currentState.AccumulatedActionGameBets;
            var accumulatedActionWin = currentState.AccumulatedActionGameWin;
            
            // Update session state
            var newState = new GameState
            {
                Balance = newBalance,
                FreeSpinsRemaining = currentState.FreeSpinsRemaining,
                LastWin = baseWin,
                Results = spinResult,
                ActionGameSpins = newActionGameSpins,
                FeatureSymbol = currentState.FeatureSymbol,
                AccumulatedActionGameWin = accumulatedActionWin,
                AccumulatedPennyGameBets = accumulatedPennyGameBets,
                AccumulatedActionGameBets = accumulatedActionGameBets,
                LosingSpinsAfterFeature = losingSpinsAfterFeature,
                LastFeatureExitType = lastFeatureExitType,
                MysteryPrizeTrigger = mysteryPrizeTrigger
            };
            
            // Update session
            session.Balance = newBalance;
            session.LastWin = baseWin;
            if (session.LastResponse == null)
            {
                session.LastResponse = new GameState();
            }
            session.LastResponse.Balance = newBalance;
            session.LastResponse.LastWin = baseWin;
            session.LastResponse.Results = spinResult;
            session.LastResponse.ActionGameSpins = newActionGameSpins;
            session.LastResponse.AccumulatedActionGameWin = accumulatedActionWin;
            session.LastResponse.AccumulatedPennyGameBets = accumulatedPennyGameBets;
            session.LastResponse.AccumulatedActionGameBets = accumulatedActionGameBets;
            session.LastResponse.LosingSpinsAfterFeature = losingSpinsAfterFeature;
            session.LastResponse.LastFeatureExitType = lastFeatureExitType;
            session.LastResponse.MysteryPrizeTrigger = mysteryPrizeTrigger;
            
            await sessionService.UpdateSessionAsync(session);
            sessionService.UpdateSession(request.SessionId, newState);
            
            var response = new PlayResponse
            {
                SessionId = request.SessionId,
                Player = newState,
                Game = newState,
                FreeSpins = currentState.FreeSpinsRemaining,
                ActionGameSpins = newActionGameSpins,
                FeatureSymbol = currentState.FeatureSymbol ?? "",
                MysteryPrizeAwarded = 0,
                AccumulatedPennyGameBets = accumulatedPennyGameBets,
                AccumulatedActionGameBets = accumulatedActionGameBets
            };
            
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEV MODE] Error triggering action games: {ex.Message}");
            return Results.Problem($"Error: {ex.Message}");
        }
    });
}

app.MapDefaultEndpoints();

app.Run();

// Dev mode request model
record DevTriggerFreeSpinsRequest(string SessionId, decimal BetAmount = 0, int NumPaylines = 0, decimal BetPerPayline = 0, string? GameId = null);
record DevTriggerActionGamesRequest(string SessionId, decimal BetAmount = 0, int NumPaylines = 0, decimal BetPerPayline = 0, string? GameId = null);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
