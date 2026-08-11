using System.Collections.Generic;
using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IDiskCleanupService
{
    Task<List<CleanupCategoryItem>> CalculateReclaimableSizesAsync();
    Task<(bool Success, string Message)> ExecuteCleanupAsync(List<CleanupCategoryItem> selectedCategories, string sessionId);
}
