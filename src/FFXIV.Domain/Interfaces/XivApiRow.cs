namespace FFXIV.Domain.Interfaces;

/// <summary>
/// Represents a row returned from XIVAPI, pairing the row ID with deserialized fields.
/// </summary>
public sealed record XivApiRow<T>(int RowId, T Fields);
