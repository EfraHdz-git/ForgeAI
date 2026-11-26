using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IFocusProfileRepository
{
    Task<IEnumerable<FocusProfile>> GetActiveAsync();
    Task<FocusProfile?> GetByNameAsync(string name);
}
