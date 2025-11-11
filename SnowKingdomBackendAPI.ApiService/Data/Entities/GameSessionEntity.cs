using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SnowKingdomBackendAPI.ApiService.Data.Entities;

/// <summary>
/// Database entity for a game session.
/// Persists player session data including balance, free spins, and game state.
/// </summary>
[Table("GameSessions")]
public class GameSessionEntity
{
    /// <summary>
    /// Primary key (auto-increment).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Unique session identifier.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Player identifier.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Operator identifier.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string OperatorId { get; set; } = string.Empty;

    /// <summary>
    /// Game identifier (e.g., "SnowKingdom", "Example5x3").
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Current player balance.
    /// </summary>
    [Required]
    public decimal Balance { get; set; }

    /// <summary>
    /// Number of free spins remaining.
    /// </summary>
    [Required]
    public int FreeSpinsRemaining { get; set; }

    /// <summary>
    /// Last win amount.
    /// </summary>
    [Required]
    public decimal LastWin { get; set; }

    /// <summary>
    /// Total wins accumulated during free spins.
    /// </summary>
    [Required]
    public decimal FreeSpinsTotalWin { get; set; }

    /// <summary>
    /// Last game state response as JSON string (nullable).
    /// </summary>
    public string? LastResponseJson { get; set; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the session was last updated.
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; }
}

