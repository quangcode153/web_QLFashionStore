using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Announcement;
using Xunit;

namespace Backend.Tests.Unit;

public class AnnouncementReadTests
{
    private readonly Mock<IAnnouncementRepository> _repoMock;
    private readonly AnnouncementService _service;

    public AnnouncementReadTests()
    {
        _repoMock = new Mock<IAnnouncementRepository>();
        _service = new AnnouncementService(_repoMock.Object);
    }

    // ==========================================
    // VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (BOUNDARY MATRIX)
    // ==========================================
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-999, 1)]
    public async Task MarkAsRead_WithInvalidAnnouncementId_ShouldReturnFail(int invalidAnnId, int userId)
    {
        var result = await _service.MarkAsReadAsync(invalidAnnId, userId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Mã thông báo không hợp lệ");
        _repoMock.Verify(r => r.MarkAsReadAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(10, -999)]
    public async Task MarkAsRead_WithInvalidUserId_ShouldReturnFail(int annId, int invalidUserId)
    {
        var result = await _service.MarkAsReadAsync(annId, invalidUserId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Mã tài khoản không hợp lệ");
        _repoMock.Verify(r => r.MarkAsReadAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task MarkAllAsRead_WithInvalidUserId_ShouldReturnFail(int invalidUserId)
    {
        var result = await _service.MarkAllAsReadAsync(invalidUserId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Mã tài khoản không hợp lệ");
        _repoMock.Verify(r => r.MarkAllAsReadAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Never);
    }

    // ==========================================
    // VÒNG 2: CHUỖI CÔ LẬP TRẠNG THÁI ĐA TÀI KHOẢN (MULTI-USER ISOLATION & STATE MACHINE PIPELINE)
    // ==========================================
    [Fact]
    public async Task MultiUserIsolation_UserAReadsNotification_ShouldNotAffectUserB()
    {
        // Kịch bản: 2 Thông báo (ID=101, 102).
        // User A (ID=1) đọc TB 101.
        // User B (ID=2) chưa đọc bất kỳ thông báo nào.
        var userAId = 1;
        var userBId = 2;

        var ann101 = new InternalAnnouncementDto { MaThongBao = 101, TieuDe = "Thông báo 101" };
        var ann102 = new InternalAnnouncementDto { MaThongBao = 102, TieuDe = "Thông báo 102" };

        // Mock danh sách trả về cho User A: 101 đã đọc, 102 chưa đọc
        _repoMock.Setup(r => r.GetAnnouncementsAsync(null, null, userAId))
            .ReturnsAsync(new List<InternalAnnouncementDto>
            {
                new() { MaThongBao = 101, TieuDe = "Thông báo 101", DaDoc = true },
                new() { MaThongBao = 102, TieuDe = "Thông báo 102", DaDoc = false }
            });

        // Mock danh sách trả về cho User B: cả 2 đều chưa đọc
        _repoMock.Setup(r => r.GetAnnouncementsAsync(null, null, userBId))
            .ReturnsAsync(new List<InternalAnnouncementDto>
            {
                new() { MaThongBao = 101, TieuDe = "Thông báo 101", DaDoc = false },
                new() { MaThongBao = 102, TieuDe = "Thông báo 102", DaDoc = false }
            });

        _repoMock.Setup(r => r.MarkAsReadAsync(101, userAId)).ReturnsAsync(true);

        // Bước 1: User A đánh dấu đọc 101
        var markResult = await _service.MarkAsReadAsync(101, userAId);
        markResult.Success.Should().BeTrue();

        // Bước 2: User A tải danh sách -> TB 101 đã đọc, 102 chưa đọc
        var listA = await _service.GetAnnouncementsAsync(null, null, userAId);
        listA.Success.Should().BeTrue();
        listA.Data.Should().HaveCount(2);
        listA.Data!.First(x => x.MaThongBao == 101).DaDoc.Should().BeTrue();
        listA.Data!.First(x => x.MaThongBao == 102).DaDoc.Should().BeFalse();

        // Bước 3: User B tải danh sách -> CẢ 2 ĐỀU CHƯA ĐỌC (Cô lập tuyệt đối, không bị lây nhiễm chéo)
        var listB = await _service.GetAnnouncementsAsync(null, null, userBId);
        listB.Success.Should().BeTrue();
        listB.Data.Should().HaveCount(2);
        listB.Data!.First(x => x.MaThongBao == 101).DaDoc.Should().BeFalse();
        listB.Data!.First(x => x.MaThongBao == 102).DaDoc.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllAsRead_WithValidUserId_ShouldCallRepoAndReturnCount()
    {
        var userId = 5;
        _repoMock.Setup(r => r.MarkAllAsReadAsync(userId, "Toàn hệ thống", 1)).ReturnsAsync(3);

        var result = await _service.MarkAllAsReadAsync(userId, "Toàn hệ thống", 1);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(3);
        result.Message.Should().Contain("Đã đánh dấu đã đọc");
        _repoMock.Verify(r => r.MarkAllAsReadAsync(userId, "Toàn hệ thống", 1), Times.Once);
    }
}
