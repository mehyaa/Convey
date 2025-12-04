using Convey.Types;
using Shouldly;
using Xunit;

namespace Convey.Tests;

public class IIdentifiableTests
{
    [Fact]
    public void Interface_Should_Be_Implemented_By_Generic_Type()
    {
        // Arrange & Act
        var testEntity = new TestEntity { Id = 123 };

        // Assert
        testEntity.Id.ShouldBe(123);
        (testEntity is IIdentifiable<int>).ShouldBeTrue();
    }

    [Fact]
    public void Interface_Should_Support_Different_Id_Types()
    {
        // Arrange & Act
        var stringEntity = new StringIdEntity { Id = "test-id" };
        var guidEntity = new GuidIdEntity { Id = System.Guid.NewGuid() };
        var longEntity = new LongIdEntity { Id = 9999999999L };

        // Assert
        stringEntity.Id.ShouldBe("test-id");
        guidEntity.Id.ShouldNotBe(System.Guid.Empty);
        longEntity.Id.ShouldBe(9999999999L);
    }

    [Fact]
    public void Interface_Should_Allow_Nullable_Reference_Types()
    {
        // Arrange & Act
        var entity = new StringIdEntity { Id = null };

        // Assert
        entity.Id.ShouldBeNull();
    }

    [Fact]
    public void Interface_Should_Be_Covariant_With_Value_Types()
    {
        // Arrange
        var entity = new TestEntity { Id = 42 };

        // Act
        IIdentifiable<int> identifiable = entity;

        // Assert
        identifiable.Id.ShouldBe(42);
    }

    // Test helper classes
    private class TestEntity : IIdentifiable<int>
    {
        public int Id { get; set; }
    }

    private class StringIdEntity : IIdentifiable<string>
    {
        public string Id { get; set; }
    }

    private class GuidIdEntity : IIdentifiable<System.Guid>
    {
        public System.Guid Id { get; set; }
    }

    private class LongIdEntity : IIdentifiable<long>
    {
        public long Id { get; set; }
    }
}
