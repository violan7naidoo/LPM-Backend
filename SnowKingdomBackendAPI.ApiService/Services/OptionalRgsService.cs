using System.Text.Json;
using System.Text;
using SnowKingdomBackendAPI.ApiService.Models;

namespace SnowKingdomBackendAPI.ApiService.Services;

public class OptionalRgsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OptionalRgsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly bool _enabled;

    public OptionalRgsService(HttpClient httpClient, ILogger<OptionalRgsService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        // Check if RGS is enabled in configuration
        _enabled = _configuration.GetValue<bool>("Rgs:Enabled", false);
        var rgsUrl = _configuration.GetValue<string>("Rgs:Url", "http://localhost:5000");
        
        if (_enabled)
        {
            _httpClient.BaseAddress = new Uri(rgsUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for non-blocking calls
        }
    }

    /// <summary>
    /// Attempts to send spin data to RGS. This is non-blocking and will not throw exceptions.
    /// </summary>
    public async Task SendSpinDataAsync(string sessionId, string gameId, PlayResponse playResponse)
    {
        if (!_enabled)
        {
            return; // RGS is disabled, skip
        }

        try
        {
            // Prepare RGS-compatible data format
            var rgsData = new
            {
                sessionId = sessionId,
                gameId = gameId,
                player = new
                {
                    sessionId = playResponse.SessionId,
                    balance = playResponse.Player.Balance,
                    freeSpinsRemaining = playResponse.Player.FreeSpinsRemaining,
                    lastWin = playResponse.Player.LastWin,
                    actionGameSpins = playResponse.Player.ActionGameSpins,
                    featureSymbol = playResponse.Player.FeatureSymbol
                },
                game = new
                {
                    results = playResponse.Game.Results,
                    mode = playResponse.Player.FreeSpinsRemaining > 0 ? 1 : 0
                },
                freeSpins = playResponse.FreeSpins,
                actionGameSpins = playResponse.ActionGameSpins,
                featureSymbol = playResponse.FeatureSymbol
            };

            var jsonContent = JsonSerializer.Serialize(rgsData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Use SendAsync with a timeout to make it truly non-blocking
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.PostAsync("/rgs/spin-data", content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully sent spin data to RGS for session: {sessionId}");
            }
            else
            {
                _logger.LogWarning($"RGS returned non-success status: {response.StatusCode} for session: {sessionId}");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning($"RGS request timed out for session: {sessionId}. Continuing without RGS.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, $"Failed to send data to RGS for session: {sessionId}. Continuing without RGS.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error sending data to RGS for session: {sessionId}. Continuing without RGS.");
        }
    }

    /// <summary>
    /// Attempts to send action game spin data to RGS. This is non-blocking and will not throw exceptions.
    /// </summary>
    public async Task SendActionGameSpinDataAsync(string sessionId, ActionGameSpinResponse actionGameResponse)
    {
        if (!_enabled)
        {
            return; // RGS is disabled, skip
        }

        try
        {
            var jsonContent = JsonSerializer.Serialize(actionGameResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.PostAsync("/rgs/action-game-spin", content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully sent action game spin data to RGS for session: {sessionId}");
            }
            else
            {
                _logger.LogWarning($"RGS returned non-success status: {response.StatusCode} for action game session: {sessionId}");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning($"RGS action game request timed out for session: {sessionId}. Continuing without RGS.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, $"Failed to send action game data to RGS for session: {sessionId}. Continuing without RGS.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error sending action game data to RGS for session: {sessionId}. Continuing without RGS.");
        }
    }
}

