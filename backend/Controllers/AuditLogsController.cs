using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Audit;

namespace Backend.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.ThuKho}")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? loaiThaoTac = null, [FromQuery] int? maChiNhanh = null, [FromQuery] int limit = 100)
    {
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
        var isChainAdmin = userRole == RoleConstants.AdminChuoi || userRole == RoleConstants.QuanLyHeThong;

        int? effectiveBranchId;
        if (isChainAdmin)
        {
            // Quản lý chuỗi (Admin): Được quyền xem toàn bộ chuỗi (khi maChiNhanh = null) hoặc lọc theo chi nhánh bất kỳ
            effectiveBranchId = maChiNhanh;
        }
        else
        {
            // Quản lý chi nhánh & Thủ kho: BẮT BUỘC chỉ được xem nhật ký thuộc chi nhánh/kho của mình
            var branchClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst("ma_chi_nhanh")?.Value;
            if (int.TryParse(branchClaim, out var bId) && bId > 0)
            {
                effectiveBranchId = bId;
            }
            else if (maChiNhanh.HasValue && maChiNhanh.Value > 0)
            {
                // Nếu không có claim branchId nhưng có query maChiNhanh (Thủ kho Online hoặc môi trường dev)
                effectiveBranchId = maChiNhanh.Value;
            }
            else if (userRole == RoleConstants.ThuKho)
            {
                // Thủ kho mặc định kho 9 (Kho Tổng Online) nếu không có claim cụ thể
                effectiveBranchId = 9;
            }
            else
            {
                return Forbid();
            }
        }

        var result = await _auditLogService.GetLogsAsync(loaiThaoTac, effectiveBranchId, limit);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAuditLog([FromBody] CreateAuditLogRequestDto request)
    {
        var result = await _auditLogService.CreateLogAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
