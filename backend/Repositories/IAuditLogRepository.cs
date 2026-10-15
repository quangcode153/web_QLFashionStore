using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Audit;

namespace Backend.Repositories;

public interface IAuditLogRepository
{
    Task<int> CreateAuditLogAsync(CreateAuditLogRequestDto dto);
    Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(string? loaiThaoTac = null, int limit = 100);
}
