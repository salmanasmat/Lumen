using System.Collections.Generic;
using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IProfileService
{
    Task<LumenProfile> GetDefaultProfileAsync();
    Task<List<LumenProfile>> GetCustomProfilesAsync();
    Task SaveProfileAsync(LumenProfile profile, string filePath);
    Task<LumenProfile> LoadProfileAsync(string filePath);
    Task<(bool Success, string Message)> ApplyProfileAsync(LumenProfile profile, IProgress<string>? progress = null);
}
