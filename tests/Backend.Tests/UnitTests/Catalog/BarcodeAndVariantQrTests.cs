using System;
using FluentAssertions;
using Shared.Helpers;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class BarcodeAndVariantQrTests
{
    [Theory(DisplayName = "VÒNG 1: Trích xuất SKU từ các định dạng quét mã vạch và QR thông dụng")]
    [InlineData("POLO-DEN-L", "POLO-DEN-L")]
    [InlineData("polo-den-l", "POLO-DEN-L")]
    [InlineData("  POLO-TRANG-M   ", "POLO-TRANG-M")]
    [InlineData("SKU:SM-XANH-XL", "SM-XANH-XL")]
    [InlineData("sku: sm-xanh-xl ", "SM-XANH-XL")]
    [InlineData("QF:JEAN-DEN-32", "JEAN-DEN-32")]
    [InlineData("QF-JEAN-DEN-32", "JEAN-DEN-32")]
    [InlineData("https://qf-fashion.vercel.app/product/1?sku=TSHIRT-RED-S", "TSHIRT-RED-S")]
    [InlineData("https://qf-fashion.vercel.app/catalog?search=polo&sku=POLO-01", "POLO-01")]
    [InlineData("{\"sku\":\"JACKET-BLK-L\",\"sp\":10}", "JACKET-BLK-L")]
    [InlineData("{\"id\":10,\"sku\":\"HOODIE-GRY-M\"}", "HOODIE-GRY-M")]
    public void ExtractSku_ValidFormats_ExtractsNormalizedUppercaseSku(string rawData, string expectedSku)
    {
        // Act
        var result = BarcodeParser.ExtractSku(rawData);

        // Assert
        result.Should().Be(expectedSku);
    }

    [Theory(DisplayName = "VÒNG 1: Giá trị biên - Chuỗi rỗng, khoảng trắng, null trả về rỗng không ném ngoại lệ")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("\t\r\n")]
    public void ExtractSku_InvalidOrEmpty_ReturnsEmptyString(string? rawData)
    {
        // Act
        var result = BarcodeParser.ExtractSku(rawData);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "VÒNG 2: Sinh URL mã QR chuẩn cho biến thể chứa đầy đủ tham số")]
    public void GenerateQrCodeUrl_ValidSku_GeneratesCorrectQrEndpoint()
    {
        // Arrange
        var sku = "POLO-DEN-L";

        // Act
        var url = BarcodeParser.GenerateQrCodeUrl(sku, 200);

        // Assert
        url.Should().NotBeNullOrWhiteSpace();
        url.Should().Contain("POLO-DEN-L");
        url.Should().Contain("200");
    }
}

