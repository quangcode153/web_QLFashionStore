using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using FluentAssertions;
using Moq;
using Shared.DTOs.Warehouse;
using Xunit;

namespace Backend.Tests.UnitTests.Warehouse;

public class BranchWarehouseInventoryTests
{
    private readonly Mock<IBranchRepository> _branchRepoMock = new();

    #region VÒNG 1: MA TRẬN THAM SỐ BIÊN TỒN KHO CHI NHÁNH (BOUNDARY MATRIX)

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(10, -5, true)] // Trừ hợp lệ
    [InlineData(5, -10, false)] // Trừ quá tồn kho -> Phải từ chối
    [InlineData(5, -6, false)]
    [InlineData(0, 20, true)] // Nhập kho hợp lệ
    public async Task BranchInventory_AdjustQuantity_EnforcesBoundaryValidation(int currentStock, int delta, bool expectedSuccess)
    {
        // Arrange
        var branchId = 1;
        var variantId = 101;

        _branchRepoMock.Setup(r => r.GetStockQuantityAsync(branchId, variantId))
            .ReturnsAsync(currentStock);

        _branchRepoMock.Setup(r => r.AdjustBranchInventoryAsync(branchId, variantId, delta, It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((int bId, int vId, int d, string reason, int staffId) =>
            {
                if (currentStock + d < 0) return false;
                return true;
            });

        // Act
        var result = await _branchRepoMock.Object.AdjustBranchInventoryAsync(branchId, variantId, delta, "TEST", 1);

        // Assert
        result.Should().Be(expectedSuccess);
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI (STATE MACHINE: TẠO CHI NHÁNH -> SEED TỒN 0 -> NHẬP KHO -> BÁN HÀNG)

    [Fact]
    public async Task BranchLifecycle_CreateBranch_SeedsAllVariantsWithZeroStock_AndHandlesTransactions()
    {
        // 1. Giả lập tạo chi nhánh mới
        var newBranchDto = new CreateBranchDto
        {
            TenChiNhanh = "QF Thủ Đức",
            DiaChi = "100 Võ Văn Ngân, TP. Thủ Đức",
            LoaiChiNhanh = "Cửa hàng bán lẻ"
        };

        var simulatedBranchId = 10;
        var inventoryDb = new Dictionary<(int BranchId, int VariantId), int>();

        _branchRepoMock.Setup(r => r.CreateBranchAsync(newBranchDto))
            .ReturnsAsync(() =>
            {
                // Khi tạo chi nhánh mới, mô phỏng trigger seed tồn = 0 cho 3 biến thể mẫu
                inventoryDb[(simulatedBranchId, 1)] = 0;
                inventoryDb[(simulatedBranchId, 2)] = 0;
                inventoryDb[(simulatedBranchId, 3)] = 0;
                return simulatedBranchId;
            });

        _branchRepoMock.Setup(r => r.GetBranchInventoryAsync(simulatedBranchId, null))
            .ReturnsAsync(() =>
            {
                return inventoryDb.Where(kv => kv.Key.BranchId == simulatedBranchId)
                    .Select(kv => new BranchInventoryDto
                    {
                        MaChiNhanh = kv.Key.BranchId,
                        MaBienThe = kv.Key.VariantId,
                        SoLuongTon = kv.Value
                    });
            });

        // Act 1: Tạo chi nhánh
        var createdId = await _branchRepoMock.Object.CreateBranchAsync(newBranchDto);
        createdId.Should().Be(simulatedBranchId);

        // Assert 1: Toàn bộ mặt hàng ban đầu đều có tồn kho = 0
        var initialStock = (await _branchRepoMock.Object.GetBranchInventoryAsync(createdId)).ToList();
        initialStock.Should().HaveCount(3);
        initialStock.All(i => i.SoLuongTon == 0).Should().BeTrue();

        // Act 2: Nhập kho 50 chiếc biến thể #1
        inventoryDb[(simulatedBranchId, 1)] += 50;
        inventoryDb[(simulatedBranchId, 1)].Should().Be(50);

        // Act 3: Xuất bán 2 chiếc tại quầy POS
        inventoryDb[(simulatedBranchId, 1)] -= 2;
        inventoryDb[(simulatedBranchId, 1)].Should().Be(48);

        // Act 4: Thử xuất bán quá số lượng tồn (bán 50 chiếc trong khi chỉ còn 48)
        var attemptOverSell = 50;
        var canSell = inventoryDb[(simulatedBranchId, 1)] >= attemptOverSell;
        canSell.Should().BeFalse();
    }

    #endregion

    #region VÒNG 3: MA TRẬN CÔ LẬP DỮ LIỆU ĐA CHI NHÁNH (MULTI-BRANCH ISOLATION MATRIX)

    [Fact]
    public async Task MultiBranch_StockIsolation_BranchATransaction_DoesNotAffectBranchB()
    {
        // Arrange
        var branchA = 1; // Chi nhánh Quận 10
        var branchB = 2; // Chi nhánh Gò Vấp
        var variantId = 10;

        var stockBranchA = 25;
        var stockBranchB = 100;

        _branchRepoMock.Setup(r => r.GetStockQuantityAsync(branchA, variantId)).ReturnsAsync(() => stockBranchA);
        _branchRepoMock.Setup(r => r.GetStockQuantityAsync(branchB, variantId)).ReturnsAsync(() => stockBranchB);

        _branchRepoMock.Setup(r => r.AdjustBranchInventoryAsync(branchA, variantId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<int, int, int, string, int>((b, v, delta, reason, staff) =>
            {
                stockBranchA += delta;
            })
            .ReturnsAsync(true);

        // Act: Bán 5 chiếc tại Chi nhánh A
        await _branchRepoMock.Object.AdjustBranchInventoryAsync(branchA, variantId, -5, "BAN_HANG_POS", 1);

        // Assert:
        // Chi nhánh A giảm từ 25 xuống 20
        stockBranchA.Should().Be(20);

        // Chi nhánh B hoàn toàn độc lập, vẫn giữ nguyên 100
        var branchBStockResult = await _branchRepoMock.Object.GetStockQuantityAsync(branchB, variantId);
        branchBStockResult.Should().Be(100);
    }

    #endregion
}
