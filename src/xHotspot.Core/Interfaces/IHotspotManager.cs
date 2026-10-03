using xHotspot.Core.Models;

namespace xHotspot.Core.Interfaces;

public interface IHotspotManager
{
    Task<HotspotStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> EnableAsync(CancellationToken cancellationToken = default);
    Task<bool> DisableAsync(CancellationToken cancellationToken = default);
    Task<bool> IsApiAvailableAsync(CancellationToken cancellationToken = default);
}
