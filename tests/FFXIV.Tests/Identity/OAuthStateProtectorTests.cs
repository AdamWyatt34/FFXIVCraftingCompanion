using FFXIV.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace FFXIV.Tests.Identity;

public class OAuthStateProtectorTests
{
    private const string Secret = "unit-test-jwt-secret-key-with-plenty-of-length-0123456789";
    private static readonly DateTimeOffset Start = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static OAuthStateProtector Create(FakeClock clock, string secret = Secret) =>
        new(Options.Create(new JwtOptions { SecretKey = secret }), clock);

    [Fact]
    public void Create_then_Validate_round_trips()
    {
        var protector = Create(new FakeClock(Start));

        var state = protector.Create();

        protector.Validate(state).Should().BeTrue();
    }

    [Fact]
    public void States_are_unique()
    {
        var protector = Create(new FakeClock(Start));

        var states = Enumerable.Range(0, 50).Select(_ => protector.Create()).ToList();

        states.Distinct().Should().HaveCount(50);
    }

    [Fact]
    public void Validate_rejects_a_tampered_signature()
    {
        var protector = Create(new FakeClock(Start));
        var state = protector.Create();
        var last = state[^1];
        var tampered = state[..^1] + (last == 'A' ? 'B' : 'A');

        protector.Validate(tampered).Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_a_tampered_payload()
    {
        var protector = Create(new FakeClock(Start));
        var state = protector.Create();
        var parts = state.Split('.');
        var tampered = $"{parts[0]}.{long.Parse(parts[1]) + 1}.{parts[2]}";

        protector.Validate(tampered).Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_a_state_signed_with_another_key()
    {
        var clock = new FakeClock(Start);
        var state = Create(clock, secret: "a-completely-different-secret-key-0123456789").Create();

        Create(clock).Validate(state).Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_an_expired_state()
    {
        var clock = new FakeClock(Start);
        var protector = Create(clock);
        var state = protector.Create();

        clock.Advance(TimeSpan.FromMinutes(9));
        protector.Validate(state).Should().BeTrue("ten minutes have not passed yet");

        clock.Advance(TimeSpan.FromMinutes(2));
        protector.Validate(state).Should().BeFalse("the state is older than ten minutes");
    }

    [Fact]
    public void Validate_rejects_a_state_from_the_future()
    {
        var clock = new FakeClock(Start);
        var protector = Create(clock);
        var state = protector.Create();

        clock.Advance(TimeSpan.FromMinutes(-2));

        protector.Validate(state).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("a.b.c")]
    [InlineData("a.b.")]
    [InlineData(".b.c")]
    public void Validate_rejects_malformed_input(string? state)
    {
        var protector = Create(new FakeClock(Start));

        protector.Validate(state).Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_oversized_input()
    {
        var protector = Create(new FakeClock(Start));
        var state = protector.Create() + new string('x', 300);

        protector.Validate(state).Should().BeFalse();
    }

    [Fact]
    public void Constructing_without_a_secret_fails_fast()
    {
        var act = () => Create(new FakeClock(Start), secret: "");

        act.Should().Throw<InvalidOperationException>();
    }
}
