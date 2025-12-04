using Shouldly;
using Xunit;

namespace Convey.CQRS.Queries.Tests
{
    public class PagedQueryBaseTests
    {
        [Fact]
        public void PagedQueryBase_Should_Set_Default_Values()
        {
            // Arrange & Act
            var query = new TestPagedQuery();

            // Assert
            query.OrderBy.ShouldBeNull();
            query.SortOrder.ShouldBeNull();
            query.Page.ShouldBe(0);
            query.Results.ShouldBe(0);
        }

        [Fact]
        public void PagedQueryBase_Should_Allow_Property_Setting()
        {
            // Arrange & Act
            var query = new TestPagedQuery
            {
                Page = 2,
                Results = 25,
                OrderBy = "name",
                SortOrder = "asc"
            };

            // Assert
            query.Page.ShouldBe(2);
            query.Results.ShouldBe(25);
            query.OrderBy.ShouldBe("name");
            query.SortOrder.ShouldBe("asc");
        }

        // Test helper class
        public class TestPagedQuery : PagedQueryBase
        {
        }
    }
}
