using System;
using System.Collections.Generic;
using FluentAssertions;
using Shared.DTOs.Customer;
using Shared.DTOs.Feedback;
using Shared.DTOs.Order;
using Xunit;

namespace Backend.Tests.UnitTests.Customers;

/// <summary>
/// Unit Tests kiểm thử tính năng Hoàn trả nhiều đợt từng phần (Multi-Stage Partial Return).
/// Tuân thủ nghiêm ngặt Rule 6 (Multi-Round Testing Standard) & Rule 8 (Red-First Gate Rule):
/// - VÒNG 1: Ma trận Tham số & Giá trị Biên đánh giá trạng thái hoàn trả (ConHangCoTheHoan & SoLuongConLai).
/// - VÒNG 2: Chuỗi Chuyển trạng thái Vòng đời Hoàn trả nhiều đợt (Khởi tạo đợt 1 -> Đang xử lý chặn đợt 2 -> Hoàn tất đợt 1 -> Mở đợt 2 -> Chặn vượt SL còn lại).
/// </summary>
public class MultiStagePartialReturnTests
{
    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory(DisplayName = "VÒNG 1: Đánh giá ConHangCoTheHoan chính xác theo ma trận số lượng và trạng thái đơn")]
    [InlineData(5, 2, "Đã hoàn thành", "Đã nhận hàng hoàn", true)]   // Còn 3 món chưa trả, đợt cũ đã xong -> TRUE
    [InlineData(5, 5, "Đã hoàn thành", "Đã nhận hàng hoàn", false)]  // Đã trả hết 5/5 -> FALSE
    [InlineData(5, 2, "Đã hoàn thành", "Cần duyệt", false)]          // Đang có đợt chờ duyệt -> FALSE (phải đợi xử lý xong)
    [InlineData(5, 2, "Đã hoàn thành", "Đang xử lý", false)]         // Đang có đợt đang xử lý -> FALSE
    [InlineData(5, 2, "Đang giao hàng", null, false)]                // Đơn chưa hoàn thành -> FALSE
    [InlineData(5, 2, "Đã hủy", null, false)]                        // Đơn đã hủy -> FALSE
    [InlineData(3, 0, "Đã hoàn thành", null, true)]                  // Đơn hoàn thành chưa trả món nào -> TRUE
    [InlineData(2, 3, "Đã hoàn thành", "Đã xử lý", false)]           // Trả vượt số lượng mua (biên lỗi) -> FALSE
    public void ConHangCoTheHoan_EvaluatesCorrectly(
        int tongMua, int tongTra, string trangThaiDon, string? trangThaiHoan, bool expectedResult)
    {
        // Arrange
        var order = new CustomerOrderHistoryItemDto
        {
            MaHoaDon = 100,
            TrangThai = trangThaiDon,
            TrangThaiHoanTra = trangThaiHoan,
            TongSoLuongDaMua = tongMua,
            TongSoLuongDaTra = tongTra
        };

        // Act
        var result = order.ConHangCoTheHoan;

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory(DisplayName = "VÒNG 1: Đánh giá SoLuongConLai và DaTraHet của từng mặt hàng chi tiết")]
    [InlineData(3, 1, 2, false)] // Mua 3, đã trả 1 -> Còn lại 2, Chưa trả hết
    [InlineData(2, 2, 0, true)]  // Mua 2, đã trả 2 -> Còn lại 0, Đã trả hết
    [InlineData(1, 0, 1, false)] // Mua 1, chưa trả -> Còn lại 1, Chưa trả hết
    [InlineData(2, 5, 0, true)]  // Đã trả vượt số mua (bảo vệ không âm) -> Còn lại 0, Đã trả hết
    public void OrderDetailItemDto_EvaluatesRemainingQtyCorrectly(
        int soLuongMua, int soLuongDaTra, int expectedConLai, bool expectedDaTraHet)
    {
        // Arrange
        var item = new OrderDetailItemDto
        {
            MaBienThe = 101,
            TenSanPham = "Áo Thun Polo Premium",
            SoLuong = soLuongMua,
            SoLuongDaTra = soLuongDaTra
        };

        // Act & Assert
        item.SoLuongConLai.Should().Be(expectedConLai);
        item.DaTraHet.Should().Be(expectedDaTraHet);
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI VÒNG ĐỜI HOÀN TRẢ NHIỀU ĐỢT (STATE MACHINE PIPELINE)

    [Fact(DisplayName = "VÒNG 2: Kiểm tra logic tính toán số lượng khả dụng cho đợt 2 khi đợt 1 đã hoàn trả xong")]
    public void MultiStageReturn_WhenStage1Completed_AllowsStage2ForRemainingItems()
    {
        // Giả lập đơn hàng #1109 có 2 mặt hàng:
        // - Item 1: Áo Khoác Gió (SL mua = 2, đợt 1 đã hoàn trả 1 cái -> SL còn lại = 1)
        // - Item 2: Quần Jean Slim (SL mua = 1, đợt 1 chưa hoàn trả cái nào -> SL còn lại = 1)
        var items = new List<OrderDetailItemDto>
        {
            new() { MaBienThe = 10, TenSanPham = "Áo Khoác Gió", SoLuong = 2, SoLuongDaTra = 1 },
            new() { MaBienThe = 20, TenSanPham = "Quần Jean Slim", SoLuong = 1, SoLuongDaTra = 0 }
        };

        // Assert Item 1
        items[0].SoLuongConLai.Should().Be(1);
        items[0].DaTraHet.Should().BeFalse();

        // Assert Item 2
        items[1].SoLuongConLai.Should().Be(1);
        items[1].DaTraHet.Should().BeFalse();

        // Kiểm tra tổng số lượng của đơn
        int totalPurchased = items.Sum(i => i.SoLuong);
        int totalReturned = items.Sum(i => i.SoLuongDaTra);

        var orderHistory = new CustomerOrderHistoryItemDto
        {
            MaHoaDon = 1109,
            TrangThai = "Đã hoàn thành",
            TrangThaiHoanTra = "Đã nhận hàng hoàn", // Đợt 1 đã xong
            TongSoLuongDaMua = totalPurchased,
            TongSoLuongDaTra = totalReturned
        };

        // Nút Hoàn trả trên OrderHistory PHẢI hiển thị để khách hoàn trả tiếp đợt 2
        orderHistory.ConHangCoTheHoan.Should().BeTrue();
    }

    [Fact(DisplayName = "VÒNG 2: Khi tất cả mặt hàng của đơn đã hoàn trả hết, khóa tính năng hoàn trả")]
    public void MultiStageReturn_WhenAllItemsReturned_DisablesFurtherReturns()
    {
        // Giả lập đơn hàng sau khi hoàn tất đợt 2:
        // - Item 1: Áo Khoác Gió (SL mua = 2, đã trả = 2)
        // - Item 2: Quần Jean Slim (SL mua = 1, đã trả = 1)
        var items = new List<OrderDetailItemDto>
        {
            new() { MaBienThe = 10, TenSanPham = "Áo Khoác Gió", SoLuong = 2, SoLuongDaTra = 2 },
            new() { MaBienThe = 20, TenSanPham = "Quần Jean Slim", SoLuong = 1, SoLuongDaTra = 1 }
        };

        items.Should().OnlyContain(i => i.DaTraHet);
        items.Should().OnlyContain(i => i.SoLuongConLai == 0);

        var orderHistory = new CustomerOrderHistoryItemDto
        {
            MaHoaDon = 1109,
            TrangThai = "Đã hoàn thành",
            TrangThaiHoanTra = "Đã xử lý",
            TongSoLuongDaMua = items.Sum(i => i.SoLuong),
            TongSoLuongDaTra = items.Sum(i => i.SoLuongDaTra)
        };

        // Không còn hàng có thể hoàn -> Nút Hoàn trả không được hiển thị
        orderHistory.ConHangCoTheHoan.Should().BeFalse();
    }

    #endregion
}
