namespace FFXIV.Domain.Interfaces;

public interface IXivApiClient
{
    /// <summary>
    /// Gets the current game version from XIVAPI.
    /// Returns (versionKey, versionName) for the entry tagged "latest".
    /// </summary>
    Task<(string Key, string Name)> GetCurrentGameVersionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a page of rows from a sheet. Returns rows and the last row_id
    /// for cursor-based pagination (pass as afterRowId to get the next page).
    /// Returns empty list when no more rows remain.
    /// </summary>
    Task<(IReadOnlyList<XivApiRow<T>> Rows, int? LastRowId)> GetSheetAsync<T>(
        string sheetName,
        string fields,
        int limit = 100,
        int? afterRowId = null,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Fetches a single row by ID from a sheet.
    /// </summary>
    Task<XivApiRow<T>?> GetSheetRowAsync<T>(
        string sheetName,
        int rowId,
        string fields,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Searches across sheets using XIVAPI's search endpoint.
    /// Returns results and the next cursor string (null when exhausted).
    /// </summary>
    Task<(IReadOnlyList<XivApiRow<T>> Results, string? NextCursor)> SearchAsync<T>(
        string sheets,
        string query,
        string fields,
        int limit = 100,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        where T : class;
}
