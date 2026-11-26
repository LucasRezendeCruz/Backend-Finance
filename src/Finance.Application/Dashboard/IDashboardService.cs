namespace Finance.Application.Dashboard;

public interface IDashboardService
{
    Task<MonthlyDashboardSummaryDto> GetMonthlySummaryAsync(int year, int month, CancellationToken ct = default);
    Task<CashflowProjectionDto> GetProjectionAsync(int startYear, int startMonth, int months = 6, CancellationToken ct = default);
}
