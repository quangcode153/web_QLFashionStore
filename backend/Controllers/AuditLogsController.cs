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
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? loaiThaoTac = null, [FromQuery] int limit = 100)
    {
        var result = await _auditLogService.GetLogsAsync(loaiThaoTac, limit);
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
