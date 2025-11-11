using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SnowKingdomBackendAPI.ApiService.Data.Entities;

/// <summary>
/// Database entity for a spin transaction.
/// Tracks all spins for auditability and analysis.
/// </summary>
[Table("SpinTransactions")]
public class SpinTransactionEntity
{
    /// <summary>
    /// Primary key (auto-increment).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Session identifier (indexed for fast lookups).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Game identifier.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Bet amount for this spin (in Rands).
    /// </summary>
    [Required]
    public decimal BetAmount { get; set; }

    /// <summary>
    /// Win amount for this spin (in Rands).
    /// </summary>
    [Required]
    public decimal WinAmount { get; set; }

    /// <summary>
    /// Total win (same as WinAmount, kept for consistency) (in Rands).
    /// </summary>
    [Required]
    public decimal TotalWin { get; set; }

    /// <summary>
    /// Number of free spins awarded (0 if none).
    /// </summary>
    [Required]
    public int FreeSpinsAwarded { get; set; }

    /// <summary>
    /// Whether this was a free spin (true) or paid spin (false).
    /// </summary>
    [Required]
    public bool IsFreeSpin { get; set; }

    /// <summary>
    /// Game grid result as JSON string (2D array of symbol names).
    /// </summary>
    [Required]
    public string GridJson { get; set; } = string.Empty;

    /// <summary>
    /// Winning lines as JSON string (array of WinningLine objects).
    /// </summary>
    [Required]
    public string WinningLinesJson { get; set; } = string.Empty;

    /// <summary>
    /// Scatter win information as JSON string.
    /// </summary>
    [Required]
    public string ScatterWinJson { get; set; } = string.Empty;

    /// <summary>
    /// When the spin occurred.
    /// </summary>
    [Required]
    public DateTime SpinAt { get; set; }
}

