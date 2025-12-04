using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class JsonWebTokenTests
{
    [Fact]
    public void JsonWebToken_Should_Set_And_Get_Properties()
    {
        // Arrange
        var token = new JsonWebToken
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            Expires = 1234567890,
            Id = "token_id",
            Role = "admin",
            Claims = new Dictionary<string, IEnumerable<string>>
            {
                { "claim1", new[] { "value1" } }
            }
        };

        // Assert
        token.AccessToken.ShouldBe("access_token");
        token.RefreshToken.ShouldBe("refresh_token");
        token.Expires.ShouldBe(1234567890);
        token.Id.ShouldBe("token_id");
        token.Role.ShouldBe("admin");
        token.Claims["claim1"].ShouldContain("value1");
    }
}
