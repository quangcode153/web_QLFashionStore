using FluentAssertions;
using Shared.Enums;
using Xunit;

namespace Backend.Tests.UnitTests.Orders;

/// <summary>
/// Kịch bản kiểm thử chuẩn hóa trạng thái đơn hàng dựa trên đặc tả TEST_SPECIFICATION.md:
/// - TS-ORD-04: Chuẩn hóa Whitelist trạng thái đơn hàng hợp lệ
/// Phải khớp chính xác với ràng buộc CSDL CK_TrangThaiHoaDon: ('Chờ xử lý', 'Đã hoàn thành', 'Đã hủy')
/// </summary>
public class OrderStatusHelperTests
{
    [Theory(DisplayName = "TS-ORD-04: Chuẩn hóa thành công các giá trị trạng thái hợp lệ trong Whitelist (cả enum không dấu và tiếng Việt có dấu, khoảng trắng thừa)")]
    [InlineData("ChoXuLy", "Chờ xử lý")]
    [InlineData("choxuly", "Chờ xử lý")]
    [InlineData("  ChoXuLy  ", "Chờ xử lý")]
    [InlineData("Chờ xử lý", "Chờ xử lý")]
    [InlineData("   chờ xử lý   ", "Chờ xử lý")]
    [InlineData("Cho_Xu_Ly", "Chờ xử lý")]
    [InlineData("DaHoanThanh", "Đã hoàn thành")]
    [InlineData("dahoanthanh", "Đã hoàn thành")]
    [InlineData("  Đã hoàn thành  ", "Đã hoàn thành")]
    [InlineData("đã hoàn thành", "Đã hoàn thành")]
    [InlineData("Da_Hoan_Thanh", "Đã hoàn thành")]
    [InlineData("Completed", "Đã hoàn thành")]
    [InlineData("DaHuy", "Đã hủy")]
    [InlineData("dahuy", "Đã hủy")]
    [InlineData("Đã hủy", "Đã hủy")]
    [InlineData("  đã hủy  ", "Đã hủy")]
    [InlineData("Da_Huy", "Đã hủy")]
    [InlineData("Cancelled", "Đã hủy")]
    [InlineData("DangGiaoHang", "Đang giao hàng")]
    [InlineData("danggiaohang", "Đang giao hàng")]
    [InlineData("  Đang giao hàng  ", "Đang giao hàng")]
    [InlineData("đang giao hàng", "Đang giao hàng")]
    [InlineData("Dang_Giao_Hang", "Đang giao hàng")]
    [InlineData("DangGiao", "Đang giao hàng")]
    [InlineData("Đang giao", "Đang giao hàng")]
    [InlineData("Shipping", "Đang giao hàng")]
    [InlineData("Delivering", "Đang giao hàng")]
    public void TryNormalizeStatus_ShouldReturnTrueAndCanonicalVietnameseString_ForValidInputs(
        string rawInput,
        string expectedVietnamese)
    {
        // Act
        var result = OrderStatusHelper.TryNormalizeStatus(rawInput, out var normalizedStatus);

        // Assert
        result.Should().BeTrue("Trạng thái hợp lệ phải được chấp nhận theo Whitelist");
        normalizedStatus.Should().Be(expectedVietnamese, "Chuỗi chuẩn hóa trả về phải là tiếng Việt có dấu thống nhất với Trigger và Constraint CSDL");
    }

    [Theory(DisplayName = "TS-ORD-04: Từ chối các chuỗi trạng thái không nằm trong Whitelist hoặc có dấu hiệu tấn công")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TrangThaiHack")]
    [InlineData("GiaoThatBai")]
    [InlineData("DangXuLySai")]
    [InlineData("DangGiao123")]
    [InlineData("Pending")]
    [InlineData("Hủy bỏ")]
    [InlineData("ChoXuLy' OR 1=1--")]
    [InlineData("<script>alert('hack')</script>")]
    public void TryNormalizeStatus_ShouldReturnFalse_ForInvalidInputs(string? invalidInput)
    {
        // Act
        var result = OrderStatusHelper.TryNormalizeStatus(invalidInput!, out var normalizedStatus);

        // Assert
        result.Should().BeFalse("Các trạng thái lạ phải bị từ chối để tránh vi phạm CK_TrangThaiHoaDon trong CSDL");
        normalizedStatus.Should().BeEmpty();
    }
}
