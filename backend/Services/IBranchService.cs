using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Common;
using Shared.DTOs.Warehouse;

namespace Backend.Services;

public interface IBranchService
{
    Task<ApiResponse<IEnumerable<BranchDto>>> GetAllBranchesAsync();
    Task<ApiResponse<BranchDto>> GetBranchByIdAsync(int branchId);
    Task<ApiResponse<int>> CreateBranchAsync(CreateBranchDto dto);
    Task<ApiResponse<IEnumerable<BranchInventoryDto>>> GetBranchInventoryAsync(int branchId, string? search = null);
    Task<ApiResponse<BranchInventoryDto>> GetVariantInventoryAtBranchAsync(int branchId, int variantId);
    Task<ApiResponse<bool>> AdjustBranchInventoryAsync(AdjustBranchInventoryDto dto);
}
