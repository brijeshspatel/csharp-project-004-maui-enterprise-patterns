namespace Authentication.Core.Tests;

/// <summary>
/// The credential itself: when it may be used, and what it says about itself.
/// </summary>
public class AccessTokenTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    [Fact]
    public void ATokenWellBeforeItsExpiry_IsUsable()
    {
        var token = new AccessToken(Fake.AccessTokenValue, Noon.AddMinutes(10));

        Assert.True(token.IsUsableAt(Noon, Skew));
    }

    [Fact]
    public void ATokenInsideTheSkewWindow_IsNotUsable_ThoughItHasNotExpired()
    {
        // Thirty seconds of life left, and a minute of margin. It has not expired; it will have by
        // the time a request carrying it is read.
        var token = new AccessToken(Fake.AccessTokenValue, Noon.AddSeconds(30));

        Assert.False(token.IsUsableAt(Noon, Skew));
    }

    [Fact]
    public void AtExactlyTheMargin_TheTokenIsNotUsable()
    {
        // The boundary is exclusive, and it is stated rather than discovered. "Before expiry"
        // without saying whether the boundary counts is how off-by-one gets in.
        var token = new AccessToken(Fake.AccessTokenValue, Noon + Skew);

        Assert.False(token.IsUsableAt(Noon, Skew));
    }

    [Fact]
    public void OneTickPastTheMargin_TheTokenIsUsable()
    {
        var token = new AccessToken(Fake.AccessTokenValue, Noon + Skew + TimeSpan.FromTicks(1));

        Assert.True(token.IsUsableAt(Noon, Skew));
    }

    [Fact]
    public void AnExpiredTokenIsNotUsable_EvenWithNoMargin()
    {
        var token = new AccessToken(Fake.AccessTokenValue, Noon.AddSeconds(-1));

        Assert.False(token.IsUsableAt(Noon, TimeSpan.Zero));
    }

    [Fact]
    public void TheTokenDoesNotPrintItself()
    {
        // The disclosure this prevents is not Console.WriteLine(token.Value). It is an interpolated
        // string written by someone who was being careful about everything except this.
        var token = new AccessToken(Fake.AccessTokenValue, Noon);

        var printed = $"acquired {token}";

        Assert.DoesNotContain(Fake.AccessTokenValue, printed, StringComparison.Ordinal);
        Assert.Contains("redacted", printed, StringComparison.Ordinal);
        Assert.Contains("2026-09-17T12:00:00", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSessionPrintsNeitherSecret()
    {
        var session = Fake.Session(Noon);

        var printed = session.ToString();

        Assert.DoesNotContain(Fake.AccessTokenValue, printed, StringComparison.Ordinal);
        Assert.DoesNotContain(Fake.RefreshTokenValue, printed, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultPolicyLeavesAMinuteOfMargin()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), AuthenticationPolicy.Default.RefreshSkew);
    }
}
