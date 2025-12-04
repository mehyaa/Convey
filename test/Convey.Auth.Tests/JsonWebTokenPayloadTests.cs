using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Convey.Auth.Tests;

public class JsonWebTokenPayloadTests
{
    [Fact]
    public void JsonWebTokenPayload_Should_Set_And_Get_Properties()
    {
        // Arrange
        var payload = new JsonWebTokenPayload
        {
            Subject = "subject",
            Role = "user",
            Expires = 9876543210,
            Claims = new Dictionary<string, IEnumerable<string>>
            {
                { "scope", new[] { "read", "write" } }
            }
        };

        // Assert
        payload.Subject.ShouldBe("subject");
        payload.Role.ShouldBe("user");
        payload.Expires.ShouldBe(9876543210);
        payload.Claims["scope"].ShouldContain("read");
        payload.Claims["scope"].ShouldContain("write");
    }
}
