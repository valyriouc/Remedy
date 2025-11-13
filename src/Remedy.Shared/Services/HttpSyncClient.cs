using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Remedy.Shared.DTOs;

namespace Remedy.Shared.Services;

/// <summary>
/// HTTP client for synchronizing with the Remedy server with retry logic and exponential backoff
/// </summary>
public class HttpSyncClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    private readonly int _initialRetryDelayMs;

    public HttpSyncClient(string baseUrl, int maxRetries = 3, int initialRetryDelayMs = 1000)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _maxRetries = maxRetries;
        _initialRetryDelayMs = initialRetryDelayMs;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Checks if the server is available
    /// </summary>
    public async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/sync/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Performs batch sync with retry logic
    /// </summary>
    public async Task<(bool Success, BatchSyncResponse? Response, string? Error)> BatchSyncAsync(BatchSyncRequest request)
    {
        int attempt = 0;
        int delayMs = _initialRetryDelayMs;

        while (attempt < _maxRetries)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/sync/batch", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BatchSyncResponse>();
                    return (true, result, null);
                }

                // If server returns error, don't retry
                if (response.StatusCode >= HttpStatusCode.BadRequest && response.StatusCode < HttpStatusCode.InternalServerError)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, null, $"Server error: {response.StatusCode} - {errorContent}");
                }

                // Server error - retry
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff
                }
            }
            catch (HttpRequestException ex)
            {
                // Network error - retry
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff
                }
                else
                {
                    return (false, null, $"Network error after {_maxRetries} attempts: {ex.Message}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Request timeout");
            }
            catch (Exception ex)
            {
                return (false, null, $"Unexpected error: {ex.Message}");
            }
        }

        return (false, null, $"Failed after {_maxRetries} retry attempts");
    }

    /// <summary>
    /// Pulls changes from server with retry logic
    /// </summary>
    public async Task<(bool Success, BatchSyncRequest? Data, string? Error)> PullChangesAsync(DateTime? since = null)
    {
        int attempt = 0;
        int delayMs = _initialRetryDelayMs;

        while (attempt < _maxRetries)
        {
            try
            {
                var url = since.HasValue
                    ? $"/api/sync/pull?since={since.Value:O}"
                    : "/api/sync/pull";

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BatchSyncRequest>();
                    return (true, result, null);
                }

                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
            }
            catch (HttpRequestException ex)
            {
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
                else
                {
                    return (false, null, $"Network error after {_maxRetries} attempts: {ex.Message}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Request timeout");
            }
            catch (Exception ex)
            {
                return (false, null, $"Unexpected error: {ex.Message}");
            }
        }

        return (false, null, $"Failed after {_maxRetries} retry attempts");
    }

    /// <summary>
    /// Syncs a single resource with retry logic
    /// </summary>
    public async Task<(bool Success, SyncResponse? Response, string? Error)> SyncResourceAsync(ResourceSyncDto resource)
    {
        int attempt = 0;
        int delayMs = _initialRetryDelayMs;

        while (attempt < _maxRetries)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/resources", resource);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
                    return (true, result, null);
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
                    return (false, result, "Conflict detected");
                }

                // Client error - don't retry
                if (response.StatusCode >= HttpStatusCode.BadRequest && response.StatusCode < HttpStatusCode.InternalServerError)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, null, $"Server error: {response.StatusCode} - {errorContent}");
                }

                // Server error - retry
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
            }
            catch (HttpRequestException ex)
            {
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
                else
                {
                    return (false, null, $"Network error after {_maxRetries} attempts: {ex.Message}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Request timeout");
            }
            catch (Exception ex)
            {
                return (false, null, $"Unexpected error: {ex.Message}");
            }
        }

        return (false, null, $"Failed after {_maxRetries} retry attempts");
    }

    /// <summary>
    /// Syncs a single time slot with retry logic
    /// </summary>
    public async Task<(bool Success, SyncResponse? Response, string? Error)> SyncTimeSlotAsync(TimeSlotSyncDto timeSlot)
    {
        int attempt = 0;
        int delayMs = _initialRetryDelayMs;

        while (attempt < _maxRetries)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/slots", timeSlot);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
                    return (true, result, null);
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
                    return (false, result, "Conflict detected");
                }

                // Client error - don't retry
                if (response.StatusCode >= HttpStatusCode.BadRequest && response.StatusCode < HttpStatusCode.InternalServerError)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, null, $"Server error: {response.StatusCode} - {errorContent}");
                }

                // Server error - retry
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
            }
            catch (HttpRequestException ex)
            {
                attempt++;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2;
                }
                else
                {
                    return (false, null, $"Network error after {_maxRetries} attempts: {ex.Message}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Request timeout");
            }
            catch (Exception ex)
            {
                return (false, null, $"Unexpected error: {ex.Message}");
            }
        }

        return (false, null, $"Failed after {_maxRetries} retry attempts");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
