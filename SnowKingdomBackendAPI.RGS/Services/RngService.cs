using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowKingdomBackendAPI.RGS.Services;

public class RngService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RngService> _logger;
    private readonly IConfiguration _configuration;

    public RngService(HttpClient httpClient, ILogger<RngService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<int> GetRandomNumberAsync(int min = 0, int max = int.MaxValue, string? gameId = null, string? roundId = null)
    {
        try
        {
            var baseUrl = _configuration["RNG:BaseUrl"] ?? "http://localhost:8080";
            var endpoint = _configuration["RNG:RngEndpoint"] ?? "/rng";
            
            var url = $"{baseUrl}{endpoint}?min={min}&max={max}";
            
            if (!string.IsNullOrEmpty(gameId))
                url += $"&gameId={gameId}";
            
            if (!string.IsNullOrEmpty(roundId))
                url += $"&roundId={roundId}";

            _logger.LogInformation($"Calling RNG service: {url}");

            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var rngResponse = JsonSerializer.Deserialize<RngResponse>(content);
                
                if (rngResponse?.Random != null)
                {
                    _logger.LogInformation($"RNG returned: {rngResponse.Random}");
                    return rngResponse.Random.Value;
                }
            }

            _logger.LogError($"RNG call failed: {response.StatusCode}");
            throw new HttpRequestException($"RNG call failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling RNG service");
            throw;
        }
    }

    public async Task<Dictionary<string, List<int>>> GetRandomPoolsAsync(Dictionary<string, PoolRequest> pools, string? gameId = null, string? roundId = null)
    {
        try
        {
            var baseUrl = _configuration["RNG:BaseUrl"] ?? "http://localhost:8080";
            var endpoint = _configuration["RNG:PoolsEndpoint"] ?? "/pools";
            var url = $"{baseUrl}{endpoint}";

            var request = new PoolsRequest
            {
                Pools = pools,
                GameId = gameId,
                RoundId = roundId
            };

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            _logger.LogInformation($"Calling RNG pools service: {url}");

            var response = await _httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var poolsResponse = JsonSerializer.Deserialize<PoolsResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                if (poolsResponse?.Pools != null)
                {
                    _logger.LogInformation($"RNG pools returned: {poolsResponse.Pools.Count} pools");
                    return poolsResponse.Pools;
                }
            }

            _logger.LogError($"RNG pools call failed: {response.StatusCode}");
            throw new HttpRequestException($"RNG pools call failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling RNG pools service");
            throw;
        }
    }
}

// RNG Service Models
public class RngResponse
{
    [JsonPropertyName("random")]
    public int? Random { get; set; }
}

public class PoolsRequest
{
    [JsonPropertyName("pools")]
    public Dictionary<string, PoolRequest> Pools { get; set; } = new();
    
    [JsonPropertyName("gameId")]
    public string? GameId { get; set; }
    
    [JsonPropertyName("roundId")]
    public string? RoundId { get; set; }
}

public class PoolRequest
{
    [JsonPropertyName("min")]
    public int Min { get; set; }
    
    [JsonPropertyName("max")]
    public int Max { get; set; }
    
    [JsonPropertyName("size")]
    public int Size { get; set; } = 1;
}

public class PoolsResponse
{
    [JsonPropertyName("pools")]
    public Dictionary<string, List<int>> Pools { get; set; } = new();
}
