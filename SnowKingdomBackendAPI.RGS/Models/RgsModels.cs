using System.Text.Json.Serialization;

namespace SnowKingdomBackendAPI.RGS.Models;

// RGS Game Start Request
public class GameStartRequest
{
    [JsonPropertyName("languageId")]
    public string LanguageId { get; set; } = "en";
    
    [JsonPropertyName("client")]
    public string Client { get; set; } = "desktop";
    
    [JsonPropertyName("funMode")]
    public int FunMode { get; set; } = 1; // 0 = real money, 1 = demo
    
    [JsonPropertyName("token")]
    public string? Token { get; set; }
    
    [JsonPropertyName("currencyId")]
    public string? CurrencyId { get; set; }
}

// RGS Game Start Response
public class GameStartResponse
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; } = 6000;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "Request processed successfully";
    
    [JsonPropertyName("player")]
    public PlayerInfo Player { get; set; } = new();
    
    [JsonPropertyName("client")]
    public ClientInfo Client { get; set; } = new();
    
    [JsonPropertyName("currency")]
    public CurrencyInfo Currency { get; set; } = new();
    
    [JsonPropertyName("game")]
    public GameInfo Game { get; set; } = new();
}

// RGS Game Play Request
public class GamePlayRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [JsonPropertyName("bets")]
    public List<BetInfo> Bets { get; set; } = new();
    
    [JsonPropertyName("bet")]
    public decimal Bet { get; set; }
    
    [JsonPropertyName("userPayload")]
    public object? UserPayload { get; set; }
    
    [JsonPropertyName("lastResponse")]
    public object? LastResponse { get; set; }
    
    [JsonPropertyName("rtpLevel")]
    public int RtpLevel { get; set; } = 1;
    
    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 0; // 0: normal play, 1: free spin, 2: bonus game, 3: free bets
    
    [JsonPropertyName("currency")]
    public CurrencyRequest? Currency { get; set; }
}

// RGS Game Play Response
public class GamePlayResponse
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; } = 6000;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "Request processed successfully";
    
    [JsonPropertyName("player")]
    public PlayerPlayInfo Player { get; set; } = new();
    
    [JsonPropertyName("game")]
    public GamePlayInfo Game { get; set; } = new();
    
    [JsonPropertyName("freeSpins")]
    public FreeSpinsInfo FreeSpins { get; set; } = new();
    
    [JsonPropertyName("promoFreeSpins")]
    public PromoFreeSpinsInfo PromoFreeSpins { get; set; } = new();
    
    [JsonPropertyName("jackpots")]
    public List<object> Jackpots { get; set; } = new();
    
    [JsonPropertyName("feature")]
    public FeatureInfo Feature { get; set; } = new();
}

// RGS Player Balance Request
public class PlayerBalanceRequest
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;
}

// RGS Player Balance Response
public class PlayerBalanceResponse
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; } = 8000;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "Request processed successfully";
    
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }
}

// Supporting Models
public class PlayerInfo
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; } = 1000.00m; // Default demo balance
}

public class ClientInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "desktop";
    
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "127.0.0.1";
    
    [JsonPropertyName("country")]
    public CountryInfo Country { get; set; } = new();
}

public class CountryInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "US";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "United States";
}

public class CurrencyInfo
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "R";
    
    [JsonPropertyName("isoCode")]
    public string IsoCode { get; set; } = "ZAR";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "South African Rand";
    
    [JsonPropertyName("decimals")]
    public int Decimals { get; set; } = 2;
    
    [JsonPropertyName("separator")]
    public SeparatorInfo Separator { get; set; } = new();
}

public class SeparatorInfo
{
    [JsonPropertyName("decimal")]
    public string Decimal { get; set; } = ".";
    
    [JsonPropertyName("thousand")]
    public string Thousand { get; set; } = ",";
}

public class GameInfo
{
    [JsonPropertyName("rtp")]
    public decimal Rtp { get; set; } = 96.00m;
    
    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 0; // 0 = normal, 1 = free spins, 2 = bonus, 3 = free bets
    
    [JsonPropertyName("bet")]
    public BetConfigInfo Bet { get; set; } = new();
    
    [JsonPropertyName("funMode")]
    public bool FunMode { get; set; } = true;
    
    [JsonPropertyName("maxWinCap")]
    public decimal MaxWinCap { get; set; } = 0;
    
    [JsonPropertyName("config")]
    public GameConfigInfo Config { get; set; } = new();
    
    [JsonPropertyName("freeSpins")]
    public FreeSpinsInfo FreeSpins { get; set; } = new();
    
    [JsonPropertyName("promoFreeSpins")]
    public PromoFreeSpinsInfo PromoFreeSpins { get; set; } = new();
    
