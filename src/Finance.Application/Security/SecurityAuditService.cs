using System.Text.Json;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Common;
using Finance.Domain.Entities;

namespace Finance.Application.Security;

public sealed class SecurityAuditService(
    ISecurityAuditRepository auditRepository,
    ICurrentUserService currentUserService) : ISecurityAuditService
{
    public async Task<PagedAuditLogsDto> GetAuditLogsAsync(
        int page,
        int pageSize,
        bool alertsOnly,
        CancellationToken ct = default)
    {
        Guid userId = currentUserService.UserId
            ?? throw AppException.Unauthorized("authentication_required", "Autenticação obrigatória.");

        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 100);

        (IReadOnlyList<SecurityAuditLog> items, int totalCount) =
            await auditRepository.GetUserAuditLogsAsync(userId, safePage, safePageSize, alertsOnly, ct);

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)safePageSize);

        IReadOnlyList<SecurityAuditLogItemDto> mapped = items
            .Select(log => new SecurityAuditLogItemDto(
                log.Id,
                log.EventType.ToScreamingSnakeCase(),
                log.IpAddress,
                log.UserAgent,
                ParseMetadata(log.MetadataJson),
                log.CreatedAtUtc))
            .ToArray();

        return new PagedAuditLogsDto(mapped, safePage, safePageSize, totalCount, totalPages);
    }

    private static JsonElement? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        using var document = JsonDocument.Parse(metadataJson);
        return document.RootElement.Clone();
    }
}
