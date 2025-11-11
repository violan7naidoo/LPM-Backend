using Microsoft.AspNetCore.Mvc;
using SnowKingdomBackendAPI.RGS.Models;
using SnowKingdomBackendAPI.ApiService.Services;
using SnowKingdomBackendAPI.ApiService.Models;

namespace SnowKingdomBackendAPI.RGS.Controllers;

[ApiController]
[Route("{operatorId}/player")]
public class PlayerController : ControllerBase
{
    private readonly SessionService _sessionService;
    private readonly ILogger<PlayerController> _logger;

    public PlayerController(SessionService sessionService, ILogger<PlayerController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    [HttpPost("balance")]
    public async Task<ActionResult<PlayerBalanceResponse>> GetPlayerBalance(
        [FromRoute] string operatorId,
        [FromBody] PlayerBalanceRequest request)
    {
        try
        {
            _logger.LogInformation($"Balance requested for player: {request.PlayerId}");

            // In a real implementation, you would look up the player by ID
            // For local development, we'll use a simple approach
            var sessions = await _sessionService.GetSessionsByPlayerIdAsync(request.PlayerId);
            
            decimal balance = 1000.00m; // Default balance
            
            if (sessions.Any())
            {
                // Get the most recent session's balance
                var latestSession = sessions.OrderByDescending(s => s.CreatedAt).First();
                balance = latestSession.Balance;
            }

            var response = new PlayerBalanceResponse
            {
                StatusCode = 8000,
                Message = "Request processed successfully",
                Balance = balance
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player balance");
            return StatusCode(500, new PlayerBalanceResponse
            {
                StatusCode = 6001,
                Message = "Internal server error",
                Balance = 0
            });
        }
    }
}
