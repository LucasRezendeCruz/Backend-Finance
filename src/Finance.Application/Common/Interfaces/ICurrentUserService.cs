using Finance.Domain.Enums;

namespace Finance.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? SessionFamilyId { get; }
    ClientType? ClientType { get; }
    string IpAddress { get; }
    string UserAgent { get; }
}
