using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class BannerServiceUnitTests
{
    private readonly Mock<IBannerRepository> _repoMock;
    private readonly BannerService _sut;

    public BannerServiceUnitTests()
    {
        _repoMock = new Mock<IBannerRepository>();
        _sut = new BannerService(_repoMock.Object);
    }

    [Fact]
    public async Task GetActiveBannersAsync_ReturnsActiveBanners_OrderedFromRepo()
    {
        // Arrange
        var mockData = new List<BannerDto>
        {
            new() { MaBanner = 1, TieuDe = "Banner 1", ThuTu = 1, KichHoat = true },
            new() { MaBanner = 2, TieuDe = "Banner 2", ThuTu = 2, KichHoat = true }
        };
        _repoMock.Setup(r => r.GetActiveBannersAsync()).ReturnsAsync(mockData);

        // Act
        var result = await _sut.GetActiveBannersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.First().TieuDe.Should().Be("Banner 1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_ThrowsArgumentException_WhenTitleIsInvalid(string? invalidTitle)
    {
        // Arrange
        var dto = new CreateUpdateBannerDto
        {
            TieuDe = invalidTitle!,
            HinhAnh = "images/valid.jpg"
        };

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tiêu đề*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_ThrowsArgumentException_WhenImageIsInvalid(string? invalidImage)
    {
        // Arrange
        var dto = new CreateUpdateBannerDto
        {
            TieuDe = "Tiêu đề hợp lệ",
            HinhAnh = invalidImage!
        };

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*hình ảnh*");
    }

    [Fact]
    public async Task CreateAsync_ValidDto_ReturnsCreatedBannerWithId()
    {
        // Arrange
        var dto = new CreateUpdateBannerDto
        {
            TieuDe = "MẶC ĐẸP ĐI LÀM",
            PhuDe = "MUA 2 GIẢM 25%",
            GiaNoiBat = "208K",
            HinhAnh = "images/hero-model.jpg",
            ThuTu = 1,
            KichHoat = true
        };
        _repoMock.Setup(r => r.CreateAsync(dto)).ReturnsAsync(10);
        _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(new BannerDto
        {
            MaBanner = 10,
            TieuDe = dto.TieuDe,
            PhuDe = dto.PhuDe,
            GiaNoiBat = dto.GiaNoiBat,
            HinhAnh = dto.HinhAnh,
            ThuTu = dto.ThuTu,
            KichHoat = dto.KichHoat
        });

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.MaBanner.Should().Be(10);
        result.TieuDe.Should().Be("MẶC ĐẸP ĐI LÀM");
    }

    [Fact]
    public async Task UpdateAsync_ThrowsArgumentException_WhenIdIsInvalid()
    {
        // Arrange
        var dto = new CreateUpdateBannerDto { TieuDe = "Banner", HinhAnh = "img.jpg" };

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(0, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ID*");
    }

    [Fact]
    public async Task DeleteAsync_CallsRepository_ReturnsResult()
    {
        // Arrange
        _repoMock.Setup(r => r.DeleteAsync(5)).ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(5);

        // Assert
        result.Should().BeTrue();
        _repoMock.Verify(r => r.DeleteAsync(5), Times.Once);
    }

    [Theory(DisplayName = "BANNER-RBAC-02: BannersController chặn Quản lý chi nhánh tạo, sửa, xóa banner chiến dịch")]
    [InlineData("CreateBanner")]
    [InlineData("UpdateBanner")]
    [InlineData("DeleteBanner")]
    public void BannersController_MutatingEndpoints_DoNotAllowBranchManagerRole(string actionMethodName)
    {
        // Arrange
        var controllerType = typeof(Backend.Controllers.BannersController);
        var method = controllerType.GetMethod(actionMethodName);

        // Assert
        method.Should().NotBeNull($"Phương thức {actionMethodName} phải tồn tại trên BannersController");
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false)
                              .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
        authAttr.Should().NotBeNull($"Phương thức {actionMethodName} phải được bảo vệ bởi [Authorize]");
        authAttr!.Roles.Should().NotBeNullOrWhiteSpace();
        authAttr.Roles!.Split(',').Should().NotContain(RoleConstants.QuanLyChiNhanh, "Quản lý chi nhánh không được phép quản trị Banner & Chiến dịch");
        authAttr.Roles.Split(',').Should().Contain(RoleConstants.AdminChuoi);
    }
}
