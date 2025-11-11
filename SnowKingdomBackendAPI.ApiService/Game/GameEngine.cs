using SnowKingdomBackendAPI.ApiService.Models;

namespace SnowKingdomBackendAPI.ApiService.Game;

public class GameEngine
{
    private readonly Random _random = new();
    private readonly GameConfig _config;

    public GameEngine(GameConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public SpinResult EvaluateSpin(List<List<string>> grid, decimal betAmount, int numPaylines = 0, bool isFreeSpin = false, string? featureSymbol = null, decimal? totalBet = null)
    {
        var result = new SpinResult();
        result.Grid = grid;

        // Determine active paylines (if numPaylines is 0, use all paylines for backward compatibility)
        var activePaylines = numPaylines > 0 
            ? _config.Paylines.Take(Math.Min(numPaylines, _config.Paylines.Count)).ToList()
            : _config.Paylines;

        var wildSymbol = _config.GetWildSymbol();
        var scatterSymbol = _config.GetScatterSymbol();
        var isBookOfRaStyle = _config.IsBookOfRaStyle();
        var bookSymbol = _config.BookSymbol ?? wildSymbol; // Fallback to wildSymbol if BookSymbol is null

        // In free spins: evaluate base game first, then feature game after expansion
        var expandedGrid = grid;
        var hasFeatureSymbol = false;
        var featureSymbolCount = 0;
        if (isFreeSpin && !string.IsNullOrEmpty(featureSymbol))
        {
            // Count how many reels contain the feature symbol
            var reelsWithFeature = new HashSet<int>();
            for (int reelIndex = 0; reelIndex < grid.Count; reelIndex++)
            {
                if (grid[reelIndex].Contains(featureSymbol))
                {
                    reelsWithFeature.Add(reelIndex);
                }
            }
            featureSymbolCount = reelsWithFeature.Count;
            hasFeatureSymbol = featureSymbolCount >= 3; // Only expand if 3+ reels have feature symbol
            
            if (hasFeatureSymbol)
            {
                expandedGrid = ExpandFeatureSymbol(grid, featureSymbol, reelsWithFeature);
            }
            result.FeatureSymbol = featureSymbol;
        }

        // 1. Evaluate Base Game Paylines (using original grid, excluding feature symbol wins)
        // In free spins, base game pays out normally for all wins EXCEPT feature symbol wins
        var gridToUse = grid; // Use original grid for base game evaluation
        for (int paylineIndex = 0; paylineIndex < activePaylines.Count; paylineIndex++)
        {
            var line = activePaylines[paylineIndex];
            var lineSymbols = line.Select((row, reel) => gridToUse[reel][row]).ToList();

            // Determine the winning symbol: start from the leftmost reel
            // The first symbol (or wild/scatter if it can substitute) determines what we're looking for
            string? winningSymbol = null;
            if (isBookOfRaStyle)
            {
                // Book of Ra: Book symbol (Scatter) can substitute for other symbols
                // Start from left, first non-book symbol is the winning symbol
                // If first symbol is book, it can substitute for the next symbol
                for (int i = 0; i < lineSymbols.Count; i++)
                {
                    if (lineSymbols[i] != bookSymbol)
                    {
                        winningSymbol = lineSymbols[i];
                        break;
                    }
                }
                // If all are book symbols, then book is the winning symbol
                if (winningSymbol == null)
                {
                    winningSymbol = bookSymbol;
                }
            }
            else
            {
                // Traditional: first non-wild symbol
                winningSymbol = lineSymbols.FirstOrDefault(s => s != wildSymbol);
                if (winningSymbol == null)
                {
                    winningSymbol = wildSymbol; // All wilds
                }
            }

            // Debug: Log the payline evaluation
            Console.WriteLine($"[PAYLINE DEBUG] Payline {paylineIndex}: Symbols=[{string.Join(", ", lineSymbols)}], WinningSymbol={winningSymbol}, BookSymbol={bookSymbol}, IsFreeSpin={isFreeSpin}, FeatureSymbol={featureSymbol}");

            // Count consecutive matching symbols from the left
            // Wild/Book can substitute for the winning symbol (except in free spins for scatter)
            var count = 0;
            while (count < lineSymbols.Count)
            {
                var currentSymbol = lineSymbols[count];
                
                // Check if this position matches the winning symbol
                bool matches = false;
                
                if (currentSymbol == winningSymbol)
                {
                    matches = true;
                }
                else if (isBookOfRaStyle && currentSymbol == bookSymbol)
                {
                    // Book symbol (Scatter) can substitute for winning symbol in base game
                    // In free spins: only substitute if the feature symbol is NOT scatter
                    if (!isFreeSpin || (isFreeSpin && featureSymbol != bookSymbol))
                    {
                        matches = true;
                    }
                }
                else if (!isBookOfRaStyle && currentSymbol == wildSymbol)
                {
                    // Wild symbol can substitute (but NOT in free spins)
                    if (!isFreeSpin)
                    {
                        matches = true;
                    }
                }
                
                if (matches)
                {
                    count++;
                }
                else
                {
                    // Stop counting when we hit a non-matching symbol
                    break;
                }
            }

            // Debug: Log the count
            Console.WriteLine($"[PAYLINE DEBUG] Payline {paylineIndex}: Count={count}, WinningSymbol={winningSymbol}");

            // Check for a win based on the count and the determined winning symbol
            // In free spins base game: exclude feature symbol wins (they'll be in feature game)
            // Note: Some symbols (Leopard, Wolf, Stone, Queen) can win with 2+ symbols
            if (count >= 2 && _config.Symbols.TryGetValue(winningSymbol, out var symbolInfo))
            {
                // Skip feature symbol wins in base game (they'll be calculated in feature game)
                if (isFreeSpin && !string.IsNullOrEmpty(featureSymbol) && winningSymbol == featureSymbol)
                {
                    continue; // Skip this win, it will be in feature game
                }

                // Get payout from bet-specific configuration
                decimal totalPayout = 0;
                int actionGames = 0;
                
                // For bet-specific payouts, we MUST use the TOTAL bet amount (R1, R2, R3, R5), not per-payline
                // If totalBet is not provided, this is an error - we cannot calculate payouts
                if (totalBet == null || totalBet <= 0)
                {
                    Console.WriteLine($"[PAYOUT ERROR] TotalBet is null or 0! Cannot lookup bet-specific payouts. Symbol: {winningSymbol}, Count: {count}");
                    // Do not calculate payout - this is a configuration error
                }
                else
                {
                    // Format bet key as "1.00", "2.00", "3.00", "5.00" using invariant culture (always use dot, not comma)
                    var betKey = totalBet.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    
                    // Debug: Log the lookup attempt
                    Console.WriteLine($"[PAYOUT DEBUG] Symbol: {winningSymbol}, Count: {count}, BetKey: '{betKey}', TotalBet: {totalBet}, BetAmount: {betAmount}");
                    
                    // Check if PayoutByBet exists and has keys
                    if (symbolInfo.PayoutByBet != null && symbolInfo.PayoutByBet.Count > 0)
                    {
                        var availableBetKeys = string.Join(", ", symbolInfo.PayoutByBet.Keys);
                        Console.WriteLine($"[PAYOUT DEBUG] PayoutByBet exists with keys: [{availableBetKeys}]");
                        
                        if (symbolInfo.PayoutByBet.TryGetValue(betKey, out var betPayouts))
                        {
                            var availableCounts = string.Join(", ", betPayouts.Keys.Select(k => k.ToString()));
                            Console.WriteLine($"[PAYOUT DEBUG] Found bet key '{betKey}', available counts: [{availableCounts}], looking for count: {count}");
                            
                            if (betPayouts.TryGetValue(count, out var payoutRands))
            {
                                // Use bet-specific absolute payout (in Rands)
                                totalPayout = payoutRands;
                                Console.WriteLine($"[PAYOUT DEBUG] ✓ Found bet-specific payout: R{totalPayout} for {winningSymbol} x{count} at bet {betKey}");
                                
                                // Check for action games
                                if (symbolInfo.ActionGamesByBet != null && symbolInfo.ActionGamesByBet.TryGetValue(betKey, out var betActionGames) &&
                                    betActionGames.TryGetValue(count, out var actionGameCount))
                                {
                                    actionGames = actionGameCount;
                                    Console.WriteLine($"[PAYOUT DEBUG] ✓ Action games awarded: {actionGames} for {winningSymbol} x{count} at bet {betKey}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[PAYOUT DEBUG] ✗ Count {count} not found in betPayouts for key '{betKey}' (available: [{availableCounts}])");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[PAYOUT DEBUG] ✗ Bet key '{betKey}' not found in PayoutByBet (available: [{availableBetKeys}])");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[PAYOUT DEBUG] ✗ PayoutByBet is null or empty for symbol {winningSymbol}");
                    }
                    
                    // NO FALLBACK - if bet-specific payout lookup fails, it's a configuration error
                    if (totalPayout == 0)
                    {
                        Console.WriteLine($"[PAYOUT ERROR] No payout found for {winningSymbol} x{count} at bet {betKey}. Check configuration!");
                    }
                }

                if (totalPayout > 0)
                {
                    result.TotalWin += totalPayout;
                    result.WinningLines.Add(new WinningLine
                    {
                        PaylineIndex = paylineIndex,
                        Symbol = winningSymbol,
                        Count = count,
                        Payout = totalPayout,
                        Line = line.ToList() // Use full payline (5 elements), not just the winning count
                    });
                    
                    // Add action games if any
                    if (actionGames > 0)
                    {
                        result.ActionGameTriggered = true;
                        result.ActionGameSpins += actionGames;
                        // Note: payout is already in totalPayout (added to TotalWin above)
                        // ActionGameWin is only for legacy action game triggers, not payout-based action games
                    }
                }
            }
        }

        // 2. Evaluate Scatters/Book Symbol (base game only)
        // In free spins: scatter only appears in base game if feature symbol is NOT scatter
        var scatterCount = 0;
        var scatterPositions = new List<ExpandedSymbolPosition>();

        for (int reelIndex = 0; reelIndex < grid.Count; reelIndex++)
        {
            for (int rowIndex = 0; rowIndex < grid[reelIndex].Count; rowIndex++)
            {
                if (grid[reelIndex][rowIndex] == scatterSymbol)
                {
                    scatterCount++;
                    scatterPositions.Add(new ExpandedSymbolPosition { Reel = reelIndex, Row = rowIndex });
                }
            }
        }

        result.ScatterWin.Count = scatterCount;
        // Scatter can trigger free spins if:
        // - Not in free spins, OR
        // - In free spins but feature symbol is NOT scatter (can retrigger)
        if (isFreeSpin && !string.IsNullOrEmpty(featureSymbol))
        {
            // In free spins, scatter can trigger more free spins only if feature symbol is NOT scatter
            result.ScatterWin.TriggeredFreeSpins = scatterCount >= 3 && featureSymbol != scatterSymbol && featureSymbol != bookSymbol;
        }
        else
        {
        result.ScatterWin.TriggeredFreeSpins = scatterCount >= 3;
        }

        // Only add scatter win to base game if:
        // - Not in free spins, OR
        // - In free spins but feature symbol is NOT scatter
        if (scatterCount >= 3 && (!isFreeSpin || (isFreeSpin && featureSymbol != scatterSymbol && featureSymbol != bookSymbol)))
        {
            // Get scatter payout from bet-specific configuration
            decimal scatterPayout = 0;
            int scatterActionGames = 0;
            
            // For bet-specific payouts, we MUST use the TOTAL bet amount (R1, R2, R3, R5), not per-payline
            if (totalBet == null || totalBet <= 0)
            {
                Console.WriteLine($"[SCATTER PAYOUT ERROR] TotalBet is null or 0! Cannot lookup bet-specific scatter payouts. Count: {scatterCount}");
                // Do not calculate payout - this is a configuration error
            }
            else
            {
                var betKey = totalBet.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); // Format as "1.00", "2.00", etc. (always use dot)
                Console.WriteLine($"[SCATTER PAYOUT DEBUG] ScatterCount: {scatterCount}, BetKey: '{betKey}', TotalBet: {totalBet}");
                
                if (_config.ScatterPayoutByBet != null && _config.ScatterPayoutByBet.TryGetValue(betKey, out var betScatterPayouts) &&
                    betScatterPayouts.TryGetValue(scatterCount, out var payoutRands))
                {
                    // Use bet-specific absolute payout (in Rands)
                    scatterPayout = payoutRands;
                    Console.WriteLine($"[SCATTER PAYOUT DEBUG] ✓ Found bet-specific scatter payout: R{scatterPayout} for {scatterCount} scatters at bet {betKey}");
                    
                    // Check for scatter action games
                    if (_config.ScatterActionGamesByBet != null && _config.ScatterActionGamesByBet.TryGetValue(betKey, out var betScatterActionGames) &&
                        betScatterActionGames.TryGetValue(scatterCount, out var actionGameCount))
                    {
                        scatterActionGames = actionGameCount;
                        Console.WriteLine($"[SCATTER PAYOUT DEBUG] ✓ Scatter action games awarded: {scatterActionGames} for {scatterCount} scatters at bet {betKey}");
                    }
                }
                else
                {
                    Console.WriteLine($"[SCATTER PAYOUT ERROR] No scatter payout found for {scatterCount} scatters at bet {betKey}. Check configuration!");
                }
            }

            if (scatterPayout > 0)
            {
                result.TotalWin += scatterPayout;

                result.WinningLines.Add(new WinningLine
                {
                    PaylineIndex = -1, // Special index for scatters
                    Symbol = scatterSymbol,
                    Count = scatterCount,
                    Payout = scatterPayout,
                    Line = scatterPositions.Select(p => p.Row).ToList()
                });
                
                // Add scatter action games if any
                if (scatterActionGames > 0)
                {
                    result.ActionGameTriggered = true;
                    result.ActionGameSpins += scatterActionGames;
                    // Note: payout is already in scatterPayout (added to TotalWin above)
                    // ActionGameWin is only for legacy action game triggers, not payout-based action games
                }
            }
        }

        // 3. Check for Action Game Triggers
        CheckActionGameTriggers(grid, betAmount, result);

        // 4. Feature Game: Evaluate wins after expansion (only feature symbol wins)
        // Feature symbol does NOT act as wild - it only expands
        // Only expand if 3+ feature symbols appear on 3+ different reels (can't win with less)
        if (isFreeSpin && !string.IsNullOrEmpty(featureSymbol) && hasFeatureSymbol && featureSymbolCount >= 3 && expandedGrid != grid)
        {
            Console.WriteLine($"[FREE SPINS] Feature symbol expansion triggered: {featureSymbolCount} reels have {featureSymbol}");
            result.ExpandedSymbols = GetExpandedPositions(grid, expandedGrid, featureSymbol);
            Console.WriteLine($"[FREE SPINS] Expanded symbols count: {result.ExpandedSymbols.Count}");
            
            // Calculate feature game wins: if 3+ reels expanded, win on ALL lines
            // Pass totalBet for bet-specific payout lookup (bet keys are total bet amounts, not per-payline)
            var featureGameResult = CalculateFeatureGameWins(expandedGrid, activePaylines, betAmount, featureSymbol, totalBet);
            result.ExpandedWin = featureGameResult.TotalWin;
            result.FeatureGameWinningLines = featureGameResult.WinningLines;
            Console.WriteLine($"[FREE SPINS] Feature game win: R{result.ExpandedWin}, Winning lines: {result.FeatureGameWinningLines.Count}");
        }
        else if (isFreeSpin && !string.IsNullOrEmpty(featureSymbol))
        {
            Console.WriteLine($"[FREE SPINS] No expansion: hasFeatureSymbol={hasFeatureSymbol}, featureSymbolCount={featureSymbolCount}, expandedGrid==grid={expandedGrid == grid}");
        }

        return result;
    }

    private List<List<string>> ExpandFeatureSymbol(List<List<string>> grid, string featureSymbol, HashSet<int> reelsToExpand)
    {
        // Grid structure: grid[reelIndex][rowIndex]
        // - reelIndex: 0-4 (5 reels/columns from left to right)
        // - rowIndex: 0-2 (3 rows from top to bottom)
        var expandedGrid = grid.Select(reel => reel.ToList()).ToList();

        // Expand only the reels that contain the feature symbol (3+ reels)
        // When a reel expands, fill the entire column (all 3 rows) with the feature symbol
        foreach (var reelIndex in reelsToExpand)
            {
                for (int rowIndex = 0; rowIndex < expandedGrid[reelIndex].Count; rowIndex++)
                {
                    expandedGrid[reelIndex][rowIndex] = featureSymbol;
            }
        }

        return expandedGrid;
    }

    private List<ExpandedSymbolPosition> GetExpandedPositions(List<List<string>> originalGrid, List<List<string>> expandedGrid, string featureSymbol)
    {
        var expandedPositions = new List<ExpandedSymbolPosition>();

        for (int reelIndex = 0; reelIndex < originalGrid.Count; reelIndex++)
        {
            for (int rowIndex = 0; rowIndex < originalGrid[reelIndex].Count; rowIndex++)
            {
                if (originalGrid[reelIndex][rowIndex] != featureSymbol && 
                    expandedGrid[reelIndex][rowIndex] == featureSymbol)
                {
                    expandedPositions.Add(new ExpandedSymbolPosition { Reel = reelIndex, Row = rowIndex });
                }
            }
        }

        return expandedPositions;
    }

    private (decimal TotalWin, List<WinningLine> WinningLines) CalculateFeatureGameWins(List<List<string>> expandedGrid, List<List<int>> activePaylines, decimal betAmount, string featureSymbol, decimal? totalBet = null)
    {
        // Feature game: if 3+ reels expanded, win on ALL lines as if they were consecutive
        // Count how many reels are expanded (filled with feature symbol)
        var expandedReelCount = 0;
        for (int reelIndex = 0; reelIndex < expandedGrid.Count; reelIndex++)
            {
            if (expandedGrid[reelIndex].All(s => s == featureSymbol))
                {
                expandedReelCount++;
                }
            }

        decimal featureWin = 0;
        var featureWinningLines = new List<WinningLine>();

        // If 3+ reels expanded, pay out on ALL active paylines using the actual expanded reel count
        if (expandedReelCount >= 3 && _config.Symbols.TryGetValue(featureSymbol, out var symbolInfo))
        {
            // Get payout for the actual expanded reel count (e.g., 3-of-a-kind, not 5-of-a-kind)
            decimal totalPayout = 0;
            
            // For bet-specific payouts, we MUST use the TOTAL bet amount (R1, R2, R3, R5), not per-payline
            if (totalBet == null || totalBet <= 0)
                {
                Console.WriteLine($"[FEATURE GAME PAYOUT ERROR] TotalBet is null or 0! Cannot lookup bet-specific payouts. Symbol: {featureSymbol}, ExpandedReelCount: {expandedReelCount}");
                // Do not calculate payout - this is a configuration error
            }
            else
            {
                var betKey = totalBet.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); // Format as "1.00", "2.00", etc. (always use dot)
                
                // Debug: Log the lookup attempt
                Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] Symbol: {featureSymbol}, ExpandedReelCount: {expandedReelCount}, BetKey: '{betKey}', TotalBet: {totalBet}, BetAmount: {betAmount}");
                
                if (symbolInfo.PayoutByBet != null && symbolInfo.PayoutByBet.Count > 0)
                {
                    var availableBetKeys = string.Join(", ", symbolInfo.PayoutByBet.Keys);
                    Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] PayoutByBet exists with keys: [{availableBetKeys}]");
                    
                    if (symbolInfo.PayoutByBet.TryGetValue(betKey, out var betPayouts))
            {
                        var availableCounts = string.Join(", ", betPayouts.Keys.Select(k => k.ToString()));
                        Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] Found bet key '{betKey}', available counts: [{availableCounts}], looking for count: {expandedReelCount}");
                        
                        if (betPayouts.TryGetValue(expandedReelCount, out var payoutRands))
                        {
                            // Use bet-specific absolute payout (in Rands)
                            totalPayout = payoutRands;
                            Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] ✓ Found bet-specific payout: R{totalPayout} for {featureSymbol} x{expandedReelCount} at bet {betKey}");
                        }
                        else
                        {
                            Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] ✗ Count {expandedReelCount} not found in betPayouts for key '{betKey}' (available: [{availableCounts}])");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] ✗ Bet key '{betKey}' not found in PayoutByBet (available: [{availableBetKeys}])");
                    }
                }
                else
                {
                    Console.WriteLine($"[FEATURE GAME PAYOUT DEBUG] ✗ PayoutByBet is null or empty for symbol {featureSymbol}");
                }
                
                // NO FALLBACK - if bet-specific payout lookup fails, it's a configuration error
                if (totalPayout == 0)
                {
                    Console.WriteLine($"[FEATURE GAME PAYOUT ERROR] No payout found for {featureSymbol} x{expandedReelCount} at bet {betKey}. Check configuration!");
                }
            }
            
            if (totalPayout > 0)
            {
                // Pay out on ALL 5 paylines: (payout for expanded reel count) × 5 paylines
                // Example: 3 expanded reels, R70 payout for 3-of-a-kind = R70 × 5 = R350 total
                var payoutPerPayline = totalPayout; // The payout from config is per payline
                var totalFeatureWin = payoutPerPayline * activePaylines.Count; // Multiply by number of paylines (5)
                
                Console.WriteLine($"[FEATURE GAME PAYOUT] {expandedReelCount} expanded reels, payout per payline: R{payoutPerPayline}, total win: R{totalFeatureWin} (×{activePaylines.Count} paylines)");
                
                featureWin = totalFeatureWin;
                
                // Create winning lines for all paylines
                for (int paylineIndex = 0; paylineIndex < activePaylines.Count; paylineIndex++)
            {
                    featureWinningLines.Add(new WinningLine
                    {
                        PaylineIndex = paylineIndex,
                        Symbol = featureSymbol,
                        Count = expandedReelCount, // Use actual expanded reel count (e.g., 3, not 5)
                        Payout = payoutPerPayline, // Payout per payline
                        Line = activePaylines[paylineIndex].ToList()
                    });
                }
            }
        }

        return (featureWin, featureWinningLines);
    }

    private void CheckActionGameTriggers(List<List<string>> grid, decimal betAmount, SpinResult result)
    {
        if (_config.ActionGameTriggers == null || _config.ActionGameTriggers.Count == 0)
        {
            return;
        }

        // Count symbols in grid
        var symbolCounts = new Dictionary<string, int>();
        foreach (var reel in grid)
        {
            foreach (var symbol in reel)
            {
                if (!symbolCounts.ContainsKey(symbol))
                {
                    symbolCounts[symbol] = 0;
                }
                symbolCounts[symbol]++;
            }
        }

        // Check each action game trigger
        foreach (var trigger in _config.ActionGameTriggers.Values)
        {
            if (symbolCounts.TryGetValue(trigger.Symbol, out var count) && count >= trigger.Count)
            {
                result.ActionGameTriggered = true;
                result.ActionGameSpins = trigger.ActionSpins;
                result.ActionGameWin = trigger.BaseWin * betAmount;
                result.TotalWin += result.ActionGameWin;
                break; // Only trigger one action game per spin
            }
        }
    }

    public string SelectFeatureSymbol()
    {
        // Select a random symbol (excluding Book symbol and low-value card symbols for better gameplay)
        var bookSymbol = _config.BookSymbol ?? "";
        var eligibleSymbols = _config.Symbols.Keys
            .Where(s => s != bookSymbol && 
                       s != "A" && s != "K" && s != "Q" && s != "J" && s != "10")
            .ToList();

        if (eligibleSymbols.Count == 0)
        {
            // Fallback to all symbols except Book
            eligibleSymbols = _config.Symbols.Keys
                .Where(s => s != bookSymbol)
                .ToList();
        }

        if (eligibleSymbols.Count == 0)
        {
            return _config.Symbols.Keys.FirstOrDefault() ?? "";
        }

        return eligibleSymbols[_random.Next(eligibleSymbols.Count)];
    }

    public ActionGameResult SpinActionGameWheel()
    {
        if (_config.ActionGameWheel == null || _config.ActionGameWheel.Count == 0)
        {
            // Default wheel if not configured
            return new ActionGameResult
            {
                Win = 0,
                AdditionalSpins = 0,
                WheelResult = "0"
            };
        }

        // Calculate total weight
        var totalWeight = _config.ActionGameWheel.Values.Sum();

        if (totalWeight == 0)
        {
            return new ActionGameResult
            {
                Win = 0,
                AdditionalSpins = 0,
                WheelResult = "0"
            };
        }

        // Spin the wheel
        var randomValue = _random.Next(totalWeight);
        var currentWeight = 0;

        foreach (var outcome in _config.ActionGameWheel)
        {
            currentWeight += outcome.Value;
            if (randomValue < currentWeight)
            {
                // Parse the outcome
                var result = new ActionGameResult
                {
                    WheelResult = outcome.Key
                };

                if (outcome.Key == "R10")
                {
                    result.Win = 10.00m;
                }
                else if (outcome.Key == "6spins")
                {
                    result.AdditionalSpins = 6;
                }
                else
                {
                    result.Win = 0;
                }

                return result;
            }
        }

        // Fallback
        return new ActionGameResult
        {
            Win = 0,
            AdditionalSpins = 0,
            WheelResult = "0"
        };
    }

    public List<List<string>> GenerateGrid()
    {
        var grid = new List<List<string>>();

        for (int reelIndex = 0; reelIndex < _config.NumReels; reelIndex++)
        {
            var reel = new List<string>();
            var strip = _config.ReelStrips[reelIndex];
            var finalStopIndex = _random.Next(strip.Count);

            for (int rowIndex = 0; rowIndex < _config.NumRows; rowIndex++)
            {
                var symbolIndex = (finalStopIndex + rowIndex) % strip.Count;
                var symbolName = strip[symbolIndex];
                reel.Add(symbolName);
            }

            grid.Add(reel);
        }

        return grid;
    }
}
