using System.Net.Http.Json;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.ExternalApis.Models;
using Microsoft.Extensions.Logging;

namespace FFXIV.Infrastructure.ExternalApis;

public sealed class XivApiClient : IXivApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XivApiClient> _logger;

    public XivApiClient(HttpClient httpClient, ILogger<XivApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(string Key, string Name)> GetCurrentGameVersionAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching current game version from XIVAPI");

        var response = await _httpClient.GetFromJsonAsync<XivApiVersionResponse>(
            "api/version", cancellationToken);

        if (response?.Versions is null || response.Versions.Count == 0)
            throw new InvalidOperationException("XIVAPI returned no version information.");

        var latest = response.Versions.FirstOrDefault(
            v => v.Names.Contains("latest", StringComparer.OrdinalIgnoreCase));

        if (latest is null)
            throw new InvalidOperationException("No version tagged 'latest' found in XIVAPI response.");

        var name = latest.Names.FirstOrDefault(
            n => !n.Equals("latest", StringComparison.OrdinalIgnoreCase)) ?? latest.Key;

        _logger.LogInformation("Current XIVAPI game version: {VersionName} (key: {VersionKey})",
            name, latest.Key);

        return (latest.Key, name);
    }

    public async Task<(IReadOnlyList<XivApiRow<T>> Rows, int? LastRowId)> GetSheetAsync<T>(
        string sheetName,
        string fields,
        int limit = 100,
        int? afterRowId = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var url = $"api/sheet/{Uri.EscapeDataString(sheetName)}?limit={limit}&fields={fields}";
        if (afterRowId.HasValue)
            url += $"&after={afterRowId.Value}";

        _logger.LogDebug("Fetching sheet {Sheet} (after={After}, limit={Limit})",
            sheetName, afterRowId, limit);

        var response = await _httpClient.GetFromJsonAsync<XivApiSheetResponse<T>>(
            url, cancellationToken);

        if (response?.Rows is null || response.Rows.Count == 0)
            return (Array.Empty<XivApiRow<T>>(), null);

        var rows = response.Rows
            .Select(r => new XivApiRow<T>(r.RowId, r.Fields))
            .ToList();
        var lastRowId = response.Rows[^1].RowId;

        _logger.LogDebug("Received {Count} rows from {Sheet}, last row_id={LastRowId}",
            rows.Count, sheetName, lastRowId);

        return (rows, lastRowId);
    }

    public async Task<XivApiRow<T>?> GetSheetRowAsync<T>(
        string sheetName,
        int rowId,
        string fields,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var url = $"api/sheet/{Uri.EscapeDataString(sheetName)}/{rowId}?fields={fields}";

        _logger.LogDebug("Fetching {Sheet} row {RowId}", sheetName, rowId);

        var httpResponse = await _httpClient.GetAsync(url, cancellationToken);

        if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        httpResponse.EnsureSuccessStatusCode();

        var response = await httpResponse.Content.ReadFromJsonAsync<XivApiSingleRowResponse<T>>(
            cancellationToken: cancellationToken);

        if (response is null)
            return null;

        return new XivApiRow<T>(response.RowId, response.Fields);
    }

    public async Task<(IReadOnlyList<XivApiRow<T>> Results, string? NextCursor)> SearchAsync<T>(
        string sheets,
        string query,
        string fields,
        int limit = 100,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var url = $"api/search?sheets={Uri.EscapeDataString(sheets)}&query={Uri.EscapeDataString(query)}&fields={fields}&limit={limit}";
        if (cursor is not null)
            url += $"&cursor={Uri.EscapeDataString(cursor)}";

        _logger.LogDebug("Searching {Sheets} with query '{Query}' (limit={Limit})",
            sheets, query, limit);

        var response = await _httpClient.GetFromJsonAsync<XivApiSearchResponse<T>>(
            url, cancellationToken);

        if (response?.Results is null || response.Results.Count == 0)
            return (Array.Empty<XivApiRow<T>>(), null);

        var results = response.Results
            .Select(r => new XivApiRow<T>(r.RowId, r.Fields))
            .ToList();

        _logger.LogDebug("Search returned {Count} results", results.Count);

        return (results, response.Next);
    }
}
