using System.Text.Json;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Common;

internal static class ApplicationContextHelper
{
    public const int MaxIpLength = 64;
    public const int MaxUserAgentLength = 1024;

    public static Guid RequireUserId(ICurrentUserService currentUser)
        => currentUser.UserId
            ?? throw AppException.Unauthorized("authentication_required", "Autenticação obrigatória.");

    public static SecurityAuditLog CreateAuditLog(
        Guid? userId,
        SecurityAuditEventType eventType,
        ICurrentUserService currentUser,
        DateTimeOffset utcNow,
        object? metadata = null)
        => new(
            userId,
            eventType,
            Truncate(currentUser.IpAddress, MaxIpLength),
            Truncate(currentUser.UserAgent, MaxUserAgentLength),
            utcNow,
            metadata is null ? null : JsonSerializer.Serialize(metadata));

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
