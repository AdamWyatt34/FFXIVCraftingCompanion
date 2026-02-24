namespace FFXIV.Application.Analytics.DTOs;

public record TrackEventRequest(
    string Event,
    string? Metadata = null,
    string? PageUrl = null,
    string? Referrer = null);
