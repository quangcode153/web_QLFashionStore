using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Audit;

namespace Backend.Controllers;

[ApiController]
[Route("api/audit-logs")]
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
        var isChainAdmin = string.IsNullOrEmpty(userRole) ||
                           userRole.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                           userRole.Contains("chuỗi", StringComparison.OrdinalIgnoreCase) ||
                           userRole.Contains("hệ thống", StringComparison.OrdinalIgnoreCase);

        int? effectiveBranchId = maChiNhanh;

        // Nếu không phải Quản lý chuỗi (Admin), bắt buộc chỉ được xem nhật ký của chi nhánh mình đang quản lý
        if (!isChainAdmin && !string.IsNullOrEmpty(userRole))
        {
            var branchClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst("ma_chi_nhanh")?.Value;
            if (int.TryParse(branchClaim, out var bId) && bId > 0)
            {
                effectiveBranchId = bId;
            }
        }

        Console.WriteLine($"[AuditLogsController] Incoming request: UserRole='{userRole}', isChainAdmin={isChainAdmin}, effectiveBranchId={effectiveBranchId}, limit={limit}");
        foreach (var c in User.Claims)
        {
            Console.WriteLine($"  Claim: {c.Type} = {c.Value}");
        }

        var result = await _auditLogService.GetLogsAsync(loaiThaoTac, effectiveBranchId, limit);
        Console.WriteLine($"[AuditLogsController] Returned logs count: {result.Data?.Count()}");
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
