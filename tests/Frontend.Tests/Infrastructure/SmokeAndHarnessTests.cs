using System;
using System.Linq;
using System.Text.Json;
using Bunit;
using FluentAssertions;
using Frontend.Pages;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.Enums;
using Xunit;

namespace Frontend.Tests.Infrastructure;

public class SmokeAndHarnessTests : BunitContext
{
    [Fact]
    public void BunitTestContext_CanRenderBasicRazorComponent()
    {
        // Act: Render trang Home bằng bUnit
        var cut = Render<Home>();

        // Assert: Xác nhận bUnit Test Harness hoạt động trơn tru
        cut.Markup.Should().Contain("Hello, world!");
        cut.Find("h1").TextContent.Should().Be("Hello, world!");
    }

    [Fact]
    public void SharedDtos_CanBeUsedSeamlesslyInFrontendTestScope()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 101,
            TenSanPham = "Áo Polo YaMe Cool Comfort",
            TenDanhMuc = "Áo Polo",
            GiaThapNhat = 350000m,
            GiaCaoNhat = 397000m
        };

        var pagedResult = new PagedResult<ProductListItemDto>
        {
            Items = new[] { product },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 10
        };

        // Act: Serialize và Deserialize
        var json = JsonSerializer.Serialize(pagedResult);
        var restored = JsonSerializer.Deserialize<PagedResult<ProductListItemDto>>(json);

        // Assert: DTO Shared dùng chung hoàn hảo không lỗi dữ liệu
        restored.Should().NotBeNull();
        restored!.TotalCount.Should().Be(1);
        restored.Items.First().TenSanPham.Should().Be("Áo Polo YaMe Cool Comfort");
        restored.Items.First().GiaThapNhat.Should().Be(350000m);
    }

    [Fact]
    public void SystemEnums_RoleAndStatusConstants_AreAccessibleInFrontend()
    {
        // Assert: Các hằng số phân quyền và trạng thái đơn hàng sẵn sàng cho Blazor SPA
        RoleConstants.AdminChuoi.Should().Be("Admin chuỗi");
        RoleConstants.ThuNgan.Should().Be("Thu ngân");
        RoleConstants.KhachHang.Should().Be("Khách hàng");
        RoleConstants.IsStaff(RoleConstants.ThuNgan).Should().BeTrue();
        RoleConstants.IsStaff(RoleConstants.KhachHang).Should().BeFalse();

        OrderStatus.ChoXuLy.Should().Be(OrderStatus.ChoXuLy);
        OrderStatusHelper.TryNormalizeStatus("ChoXuLy", out var normalized).Should().BeTrue();
        normalized.Should().Be("Chờ xử lý");
    }
}
