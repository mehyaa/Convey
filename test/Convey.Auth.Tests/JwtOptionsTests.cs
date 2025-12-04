using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class JwtOptionsTests
{
    [Fact]
    public void JwtOptions_Should_Have_Default_Values()
    {
        var options = new JwtOptions();

        options.SaveToken.ShouldBeTrue();
        options.RequireAudience.ShouldBeTrue();
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.RequireExpirationTime.ShouldBeTrue();
        options.RequireSignedTokens.ShouldBeTrue();
        options.ValidateAudience.ShouldBeTrue();
        options.ValidateIssuer.ShouldBeTrue();
        options.ValidateLifetime.ShouldBeTrue();
        options.RefreshOnIssuerKeyNotFound.ShouldBeTrue();
        options.IncludeErrorDetails.ShouldBeTrue();
        options.Challenge.ShouldBe("Bearer");
    }
}
