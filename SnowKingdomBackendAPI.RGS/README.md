# Snow Kingdom RGS (Remote Game Server)

## Overview
The Remote Game Server (RGS) acts as a middleware layer between the frontend game client and the backend game engine. It handles game sessions, player management, and communicates with external services like RNG (Random Number Generator).

## Architecture

```
Frontend (React) → RGS (ASP.NET Core) → Backend (Game Engine) → RNG Service
```

## Features

### ✅ Complete RGS Implementation
- **Game Session Management**: Start, play, and manage game sessions
- **Player Balance Management**: Track and update player balances
- **Backend Communication**: Real HTTP calls to game engine
- **RNG Integration**: Fortuna algorithm random number generation
- **Error Handling**: Comprehensive error responses with proper status codes
- **Validation**: Request parameter validation
- **Health Checks**: Health monitoring endpoint
- **Docker Support**: Containerized deployment ready

### API Endpoints

#### Game Start
```
POST /{operatorId}/{gameId}/start
```
**Request:**
```json
{
  "languageId": "en",
  "client": "desktop",
  "funMode": 1,
  "token": "optional-token",
  "currencyId": "EUR"
}
```

#### Game Play
```
POST /{operatorId}/{gameId}/play
```
**Request:**
```json
{
  "sessionId": "session-id",
  "bets": [{"amount": 1.0}],
  "bet": 1.0,
  "userPayload": {},
  "lastResponse": {},
  "rtpLevel": 1,
  "mode": 0,
  "currency": {"id": "EUR"}
}
```

#### Player Balance
```
POST /{operatorId}/player/balance
```
**Request:**
```json
{
  "playerId": "player-id"
}
```

#### Health Check
```
GET /health
```

## Configuration

### appsettings.json
```json
{
  "Backend": {
    "Url": "http://localhost:5001/play"
  },
  "RNG": {
    "BaseUrl": "http://localhost:8080",
    "PoolsEndpoint": "/pools",
    "RngEndpoint": "/rng"
  }
}
```

## Game Modes
- **0**: Normal play
- **1**: In-game free spin
- **2**: Bonus game
- **3**: Free bets given by a bonus

## Error Codes
- **6000**: Success
- **6001**: Invalid session / Internal server error
- **6002**: Invalid request parameters
- **6003**: Backend service unavailable
- **6004**: Invalid data format

## Docker Deployment

### Build Image
```bash
docker build -f SnowKingdomBackendAPI.RGS/Dockerfile -t snow-kingdom-rgs .
```

### Run Container
```bash
docker run -p 5000:80 -e Backend__Url=http://backend:5001/play snow-kingdom-rgs
```

### Docker Compose
```bash
docker-compose up -d
```

## Development

### Prerequisites
- .NET 8 SDK
- Docker (optional)

### Run Locally
```bash
cd SnowKingdomBackendAPI.RGS
dotnet run
```

### Test Endpoints
```bash
# Health check
curl http://localhost:5000/health

# Start game
curl -X POST http://localhost:5000/LOCAL/FROSTY_FORTUNES/start \
  -H "Content-Type: application/json" \
  -d '{"languageId":"en","client":"desktop","funMode":1}'

# Play game
curl -X POST http://localhost:5000/LOCAL/FROSTY_FORTUNES/play \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"test-session","bets":[{"amount":1.0}],"bet":1.0,"mode":0}'
```

## RNG Service Integration

The RGS integrates with a Fortuna-based RNG service for cryptographically secure random number generation:

### Single Random Number
```csharp
var randomNumber = await rngService.GetRandomNumberAsync(min: 0, max: 100, gameId: "FROSTY_FORTUNES", roundId: "round-123");
```

### Random Pools
```csharp
var pools = new Dictionary<string, PoolRequest>
{
    ["REEL_STOPS"] = new PoolRequest { Min = 0, Max = 100, Size = 5 },
    ["FREE_SPINS"] = new PoolRequest { Min = 0, Max = 100, Size = 3 }
};
var results = await rngService.GetRandomPoolsAsync(pools, gameId: "FROSTY_FORTUNES", roundId: "round-123");
```

## Compliance Features

- **Jurisdiction Enforcement**: RNG requests include gameId and roundId for audit trails
- **Session Management**: Secure session handling with proper validation
- **Error Logging**: Comprehensive logging for debugging and compliance
- **Health Monitoring**: Built-in health checks for service monitoring

## Next Steps

1. **Backend Integration**: Update backend to handle RGS requests
2. **Frontend Integration**: Update frontend to use RGS endpoints
3. **RNG Service**: Deploy Fortuna RNG service
4. **Testing**: Comprehensive integration testing
5. **Production**: Deploy to production environment

## Support

For issues or questions, check the logs and ensure all services are running:
- RGS: `http://localhost:5000`
- Backend: `http://localhost:5001`
- RNG: `http://localhost:8080`
