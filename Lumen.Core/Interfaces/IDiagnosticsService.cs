using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IDiagnosticsService
{
    Task<DiagnosticsSnapshot> RunFullScanAsync();
}
