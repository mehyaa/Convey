using Convey.CQRS.Events;
using Shouldly;
using Xunit;

namespace Convey.CQRS.Events.Tests;

public class RejectedEventTests
{
    [Fact]
    public void RejectedEvent_Should_Set_Properties()
    {
        // Arrange & Act
        var rejectedEvent = new RejectedEvent("TestReason", "TestCode");

        // Assert
        rejectedEvent.Reason.ShouldBe("TestReason");
        rejectedEvent.Code.ShouldBe("TestCode");
    }

    [Fact]
    public void RejectedEvent_Should_Implement_IRejectedEvent()
    {
        // Arrange & Act
        IRejectedEvent rejectedEvent = new RejectedEvent("Reason", "Code");

        // Assert
        rejectedEvent.ShouldNotBeNull();
        (rejectedEvent is IEvent).ShouldBeTrue();
    }

    [Fact]
    public void RejectedEvent_Should_Allow_Null_Reason()
    {
        // Arrange & Act
        var rejectedEvent = new RejectedEvent(null, "Code");

        // Assert
        rejectedEvent.Reason.ShouldBeNull();
    }

    [Fact]
    public void RejectedEvent_Should_Allow_Null_Code()
    {
        // Arrange & Act
        var rejectedEvent = new RejectedEvent("Reason", null);

        // Assert
        rejectedEvent.Code.ShouldBeNull();
    }
}
