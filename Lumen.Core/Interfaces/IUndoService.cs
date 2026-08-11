using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IUndoService
{
    Task<(bool Success, string Message)> UndoActionAsync(ActionRecord action);
    Task<(bool Success, string Message)> UndoSessionAsync(string sessionId);
}
