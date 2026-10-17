using System.Security.Claims;
using FFXIV.Application.Auth;
using FFXIV.Application.Auth.DTOs;
using FFXIV.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private const string InvalidCredentials = "Invalid credentials.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IDiscordOAuthService _discordService;
    private readonly IOAuthStateProtector _stateProtector;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IDiscordOAuthService discordService,
        IOAuthStateProtector stateProtector)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _discordService = discordService;
        _stateProtector = stateProtector;
    }

    /// <summary>
    /// Register a new user with email and password.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
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
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { error = InvalidCredentials });

        // A locked-out account answers exactly like a wrong password, so the lockout cannot be
        // used to confirm that an address is registered.
        if (await _userManager.IsLockedOutAsync(user))
            return Unauthorized(new { error = InvalidCredentials });

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { error = InvalidCredentials });
        }

        if (user.AccessFailedCount > 0)
            await _userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var tokens = await _tokenService.GenerateTokensAsync(user.Id, user.Email!, user.DisplayName, ct);
        return Ok(MapTokens(tokens));
    }

    /// <summary>
    /// Refresh an expired access token using a valid refresh token. Guessing a 512-bit token is
    /// not a realistic attack, and every signed-in page load refreshes, so this shares the
    /// public-read budget rather than the login one.
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("public-read")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
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
    /// Get the Discord OAuth authorization URL for the frontend to redirect to. The response also
    /// carries the <c>state</c> embedded in the URL; the browser stores it and sends it back with
    /// the authorization code.
    /// </summary>
    [HttpGet("discord/url")]
    [EnableRateLimiting("public-read")]
    [ProducesResponseType(typeof(DiscordAuthUrlDto), StatusCodes.Status200OK)]
    public IActionResult GetDiscordUrl()
    {
        var state = _stateProtector.Create();
        var url = _discordService.GetAuthorizationUrl(state);
        return Ok(new DiscordAuthUrlDto(url, state));
    }

    /// <summary>
    /// Exchange a Discord OAuth code for authentication tokens.
    /// </summary>
    [HttpPost("discord/callback")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthTokensDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DiscordCallback([FromBody] DiscordCallbackRequest request, CancellationToken ct)
    {
        if (!_stateProtector.Validate(request.State))
            return BadRequest(new { error = "The sign-in request has expired or is invalid. Please try again." });

        DiscordUserInfo discordUser;
        try
        {
            discordUser = await _discordService.ExchangeCodeAsync(request.Code, ct);
        }
        catch (HttpRequestException)
        {
            return BadRequest(new { error = "Failed to exchange Discord authorization code." });
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DiscordId == discordUser.DiscordId, ct);

        if (user is null)
        {
            // An account is only created from a Discord identity when Discord has verified the
            // address; an unverified one could have been typed in by anyone.
            if (discordUser.Email is null || !discordUser.EmailVerified)
            {
                return BadRequest(new
                {
                    error = "Your Discord account has no verified email address. Verify it on Discord, or sign in with your email and password."
                });
            }

            // Addresses on password accounts are never verified here, so a Discord identity is not
            // attached to one automatically: someone who registered the address first would
            // otherwise receive the Discord user's sessions.
            var existingUser = await _userManager.FindByEmailAsync(discordUser.Email);
            if (existingUser is not null)
            {
                return Conflict(new
                {
                    error = "An account with this email already exists. Sign in with your email and password."
                });
            }

            user = new ApplicationUser
            {
                UserName = discordUser.Email,
                Email = discordUser.Email,
                DisplayName = discordUser.Username,
                AvatarUrl = discordUser.AvatarUrl,
                DiscordId = discordUser.DiscordId,
                CreatedAt = DateTimeOffset.UtcNow,
                EmailConfirmed = true
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
