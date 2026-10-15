using Backend.Repositories;
using Backend.Security;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;

namespace Backend.Services;

public interface ICustomerService
{
    Task<ApiResponse<CustomerProfileDto>> GetMyProfileAsync(int customerId);
    Task<ApiResponse<CustomerProfileDto>> GetProfileByPhoneAsync(string phone);
    Task<ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>> GetMyOrderHistoryAsync(int customerId);
    Task<ApiResponse<IEnumerable<RankEvaluationResultDto>>> EvaluateMemberRanksAsync();
    Task<ApiResponse<IEnumerable<CustomerAccountDto>>> GetAllCustomersAsync(string? search = null, int? capDo = null, bool? daXoa = null);
    Task<ApiResponse<bool>> ToggleCustomerStatusAsync(int customerId);
    Task<ApiResponse<bool>> UpdateCustomerAsync(UpdateCustomerAccountDto dto);
    Task<ApiResponse<int>> CreateCustomerAsync(CreateCustomerAccountDto dto);
    Task<ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>> GetCustomerOrderHistoryForAdminAsync(int customerId);
}

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IPasswordHasher? _passwordHasher;

    public CustomerService(ICustomerRepository customerRepository, IPasswordHasher? passwordHasher = null)
    {
        _customerRepository = customerRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiResponse<CustomerProfileDto>> GetProfileByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return ApiResponse<CustomerProfileDto>.Fail("Số điện thoại không được để trống.");
        }

        var profile = await _customerRepository.GetProfileByPhoneAsync(phone.Trim());
        if (profile == null)
        {
            return ApiResponse<CustomerProfileDto>.Fail($"Không tìm thấy khách hàng với số điện thoại '{phone}'.");
        }

        return ApiResponse<CustomerProfileDto>.Ok(profile);
    }

    public async Task<ApiResponse<CustomerProfileDto>> GetMyProfileAsync(int customerId)
    {
        if (customerId <= 0)
        {
            return ApiResponse<CustomerProfileDto>.Fail("Mã định danh khách hàng không hợp lệ.");
        }

        var profile = await _customerRepository.GetProfileByIdAsync(customerId);
        if (profile == null)
        {
            return ApiResponse<CustomerProfileDto>.Fail("Không tìm thấy thông tin tài khoản khách hàng trên hệ thống.");
        }

        return ApiResponse<CustomerProfileDto>.Ok(profile);
    }

    public async Task<ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>> GetMyOrderHistoryAsync(int customerId)
    {
        if (customerId <= 0)
        {
            return ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>.Fail("Mã định danh khách hàng không hợp lệ.");
        }

        var history = await _customerRepository.GetOrderHistoryAsync(customerId);
        return ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>.Ok(history);
    }

    public async Task<ApiResponse<IEnumerable<RankEvaluationResultDto>>> EvaluateMemberRanksAsync()
    {
        var results = await _customerRepository.EvaluateRanksAsync();
        return ApiResponse<IEnumerable<RankEvaluationResultDto>>.Ok(results, "Xét duyệt và cập nhật hạng thành viên tự động thành công.");
    }

    public async Task<ApiResponse<IEnumerable<CustomerAccountDto>>> GetAllCustomersAsync(string? search = null, int? capDo = null, bool? daXoa = null)
    {
        var customers = await _customerRepository.GetAllCustomersAsync(search, capDo, daXoa);
        return ApiResponse<IEnumerable<CustomerAccountDto>>.Ok(customers);
    }

    public async Task<ApiResponse<bool>> ToggleCustomerStatusAsync(int customerId)
    {
        if (customerId <= 0)
        {
            return ApiResponse<bool>.Fail("Mã định danh khách hàng không hợp lệ.");
        }

        var success = await _customerRepository.ToggleCustomerStatusAsync(customerId);
        if (!success)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy khách hàng hoặc không thể cập nhật trạng thái.");
        }

        return ApiResponse<bool>.Ok(true, "Cập nhật trạng thái tài khoản khách hàng thành công.");
    }

    public async Task<ApiResponse<bool>> UpdateCustomerAsync(UpdateCustomerAccountDto dto)
    {
        if (dto == null || dto.MaKhachHang <= 0)
        {
            return ApiResponse<bool>.Fail("Dữ liệu khách hàng không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(dto.TenKhachHang))
        {
            return ApiResponse<bool>.Fail("Tên khách hàng không được để trống.");
        }

        var success = await _customerRepository.UpdateCustomerProfileAsync(dto);
        if (!success)
        {
            return ApiResponse<bool>.Fail("Không thể cập nhật thông tin khách hàng.");
        }

        return ApiResponse<bool>.Ok(true, "Cập nhật thông tin khách hàng thành công.");
    }

    public async Task<ApiResponse<int>> CreateCustomerAsync(CreateCustomerAccountDto dto)
    {
        if (dto == null)
        {
            return ApiResponse<int>.Fail("Dữ liệu không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(dto.TenKhachHang))
        {
            return ApiResponse<int>.Fail("Tên khách hàng không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.DienThoai))
        {
            return ApiResponse<int>.Fail("Số điện thoại khách hàng không được để trống.");
        }

        var password = string.IsNullOrWhiteSpace(dto.MatKhau) ? "123456" : dto.MatKhau;
        var pwdHash = _passwordHasher != null ? _passwordHasher.HashPassword(password) : password;

        if (dto.MaCapDo <= 0) dto.MaCapDo = 1;
        if (dto.DiemThuong < 0) dto.DiemThuong = 0;

        try
        {
            var newId = await _customerRepository.CreateCustomerAsync(dto, pwdHash);
            return ApiResponse<int>.Ok(newId, "Tạo tài khoản khách hàng mới thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi tạo tài khoản khách hàng: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>> GetCustomerOrderHistoryForAdminAsync(int customerId)
    {
        if (customerId <= 0)
        {
            return ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>.Fail("Mã khách hàng không hợp lệ.");
        }

        var orders = await _customerRepository.GetOrderHistoryAsync(customerId);
        return ApiResponse<IEnumerable<CustomerOrderHistoryItemDto>>.Ok(orders);
    }
}
