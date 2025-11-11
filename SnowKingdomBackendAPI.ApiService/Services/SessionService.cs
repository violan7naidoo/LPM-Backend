using SnowKingdomBackendAPI.ApiService.Models;

namespace SnowKingdomBackendAPI.ApiService.Services;

public class SessionService
{
    private readonly Dictionary<string, GameSession> _sessions = new();
    private readonly Dictionary<string, List<GameSession>> _playerSessions = new();
    private readonly IServiceProvider? _serviceProvider;
    private const decimal DefaultBalance = 500.00m; // R500.00

    public SessionService(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    private async Task<T?> ExecuteWithDataServiceAsync<T>(Func<GameDataService, Task<T>> operation)
    {
        if (_serviceProvider == null) return default;
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetService<GameDataService>();
        if (dataService == null) return default;
        return await operation(dataService);
    }

    private async Task ExecuteWithDataServiceAsync(Func<GameDataService, Task> operation)
    {
        if (_serviceProvider == null) return;
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetService<GameDataService>();
        if (dataService == null) return;
        await operation(dataService);
    }

    private T? ExecuteWithDataServiceSync<T>(Func<GameDataService, T> operation)
    {
        if (_serviceProvider == null) return default;
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetService<GameDataService>();
        if (dataService == null) return default;
        return operation(dataService);
    }

    private void ExecuteWithDataServiceSync(Action<GameDataService> operation)
    {
        if (_serviceProvider == null) return;
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetService<GameDataService>();
        if (dataService == null) return;
        operation(dataService);
    }

    // Legacy methods for backward compatibility
    public GameState GetOrCreateSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
        }

        // Try to get from in-memory cache first
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            // Try to load from database
            var dbSession = ExecuteWithDataServiceSync(ds => ds.GetSessionAsync(sessionId).GetAwaiter().GetResult());
            if (dbSession != null)
            {
                session = dbSession;
                _sessions[sessionId] = session;
            }

            // If still not found, create new session
            if (session == null)
            {
                session = new GameSession
                {
                    SessionId = sessionId,
                    PlayerId = $"Player-{DateTime.Now.Ticks}",
                    OperatorId = "LOCAL",
                    GameId = "SnowKingdom",
                    Balance = DefaultBalance,
                    FreeSpinsRemaining = 0,
                    LastWin = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _sessions[sessionId] = session;

                // Persist to database
                ExecuteWithDataServiceSync(ds => ds.SaveSessionAsync(session).GetAwaiter().GetResult());
            }
        }

        return new GameState
        {
            Balance = session.Balance,
            FreeSpinsRemaining = session.FreeSpinsRemaining,
            LastWin = session.LastWin,
            Results = session.LastResponse?.Results ?? new SpinResult(),
            ActionGameSpins = session.LastResponse?.ActionGameSpins ?? 0,
            FeatureSymbol = session.LastResponse?.FeatureSymbol ?? ""
        };
    }

    public GameState UpdateSession(string sessionId, GameState newState)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Balance = newState.Balance;
            session.FreeSpinsRemaining = newState.FreeSpinsRemaining;
            session.LastWin = newState.LastWin;
            session.LastResponse = newState;
            session.UpdatedAt = DateTime.UtcNow;

            // Persist to database
            ExecuteWithDataServiceSync(ds => ds.SaveSessionAsync(session).GetAwaiter().GetResult());
        }
        return newState;
    }

    public void ResetSession(string sessionId)
    {
        if (_sessions.ContainsKey(sessionId))
        {
            var oldSession = _sessions[sessionId];
            _sessions[sessionId] = new GameSession
            {
                SessionId = sessionId,
                PlayerId = oldSession.PlayerId, // Keep same player ID
                OperatorId = oldSession.OperatorId, // Keep same operator ID
                GameId = oldSession.GameId, // Keep same game ID
                Balance = DefaultBalance,
                FreeSpinsRemaining = 0,
                LastWin = 0,
                CreatedAt = oldSession.CreatedAt, // Keep original creation time
                UpdatedAt = DateTime.UtcNow
            };

            // Persist to database
            ExecuteWithDataServiceSync(ds => ds.SaveSessionAsync(_sessions[sessionId]).GetAwaiter().GetResult());
        }
    }

    // New RGS-compatible methods
    public async Task<GameSession?> GetSessionAsync(string sessionId)
    {
        // Try in-memory cache first
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        // Try database
        session = await ExecuteWithDataServiceAsync(async ds => await ds.GetSessionAsync(sessionId));
        if (session != null)
        {
            _sessions[sessionId] = session; // Cache it
            return session;
        }

        return null;
    }

    public async Task CreateSessionAsync(GameSession session)
    {
        _sessions[session.SessionId] = session;
        
        if (!_playerSessions.ContainsKey(session.PlayerId))
        {
            _playerSessions[session.PlayerId] = new List<GameSession>();
        }
        _playerSessions[session.PlayerId].Add(session);

        // Persist to database
        await ExecuteWithDataServiceAsync(async ds => await ds.SaveSessionAsync(session));
    }

    public async Task UpdateSessionAsync(GameSession session)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _sessions[session.SessionId] = session;

        // Persist to database
        await ExecuteWithDataServiceAsync(async ds => await ds.SaveSessionAsync(session));
    }

    public async Task<List<GameSession>> GetSessionsByPlayerIdAsync(string playerId)
    {
        await Task.CompletedTask; // For async compatibility
        return _playerSessions.TryGetValue(playerId, out var sessions) ? sessions : new List<GameSession>();
    }
}
