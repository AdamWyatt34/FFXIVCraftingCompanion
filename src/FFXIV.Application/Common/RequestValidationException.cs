namespace FFXIV.Application.Common;

/// <summary>
/// A request that passed model binding but fails a rule that needs stored state to check (a list
/// that is already full, for example). The message is written for the client and is returned as
/// a 400 by the API's exception middleware.
/// </summary>
public sealed class RequestValidationException(string message) : Exception(message);
