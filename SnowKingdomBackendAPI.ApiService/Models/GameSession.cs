using SnowKingdomBackendAPI.ApiService.Models;

namespace SnowKingdomBackendAPI.ApiService.Models;

public class GameSession
{
    public string SessionId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string OperatorId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public int FreeSpinsRemaining { get; set; }
    public decimal LastWin { get; set; }
    public decimal FreeSpinsTotalWin { get; set; } // Track total wins accumulated during free spins
    public GameState? LastResponse { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
