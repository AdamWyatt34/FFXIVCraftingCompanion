using System.Security.Claims;
using FFXIV.Application.Auth;
using FFXIV.Application.Auth.DTOs;
using FFXIV.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Authentication endpoints for user registration, login, token refresh, and Discord OAuth.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IDiscordOAuthService _discordService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IDiscordOAuthService discordService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _discordService = discordService;
    }

    /// <summary>
    /// Register a new user with email and password.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return Conflict(new { error = "An account with this email already exists." });

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return BadRequest(new { error = errors });
        }

        var tokens = await _tokenService.GenerateTokensAsync(user.Id, user.Email, user.DisplayName, ct);
        return Ok(MapTokens(tokens));
    }

    /// <summary>
    /// Login with email and password.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Invalid credentials." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { error = "Invalid credentials." });

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            return Unauthorized(new { error = "Invalid credentials." });

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var tokens = await _tokenService.GenerateTokensAsync(user.Id, user.Email!, user.DisplayName, ct);
        return Ok(MapTokens(tokens));
    }

    /// <summary>
    /// Refresh an expired access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Unauthorized(new { error = "Refresh token is required." });

        try
        {
            var tokens = await _tokenService.RefreshTokensAsync(request.RefreshToken, ct);
            return Ok(MapTokens(tokens));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
    }

    /// <summary>
    /// Logout by revoking a refresh token. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        return NoContent();
    }

    /// <summary>
    /// Get the current authenticated user's profile.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        return Ok(new UserInfoDto(
            UserId: user.Id,
            Email: user.Email!,
            DisplayName: user.DisplayName,
            AvatarUrl: user.AvatarUrl,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            DiscordId: user.DiscordId));
    }

    /// <summary>
    /// Get the Discord OAuth authorization URL for the frontend to redirect to.
    /// </summary>
    [HttpGet("discord/url")]
    [ProducesResponseType(typeof(DiscordAuthUrlDto), StatusCodes.Status200OK)]
    public IActionResult GetDiscordUrl()
    {
        var state = Guid.NewGuid().ToString("N");
        var url = _discordService.GetAuthorizationUrl(state);
        return Ok(new DiscordAuthUrlDto(url));
    }

    /// <summary>
    /// Exchange a Discord OAuth code for authentication tokens.
    /// </summary>
    [HttpPost("discord/callback")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DiscordCallback([FromBody] DiscordCallbackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Authorization code is required." });

        DiscordUserInfo discordUser;
        try
        {
            discordUser = await _discordService.ExchangeCodeAsync(request.Code, ct);
        }
        catch (HttpRequestException)
        {
            return BadRequest(new { error = "Failed to exchange Discord authorization code." });
        }

        // Find existing user by Discord ID
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DiscordId == discordUser.DiscordId, ct);

        if (user is null && !string.IsNullOrEmpty(discordUser.Email))
        {
            // Try to find by email and link Discord account
            user = await _userManager.FindByEmailAsync(discordUser.Email);
            if (user is not null)
            {
                user.DiscordId = discordUser.DiscordId;
                user.AvatarUrl ??= discordUser.AvatarUrl;
            }
        }

        if (user is null)
        {
            // Create new user from Discord info
            user = new ApplicationUser
            {
                UserName = discordUser.Email ?? $"discord_{discordUser.DiscordId}",
                Email = discordUser.Email,
                DisplayName = discordUser.Username,
                AvatarUrl = discordUser.AvatarUrl,
                DiscordId = discordUser.DiscordId,
                CreatedAt = DateTimeOffset.UtcNow,
                EmailConfirmed = !string.IsNullOrEmpty(discordUser.Email)
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return BadRequest(new { error = $"Failed to create account: {errors}" });
            }
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var tokens = await _tokenService.GenerateTokensAsync(
            user.Id, user.Email ?? string.Empty, user.DisplayName, ct);
        return Ok(MapTokens(tokens));
    }

    private static AuthTokensDto MapTokens(AuthTokens tokens) =>
        new(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt);
}
