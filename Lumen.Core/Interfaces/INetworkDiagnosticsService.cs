using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface INetworkDiagnosticsService
{
    Task<NetworkDiagnosticResult> RunNetworkDiagnosticsAsync(string serverTarget);
    Task<(bool Success, string Message)> DisableDriveReconnectAsync(string driveLetter, string sessionId);
}
