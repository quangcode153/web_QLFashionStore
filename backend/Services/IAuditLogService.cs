using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Audit;
using Shared.DTOs.Common;

namespace Backend.Services;

public interface IAuditLogService
{
    Task<ApiResponse<int>> CreateLogAsync(CreateAuditLogRequestDto request);
    Task<ApiResponse<IEnumerable<AuditLogDto>>> GetLogsAsync(string? loaiThaoTac = null, int limit = 100);
}