    [JsonPropertyName("lastPlay")]
    public LastPlayInfo LastPlay { get; set; } = new();
}

public class BetConfigInfo
{
    [JsonPropertyName("default")]
    public int Default { get; set; } = 1;
    
    [JsonPropertyName("levels")]
    public List<decimal> Levels { get; set; } = new() { 1, 2, 3, 5 };
}

public class GameConfigInfo
{
    [JsonPropertyName("startScreen")]
    public object? StartScreen { get; set; }
    
    [JsonPropertyName("settings")]
    public GameSettingsInfo Settings { get; set; } = new();
}

public class GameSettingsInfo
{
    [JsonPropertyName("isAutoplay")]
    public string IsAutoplay { get; set; } = "1";
    
    [JsonPropertyName("isSlamStop")]
    public string IsSlamStop { get; set; } = "1";
    
    [JsonPropertyName("isBuyFeatures")]
    public string IsBuyFeatures { get; set; } = "0";
    
    [JsonPropertyName("isTurboSpin")]
    public string IsTurboSpin { get; set; } = "1";
    
    [JsonPropertyName("isRealityCheck")]
    public string IsRealityCheck { get; set; } = "1";
    
    [JsonPropertyName("minSpin")]
    public string MinSpin { get; set; } = "0";
    
    [JsonPropertyName("maxSpin")]
    public string MaxSpin { get; set; } = "0";
}

public class FreeSpinsInfo
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; } = 0;
    
    [JsonPropertyName("left")]
    public int Left { get; set; } = 0;
    
    [JsonPropertyName("betValue")]
    public decimal BetValue { get; set; } = 0;
    
    [JsonPropertyName("roundWin")]
    public decimal RoundWin { get; set; } = 0;
    
    [JsonPropertyName("totalWin")]
    public decimal TotalWin { get; set; } = 0;
    
    [JsonPropertyName("totalBet")]
    public decimal TotalBet { get; set; } = 0;
    
    [JsonPropertyName("won")]
    public int Won { get; set; } = 0;
    
    [JsonPropertyName("isPromotion")]
    public bool IsPromotion { get; set; } = false;
}

public class PromoFreeSpinsInfo
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; } = 0;
    
    [JsonPropertyName("left")]
    public int Left { get; set; } = 0;
    
    [JsonPropertyName("betValue")]
    public decimal BetValue { get; set; } = 0;
    
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;
    
    [JsonPropertyName("totalWin")]
    public decimal TotalWin { get; set; } = 0;
    
    [JsonPropertyName("totalBet")]
    public decimal TotalBet { get; set; } = 0;
    
    [JsonPropertyName("isPromotion")]
    public bool IsPromotion { get; set; } = false;
}

public class LastPlayInfo
{
    [JsonPropertyName("betLevel")]
    public BetLevelInfo BetLevel { get; set; } = new();
    
    [JsonPropertyName("results")]
    public List<object> Results { get; set; } = new();
}

public class BetLevelInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; } = 0;
    
    [JsonPropertyName("value")]
    public decimal Value { get; set; } = 1;
}

public class BetInfo
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public class PlayerPlayInfo
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [JsonPropertyName("roundId")]
    public string RoundId { get; set; } = string.Empty;
    
    [JsonPropertyName("transaction")]
    public TransactionInfo Transaction { get; set; } = new();
    
    [JsonPropertyName("prevBalance")]
    public decimal PrevBalance { get; set; }
    
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }
    
    [JsonPropertyName("bet")]
    public decimal Bet { get; set; }
    
    [JsonPropertyName("win")]
    public decimal Win { get; set; }
    
    [JsonPropertyName("currencyId")]
    public string CurrencyId { get; set; } = "ZAR";
}

public class TransactionInfo
{
    [JsonPropertyName("withdraw")]
    public string Withdraw { get; set; } = string.Empty;
    
    [JsonPropertyName("deposit")]
    public string Deposit { get; set; } = string.Empty;
}

public class GamePlayInfo
{
    [JsonPropertyName("results")]
    public object Results { get; set; } = new();
    
    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 0;
    
    [JsonPropertyName("maxWinCap")]
    public MaxWinCapInfo MaxWinCap { get; set; } = new();
}

public class MaxWinCapInfo
{
    [JsonPropertyName("achieved")]
    public bool Achieved { get; set; } = false;
    
    [JsonPropertyName("value")]
    public decimal Value { get; set; } = 0;
    
    [JsonPropertyName("realWin")]
    public decimal RealWin { get; set; } = 0;
}

public class CurrencyRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "EUR";
}

public class FeatureInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("isClosure")]
    public int IsClosure { get; set; } = 0;
}
