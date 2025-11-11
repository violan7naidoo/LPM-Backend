using System.Text.Json;
using System.Text.Json.Serialization;
using SnowKingdomBackendAPI.ApiService.Game;

namespace SnowKingdomBackendAPI.ApiService.Services;

public class GameConfigService
{
    private readonly Dictionary<string, GameConfig> _configCache = new();
    private readonly ILogger<GameConfigService> _logger;
    private readonly string _configsDirectory;

    public GameConfigService(ILogger<GameConfigService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _configsDirectory = Path.Combine(environment.ContentRootPath, "GameConfigs");
    }

    public GameConfig LoadGameConfig(string gameId)
    {
        // Check cache first
        if (_configCache.TryGetValue(gameId, out var cachedConfig))
        {
            return cachedConfig;
        }

        // Load from file
        var configPath = Path.Combine(_configsDirectory, $"{gameId}.json");
        
        if (!File.Exists(configPath))
        {
            _logger.LogError($"Game configuration file not found: {configPath}");
            throw new FileNotFoundException($"Game configuration file not found for game: {gameId}", configPath);
        }

        try
        {
            var jsonContent = File.ReadAllText(configPath);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { 
                    new JsonStringToIntDictionaryConverter(),
                    new JsonStringToIntDecimalDictionaryConverter()
                }
            };
            var config = JsonSerializer.Deserialize<GameConfig>(jsonContent, jsonOptions);

            if (config == null)
            {
                throw new InvalidOperationException($"Failed to deserialize game configuration for: {gameId}");
            }

            // Validate required properties
            var isBookOfRaStyle = !string.IsNullOrEmpty(config.BookSymbol);
            
            if (!isBookOfRaStyle)
            {
                // Traditional style: need both WildSymbol and ScatterSymbol
                if (string.IsNullOrEmpty(config.WildSymbol))
                {
                    throw new InvalidOperationException($"WildSymbol is not defined in configuration for: {gameId}");
                }

                if (string.IsNullOrEmpty(config.ScatterSymbol))
                {
                    throw new InvalidOperationException($"ScatterSymbol is not defined in configuration for: {gameId}");
                }

                // Validate that wild and scatter symbols exist in the symbols dictionary
                if (!config.Symbols.ContainsKey(config.WildSymbol))
                {
                    throw new InvalidOperationException($"WildSymbol '{config.WildSymbol}' is not defined in the symbols dictionary for: {gameId}");
                }

                if (!config.Symbols.ContainsKey(config.ScatterSymbol))
                {
                    throw new InvalidOperationException($"ScatterSymbol '{config.ScatterSymbol}' is not defined in the symbols dictionary for: {gameId}");
                }
            }
            else
            {
                // Book of Ra style: need BookSymbol
                // We know BookSymbol is not null here because we checked !string.IsNullOrEmpty earlier (isBookOfRaStyle = true)
                if (!config.Symbols.ContainsKey(config.BookSymbol!))
                {
                    throw new InvalidOperationException($"BookSymbol '{config.BookSymbol}' is not defined in the symbols dictionary for: {gameId}");
                }
            }

            if (config.Symbols.Count == 0)
            {
                throw new InvalidOperationException($"No symbols defined in configuration for: {gameId}");
            }

            if (config.ReelStrips.Count == 0)
            {
                throw new InvalidOperationException($"No reel strips defined in configuration for: {gameId}");
            }

            // Validate that all symbols referenced in reel strips exist in the symbols dictionary
            var symbolKeys = new HashSet<string>(config.Symbols.Keys);
            for (int reelIndex = 0; reelIndex < config.ReelStrips.Count; reelIndex++)
            {
                var reel = config.ReelStrips[reelIndex];
                foreach (var symbol in reel)
                {
                    if (!symbolKeys.Contains(symbol))
                    {
                        throw new InvalidOperationException($"Symbol '{symbol}' in reel {reelIndex + 1} is not defined in the symbols dictionary for: {gameId}");
                    }
                }
            }

            // Cache the config
            _configCache[gameId] = config;
            _logger.LogInformation($"Loaded game configuration for: {gameId}");
            
            return config;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"Failed to parse JSON configuration file for: {gameId}");
            throw new InvalidOperationException($"Invalid JSON in game configuration file for: {gameId}", ex);
        }
    }
}

// Custom converter to handle string keys in JSON that should be deserialized as int keys
public class JsonStringToIntDictionaryConverter : JsonConverter<Dictionary<int, int>>
{
    public override Dictionary<int, int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<int, int>();
        
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var keyString = reader.GetString();
            if (string.IsNullOrEmpty(keyString))
            {
                throw new JsonException("Dictionary key cannot be null or empty");
            }

            if (!int.TryParse(keyString, out var key))
            {
                throw new JsonException($"Cannot convert '{keyString}' to int for dictionary key");
            }

            reader.Read();
            var value = reader.GetInt32();
            dictionary[key] = value;
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<int, int> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key.ToString());
            writer.WriteNumberValue(kvp.Value);
        }
        writer.WriteEndObject();
    }
}

// Custom converter to handle string keys in JSON that should be deserialized as int keys for Dictionary<int, decimal>
public class JsonStringToIntDecimalDictionaryConverter : JsonConverter<Dictionary<int, decimal>>
{
    public override Dictionary<int, decimal> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<int, decimal>();
        
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var keyString = reader.GetString();
            if (string.IsNullOrEmpty(keyString))
            {
                throw new JsonException("Dictionary key cannot be null or empty");
            }

            if (!int.TryParse(keyString, out var key))
            {
                throw new JsonException($"Cannot convert '{keyString}' to int for dictionary key");
            }

            reader.Read();
            var value = reader.GetDecimal();
            dictionary[key] = value;
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<int, decimal> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key.ToString());
            writer.WriteNumberValue(kvp.Value);
        }
        writer.WriteEndObject();
    }
}

