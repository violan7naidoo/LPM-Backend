using Microsoft.EntityFrameworkCore;
using SnowKingdomBackendAPI.ApiService.Data;
using SnowKingdomBackendAPI.ApiService.Data.Entities;
using SnowKingdomBackendAPI.ApiService.Models;
using System.Text.Json;

namespace SnowKingdomBackendAPI.ApiService.Services;

/// <summary>
/// Service for persisting slot game data to the database.
/// Handles saving sessions and spin transactions for complete auditability.
/// </summary>
public class GameDataService
{
    private readonly GameDbContext _context;
    private readonly ILogger<GameDataService>? _logger;

    public GameDataService(GameDbContext context, ILogger<GameDataService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Saves or updates a game session to the database.
    /// </summary>
    public async Task SaveSessionAsync(GameSession session, CancellationToken ct = default)
    {
        try
        {
            var existingSession = await _context.GameSessions
                .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, ct);

            GameSessionEntity sessionEntity;
            if (existingSession != null)
            {
                sessionEntity = existingSession;
            }
            else
            {
                sessionEntity = new GameSessionEntity
                {
                    SessionId = session.SessionId,
                    PlayerId = session.PlayerId,
                    OperatorId = session.OperatorId,
                    GameId = session.GameId,
                    CreatedAt = session.CreatedAt
                };
                _context.GameSessions.Add(sessionEntity);
            }

            // Update session properties
            sessionEntity.Balance = session.Balance;
            sessionEntity.FreeSpinsRemaining = session.FreeSpinsRemaining;
            sessionEntity.LastWin = session.LastWin;
            sessionEntity.FreeSpinsTotalWin = session.FreeSpinsTotalWin;
            sessionEntity.LastResponseJson = session.LastResponse != null
                ? JsonSerializer.Serialize(session.LastResponse)
                : null;
            sessionEntity.UpdatedAt = session.UpdatedAt;

            await _context.SaveChangesAsync(ct);

            _logger?.LogDebug("[GAME DATA] Saved session {SessionId} to database", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[GAME DATA] Error saving session {SessionId} to database", session.SessionId);
            throw;
        }
    }

    /// <summary>
    /// Gets a game session from the database.
    /// </summary>
    public async Task<GameSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var sessionEntity = await _context.GameSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

            if (sessionEntity == null)
            {
                return null;
            }

            // Deserialize LastResponse if it exists
            GameState? lastResponse = null;
            if (!string.IsNullOrEmpty(sessionEntity.LastResponseJson))
            {
                try
                {
                    lastResponse = JsonSerializer.Deserialize<GameState>(sessionEntity.LastResponseJson);
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning(ex, "[GAME DATA] Failed to deserialize LastResponse for session {SessionId}", sessionId);
                }
            }

            return new GameSession
            {
                SessionId = sessionEntity.SessionId,
                PlayerId = sessionEntity.PlayerId,
                OperatorId = sessionEntity.OperatorId,
                GameId = sessionEntity.GameId,
                Balance = sessionEntity.Balance,
                FreeSpinsRemaining = sessionEntity.FreeSpinsRemaining,
                LastWin = sessionEntity.LastWin,
                FreeSpinsTotalWin = sessionEntity.FreeSpinsTotalWin,
                LastResponse = lastResponse,
                CreatedAt = sessionEntity.CreatedAt,
                UpdatedAt = sessionEntity.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[GAME DATA] Error getting session {SessionId} from database", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Saves a spin transaction to the database.
    /// </summary>
    public async Task SaveSpinTransactionAsync(
        string sessionId,
        string gameId,
        decimal betAmount,
        SpinResult spinResult,
        bool isFreeSpin,
        int freeSpinsAwarded,
        CancellationToken ct = default)
    {
        try
        {
            var transaction = new SpinTransactionEntity
            {
                SessionId = sessionId,
                GameId = gameId,
                BetAmount = betAmount,
                WinAmount = spinResult.TotalWin,
                TotalWin = spinResult.TotalWin,
                FreeSpinsAwarded = freeSpinsAwarded,
                IsFreeSpin = isFreeSpin,
                GridJson = JsonSerializer.Serialize(spinResult.Grid),
                WinningLinesJson = JsonSerializer.Serialize(spinResult.WinningLines),
                ScatterWinJson = JsonSerializer.Serialize(spinResult.ScatterWin),
                SpinAt = DateTime.UtcNow
            };

            _context.SpinTransactions.Add(transaction);
            await _context.SaveChangesAsync(ct);

            _logger?.LogDebug(
                "[GAME DATA] Saved spin transaction for session {SessionId}: Bet={BetAmount}, Win={WinAmount}",
                sessionId, betAmount, spinResult.TotalWin);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[GAME DATA] Error saving spin transaction for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Gets spin history for a session.
    /// </summary>
    public async Task<List<SpinTransactionEntity>> GetSpinHistoryAsync(
        string sessionId,
        int? limit = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.SpinTransactions
                .Where(t => t.SessionId == sessionId)
                .OrderByDescending(t => t.SpinAt);

            if (limit.HasValue)
            {
                query = (IOrderedQueryable<SpinTransactionEntity>)query.Take(limit.Value);
            }

            return await query.ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[GAME DATA] Error getting spin history for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Gets game statistics from the database.
    /// </summary>
    public async Task<GameStats> GetGameStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var totalSessions = await _context.GameSessions.CountAsync(ct);
            var totalSpins = await _context.SpinTransactions.CountAsync(ct);
            var totalBets = await _context.SpinTransactions.SumAsync(t => t.BetAmount, ct);
            var totalWins = await _context.SpinTransactions.SumAsync(t => t.TotalWin, ct);
            var freeSpinsCount = await _context.SpinTransactions.CountAsync(t => t.IsFreeSpin, ct);
            var totalFreeSpinsAwarded = await _context.SpinTransactions.SumAsync(t => t.FreeSpinsAwarded, ct);

            return new GameStats
            {
                TotalSessions = totalSessions,
                TotalSpins = totalSpins,
                TotalBets = totalBets,
                TotalWins = totalWins,
                FreeSpinsCount = freeSpinsCount,
                TotalFreeSpinsAwarded = totalFreeSpinsAwarded
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[GAME DATA] Error getting game statistics");
            throw;
        }
    }
}

/// <summary>
/// Statistics about the game for reporting.
/// </summary>
public class GameStats
{
    public int TotalSessions { get; set; }
    public int TotalSpins { get; set; }
    public decimal TotalBets { get; set; }
    public decimal TotalWins { get; set; }
    public int FreeSpinsCount { get; set; }
    public int TotalFreeSpinsAwarded { get; set; }
}

