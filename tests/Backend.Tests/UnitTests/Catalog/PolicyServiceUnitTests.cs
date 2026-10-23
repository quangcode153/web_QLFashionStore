using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Catalog;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class PolicyServiceUnitTests
{
    private readonly Mock<IPolicyRepository> _repoMock;
    private readonly PolicyService _sut;

    public PolicyServiceUnitTests()
    {
        _repoMock = new Mock<IPolicyRepository>();
        _sut = new PolicyService(_repoMock.Object);
    }

    [Fact]
    public async Task GetActivePoliciesAsync_ReturnsOnlyActivePolicies()
    {
        // Arrange
        var mockData = new List<PolicyDto>
        {
            new() { MaChinhSach = 1, Slug = "chinh-sach-giao-hang", TieuDe = "Chính sách giao hàng", NoiDung = "<p>Nội dung</p>", KichHoat = true, ThuTu = 1 },
            new() { MaChinhSach = 2, Slug = "chinh-sach-doi-tra", TieuDe = "Chính sách đổi trả", NoiDung = "<p>Nội dung</p>", KichHoat = true, ThuTu = 2 }
        };
        _repoMock.Setup(r => r.GetActivePoliciesAsync()).ReturnsAsync(mockData);

        // Act
        var result = await _sut.GetActivePoliciesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.First().Slug.Should().Be("chinh-sach-giao-hang");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_ThrowsArgumentException_WhenTitleIsInvalid(string? invalidTitle)
    {
        // Arrange
        var dto = new CreateUpdatePolicyDto
        {
            Slug = "chinh-sach-moi",
            TieuDe = invalidTitle!,
            NoiDung = "<p>Nội dung hợp lệ</p>"
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
    public async Task CreateAsync_ThrowsArgumentException_WhenSlugIsInvalid(string? invalidSlug)
    {
        // Arrange
        var dto = new CreateUpdatePolicyDto
        {
            Slug = invalidSlug!,
            TieuDe = "Chính sách hợp lệ",
            NoiDung = "<p>Nội dung hợp lệ</p>"
        };

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*slug*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_ThrowsArgumentException_WhenContentIsInvalid(string? invalidContent)
    {
        // Arrange
        var dto = new CreateUpdatePolicyDto
        {
            Slug = "chinh-sach-moi",
            TieuDe = "Chính sách hợp lệ",
            NoiDung = invalidContent!
        };

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*nội dung*");
    }

    [Fact]
    public async Task CreateAsync_CreatesAndReturnsPolicy_WhenValid()
    {
        // Arrange
        var dto = new CreateUpdatePolicyDto
        {
            Slug = "chinh-sach-moi",
            TieuDe = "Chính sách mới",
            NoiDung = "<p>Chi tiết chính sách</p>",
            ThuTu = 1,
            KichHoat = true
        };
        var createdPolicy = new PolicyDto
        {
            MaChinhSach = 10,
            Slug = "chinh-sach-moi",
            TieuDe = "Chính sách mới",
            NoiDung = "<p>Chi tiết chính sách</p>",
            ThuTu = 1,
            KichHoat = true
        };

        _repoMock.Setup(r => r.CreateAsync(dto)).ReturnsAsync(10);
        _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(createdPolicy);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.MaChinhSach.Should().Be(10);
        result.TieuDe.Should().Be("Chính sách mới");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsTrue_WhenRepoSucceeds()
    {
        // Arrange
        var dto = new CreateUpdatePolicyDto
        {
            Slug = "chinh-sach-giao-hang",
            TieuDe = "Chính sách giao hàng cập nhật",
            NoiDung = "<p>Nội dung mới</p>"
        };
        _repoMock.Setup(r => r.UpdateAsync(1, dto)).ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateAsync(1, dto);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenRepoSucceeds()
    {
        // Arrange
        _repoMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.Should().BeTrue();
    }
}
