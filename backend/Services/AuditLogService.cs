using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Shared.DTOs.Audit;
using Shared.DTOs.Common;

namespace Backend.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<ApiResponse<int>> CreateLogAsync(CreateAuditLogRequestDto request)
    {
        if (request == null)
        {
            return ApiResponse<int>.Fail("Dữ liệu nhật ký không được để trống.");
        }

        try
        {
            var id = await _auditLogRepository.CreateAuditLogAsync(request);
            return ApiResponse<int>.Ok(id, "Ghi nhật ký hệ thống thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi ghi nhật ký: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<AuditLogDto>>> GetLogsAsync(string? loaiThaoTac = null, int limit = 100)
    {
        try
        {
            var logs = await _auditLogRepository.GetAuditLogsAsync(loaiThaoTac, limit);
            return ApiResponse<IEnumerable<AuditLogDto>>.Ok(logs);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<AuditLogDto>>.Fail($"Lỗi tải nhật ký hệ thống: {ex.Message}");
        }
    }
}
