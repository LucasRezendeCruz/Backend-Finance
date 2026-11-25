namespace Finance.Application.Dashboard;

public interface IDashboardService
{
    Task<MonthlyDashboardSummaryDto> GetMonthlySummaryAsync(int year, int month, CancellationToken ct = default);
}
