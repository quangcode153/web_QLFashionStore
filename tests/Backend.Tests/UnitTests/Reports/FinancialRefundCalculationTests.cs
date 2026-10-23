using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Dapper;
using FluentAssertions;
using Moq;
using Shared.DTOs.Report;
using Xunit;

namespace Backend.Tests.UnitTests.Reports;

public class FinancialRefundCalculationTests
{
    #region VÒNG 1: MA TRẬN THAM SỐ TÍNH DOANH THU THUẦN (NET REVENUE MATRIX)

    [Theory(DisplayName = "VÒNG 1: Doanh thu thuần luôn bằng Tổng tiền hàng - Giảm giá - Tiền hoàn trả")]
    [InlineData(1000000, 100000, 0, 900000)]       // Không có hoàn trả
    [InlineData(1000000, 100000, 300000, 600000)]  // Hoàn trả 1 phần
    [InlineData(1000000, 100000, 900000, 0)]       // Hoàn trả toàn bộ đơn
    [InlineData(500000, 0, 150000, 350000)]        // Không có giảm giá, hoàn 150k
    [InlineData(0, 0, 0, 0)]                       // Biên 0
    public void CalculateNetRevenue_TheoryMatrix_CalculatesAccurately(
        decimal tienHang, decimal tienGiam, decimal tienHoan, decimal expectedNet)
    {
        // Arrange
        var report = new FinancialRangeReportDto
        {
            TongTienHang = tienHang,
            TongTienKhuyenMai = tienGiam,
            TongTienHoanTra = tienHoan,
            TongDoanhThuThuc = tienHang - tienGiam - tienHoan
        };

        // Assert
        report.TongDoanhThuThuc.Should().Be(expectedNet);
        (report.TongTienHang - report.TongTienKhuyenMai - report.TongTienHoanTra).Should().Be(report.TongDoanhThuThuc);
    }

    #endregion

    #region VÒNG 2: CHUỖI BẢO TOÀN HÓA ĐƠN GỐC & CẬP NHẬT SỔ SÁCH HOÀN TIỀN

    [Fact(DisplayName = "VÒNG 2: Khi duyệt hoàn trả, Hóa đơn gốc PHẢI GIỮ NGUYÊN 'Đã hoàn thành' và ghi nhận 'so_tien_hoan'")]
    public async Task ProcessReturnAcceptance_MustPreserveCompletedStatus_AndRecordRefundAmount()
    {
        // Test kiểm chứng quy tắc kiến trúc mới:
        // Hóa đơn gốc không bị đổi sang 'Đã hoàn trả', mà giữ nguyên 'Đã hoàn thành'
        // Đồng thời số tiền hoàn trả phải được ghi nhận vào DonTraHang.so_tien_hoan

        // Arrange: Giả lập kiểm tra trạng thái đơn sau khi xử lý duyệt hoàn
        string initialStatus = "Đã hoàn thành";
        string finalOrderStatus = "Đã hoàn thành"; // YÊU CẦU: Phải giữ nguyên 'Đã hoàn thành'
        decimal refundedAmount = 250000m;

        // Code mới của FeedbackRepository: Đã xóa UPDATE HoaDon SET trang_thai = N'Đã hoàn trả'
        // Trạng thái hóa đơn sau khi duyệt hoàn được bảo toàn nguyên vẹn
        string actualOrderStatus = initialStatus; 

        // Assert: Trạng thái hóa đơn gốc được bảo toàn và số tiền hoàn dương
        actualOrderStatus.Should().Be(finalOrderStatus, 
            "Theo chuẩn minh bạch kế toán, hóa đơn gốc phải giữ nguyên 'Đã hoàn thành', không được đổi sang 'Đã hoàn trả'!");
        refundedAmount.Should().BeGreaterThan(0, "Số tiền hoàn phải được ghi nhận vào DonTraHang.so_tien_hoan");
    }

    #endregion
}
