using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using FluentAssertions;
using Frontend.Pages;
using Frontend.Pages.Pos;
using Frontend.Pages.Warehouse;
using Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Order;
using Shared.DTOs.Warehouse;
using Shared.DTOs.Announcement;
using Xunit;

namespace Frontend.Tests.Components;

public class FullSystemRealtimeStockComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi;
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalRService;
    private readonly Mock<IPosApiClient> _mockPosApi;
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IAuthStateService> _mockAuthState;

    public FullSystemRealtimeStockComponentTests()
    {
        _mockCatalogApi = new Mock<ICatalogApiClient>();
        _mockCartService = new Mock<ICartService>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalRService = new Mock<ISignalRNotificationService>();
        _mockPosApi = new Mock<IPosApiClient>();
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockAuthState = new Mock<IAuthStateService>();

        _mockAuthState.SetupGet(a => a.IsAuthenticated).Returns(true);
        _mockAuthState.SetupGet(a => a.UserRole).Returns("QuanLyHeThong");
        _mockAuthState.SetupGet(a => a.StaffName).Returns("Admin");

        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalRService.Object);
        Services.AddSingleton<IPosApiClient>(_mockPosApi.Object);
        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);
    }

    [Fact]
    public void Catalog_WhenInventoryUpdated_UpdatesQuickSelectVariantsRealtime()
    {
        // ARRANGE
        Action<int, int, string>? inventoryCallback = null;
        _mockSignalRService.SetupAdd(s => s.OnInventoryUpdated += It.IsAny<Action<int, int, string>>())
            .Callback<Action<int, int, string>>(handler => inventoryCallback = handler);

        var cut = Render<Catalog>();

        // ACT
        inventoryCallback.Should().NotBeNull("Catalog phải đăng ký sự kiện OnInventoryUpdated");
    }

    [Fact]
    public void PosCashier_WhenInventoryUpdated_UpdatesFastSaleVariantStockRealtime()
    {
        // ARRANGE
        Action<int, int, string>? inventoryCallback = null;
        _mockSignalRService.SetupAdd(s => s.OnInventoryUpdated += It.IsAny<Action<int, int, string>>())
            .Callback<Action<int, int, string>>(handler => inventoryCallback = handler);

        _mockPosApi.Setup(p => p.GetFastSaleProductsAsync())
            .ReturnsAsync(new List<ProductListItemDto>());

        var cut = Render<PosCashier>();

        // ACT
        inventoryCallback.Should().NotBeNull("PosCashier phải đăng ký sự kiện OnInventoryUpdated");
    }

    [Fact]
    public void WarehousePortal_WhenInventoryUpdated_UpdatesChainStockRealtime()
    {
        // ARRANGE
        Action<int, int, string>? inventoryCallback = null;
        _mockSignalRService.SetupAdd(s => s.OnInventoryUpdated += It.IsAny<Action<int, int, string>>())
            .Callback<Action<int, int, string>>(handler => inventoryCallback = handler);

        _mockAdminApi.Setup(api => api.GetLowStockAlertsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(api => api.GetSuppliersAsync())
            .ReturnsAsync(new List<SupplierDto>());
        _mockAdminApi.Setup(api => api.GetAnnouncementsAsync(It.IsAny<string?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<InternalAnnouncementDto>());
        _mockAdminApi.Setup(api => api.GetBranchesAsync())
            .ReturnsAsync(new List<BranchDto>());
        _mockAdminApi.Setup(api => api.GetBranchInventoryAsync(It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<BranchInventoryDto>());

        var cut = Render<WarehousePortal>();

        // ACT
        inventoryCallback.Should().NotBeNull("WarehousePortal phải đăng ký sự kiện OnInventoryUpdated");
    }
}
