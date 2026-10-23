using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.DTOs.Report;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class AdminDashboardComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public AdminDashboardComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync(It.IsAny<int?>())).ReturnsAsync(CreateSampleOrders());
        _mockAdminApi.Setup(api => api.GetBranchesAsync()).ReturnsAsync(new List<BranchDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)" },
            new() { MaChiNhanh = 2, TenChiNhanh = "QF Quang Trung (Q. Gò Vấp, TP.HCM)" },
            new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình (TP.HCM)", IsOnlineWarehouse = true }
        });

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
    }

    private List<DailyRevenueReportDto> CreateSampleDailyRevenue()
    {
        return new List<DailyRevenueReportDto>
        {
            new() { NgayBan = new DateTime(2026, 10, 7), TongSoDon = 15, TongTienHang = 15000000m, TongTienGiam = 500000m, DoanhThuThucTe = 14500000m },
            new() { NgayBan = new DateTime(2026, 10, 8), TongSoDon = 20, TongTienHang = 22000000m, TongTienGiam = 1000000m, DoanhThuThucTe = 21000000m }
        };
    }

    private List<TopSellingProductDto> CreateSampleTopSelling()
    {
        return new List<TopSellingProductDto>
        {
            new() { MaSanPham = 1, TenSanPham = "Áo Thun Oversize YaMe", TenDanhMuc = "Áo Thun", TongSoLuongDaBan = 85, TongDoanhThuMangLai = 12750000m },
            new() { MaSanPham = 2, TenSanPham = "Quần Jean Slimfit Co Giãn", TenDanhMuc = "Quần Jean", TongSoLuongDaBan = 42, TongDoanhThuMangLai = 14700000m }
        };
    }

    private List<LowStockAlertDto> CreateSampleLowStock()
    {
        return new List<LowStockAlertDto>
        {
            new() { MaBienThe = 101, TenSanPham = "Áo Thun Cotton Basic", MaSku = "AT-COT-01", MauSac = "Đen", KichCo = "L", SoLuongTon = 3, GiaNhap = 80000m },
            new() { MaBienThe = 102, TenSanPham = "Quần Kaki Túi Hộp", MaSku = "QK-BOX-02", MauSac = "Rêu", KichCo = "XL", SoLuongTon = 8, GiaNhap = 190000m }
        };
    }

    private List<SupplierDto> CreateSampleSuppliers()
    {
        return new List<SupplierDto>
        {
            new() { MaNhaCungCap = 1, TenNhaCungCap = "Xưởng Dệt Phong Cách Mới", DienThoaiLienHe = "0901112233" },
            new() { MaNhaCungCap = 2, TenNhaCungCap = "Công Ty TNHH May Mặc Sài Gòn", DienThoaiLienHe = "0904445566" }
        };
    }

    private List<OrderSummaryDto> CreateSampleOrders()
    {
        return new List<OrderSummaryDto>
        {
            new() { MaHoaDon = 5001, TenKhachHang = "Lê Hoàng Long", SdtKhachHang = "0912345678", NgayLapHoaDon = DateTime.Now, TienHang = 500000m, TienGiamGia = 0m, TongThanhToan = 500000m, TrangThai = "Chờ xử lý" },
            new() { MaHoaDon = 5002, TenKhachHang = "Nguyễn Minh Châu", SdtKhachHang = "0987654321", NgayLapHoaDon = DateTime.Now.AddHours(-2), TienHang = 1200000m, TienGiamGia = 60000m, TongThanhToan = 1140000m, TrangThai = "Đã hoàn thành" },
            new() { MaHoaDon = 5003, TenKhachHang = "Phạm Anh Tuấn", SdtKhachHang = "0909090909", NgayLapHoaDon = DateTime.Now.AddDays(-1), TienHang = 350000m, TienGiamGia = 0m, TongThanhToan = 350000m, TrangThai = "Đã hủy" }
        };
    }

    private PagedResult<ProductListItemDto> CreateSamplePagedProducts()
    {
        return new PagedResult<ProductListItemDto>
        {
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 2,
            Items = new List<ProductListItemDto>
            {
                new() { MaSanPham = 1, TenSanPham = "Áo Polo Pique YaMe", TenDanhMuc = "Áo Polo", TenThuongHieu = "YaMe", GiaThapNhat = 220000m, GiaCaoNhat = 220000m, TongTonKho = 45 },
                new() { MaSanPham = 2, TenSanPham = "Áo Sơ Mi Oxford Dài Tay", TenDanhMuc = "Áo Sơ Mi", TenThuongHieu = "YaMe", GiaThapNhat = 320000m, GiaCaoNhat = 320000m, TongTonKho = 18 }
            }
        };
    }

    #region 1. Report & Dashboard Tests (SCR-12 / TS-FE-06)

    [Fact]
    public void ReportDashboard_InitialRender_ShowsKpisDailyRevenueAndTopSelling()
    {
        _mockAdminApi.Setup(api => api.GetDailyRevenueAsync()).ReturnsAsync(CreateSampleDailyRevenue());
        _mockAdminApi.Setup(api => api.GetTopSellingProductsAsync(It.IsAny<int>())).ReturnsAsync(CreateSampleTopSelling());

        var cut = Render<ReportDashboard>();

        cut.Markup.Should().Contain("BÁO CÁO & TỔNG QUAN");
        cut.Markup.Should().Contain("Áo Thun Oversize YaMe");
        cut.Markup.Should().Contain("Quần Jean Slimfit Co Giãn");
        cut.Markup.Should().Contain("12.750.000"); // Doanh thu SP 1
        cut.Markup.Should().Contain("21.000.000"); // Doanh thu ngày 8/10
    }

    [Fact]
    public void ReportDashboard_DateRangeFilter_FetchesFinancialReport()
    {
        _mockAdminApi.Setup(api => api.GetDailyRevenueAsync()).ReturnsAsync(new List<DailyRevenueReportDto>());
        _mockAdminApi.Setup(api => api.GetTopSellingProductsAsync(It.IsAny<int>())).ReturnsAsync(new List<TopSellingProductDto>());
        _mockAdminApi.Setup(api => api.GetFinancialReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new FinancialRangeReportDto
            {
                TongSoDonHoanThanh = 58,
                TongSoKhachHang = 45,
                TongTienHang = 60000000m,
                TongTienKhuyenMai = 3500000m,
                TongDoanhThuThuc = 56500000m
            });

        var cut = Render<ReportDashboard>();

        cut.Find("button.btn-apply-range").Click();

        cut.Markup.Should().Contain("56.500.000"); // Doanh thu thực
        cut.Markup.Should().Contain("58"); // Số đơn hoàn thành
    }

    [Fact]
    public void ReportDashboard_RendersBranchLeaderboardAndWarehouseOversight()
    {
        _mockAdminApi.Setup(api => api.GetDailyRevenueAsync()).ReturnsAsync(new List<DailyRevenueReportDto>());
        _mockAdminApi.Setup(api => api.GetTopSellingProductsAsync(It.IsAny<int>())).ReturnsAsync(new List<TopSellingProductDto>());
        _mockAdminApi.Setup(api => api.GetBranchSalesLeaderboardAsync()).ReturnsAsync(new List<BranchSalesLeaderboardDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, TongSoDon = 123, TongDoanhThu = 112848000m, TyLeDongGop = 86.04m },
            new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình", LoaiChiNhanh = "Kho tổng online", IsOnlineWarehouse = true, TongSoDon = 45, TongDoanhThu = 35000000m, TyLeDongGop = 13.96m }
        });
        _mockAdminApi.Setup(api => api.GetBranchInventorySummaryAsync()).ReturnsAsync(new List<BranchInventorySummaryDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh", IsOnlineWarehouse = false, TongSoMatHang = 351, TongSoLuongTon = 12568, SoMatHangSapHet = 7, TongGiaTriTonKho = 2464833000m },
            new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình", IsOnlineWarehouse = true, TongSoMatHang = 351, TongSoLuongTon = 15000, SoMatHangSapHet = 2, TongGiaTriTonKho = 3000000000m }
        });

        var cut = Render<ReportDashboard>();

        cut.Markup.Should().Contain("HIỆU SUẤT CHI NHÁNH & GIÁM SÁT KHO HÀNG ĐA ĐIỂM");
        cut.Markup.Should().Contain("QF Sư Vạn Hạnh");
        cut.Markup.Should().Contain("Kho Tổng QF Tân Bình");
        cut.Markup.Should().Contain("Kho Tổng Online");
        cut.Markup.Should().Contain("Cửa hàng Offline");
        cut.Markup.Should().Contain("112.848.000");
        cut.Markup.Should().Contain("12.568");
        cut.Markup.Should().Contain("7 SKU");
    }

    #endregion

    #region 2. Warehouse & Low Stock Alert Tests (SCR-11 / TS-FE-06)

    [Fact]
    public void WarehouseManagement_InitialRender_ShowsLowStockAlertsAndSuppliers()
    {
        _mockAdminApi.Setup(api => api.GetLowStockAlertsAsync(It.IsAny<int>())).ReturnsAsync(CreateSampleLowStock());
        _mockAdminApi.Setup(api => api.GetSuppliersAsync()).ReturnsAsync(CreateSampleSuppliers());

        var cut = Render<WarehouseManagement>();

        cut.Markup.Should().Contain("CẢNH BÁO TỒN KHO");
        cut.Markup.Should().Contain("AT-COT-01");
        cut.Markup.Should().Contain("Sắp hết hàng"); // Badge cảnh báo <= 10
        cut.Markup.Should().Contain("Xưởng Dệt Phong Cách Mới");
    }

    [Fact]
    public void WarehouseManagement_SubmitImportReceipt_CallsImportApiAndShowsToast()
    {
        _mockAdminApi.Setup(api => api.GetLowStockAlertsAsync(It.IsAny<int>())).ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(api => api.GetSuppliersAsync()).ReturnsAsync(CreateSampleSuppliers());
        _mockAdminApi.Setup(api => api.ImportStockAsync(It.IsAny<ImportStockRequestDto>())).ReturnsAsync(true);

        var cut = Render<WarehouseManagement>();

        // Điền form nhập kho
        cut.Find("input.input-import-variant").Change("101");
        cut.Find("input.input-import-qty").Change("50");
        cut.Find("input.input-import-price").Change("85000");

        cut.Find("button.btn-submit-import").Click();

        _mockAdminApi.Verify(api => api.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Once);
        _mockToastService.Verify(toast => toast.ShowSuccess(It.Is<string>(s => s.Contains("thành công"))), Times.Once);
    }

    [Fact]
    public void WarehouseManagement_SelectBranch_PassesChosenBranchToApi()
    {
        _mockAdminApi.Setup(api => api.GetLowStockAlertsAsync(It.IsAny<int>())).ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(api => api.GetSuppliersAsync()).ReturnsAsync(CreateSampleSuppliers());
        ImportStockRequestDto capturedReq = null!;
        _mockAdminApi.Setup(api => api.ImportStockAsync(It.IsAny<ImportStockRequestDto>()))
            .Callback<ImportStockRequestDto>(r => capturedReq = r)
            .ReturnsAsync(true);

        var cut = Render<WarehouseManagement>();

        // Kiểm tra nút thêm nguồn hàng không bị dư dấu +
        cut.Find("button.btn-toggle-supplier-mode span").TextContent.Should().Be("Nhập nguồn hàng mới (Admin)");

        // Chọn chi nhánh 2 (Quận 10)
        var branchSelect = cut.Find("select.select-branch");
        branchSelect.Change("2");

        // Điền form và submit
        cut.Find("input.input-import-variant").Change("102");
        cut.Find("input.input-import-qty").Change("20");
        cut.Find("input.input-import-price").Change("150000");
        cut.Find("button.btn-submit-import").Click();

        capturedReq.Should().NotBeNull();
        capturedReq.MaChiNhanh.Should().Be(2);
        capturedReq.MaBienThe.Should().Be(102);
    }

    #endregion

    #region 3. Order Management & Status Transition Tests (SCR-10 / TS-FE-06)

    [Fact]
    public void OrderManagement_InitialRender_DisplaysAllOrders()
    {
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());

        var cut = Render<OrderManagement>();

        cut.Markup.Should().Contain("QUẢN LÝ ĐƠN HÀNG");
        cut.Markup.Should().Contain("5001");
        cut.Markup.Should().Contain("Lê Hoàng Long");
        cut.Markup.Should().Contain("5002");
        cut.Markup.Should().Contain("Nguyễn Minh Châu");
    }

    [Fact]
    public void OrderManagement_FilterByStatus_FiltersOrdersList()
    {
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());

        var cut = Render<OrderManagement>();

        // Lọc "Đã hoàn thành"
        var btnFilter = cut.Find("button[data-filter='Đã hoàn thành']");
        btnFilter.Click();

        cut.Markup.Should().Contain("5002");
        cut.Markup.Should().NotContain("5001"); // Đơn 5001 là "Chờ xử lý"
    }

    [Fact]
    public void OrderManagement_UpdateStatus_CallsApiAndUpdatesState()
    {
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());
        _mockAdminApi.Setup(api => api.UpdateOrderStatusAsync(5001, "Đã hoàn thành")).ReturnsAsync(true);

        var cut = Render<OrderManagement>();

        // Bấm hoàn thành đơn 5001
        var btnComplete = cut.Find("button[data-action-complete='5001']");
        btnComplete.Click();

        _mockAdminApi.Verify(api => api.UpdateOrderStatusAsync(5001, "Đã hoàn thành"), Times.Once);
        _mockToastService.Verify(toast => toast.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void OrderManagement_SearchByOrderId_FiltersCorrectOrder()
    {
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());

        var cut = Render<OrderManagement>();

        // Tìm kiếm đơn #5002
        var searchInput = cut.Find("input.order-search-input");
        searchInput.Input("5002");

        cut.Markup.Should().Contain("5002");
        cut.Markup.Should().NotContain("5001");
        cut.Markup.Should().Contain("Nguyễn Minh Châu");
    }

    [Fact]
    public void OrderManagement_OpenDetailModal_DisplaysOrderItemsAndTotals()
    {
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());
        _mockAdminApi.Setup(api => api.GetOrderDetailsAsync(5001)).ReturnsAsync(new List<OrderDetailItemDto>
        {
            new() { TenSanPham = "Áo Polo Pique YaMe", MauSac = "Trắng", KichCo = "L", SoLuong = 2, DonGia = 200000m, ThanhTien = 400000m }
        });

        var cut = Render<OrderManagement>();

        // Bấm xem chi tiết đơn 5001
        var btnDetail = cut.Find("button.btn-outline-info");
        btnDetail.Click();

        cut.Markup.Should().Contain("CHI TIẾT ĐƠN HÀNG #5001");
        cut.Markup.Should().Contain("Áo Polo Pique YaMe");
        cut.Markup.Should().Contain("Trắng");
    }

    #endregion

    #region 4. Product Management Tests (SCR-09 / TS-FE-06)

    [Fact]
    public void ProductManagement_InitialRender_DisplaysProductTable()
    {
        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(CreateSamplePagedProducts());

        var cut = Render<ProductManagement>();

        cut.Markup.Should().Contain("QUẢN LÝ SẢN PHẨM");
        cut.Markup.Should().Contain("Áo Polo Pique YaMe");
        cut.Markup.Should().Contain("Áo Sơ Mi Oxford Dài Tay");
        cut.Markup.Should().Contain("45"); // Tồn kho SP 1
    }

    [Fact]
    public void ProductManagement_ClickAddVariantButton_OpensVariantCreationModal()
    {
        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(CreateSamplePagedProducts());

        var cut = Render<ProductManagement>();

        // Ban đầu modal chưa mở
        cut.Markup.Should().NotContain("THÊM BIẾN THỂ SẢN PHẨM");

        // Click nút "+ Thêm biến thể" ở header
        var btnAddVariant = cut.Find("button.btn-action-primary");
        btnAddVariant.Click();

        // Modal form nhập thông tin biến thể phải được hiển thị
        cut.Markup.Should().Contain("THÊM BIẾN THỂ SẢN PHẨM");
        cut.Markup.Should().Contain("Mã SKU (Stock Keeping Unit)");
        cut.Markup.Should().Contain("Lưu biến thể vào CSDL");
    }

    [Fact]
    public void ProductManagement_RowAddVariantClick_PrefillsSelectedProduct()
    {
        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(CreateSamplePagedProducts());

        var cut = Render<ProductManagement>();

        // Bấm nút thêm biến thể ở dòng sản phẩm đầu tiên
        var btnRowAddVariant = cut.Find("button.btn-add-variant");
        btnRowAddVariant.Click();

        // Modal mở ra và hiển thị thông tin sản phẩm đã chọn
        cut.Markup.Should().Contain("THÊM BIẾN THỂ SẢN PHẨM");
        cut.Markup.Should().Contain("Áo Polo Pique YaMe");
        cut.Markup.Should().Contain("#1");
    }

    [Fact]
    public void ProductManagement_SubmitValidVariant_CallsApiAndShowsSuccessToast()
    {
        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(CreateSamplePagedProducts());
        _mockAdminApi.Setup(api => api.CreateProductVariantAsync(It.IsAny<CreateProductVariantRequest>()))
            .ReturnsAsync(ApiResponse<int>.Ok(999, "Thành công"));

        var cut = Render<ProductManagement>();

        // Mở modal thêm biến thể
        cut.Find("button.btn-action-primary").Click();

        // Bấm nút Lưu biến thể
        cut.Find("button.btn-modal-submit").Click();

        // Assert API được gọi
        _mockAdminApi.Verify(api => api.CreateProductVariantAsync(It.IsAny<CreateProductVariantRequest>()), Times.Once);
        _mockToastService.Verify(toast => toast.ShowSuccess(It.Is<string>(s => s.Contains("thành công"))), Times.Once);
    }

    #endregion

    #region 5. Warehouse & Order State Machine Pipeline (Multi-Round Testing)

    [Theory]
    [InlineData(0, 50, 100000)]   // VariantId = 0
    [InlineData(-1, 50, 100000)]  // VariantId âm
    [InlineData(101, 0, 100000)]   // Số lượng = 0
    [InlineData(101, -10, 100000)] // Số lượng âm
    [InlineData(101, 50, 0)]       // Giá = 0
    [InlineData(101, 50, -50000)]  // Giá âm
    public void WarehouseManagement_InvalidImportInputs_RejectsSubmissionAndShowsWarning(int variantId, int qty, decimal price)
    {
        // Arrange
        _mockAdminApi.Setup(api => api.GetLowStockAlertsAsync(It.IsAny<int>())).ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(api => api.GetSuppliersAsync()).ReturnsAsync(CreateSampleSuppliers());

        var cut = Render<WarehouseManagement>();

        cut.Find("input.input-import-variant").Change(variantId.ToString());
        cut.Find("input.input-import-qty").Change(qty.ToString());
        cut.Find("input.input-import-price").Change(price.ToString());

        // Act
        cut.Find("button.btn-submit-import").Click();

        // Assert: API không được gọi và hiển thị cảnh báo
        _mockAdminApi.Verify(api => api.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
        _mockToastService.Verify(toast => toast.ShowWarning(It.Is<string>(s => s.Contains("hợp lệ"))), Times.Once);
    }

    [Fact]
    public void OrderManagement_CancelOrder_CallsApiWithCancelledStatus()
    {
        // Arrange
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync()).ReturnsAsync(CreateSampleOrders());
        _mockAdminApi.Setup(api => api.UpdateOrderStatusAsync(5001, "Đã hủy")).ReturnsAsync(true);

        var cut = Render<OrderManagement>();

        // Act: Bấm Hủy đơn 5001
        var btnCancel = cut.Find("button[data-action-cancel='5001']");
        btnCancel.Click();

        // Assert: API được gọi với "Đã hủy"
        _mockAdminApi.Verify(api => api.UpdateOrderStatusAsync(5001, "Đã hủy"), Times.Once);
        _mockToastService.Verify(toast => toast.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    #endregion
}
