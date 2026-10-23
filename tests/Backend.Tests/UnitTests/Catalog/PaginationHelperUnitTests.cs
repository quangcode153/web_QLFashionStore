using FluentAssertions;
using Shared.DTOs.Common;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class PaginationHelperUnitTests
{
    [Theory]
    [InlineData(0, 10, 1, 0, false, false)]
    [InlineData(5, 10, 1, 1, false, false)]
    [InlineData(10, 10, 1, 1, false, false)]
    [InlineData(11, 10, 1, 2, false, true)]
    [InlineData(11, 10, 2, 2, true, false)]
    [InlineData(25, 10, 2, 3, true, true)]
    [InlineData(100, 10, 10, 10, true, false)]
    public void PagedResult_Calculates_TotalPages_And_NavigationFlags_Correctly(
        int totalCount, int pageSize, int pageNumber, int expectedTotalPages, bool expectedHasPrev, bool expectedHasNext)
    {
        // Act
        var result = new PagedResult<string>(new List<string>(), totalCount, pageNumber, pageSize);

        // Assert
        result.TotalPages.Should().Be(expectedTotalPages);
        result.HasPreviousPage.Should().Be(expectedHasPrev);
        result.HasNextPage.Should().Be(expectedHasNext);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void PagedResult_WithZeroOrNegativePageSize_ReturnsZeroTotalPages(int pageSize)
    {
        // Act
        var result = new PagedResult<int>(new List<int>(), 50, 1, pageSize);

        // Assert
        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }
}
