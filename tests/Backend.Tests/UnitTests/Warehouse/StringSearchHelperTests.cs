using FluentAssertions;
using Shared.Helpers;
using Xunit;

namespace Backend.Tests.UnitTests.Warehouse;

public class StringSearchHelperTests
{
    #region Vòng 1: Boundary Matrix & Parameterized Tests cho RemoveDiacritics

    [Theory(DisplayName = "TS-FUZZY-01: RemoveDiacritics loại bỏ chính xác toàn bộ dấu tiếng Việt")]
    [InlineData("Áo Thun Nam Cổ Tròn", "ao thun nam co tron")]
    [InlineData("Đầm Dạ Hội Đi Tiệc Đỏ", "dam da hoi di tiec do")]
    [InlineData("Quần Jean Rách Gối", "quan jean rach goi")]
    [InlineData("Sơ Mi Oxford Trắng Cổ Bẻ", "so mi oxford trang co be")]
    [InlineData("Áo Khoác Gió Chống Nước", "ao khoac gio chong nuoc")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void RemoveDiacritics_ShouldReturnCleanUnaccentedString(string? input, string expected)
    {
        // Act
        var result = StringSearchHelper.RemoveDiacritics(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Vòng 1: Boundary Matrix & Multi-token Search cho IsFuzzyMatch

    [Theory(DisplayName = "TS-FUZZY-02: IsFuzzyMatch khớp chuỗi không dấu, đảo vị trí từ khóa và SKU")]
    // Tìm kiếm không dấu
    [InlineData("Áo Thun Nam Cổ Tròn Đen Size L - SKU: ATN-DEN-L", "ao thun den", true)]
    [InlineData("Áo Thun Nam Cổ Tròn Đen Size L - SKU: ATN-DEN-L", "atn-den", true)]
    [InlineData("Áo Thun Nam Cổ Tròn Đen Size L - SKU: ATN-DEN-L", "size l", true)]
    // Đảo thứ tự từ khóa
    [InlineData("Áo Thun Nam Cổ Tròn Đen Size L - SKU: ATN-DEN-L", "den thun ao", true)]
    [InlineData("Quần Tây Nam Cao Cấp Xám Size 32 - SKU: QT-XAM-32", "32 quan xam", true)]
    // Tìm từ viết tắt / subsequence
    [InlineData("Áo Sơ Mi Oxford Dài Tay", "smodt", true)]
    // Không khớp
    [InlineData("Áo Thun Nam Cổ Tròn Đen Size L - SKU: ATN-DEN-L", "quan jean", false)]
    [InlineData("Áo Thun Nam Cổ Tròn Đen Size L - SKU: ATN-DEN-L", "mau do", false)]
    // Query rỗng hoặc khoảng trắng luôn coi là khớp
    [InlineData("Áo Thun Nam", "", true)]
    [InlineData("Áo Thun Nam", "   ", true)]
    [InlineData("Áo Thun Nam", null, true)]
    public void IsFuzzyMatch_ShouldMatchCorrectly(string source, string? query, bool expected)
    {
        // Act
        var result = StringSearchHelper.IsFuzzyMatch(source, query);

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "TS-FUZZY-03: Source null hoặc rỗng nhưng query có giá trị phải trả về false")]
    public void IsFuzzyMatch_NullSourceWithQuery_ReturnsFalse()
    {
        // Act & Assert
        StringSearchHelper.IsFuzzyMatch(null, "ao thun").Should().BeFalse();
        StringSearchHelper.IsFuzzyMatch("", "ao thun").Should().BeFalse();
        StringSearchHelper.IsFuzzyMatch("   ", "ao thun").Should().BeFalse();
    }

    #endregion
}
