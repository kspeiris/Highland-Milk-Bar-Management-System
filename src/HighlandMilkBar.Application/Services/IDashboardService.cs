using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
