using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Warehouse;

namespace Backend.Repositories;

public interface IBranchRepository
{
    Task<IEnumerable<BranchDto>> GetAllBranchesAsync();
    Task<BranchDto?> GetBranchByIdAsync(int branchId);
    Task<int> CreateBranchAsync(CreateBranchDto dto);
    Task<IEnumerable<BranchInventoryDto>> GetBranchInventoryAsync(int branchId, string? search = null);
    Task<BranchInventoryDto?> GetVariantInventoryAtBranchAsync(int branchId, int variantId);
    Task<bool> AdjustBranchInventoryAsync(int branchId, int variantId, int deltaQuantity, string reason, int staffId);
    Task<int> GetStockQuantityAsync(int branchId, int variantId);
}
