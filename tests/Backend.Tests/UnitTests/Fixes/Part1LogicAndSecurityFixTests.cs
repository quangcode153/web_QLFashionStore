using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Controllers;
using Backend.Repositories;
using Backend.Security;
using Backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shared.Constants;
using Shared.DTOs.Auth;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.DTOs.Payment;
using Xunit;

namespace Backend.Tests.UnitTests.Fixes;

public class Part1LogicAndSecurityFixTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<IAuthRepository> _mockAuthRepo;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<IJwtTokenGenerator> _mockJwtGenerator;
    private readonly Mock<IPaymentService> _mockPaymentService;

    public Part1LogicAndSecurityFixTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockAuthRepo = new Mock<IAuthRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        _mockPaymentService = new Mock<IPaymentService>();
    }

    #region TASK 1: COMPENSATING ROLLBACK IN CREATE ORDER (ERR-41)

    [Fact(DisplayName = "ERR-41: CreateOrderAsync phải bồi hoàn hủy đơn khi một mặt hàng thêm thất bại")]
    public async Task CreateOrderAsync_WhenAnyItemFails_ShouldExecuteCompensatingRollback()
    {
        // Arrange
        var orderService = new OrderService(_mockOrderRepo.Object);
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            MaNhanVien = 2,
            Items = new List<CreateOrderItemDto>
            {
                new() { MaBienThe = 10, SoLuong = 1 },
                new() { MaBienThe = 20, SoLuong = 2 }
            }
        };

        _mockOrderRepo.Setup(r => r.CreateOrderAsync(1, 2, null))
            .ReturnsAsync(999);

        // Món 1 thành công
        _mockOrderRepo.Setup(r => r.AddOrderItemAsync(999, 10, 1))
            .Returns(Task.CompletedTask);

        // Món 2 thất bại (hết hàng / lỗi DB)
        _mockOrderRepo.Setup(r => r.AddOrderItemAsync(999, 20, 2))
            .ThrowsAsync(new Exception("Chi nhánh không đủ số lượng tồn kho!"));

        // Act
        var result = await orderService.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không đủ số lượng");

        // BẮT BUỘC phải gọi bồi hoàn UpdateOrderStatusAsync sang 'Đã hủy' để hoàn tồn kho món 1
        _mockOrderRepo.Verify(r => r.UpdateOrderStatusAsync(999, "Đã hủy"), Times.Once);
    }

    #endregion

    #region TASK 4: PAYMENT CONFIRMATION IDOR & STATE CHECK (ERR-44)

    [Fact(DisplayName = "ERR-44: ConfirmPaid chặn khách hàng xác nhận đơn của người khác (IDOR Defense)")]
    public async Task ConfirmPaid_CustomerAttemptingOtherCustomerOrder_ReturnsForbidden()
    {
        // Arrange
        var orderService = new OrderService(_mockOrderRepo.Object);
        var controller = new PaymentController(_mockPaymentService.Object, orderService, _mockOrderRepo.Object);

        // Giả lập user đăng nhập là Customer ID 5
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim(ClaimTypes.Role, RoleConstants.KhachHang)
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Đơn hàng thuộc về Customer ID 99
        _mockOrderRepo.Setup(r => r.GetOrderOwnershipAsync(123))
            .ReturnsAsync((Exists: true, CustomerId: 99));

        // Act
        var actionResult = await controller.ConfirmPaid(123, "VIETQR");

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "ERR-44: ConfirmPaid từ chối xác nhận nếu đơn hàng đã hoàn thành hoặc đã hủy")]
    public async Task ConfirmPaid_WhenOrderIsAlreadyCompletedOrCancelled_ReturnsBadRequest()
    {
        // Arrange
        var orderService = new OrderService(_mockOrderRepo.Object);
        var controller = new PaymentController(_mockPaymentService.Object, orderService, _mockOrderRepo.Object);

        // Giả lập Staff đăng nhập
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, RoleConstants.AdminChuoi)
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        _mockOrderRepo.Setup(r => r.GetOrderOwnershipAsync(123))
            .ReturnsAsync((Exists: true, CustomerId: 10));

        _mockOrderRepo.Setup(r => r.GetOrderStatusAsync(123))
            .ReturnsAsync("Đã hoàn thành");

        // Act
        var actionResult = await controller.ConfirmPaid(123, "VIETQR");

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<bool>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Đã hoàn thành");
    }

    #endregion

    #region TASK 5: FORGOT PASSWORD FLOW (ERR-45)

    [Fact(DisplayName = "ERR-45: ForgotPasswordAsync thành công khi khớp tên đăng nhập và số điện thoại")]
    public async Task ForgotPasswordAsync_WithValidUsernameAndPhone_ReturnsSuccess()
    {
        // Arrange
        var authService = new AuthService(_mockAuthRepo.Object, _mockPasswordHasher.Object, _mockJwtGenerator.Object);
        var request = new ForgotPasswordDto
        {
            TenDangNhap = "khachhang01",
            DienThoai = "0987654321",
            MatKhauMoi = "newpassword123"
        };

        _mockAuthRepo.Setup(r => r.VerifyCustomerPhoneAsync("khachhang01", "0987654321"))
            .ReturnsAsync(true);

        _mockPasswordHasher.Setup(h => h.HashPassword("newpassword123"))
            .Returns("hashed_secret_new_pass");

        _mockAuthRepo.Setup(r => r.ResetCustomerPasswordAsync("khachhang01", "hashed_secret_new_pass"))
            .ReturnsAsync(true);

        // Act
        var result = await authService.ForgotPasswordAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("thành công");
        _mockAuthRepo.Verify(r => r.ResetCustomerPasswordAsync("khachhang01", "hashed_secret_new_pass"), Times.Once);
    }

    [Fact(DisplayName = "ERR-45: ForgotPasswordAsync báo lỗi khi số điện thoại không khớp với thông tin đã đăng ký")]
    public async Task ForgotPasswordAsync_WithMismatchedPhone_ReturnsFail()
    {
        // Arrange
        var authService = new AuthService(_mockAuthRepo.Object, _mockPasswordHasher.Object, _mockJwtGenerator.Object);
        var request = new ForgotPasswordDto
        {
            TenDangNhap = "khachhang01",
            DienThoai = "0111222333",
            MatKhauMoi = "newpassword123"
        };

        _mockAuthRepo.Setup(r => r.VerifyCustomerPhoneAsync("khachhang01", "0111222333"))
            .ReturnsAsync(false);

        // Act
        var result = await authService.ForgotPasswordAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không khớp");
        _mockAuthRepo.Verify(r => r.ResetCustomerPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion
}
