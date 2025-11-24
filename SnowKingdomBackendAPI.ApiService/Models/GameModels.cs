using System.Text.Json.Serialization;

namespace SnowKingdomBackendAPI.ApiService.Models;

public class SymbolConfig
{
    public string Name { get; set; } = string.Empty;
    // Legacy: simple multiplier-based payout (for backward compatibility)
    public Dictionary<int, decimal> Payout { get; set; } = new();
    // New: bet-specific payouts (key: bet amount in Rands as string, value: count -> payout in Rands)
    [System.Text.Json.Serialization.JsonPropertyName("payoutByBet")]
    public Dictionary<string, Dictionary<int, decimal>>? PayoutByBet { get; set; }
    // New: bet-specific action games (key: bet amount in Rands as string, value: count -> action games)
    [System.Text.Json.Serialization.JsonPropertyName("actionGamesByBet")]
    public Dictionary<string, Dictionary<int, int>>? ActionGamesByBet { get; set; }
    public string Image { get; set; } = string.Empty;
}

public class WinningLine
{
    public int PaylineIndex { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Payout { get; set; }
    public List<int> Line { get; set; } = new();
}

public class ScatterWin
{
    public int Count { get; set; }
    public bool TriggeredFreeSpins { get; set; }
}

public class ActionGameTrigger
{
    public string Symbol { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal BaseWin { get; set; }
    public int ActionSpins { get; set; }
}

public class ActionGameResult
{
    public decimal Win { get; set; }
    public int AdditionalSpins { get; set; }
    public string WheelResult { get; set; } = string.Empty;
    public int SegmentIndex { get; set; } = -1; // 0-11, indicates which specific segment was selected
}

public class FreeSpinState
{
    public string FeatureSymbol { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Remaining { get; set; }
}

public class ExpandedSymbolPosition
{
    public int Reel { get; set; }
    public int Row { get; set; }
}

public class SpinResult
{
    public decimal TotalWin { get; set; }
    public List<WinningLine> WinningLines { get; set; } = new();
    public ScatterWin ScatterWin { get; set; } = new();
    public List<List<string>> Grid { get; set; } = new();
    
    // Book of Ra specific fields
    public bool ActionGameTriggered { get; set; }
    public int ActionGameSpins { get; set; }
    public decimal ActionGameWin { get; set; }
    public string FeatureSymbol { get; set; } = string.Empty;
    public List<ExpandedSymbolPosition> ExpandedSymbols { get; set; } = new();
    public decimal ExpandedWin { get; set; }
    public List<WinningLine> FeatureGameWinningLines { get; set; } = new();
}

public class PlayRequest
{
    public string SessionId { get; set; } = string.Empty;
    public decimal BetAmount { get; set; }
    public GameState? LastResponse { get; set; }
    public string? GameId { get; set; } // Optional: allows RGS to specify which game config to use
    
    // Book of Ra specific fields
    public int NumPaylines { get; set; } = 1;
    public decimal BetPerPayline { get; set; } = 1;
    public int ActionGameSpins { get; set; } = 0;
}

public class GameState
{
    public decimal Balance { get; set; }
    public int FreeSpinsRemaining { get; set; }
    public decimal LastWin { get; set; }
    public SpinResult Results { get; set; } = new();
    
    // Book of Ra specific fields
    public int ActionGameSpins { get; set; } = 0;
    public string FeatureSymbol { get; set; } = string.Empty;
    public decimal AccumulatedActionGameWin { get; set; } = 0;
    
    // Penny games and action games R0.10 deduction tracking
    public decimal AccumulatedPennyGameBets { get; set; } = 0;
    public decimal AccumulatedActionGameBets { get; set; } = 0;
    public int LosingSpinsAfterFeature { get; set; } = 0;
    public string? LastFeatureExitType { get; set; } = null; // "freeSpins" or "actionGames"
    public int? MysteryPrizeTrigger { get; set; } = null; // Random trigger value (2-5) set when first reaching 2 losing spins
}

public class PlayResponse
{
    public string SessionId { get; set; } = string.Empty;
    public GameState Player { get; set; } = new();
    public GameState Game { get; set; } = new();
    public int FreeSpins { get; set; }
    
    // Book of Ra specific fields
    public int ActionGameSpins { get; set; } = 0;
    public string FeatureSymbol { get; set; } = string.Empty;
    
    // Mystery prize fields
    public decimal MysteryPrizeAwarded { get; set; } = 0;
    public decimal AccumulatedPennyGameBets { get; set; } = 0;
    public decimal AccumulatedActionGameBets { get; set; } = 0;
}

public class ActionGameSpinRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public class ActionGameSpinResponse
{
    public string SessionId { get; set; } = string.Empty;
    public ActionGameResult Result { get; set; } = new();
    public int RemainingSpins { get; set; }
    public decimal AccumulatedWin { get; set; } = 0;
    public int TotalActionSpins { get; set; } = 0;
    public decimal Balance { get; set; } = 0;
}

public static class GameConstants
{
    public const int NumReels = 6;
    public const int NumRows = 4;
    public static readonly int[] BetAmounts = [1, 2, 3, 5];
    public const int FreeSpinsAwarded = 10;

    public static readonly List<List<int>> Paylines =
[
    [0, 0, 0, 0, 0, 0], // Line 1: Top row (was Line 3)
    [1, 1, 1, 1, 1, 1], // Line 2: Second row (was Line 1)
    [2, 2, 2, 2, 2, 2], // Line 3: Third row (was Line 2)
    [3, 3, 3, 3, 3, 3], // Line 4: Bottom row (unchanged)
    [1, 0, 0, 0, 0, 1], // Line 5 (unchanged)
    [2, 3, 3, 3, 3, 2], // Line 6 (unchanged)
    [2, 1, 2, 1, 2, 1], // Line 7 (unchanged)
    [1, 2, 1, 2, 1, 2], // Line 8 (unchanged)
    [0, 1, 0, 1, 0, 1], // Line 9 (unchanged)
    [3, 2, 3, 2, 3, 2]  // Line 10 (unchanged)
];
}
