using System.Collections.Generic;
using System.Linq;
using Convey.CQRS.Queries;
using Shouldly;
using Xunit;

namespace Convey.CQRS.Queries.Tests;

public class PagedResultTests
{
    [Fact]
    public void PagedResult_IsEmpty_Should_Return_True_When_No_Items()
    {
        // Arrange & Act
        var result = PagedResult<string>.Empty;

        // Assert
        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void PagedResult_IsEmpty_Should_Return_False_When_Has_Items()
    {
        // Arrange
        var items = new List<string> { "item" };
        var result = PagedResult<string>.Create(items, 1, 10, 1, 1);

        // Assert
        result.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void PagedResult_IsNotEmpty_Should_Return_True_When_Has_Items()
    {
        // Arrange
        var items = new List<string> { "item" };
        var result = PagedResult<string>.Create(items, 1, 10, 1, 1);

        // Assert
        result.IsNotEmpty.ShouldBeTrue();
    }

    [Fact]
    public void PagedResult_IsNotEmpty_Should_Return_False_When_No_Items()
    {
        // Arrange & Act
        var result = PagedResult<string>.Empty;

        // Assert
        result.IsNotEmpty.ShouldBeFalse();
    }

    [Fact]
    public void PagedResult_Empty_Should_Create_Empty_Result()
    {
        // Arrange & Act
        var result = PagedResult<string>.Empty;

        // Assert
        result.IsEmpty.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public void PagedResult_Create_Should_Create_Result()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };

        // Act
        var result = PagedResult<int>.Create(items, 1, 10, 1, 3);

        // Assert
        result.Items.ShouldBe(items);
        result.CurrentPage.ShouldBe(1);
        result.ResultsPerPage.ShouldBe(10);
        result.TotalPages.ShouldBe(1);
        result.TotalResults.ShouldBe(3);
        result.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void PagedResult_Map_Should_Transform_Items()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };
        var result = PagedResult<int>.Create(items, 1, 10, 1, 3);

        // Act
        var mappedResult = result.Map(x => x.ToString());

        // Assert
        mappedResult.Items.ShouldBe(new[] { "1", "2", "3" });
        mappedResult.CurrentPage.ShouldBe(result.CurrentPage);
        mappedResult.TotalResults.ShouldBe(result.TotalResults);
    }
}
