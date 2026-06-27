using System.Net.Http.Headers;
using Newtonsoft.Json;
using DevHabit.Api.DTOs;
using DevHabit.Api.DTOs.GitHub;

namespace DevHabit.Api.Services;

public sealed class GitHubService(IHttpClientFactory httpClientFactory, ILogger<GitHubService> logger)
{
    public async Task<GitHubUserProfileDto?> GetUserProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using HttpClient client = CreateGitHubClient(accessToken);
        HttpResponseMessage response = await client.GetAsync("user", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to retrieve GitHub user profile. Status code: {StatusCode}", response.StatusCode);
            return null;
        }
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonConvert.DeserializeObject<GitHubUserProfileDto>(content);
    }

    public async Task<IReadOnlyList<GitHubEventDto>?> GetUserEventsAsync(string username, string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        using HttpClient client = CreateGitHubClient(accessToken);
        HttpResponseMessage response = await client.GetAsync($"users/{username}/events?per_page=100", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to retrieve GitHub user events for {Username}. Status code: {StatusCode}", username, response.StatusCode);
            return null;
        }
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonConvert.DeserializeObject<IReadOnlyList<GitHubEventDto>>(content);
    }

    private HttpClient CreateGitHubClient(string accessToken)
    {
        HttpClient client = httpClientFactory.CreateClient("github");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
