namespace FFXIV.Application.Auth;

/// <summary>
/// Issues and verifies the opaque <c>state</c> value carried through an OAuth authorisation
/// round-trip. The value is signed and time-limited so the callback can tell a state this API
/// issued from a forged or replayed one; the browser additionally compares it with the value it
/// stored before redirecting, which is the login-CSRF check proper.
/// </summary>
public interface IOAuthStateProtector
{
    string Create();

    bool Validate(string? state);
}
