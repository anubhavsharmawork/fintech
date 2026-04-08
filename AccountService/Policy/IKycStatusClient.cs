namespace AccountService.Policy;

/// <summary>
/// Retrieves the KYC status for a user from the UserService.
/// Abstracted so tests can mock it without any HTTP calls.
/// </summary>
public interface IKycStatusClient
{
    /// <summary>
    /// Returns the KYC status string (e.g. "Pending", "Verified", "Rejected", "UnderReview").
    /// Returns "Verified" as a safe default if the UserService is unreachable — avoids
    /// blocking users on downstream outages. Override in high-security environments.
    /// </summary>
    Task<string> GetKycStatusAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Production implementation — calls GET /api/kyc/status on the UserService with the
/// user's bearer token forwarded, then parses the JSON response.
/// </summary>
public sealed class HttpKycStatusClient : IKycStatusClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpKycStatusClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpKycStatusClient(
        HttpClient http,
        ILogger<HttpKycStatusClient> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> GetKycStatusAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/kyc/status");

            // Forward the caller's bearer token so the UserService can authorize the request
            var token = _httpContextAccessor.HttpContext?
                .Request.Headers.Authorization
                .FirstOrDefault()?.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[KycStatusClient] UserService returned {StatusCode} for userId={UserId}. Defaulting to Verified.",
                    (int)response.StatusCode, userId);
                return "Verified";
            }

            var body = await response.Content.ReadFromJsonAsync<KycStatusResponse>(cancellationToken: ct);
            return body?.Status ?? "Verified";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[KycStatusClient] Failed to reach UserService for userId={UserId}. Defaulting to Verified.", userId);
            return "Verified";
        }
    }

    private sealed record KycStatusResponse(Guid UserId, string Status);
}
