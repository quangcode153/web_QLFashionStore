using System;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Security;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Auth;
using Xunit;

namespace Backend.Tests.UnitTests.Security;

public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepo;
    private readonly Mock<IJwtTokenGenerator> _mockJwtGenerator;
    private readonly PasswordHasher _passwordHasher;
    private readonly AuthService _service;
    private const string TestPepper = "Test_Pepper_For_Auth_Service_Tests_2026_Secure_Key!";

    public AuthServiceTests()
    {
        _mockAuthRepo = new Mock<IAuthRepository>();
        _mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        _passwordHasher = new PasswordHasher(TestPepper);

        _mockJwtGenerator.Setup(j => j.GenerateToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("test_jwt_token_123", DateTime.UtcNow.AddHours(2)));

        _service = new AuthService(_mockAuthRepo.Object, _passwordHasher, _mockJwtGenerator.Object);
    }

    #region ĐĂNG NHẬP KHÁCH HÀNG & PHÂN ĐỊNH USER TYPE

    [Fact(DisplayName = "UNIT-AUTH-01: Khách hàng đăng nhập với mật khẩu PBKDF2 chuẩn trả về thành công")]
    public async Task LoginAsync_CustomerUser_ValidPassword_ReturnsSuccess()
    {
        // Arrange
        var rawPassword = "CustomerSecurePassword@123";
        var passwordHash = _passwordHasher.HashPassword(rawPassword);

        var user = new UserAuthModel
        {
            MaTaiKhoan = 10,
            TenDangNhap = "customer_vip",
            MatKhauHash = passwordHash,
            DangHoatDong = true,
            MaNhanVien = 0,
            TenNhanVien = "Nguyễn Văn Khách",
            VaiTro = "Khách hàng",
            UserType = "CUSTOMER"
        };

        _mockAuthRepo.Setup(r => r.GetUserByUsernameAsync("customer_vip"))
            .ReturnsAsync(user);

        var request = new LoginRequestDto
        {
            TenDangNhap = "customer_vip",
            MatKhau = rawPassword
        };

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().Be("test_jwt_token_123");
        result.Data.VaiTro.Should().Be("Khách hàng");
        result.Data.TenNhanVien.Should().Be("Nguyễn Văn Khách");
    }

    [Fact(DisplayName = "UNIT-AUTH-02: Khách hàng nhập sai mật khẩu trả về thất bại")]
    public async Task LoginAsync_CustomerUser_WrongPassword_ReturnsFail()
    {
        // Arrange
        var correctHash = _passwordHasher.HashPassword("CorrectPassword");
        var user = new UserAuthModel
        {
            MaTaiKhoan = 10,
            TenDangNhap = "customer_vip",
            MatKhauHash = correctHash,
            DangHoatDong = true,
            VaiTro = "Khách hàng",
            UserType = "CUSTOMER"
        };

        _mockAuthRepo.Setup(r => r.GetUserByUsernameAsync("customer_vip"))
            .ReturnsAsync(user);

        var request = new LoginRequestDto
        {
            TenDangNhap = "customer_vip",
            MatKhau = "WrongPassword"
        };

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không chính xác");
    }

    [Fact(DisplayName = "UNIT-AUTH-03: Đăng nhập tài khoản bị khóa (DangHoatDong = false) bị từ chối")]
    public async Task LoginAsync_DisabledAccount_ReturnsFail()
    {
        // Arrange
        var user = new UserAuthModel
        {
            MaTaiKhoan = 2,
            TenDangNhap = "locked_user",
            MatKhauHash = _passwordHasher.HashPassword("any_password"),
            DangHoatDong = false,
            VaiTro = "Khách hàng"
        };

        _mockAuthRepo.Setup(r => r.GetUserByUsernameAsync("locked_user"))
            .ReturnsAsync(user);

        var request = new LoginRequestDto
        {
            TenDangNhap = "locked_user",
            MatKhau = "any_password"
        };

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã bị khóa");
    }

    #endregion

    #region ĐĂNG KÝ KHÁCH HÀNG MỚI (REGISTER)

    [Fact(DisplayName = "UNIT-AUTH-04: Đăng ký khách hàng mới băm mật khẩu PBKDF2 và gọi Repo với hash thật")]
    public async Task RegisterCustomerAsync_ValidRequest_HashesPasswordAndCallsRepo()
    {
        // Arrange
        _mockAuthRepo.Setup(r => r.IsUsernameTakenAsync("new_customer"))
            .ReturnsAsync(false);

        _mockAuthRepo.Setup(r => r.RegisterCustomerAsync(It.IsAny<RegisterCustomerDto>(), It.IsAny<string>()))
            .ReturnsAsync(55);

        var dto = new RegisterCustomerDto
        {
            TenDangNhap = "new_customer",
            MatKhau = "SecretPass@2026",
            TenKhachHang = "Phan Hải",
            DienThoai = "0944444444",
            Email = "hai@gmail.com"
        };

        // Act
        var result = await _service.RegisterCustomerAsync(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        _mockAuthRepo.Verify(r => r.RegisterCustomerAsync(
            dto,
            It.Is<string>(h => _passwordHasher.VerifyPassword("SecretPass@2026", h))), Times.Once);
    }

    [Fact(DisplayName = "UNIT-AUTH-05: Đăng ký với tên đăng nhập đã tồn tại trả về thất bại")]
    public async Task RegisterCustomerAsync_ExistingUsername_ReturnsFail()
    {
        // Arrange
        _mockAuthRepo.Setup(r => r.IsUsernameTakenAsync("existing_user"))
            .ReturnsAsync(true);

        var dto = new RegisterCustomerDto
        {
            TenDangNhap = "existing_user",
            MatKhau = "Pass123",
            TenKhachHang = "Nguyễn Văn A"
        };

        // Act
        var result = await _service.RegisterCustomerAsync(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã được sử dụng");
        _mockAuthRepo.Verify(r => r.RegisterCustomerAsync(It.IsAny<RegisterCustomerDto>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-AUTH-06: Đăng ký với số điện thoại đã tồn tại trả về thông báo thất bại")]
    public async Task RegisterCustomerAsync_ExistingPhone_ReturnsFail()
    {
        // Arrange
        _mockAuthRepo.Setup(r => r.IsUsernameTakenAsync("unique_user"))
            .ReturnsAsync(false);
        _mockAuthRepo.Setup(r => r.IsPhoneTakenAsync("0869161050"))
            .ReturnsAsync(true);

        var dto = new RegisterCustomerDto
        {
            TenDangNhap = "unique_user",
            MatKhau = "Pass123",
            TenKhachHang = "Hà Văn Quan",
            DienThoai = "0869161050"
        };

        // Act
        var result = await _service.RegisterCustomerAsync(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Số điện thoại này đã được đăng ký tài khoản");
        _mockAuthRepo.Verify(r => r.RegisterCustomerAsync(It.IsAny<RegisterCustomerDto>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-AUTH-07: Đăng ký với email đã tồn tại trả về thông báo thất bại")]
    public async Task RegisterCustomerAsync_ExistingEmail_ReturnsFail()
    {
        // Arrange
        _mockAuthRepo.Setup(r => r.IsUsernameTakenAsync("unique_user2"))
            .ReturnsAsync(false);
        _mockAuthRepo.Setup(r => r.IsPhoneTakenAsync("0912345678"))
            .ReturnsAsync(false);
        _mockAuthRepo.Setup(r => r.IsEmailTakenAsync("existing@gmail.com"))
            .ReturnsAsync(true);

        var dto = new RegisterCustomerDto
        {
            TenDangNhap = "unique_user2",
            MatKhau = "Pass123",
            TenKhachHang = "Trần Văn B",
            DienThoai = "0912345678",
            Email = "existing@gmail.com"
        };

        // Act
        var result = await _service.RegisterCustomerAsync(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Địa chỉ email này đã được sử dụng");
        _mockAuthRepo.Verify(r => r.RegisterCustomerAsync(It.IsAny<RegisterCustomerDto>(), It.IsAny<string>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-AUTH-08: Đăng ký không nhập email hoặc email rỗng vẫn thành công và chuẩn hóa email thành null")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterCustomerAsync_EmptyOrNullEmail_SucceedsWithNullEmail(string? emptyEmail)
    {
        // Arrange
        _mockAuthRepo.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockAuthRepo.Setup(r => r.IsPhoneTakenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockAuthRepo.Setup(r => r.RegisterCustomerAsync(It.IsAny<RegisterCustomerDto>(), It.IsAny<string>()))
            .ReturnsAsync(999);

        var dto = new RegisterCustomerDto
        {
            TenDangNhap = "new_customer_no_email",
            MatKhau = "Pass123456",
            TenKhachHang = "Khách Hàng Mới",
            DienThoai = "0988776655",
            Email = emptyEmail
        };

        // Act
        var result = await _service.RegisterCustomerAsync(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        dto.Email.Should().BeNull();
        _mockAuthRepo.Verify(r => r.RegisterCustomerAsync(
            It.Is<RegisterCustomerDto>(d => d.Email == null),
            It.IsAny<string>()), Times.Once);
    }

    #endregion
}
