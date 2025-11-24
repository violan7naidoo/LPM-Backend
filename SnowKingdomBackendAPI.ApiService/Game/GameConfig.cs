using SnowKingdomBackendAPI.ApiService.Models;

namespace SnowKingdomBackendAPI.ApiService.Game;

public class GameConfig
{
    public string GameName { get; set; } = string.Empty;
    public int NumReels { get; set; }
    public int NumRows { get; set; }
    public Dictionary<string, SymbolConfig> Symbols { get; set; } = new();
    
    // Legacy fields (for backward compatibility)
    public string WildSymbol { get; set; } = "WILD";
    public string ScatterSymbol { get; set; } = "SCATTER";
    
    // Book of Ra specific fields
    public string? BookSymbol { get; set; } // Combined wild/scatter symbol (null if using separate wild/scatter)
    public int MaxPaylines { get; set; } = 10;
    public Dictionary<string, ActionGameTrigger> ActionGameTriggers { get; set; } = new();
    public Dictionary<string, int> ActionGameWheel { get; set; } = new(); // Key: "0", "R10", "6spins", Value: probability weight
    
    public List<List<string>> ReelStrips { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonPropertyName("featureReelStrips")]
    public Dictionary<string, List<List<string>>> FeatureReelStrips { get; set; } = new();
    
    public List<List<int>> Paylines { get; set; } = new();
    // Legacy: simple multiplier-based scatter payout (for backward compatibility)
    public Dictionary<int, decimal> ScatterPayout { get; set; } = new();
    // New: bet-specific scatter payouts (key: bet amount in Rands as string, value: count -> payout in Rands)
    [System.Text.Json.Serialization.JsonPropertyName("scatterPayoutByBet")]
    public Dictionary<string, Dictionary<int, decimal>>? ScatterPayoutByBet { get; set; }
    // New: bet-specific scatter action games (key: bet amount in Rands as string, value: count -> action games)
    [System.Text.Json.Serialization.JsonPropertyName("scatterActionGamesByBet")]
    public Dictionary<string, Dictionary<int, int>>? ScatterActionGamesByBet { get; set; }
    public int FreeSpinsAwarded { get; set; }
    public decimal[] BetAmounts { get; set; } = Array.Empty<decimal>();
    
    // Helper method to get the wild symbol (BookSymbol or WildSymbol)
    public string GetWildSymbol()
    {
        return BookSymbol ?? WildSymbol;
    }
    
    // Helper method to get the scatter symbol (BookSymbol or ScatterSymbol)
    public string GetScatterSymbol()
    {
        return BookSymbol ?? ScatterSymbol;
    }
    
    // Check if using Book of Ra style (combined wild/scatter)
    public bool IsBookOfRaStyle()
    {
        return !string.IsNullOrEmpty(BookSymbol);
    }
}

