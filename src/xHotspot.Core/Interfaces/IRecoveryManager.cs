using xHotspot.Core.Models;

namespace xHotspot.Core.Interfaces;

public interface IRecoveryManager
{
    Task EvaluateAndRecoverAsync(CancellationToken cancellationToken = default);
}
