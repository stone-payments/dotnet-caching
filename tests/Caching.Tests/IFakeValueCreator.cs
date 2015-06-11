using System.Collections.Generic;
using System.Threading.Tasks;

namespace Vtex.Caching.Tests
{
    public interface IFakeValueCreator
    {
        Task<string> CreateString();
        Task<Dictionary<string, string>> CreateDictionary();
    }
}
