namespace Finance.Application.Common.Interfaces;

public interface ICurrentUserOverride
{
    IDisposable RunAs(Guid userId, string userAgent);
}
