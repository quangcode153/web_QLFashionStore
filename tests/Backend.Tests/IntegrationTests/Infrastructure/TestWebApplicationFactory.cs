using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Repositories;
using Backend.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.DTOs.Report;
using Shared.DTOs.Warehouse;

namespace Backend.Tests.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory khởi chạy máy chủ kiểm thử in-memory cho các kịch bản Integration Test.
/// Cung cấp Mock Repositories dựa trên Specification để kiểm thử trọn vẹn HTTP Pipeline,
/// Routing, Controllers, Middlewares, Rate Limiting, JWT và IDOR độc lập với CSDL bên ngoài.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestPepper = "FashionStore_Secret_Pepper_Password_Hasher_2026_Secure_Key_X9#mK@vL!";
    private const string TestJwtSecret = TestTokenHelper.FallbackJwtSecret;

    static TestWebApplicationFactory()
    {
        DotNetEnv.Env.TraversePath().Load();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfigs = new Dictionary<string, string?>
            {
                ["JWT_SECRET_KEY"] = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? TestJwtSecret,
                ["PASSWORD_PEPPER"] = Environment.GetEnvironmentVariable("PASSWORD_PEPPER") ?? TestPepper,
                ["JWT_ISSUER"] = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "FashionStoreBackend",
                ["JWT_AUDIENCE"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "FashionStoreClients",
                ["CONNECTION_STRING"] = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "Server=localhost;Database=FashionStore;Trusted_Connection=True;TrustServerCertificate=True;"
            };

            config.AddInMemoryCollection(testConfigs);
        });

        builder.ConfigureServices(services =>
        {
            // 1. Mock IAuthRepository theo dữ liệu mẫu trong Specification
            var hasher = new PasswordHasher(TestPepper);
            var mockAuthRepo = new Mock<IAuthRepository>();
            mockAuthRepo.Setup(r => r.GetUserByUsernameAsync("admin"))
                .ReturnsAsync(new UserAuthModel
                {
                    MaTaiKhoan = 1,
                    TenDangNhap = "admin",
                    MatKhauHash = hasher.HashPassword("admin123"),
                    DangHoatDong = true,
                    MaNhanVien = 1,
                    TenNhanVien = "Admin chuỗi",
                    VaiTro = "Admin chuỗi"
                });

            // 2. Mock IProductRepository theo Specification Catalog & PDP
            var mockProductRepo = new Mock<IProductRepository>();
            mockProductRepo.Setup(r => r.GetProductsCatalogAsync())
                .ReturnsAsync(new List<ProductListItemDto>
                {
                    new()
                    {
                        MaSanPham = 1,
                        TenSanPham = "Áo Polo Nam Classic",
                        TenDanhMuc = "Áo Nam",
                        TenThuongHieu = "Coolmate",
                        GiaThapNhat = 250000,
                        GiaCaoNhat = 290000,
                        TongTonKho = 150
                    }
                });

            mockProductRepo.Setup(r => r.GetProductVariantsAsync(1))
                .ReturnsAsync(new List<ProductVariantDto>
                {
                    new()
                    {
                        MaBienThe = 1,
                        MaSanPham = 1,
                        TenSanPham = "Áo Polo Nam Classic",
                        MaSku = "AP-NAM-BLK-M",
                        MauSac = "Đen",
                        KichCo = "M",
                        GiaBan = 250000,
                        SoLuongTon = 50,
                        TinhTrangHang = "Còn hàng"
                    }
                });

            // 2b. Mock ICatalogRepository theo Specification Catalog & POS SKU
            var mockCatalogRepo = new Mock<ICatalogRepository>();
            mockCatalogRepo.Setup(r => r.GetProductsPagedAsync(It.IsAny<ProductQueryParameters>()))
                .ReturnsAsync((ProductQueryParameters q) =>
                {
                    var page = q.PageNumber <= 0 ? 1 : q.PageNumber;
                    var size = q.PageSize <= 0 ? 10 : (q.PageSize > 100 ? 100 : q.PageSize);
                    return new PagedResult<ProductListItemDto>
                    {
                        PageNumber = page,
                        PageSize = size,
                        TotalCount = 2,
                        Items = page == 1
                            ? new List<ProductListItemDto>
                            {
                                new() { MaSanPham = 1, TenSanPham = "Áo Polo Nam Classic", GiaThapNhat = 250000, GiaCaoNhat = 290000, TongTonKho = 150 },
                                new() { MaSanPham = 2, TenSanPham = "Quần Jean Nam Slimfit", GiaThapNhat = 350000, GiaCaoNhat = 390000, TongTonKho = 80 }
                            }
                            : new List<ProductListItemDto>
                            {
                                new() { MaSanPham = 3, TenSanPham = "Áo Thun Cotton Basic", GiaThapNhat = 150000, GiaCaoNhat = 150000, TongTonKho = 200 }
                            }
                    };
                });

            mockCatalogRepo.Setup(r => r.GetProductVariantsAsync(1))
                .ReturnsAsync(new List<ProductVariantDto>
                {
                    new()
                    {
                        MaBienThe = 1,
                        MaSanPham = 1,
                        TenSanPham = "Áo Polo Nam Classic",
                        MaSku = "AP-NAM-BLK-M",
                        MauSac = "Đen",
                        KichCo = "M",
                        GiaBan = 250000,
                        SoLuongTon = 50,
                        TinhTrangHang = "Còn hàng"
                    }
                });

            mockCatalogRepo.Setup(r => r.GetProductVariantsAsync(It.Is<int>(id => id != 1)))
                .ReturnsAsync(new List<ProductVariantDto>());

            mockCatalogRepo.Setup(r => r.GetVariantBySkuAsync("AP-NAM-BLK-M"))
                .ReturnsAsync(new ProductVariantDto
                {
                    MaBienThe = 1,
                    MaSanPham = 1,
                    TenSanPham = "Áo Polo Nam Classic",
                    MaSku = "AP-NAM-BLK-M",
                    MauSac = "Đen",
                    KichCo = "M",
                    GiaBan = 250000,
                    SoLuongTon = 50,
                    TinhTrangHang = "Còn hàng"
                });

            mockCatalogRepo.Setup(r => r.GetVariantBySkuAsync(It.Is<string>(s => s != "AP-NAM-BLK-M")))
                .ReturnsAsync((ProductVariantDto?)null);

            mockCatalogRepo.Setup(r => r.GetCategoriesAsync())
                .ReturnsAsync(new List<CategoryDto>
                {
                    new() { MaDanhMuc = 1, TenDanhMuc = "Áo Nam" },
                    new() { MaDanhMuc = 2, TenDanhMuc = "Quần Nam" }
                });

            mockCatalogRepo.Setup(r => r.GetBrandsAsync())
                .ReturnsAsync(new List<BrandDto>
                {
                    new() { MaThuongHieu = 1, TenThuongHieu = "Coolmate" },
                    new() { MaThuongHieu = 2, TenThuongHieu = "YaMe" }
                });

            // 3. Mock IOrderRepository theo Specification IDOR & Order Status
            var mockOrderRepo = new Mock<IOrderRepository>();
            // Đơn 1 thuộc về khách hàng ID = 1
            mockOrderRepo.Setup(r => r.GetOrderOwnershipAsync(1))
                .ReturnsAsync((true, 1));
            // Đơn khác không tồn tại
            mockOrderRepo.Setup(r => r.GetOrderOwnershipAsync(It.Is<int>(id => id != 1)))
                .ReturnsAsync((false, (int?)null));

            mockOrderRepo.Setup(r => r.GetOrderStatusAsync(1))
                .ReturnsAsync("Chờ xử lý");
            mockOrderRepo.Setup(r => r.GetOrderStatusAsync(It.Is<int>(id => id != 1)))
                .ReturnsAsync((string?)null);

            mockOrderRepo.Setup(r => r.GetOrderDetailsAsync(1))
                .ReturnsAsync(new List<OrderDetailItemDto>
                {
                    new()
                    {
                        TenSanPham = "Áo Polo Nam Classic",
                        MauSac = "Đen",
                        KichCo = "M",
                        SoLuong = 1,
                        DonGia = 250000,
                        ThanhTien = 250000
                    }
                });

            mockOrderRepo.Setup(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Mock cho In Hóa Đơn K80
            mockOrderRepo.Setup(r => r.GetReceiptDetailsAsync(1))
                .ReturnsAsync(new ReceiptPrintDto
                {
                    MaHoaDon = 1,
                    NgayLapHoaDon = new DateTime(2026, 10, 7, 10, 30, 0),
                    TenKhachHang = "Nguyễn Thảo Vy",
                    SdtKhachHang = "0922222222",
                    ThuNgan = "Trần Thị Bích",
                    TenKhuyenMai = "Ưu Đãi Chào Hè 2026",
                    TienHang = 400000,
                    TienGiamGia = 40000,
                    TongThanhToan = 360000,
                    TrangThai = "Đã hoàn thành",
                    Items = new List<ReceiptItemDto>
                    {
                        new()
                        {
                            TenSanPham = "Áo Thun Cotton Basic",
                            MauSac = "Trắng",
                            KichCo = "M",
                            SoLuong = 2,
                            DonGia = 200000,
                            ThanhTien = 400000
                        }
                    }
                });

            // Mock cho Đổi Trả Hàng (Return Order)
            mockOrderRepo.Setup(r => r.ProcessOrderReturnAsync(1, 1, It.IsInRange(1, 2, Moq.Range.Inclusive), It.IsAny<string?>(), It.IsAny<int>()))
                .ReturnsAsync((int orderId, int variantId, int qty, string? reason, int staffId) => (
                    true,
                    "Đổi trả hàng thành công và đã hoàn lại tồn kho.",
                    new ReturnOrderResultDto
                    {
                        MaDonTra = 1,
                        MaHoaDon = orderId,
                        MaBienThe = variantId,
                        SoLuongTra = qty,
                        LyDoTra = reason ?? "Khách đổi trả hàng",
                        TrangThai = "Đã xử lý"
                    }
                ));

            mockOrderRepo.Setup(r => r.ProcessOrderReturnAsync(1, 1, It.Is<int>(qty => qty > 2), It.IsAny<string?>(), It.IsAny<int>()))
                .ReturnsAsync((false, "Số lượng trả vượt quá số lượng đã mua trong đơn hàng.", (ReturnOrderResultDto?)null));

            mockOrderRepo.Setup(r => r.ProcessOrderReturnAsync(It.Is<int>(id => id != 1), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
                .ReturnsAsync((false, "Chỉ được phép đổi trả hàng cho hóa đơn đã hoàn thành.", (ReturnOrderResultDto?)null));

            // 4. Mock IWarehouseRepository theo Specification RBAC Warehouse
            var mockWarehouseRepo = new Mock<IWarehouseRepository>();
            var suppliersList = new List<Shared.DTOs.Warehouse.SupplierDto>
            {
                new()
                {
                    MaNhaCungCap = 1,
                    TenNhaCungCap = "Tổng Công Ty May Việt Tiến",
                    DienThoaiLienHe = "0988888888"
                }
            };
            mockWarehouseRepo.Setup(r => r.ImportStockAsync(It.IsAny<Shared.DTOs.Warehouse.ImportStockRequestDto>()))
                .Returns(Task.CompletedTask);
            mockWarehouseRepo.Setup(r => r.GetLowStockAlertsAsync())
                .ReturnsAsync(new List<Shared.DTOs.Warehouse.LowStockAlertDto>
                {
                    new()
                    {
                        MaBienThe = 4,
                        TenSanPham = "Áo Polo Thể Thao Nam",
                        MaSku = "POLO-DEN-M",
                        MauSac = "Đen",
                        KichCo = "M",
                        SoLuongTon = 5,
                        GiaNhap = 150000
                    }
                });
            mockWarehouseRepo.Setup(r => r.GetSuppliersAsync())
                .ReturnsAsync(() => suppliersList);
            mockWarehouseRepo.Setup(r => r.CreateSupplierAsync(It.IsAny<Shared.DTOs.Warehouse.CreateSupplierRequestDto>()))
                .ReturnsAsync((Shared.DTOs.Warehouse.CreateSupplierRequestDto req) =>
                {
                    var created = new Shared.DTOs.Warehouse.SupplierDto
                    {
                        MaNhaCungCap = suppliersList.Count + 1,
                        TenNhaCungCap = req.TenNhaCungCap,
                        DienThoaiLienHe = req.DienThoaiLienHe
                    };
                    suppliersList.Add(created);
                    return created;
                });

            // 5. Mock IPromotionRepository theo Specification TS-PRO-01..06
            var promotionsData = new List<PromotionEntity>
            {
                new()
                {
                    MaKhuyenMai = 1,
                    TenKhuyenMai = "Ưu Đãi Chào Hè 2026",
                    MaGiamGia = "SUMMER10",
                    PhanTramGiam = 10,
                    SoTienGiam = 50000,
                    MuaToiThieu = 200000,
                    NgayBatDau = new DateTime(2026, 1, 1),
                    NgayKetThuc = new DateTime(2026, 12, 31),
                    SoLuongDung = 100
                },
                new()
                {
                    MaKhuyenMai = 2,
                    TenKhuyenMai = "Giảm Ngay 50K Đơn Từ 400K",
                    MaGiamGia = "SALE50K",
                    PhanTramGiam = 0,
                    SoTienGiam = 50000,
                    MuaToiThieu = 400000,
                    NgayBatDau = new DateTime(2026, 1, 1),
                    NgayKetThuc = new DateTime(2026, 12, 31),
                    SoLuongDung = 50
                },
                new()
                {
                    MaKhuyenMai = 3,
                    TenKhuyenMai = "Voucher Đơn Tối Thiểu 500K",
                    MaGiamGia = "MIN500K",
                    PhanTramGiam = 15,
                    SoTienGiam = 100000,
                    MuaToiThieu = 500000,
                    NgayBatDau = new DateTime(2026, 1, 1),
                    NgayKetThuc = new DateTime(2026, 12, 31),
                    SoLuongDung = 20
                },
                new()
                {
                    MaKhuyenMai = 4,
                    TenKhuyenMai = "Khuyến Mãi Đã Hết Hạn",
                    MaGiamGia = "EXPIRED2025",
                    PhanTramGiam = 20,
                    SoTienGiam = 0,
                    MuaToiThieu = 100000,
                    NgayBatDau = new DateTime(2025, 1, 1),
                    NgayKetThuc = new DateTime(2025, 12, 31),
                    SoLuongDung = 10
                },
                new()
                {
                    MaKhuyenMai = 5,
                    TenKhuyenMai = "Khuyến Mãi Hết Lượt Dùng",
                    MaGiamGia = "OUTOFSTOCK",
                    PhanTramGiam = 10,
                    SoTienGiam = 0,
                    MuaToiThieu = 100000,
                    NgayBatDau = new DateTime(2026, 1, 1),
                    NgayKetThuc = new DateTime(2026, 12, 31),
                    SoLuongDung = 0
                }
            };

            var mockPromoRepo = new Mock<IPromotionRepository>();
            mockPromoRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>()))
                .ReturnsAsync((string code) =>
                    promotionsData.FirstOrDefault(p => string.Equals(p.MaGiamGia, code?.Trim(), StringComparison.OrdinalIgnoreCase)));

            mockPromoRepo.Setup(r => r.GetActivePromotionsAsync())
                .ReturnsAsync(promotionsData.Where(p =>
                    (!p.NgayKetThuc.HasValue || p.NgayKetThuc.Value >= DateTime.UtcNow.Date) &&
                    (!p.SoLuongDung.HasValue || p.SoLuongDung.Value > 0)));

            // 6. Mock ICustomerRepository theo Specification TS-CUS-01..03
            var mockCustomerRepo = new Mock<ICustomerRepository>();
            mockCustomerRepo.Setup(r => r.GetProfileByIdAsync(1))
                .ReturnsAsync(new Shared.DTOs.Customer.CustomerProfileDto
                {
                    MaKhachHang = 1,
                    TenKhachHang = "Phạm Minh Quân",
                    DienThoai = "0911111111",
                    Email = "quan@gmail.com",
                    DiemThuong = 50,
                    MaCapDo = 1,
                    TenCapDo = "Thường",
                    PhanTramGiamGia = 0,
                    HeSoTichDiem = 1.0m,
                    TongChiTieu = 1200000
                });

            mockCustomerRepo.Setup(r => r.GetProfileByIdAsync(2))
                .ReturnsAsync(new Shared.DTOs.Customer.CustomerProfileDto
                {
                    MaKhachHang = 2,
                    TenKhachHang = "Nguyễn Thảo Vy",
                    DienThoai = "0922222222",
                    Email = "vy@gmail.com",
                    DiemThuong = 320,
                    MaCapDo = 2,
                    TenCapDo = "Bạc",
                    PhanTramGiamGia = 5,
                    HeSoTichDiem = 1.2m,
                    TongChiTieu = 5500000
                });

            mockCustomerRepo.Setup(r => r.GetProfileByIdAsync(3))
                .ReturnsAsync(new Shared.DTOs.Customer.CustomerProfileDto
                {
                    MaKhachHang = 3,
                    TenKhachHang = "Lê Hoàng Nam",
                    DienThoai = "0933333333",
                    Email = "nam@gmail.com",
                    DiemThuong = 950,
                    MaCapDo = 3,
                    TenCapDo = "Vàng",
                    PhanTramGiamGia = 10,
                    HeSoTichDiem = 1.5m,
                    TongChiTieu = 22000000
                });

            mockCustomerRepo.Setup(r => r.GetProfileByPhoneAsync("0911111111"))
                .ReturnsAsync(new Shared.DTOs.Customer.CustomerProfileDto
                {
                    MaKhachHang = 1,
                    TenKhachHang = "Phạm Minh Quân",
                    DienThoai = "0911111111",
                    Email = "quan@gmail.com",
                    DiemThuong = 50,
                    MaCapDo = 1,
                    TenCapDo = "Thường",
                    PhanTramGiamGia = 0,
                    HeSoTichDiem = 1.0m,
                    TongChiTieu = 1200000
                });

            mockCustomerRepo.Setup(r => r.GetProfileByPhoneAsync(It.Is<string>(p => p != "0911111111")))
                .ReturnsAsync((Shared.DTOs.Customer.CustomerProfileDto?)null);

            mockCustomerRepo.Setup(r => r.GetOrderHistoryAsync(1))
                .ReturnsAsync(new List<Shared.DTOs.Customer.CustomerOrderHistoryItemDto>
                {
                    new()
                    {
                        MaHoaDon = 101,
                        NgayLapHoaDon = DateTime.UtcNow.AddDays(-2),
                        TienHang = 500000,
                        TienGiamGia = 0,
                        TongThanhToan = 500000,
                        TrangThai = "Đã hoàn thành",
                        SoMatHang = 2
                    },
                    new()
                    {
                        MaHoaDon = 102,
                        NgayLapHoaDon = DateTime.UtcNow.AddDays(-10),
                        TienHang = 700000,
                        TienGiamGia = 0,
                        TongThanhToan = 700000,
                        TrangThai = "Đã hoàn thành",
                        SoMatHang = 3
                    }
                });

            mockCustomerRepo.Setup(r => r.EvaluateRanksAsync())
                .ReturnsAsync(new List<Shared.DTOs.Customer.RankEvaluationResultDto>
                {
                    new() { MaKhachHang = 1, TenKhachHang = "Phạm Minh Quân", TongChiTieu = 1200000, CapDoMoi = "Thường" },
                    new() { MaKhachHang = 2, TenKhachHang = "Nguyễn Thảo Vy", TongChiTieu = 5500000, CapDoMoi = "Bạc" },
                    new() { MaKhachHang = 3, TenKhachHang = "Lê Hoàng Nam", TongChiTieu = 22000000, CapDoMoi = "Vàng" }
                });

            // 7. Mock IReportRepository theo Specification TS-REP-01..05
            var mockReportRepo = new Mock<IReportRepository>();
            mockReportRepo.Setup(r => r.GetDailyRevenueAsync())
                .ReturnsAsync(new List<DailyRevenueReportDto>
                {
                    new()
                    {
                        NgayBan = DateTime.UtcNow.Date,
                        TongSoDon = 15,
                        TongTienHang = 15000000,
                        TongTienGiam = 1500000,
                        DoanhThuThucTe = 13500000
                    }
                });

            mockReportRepo.Setup(r => r.GetTopSellingProductsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<TopSellingProductDto>
                {
                    new()
                    {
                        MaSanPham = 1,
                        TenSanPham = "Áo Polo Nam Classic",
                        TenDanhMuc = "Áo Nam",
                        TongSoLuongDaBan = 120,
                        TongDoanhThuMangLai = 30000000
                    }
                });

            mockReportRepo.Setup(r => r.GetFinancialRangeReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FinancialRangeReportDto
                {
                    TongSoDonHoanThanh = 25,
                    TongSoKhachHang = 18,
                    TongTienHang = 25000000,
                    TongTienKhuyenMai = 2500000,
                    TongDoanhThuThuc = 22500000
                });

            // 9. Mock IBranchRepository theo Specification TS-BRANCH-01..04
            var branchesList = new List<BranchDto>
            {
                new() { MaChiNhanh = 1, TenChiNhanh = "Chi nhánh Quận 10", DiaChi = "123 Đường 3/2, Quận 10", DienThoai = "0281111111", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 2, TenChiNhanh = "Chi nhánh Quận 1", DiaChi = "45 Lê Lợi, Quận 1", DienThoai = "0282222222", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 3, TenChiNhanh = "Chi nhánh Quận 3", DiaChi = "78 CMT8, Quận 3", DienThoai = "0283333333", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 4, TenChiNhanh = "Chi nhánh Quận 5", DiaChi = "90 Trần Hưng Đạo, Quận 5", DienThoai = "0284444444", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 5, TenChiNhanh = "Chi nhánh Quận 7", DiaChi = "12 Nguyễn Thị Thập, Quận 7", DienThoai = "0285555555", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 6, TenChiNhanh = "Chi nhánh Bình Thạnh", DiaChi = "34 Bạch Đằng, Bình Thạnh", DienThoai = "0286666666", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 7, TenChiNhanh = "Chi nhánh Gò Vấp", DiaChi = "56 Quang Trung, Gò Vấp", DienThoai = "0287777777", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 8, TenChiNhanh = "Chi nhánh Tân Bình", DiaChi = "89 Cộng Hòa, Tân Bình", DienThoai = "0288888888", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, DangHoatDong = true },
                new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng Online", DiaChi = "100 Hoàng Văn Thụ, Phú Nhuận", DienThoai = "0289999999", LoaiChiNhanh = "Kho tổng online", IsOnlineWarehouse = true, DangHoatDong = true }
            };

            var branchInventories = new Dictionary<int, List<BranchInventoryDto>>
            {
                [1] = new List<BranchInventoryDto>
                {
                    new()
                    {
                        MaChiNhanh = 1,
                        TenChiNhanh = "Chi nhánh Quận 10",
                        MaBienThe = 1,
                        TenSanPham = "Áo Polo Nam Classic",
                        KichCo = "M",
                        MauSac = "Đen",
                        MaSku = "AP-NAM-BLK-M",
                        SoLuongTon = 50,
                        MucCanhBaoTon = 10,
                        GiaBan = 250000
                    }
                }
            };

            var mockBranchRepo = new Mock<IBranchRepository>();
            mockBranchRepo.Setup(r => r.GetAllBranchesAsync())
                .ReturnsAsync(() => branchesList);

            mockBranchRepo.Setup(r => r.GetBranchByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => branchesList.FirstOrDefault(b => b.MaChiNhanh == id));

            mockBranchRepo.Setup(r => r.CreateBranchAsync(It.IsAny<CreateBranchDto>()))
                .ReturnsAsync((CreateBranchDto dto) =>
                {
                    var newId = branchesList.Max(b => b.MaChiNhanh) + 1;
                    var branch = new BranchDto
                    {
                        MaChiNhanh = newId,
                        TenChiNhanh = dto.TenChiNhanh,
                        DiaChi = dto.DiaChi,
                        DienThoai = dto.DienThoai,
                        LoaiChiNhanh = dto.LoaiChiNhanh ?? "Cửa hàng bán lẻ",
                        IsOnlineWarehouse = false,
                        DangHoatDong = true
                    };
                    branchesList.Add(branch);

                    branchInventories[newId] = new List<BranchInventoryDto>
                    {
                        new()
                        {
                            MaChiNhanh = newId,
                            TenChiNhanh = dto.TenChiNhanh,
                            MaBienThe = 1,
                            TenSanPham = "Áo Polo Nam Classic",
                            KichCo = "M",
                            MauSac = "Đen",
                            MaSku = "AP-NAM-BLK-M",
                            SoLuongTon = 0,
                            MucCanhBaoTon = 10,
                            GiaBan = 250000
                        }
                    };
                    return newId;
                });

            mockBranchRepo.Setup(r => r.GetBranchInventoryAsync(It.IsAny<int>(), It.IsAny<string?>()))
                .ReturnsAsync((int bId, string? search) =>
                {
                    if (branchInventories.TryGetValue(bId, out var items))
                    {
                        if (string.IsNullOrWhiteSpace(search)) return items;
                        return items.Where(x => x.TenSanPham.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                                (x.MaSku != null && x.MaSku.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
                    }
                    return new List<BranchInventoryDto>();
                });

            mockBranchRepo.Setup(r => r.GetVariantInventoryAtBranchAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((int bId, int vId) =>
                {
                    if (branchInventories.TryGetValue(bId, out var items))
                    {
                        return items.FirstOrDefault(x => x.MaBienThe == vId);
                    }
                    return null;
                });

            mockBranchRepo.Setup(r => r.GetStockQuantityAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((int bId, int vId) =>
                {
                    if (branchInventories.TryGetValue(bId, out var items))
                    {
                        var found = items.FirstOrDefault(x => x.MaBienThe == vId);
                        return found?.SoLuongTon ?? 0;
                    }
                    return 0;
                });

            mockBranchRepo.Setup(r => r.AdjustBranchInventoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((int bId, int vId, int delta, string reason, int staffId) =>
                {
                    if (branchInventories.TryGetValue(bId, out var items))
                    {
                        var found = items.FirstOrDefault(x => x.MaBienThe == vId);
                        if (found != null)
                        {
                            if (found.SoLuongTon + delta < 0) return false;
                            found.SoLuongTon += delta;
                            return true;
                        }
                    }
                    return false;
                });

            // Thay thế các Service Descriptor thật bằng Mock Repositories
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(IAuthRepository) ||
                d.ServiceType == typeof(IProductRepository) ||
                d.ServiceType == typeof(ICatalogRepository) ||
                d.ServiceType == typeof(IOrderRepository) ||
                d.ServiceType == typeof(IWarehouseRepository) ||
                d.ServiceType == typeof(IPromotionRepository) ||
                d.ServiceType == typeof(ICustomerRepository) ||
                d.ServiceType == typeof(IReportRepository) ||
                d.ServiceType == typeof(IBranchRepository)).ToList();

            foreach (var d in descriptorsToRemove)
            {
                services.Remove(d);
            }

            services.AddSingleton(mockAuthRepo.Object);
            services.AddSingleton(mockProductRepo.Object);
            services.AddSingleton(mockCatalogRepo.Object);
            services.AddSingleton(mockOrderRepo.Object);
            services.AddSingleton(mockWarehouseRepo.Object);
            services.AddSingleton(mockPromoRepo.Object);
            services.AddSingleton(mockCustomerRepo.Object);
            services.AddSingleton(mockReportRepo.Object);
            services.AddSingleton(mockBranchRepo.Object);
        });
    }
}
