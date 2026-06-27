using DevHabit.Api.DTOs.GitHub;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.Controllers;

[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("api/[controller]")]
public sealed class GithubController(
    GitHubAccessTokenService githubAccessTokenService, 
    GitHubService githubService,
    UserContext userContext,
    LinkService linkService) : ControllerBase
{
    [HttpPut("personal-access-token")]
    public async Task<IActionResult> StoreAccessToken(StoreGitHubAccessTokenDto storeGithubAccessTokenDto)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        await githubAccessTokenService.StoreAsync(userId, storeGithubAccessTokenDto);
        return NoContent();
    }

    [HttpDelete("personal-access-token")]
    public async Task<IActionResult> RevokeAccessToken()
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        await githubAccessTokenService.RevokeAsync(userId);
        return NoContent();
    }

    [HttpGet("profile")]
    public async Task<ActionResult<GitHubUserProfileDto>> GetUserProfile([FromHeader] AcceptHeaderDto acceptHeaderDto)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        string? accessToken = await githubAccessTokenService.GetAsync(userId);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return NotFound("GitHub access token not found.");
        }
        GitHubUserProfileDto? userProfile = await githubService.GetUserProfileAsync(accessToken);
        if (userProfile is null)
        {
            return NotFound("GitHub user profile not found.");
        }
        if(acceptHeaderDto.IncludeLinks)
        {
            userProfile.Links = [
                linkService.Create(nameof(GetUserProfile), "self", HttpMethods.Get),
                linkService.Create(nameof(StoreAccessToken), "store-access-token", HttpMethods.Put),
                linkService.Create(nameof(RevokeAccessToken), "revoke-access-token", HttpMethods.Delete)
            ];
        }
        return Ok(userProfile);
    }
}
