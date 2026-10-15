using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Shared.DTOs.Common;
using Shared.DTOs.Warehouse;

namespace Backend.Services;

public class BranchService : IBranchService
{
    private readonly IBranchRepository _branchRepository;

    public BranchService(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<ApiResponse<IEnumerable<BranchDto>>> GetAllBranchesAsync()
    {
        try
        {
            var branches = await _branchRepository.GetAllBranchesAsync();
            return ApiResponse<IEnumerable<BranchDto>>.Ok(branches);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<BranchDto>>.Fail($"Lỗi khi lấy danh sách chi nhánh: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BranchDto>> GetBranchByIdAsync(int branchId)
    {
        try
        {
            var branch = await _branchRepository.GetBranchByIdAsync(branchId);
            if (branch == null)
            {
                return ApiResponse<BranchDto>.Fail($"Không tìm thấy chi nhánh với ID {branchId}");
            }
            return ApiResponse<BranchDto>.Ok(branch);
        }
        catch (Exception ex)
        {
            return ApiResponse<BranchDto>.Fail($"Lỗi khi lấy thông tin chi nhánh: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateBranchAsync(CreateBranchDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.TenChiNhanh))
            {
                return ApiResponse<int>.Fail("Tên chi nhánh không được để trống.");
            }
            if (string.IsNullOrWhiteSpace(dto.DiaChi))
            {
                return ApiResponse<int>.Fail("Địa chỉ chi nhánh không được để trống.");
            }

            var branchId = await _branchRepository.CreateBranchAsync(dto);
            return ApiResponse<int>.Ok(branchId, $"Thêm chi nhánh thành công! Kho chi nhánh đã được tự động khởi tạo.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi tạo chi nhánh mới: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<BranchInventoryDto>>> GetBranchInventoryAsync(int branchId, string? search = null)
    {
        try
        {
            var items = await _branchRepository.GetBranchInventoryAsync(branchId, search);
            return ApiResponse<IEnumerable<BranchInventoryDto>>.Ok(items);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<BranchInventoryDto>>.Fail($"Lỗi khi lấy tồn kho chi nhánh #{branchId}: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BranchInventoryDto>> GetVariantInventoryAtBranchAsync(int branchId, int variantId)
    {
        try
        {
            var item = await _branchRepository.GetVariantInventoryAtBranchAsync(branchId, variantId);
            if (item == null)
            {
                return ApiResponse<BranchInventoryDto>.Fail($"Không tìm thấy biến thể #{variantId} tại chi nhánh #{branchId}");
            }
            return ApiResponse<BranchInventoryDto>.Ok(item);
        }
        catch (Exception ex)
        {
            return ApiResponse<BranchInventoryDto>.Fail($"Lỗi khi lấy thông tin biến thể: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> AdjustBranchInventoryAsync(AdjustBranchInventoryDto dto)
    {
        try
        {
            if (dto.SoLuongThayDoi == 0)
            {
                return ApiResponse<bool>.Fail("Số lượng điều chỉnh không thể bằng 0.");
            }

            var success = await _branchRepository.AdjustBranchInventoryAsync(
                dto.MaChiNhanh, 
                dto.MaBienThe, 
                dto.SoLuongThayDoi, 
                string.IsNullOrWhiteSpace(dto.LyDo) ? "DIEU_CHINH" : dto.LyDo, 
                dto.MaNhanVien);

            if (!success)
            {
                return ApiResponse<bool>.Fail("Tồn kho tại chi nhánh không đủ để thực hiện thao tác giảm tồn!");
            }

            return ApiResponse<bool>.Ok(true, "Điều chỉnh tồn kho chi nhánh thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi khi cập nhật tồn kho chi nhánh: {ex.Message}");
        }
    }
}
