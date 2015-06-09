using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridCaching.Tests
{
    public interface IFakeValueCreator
    {
        Task<string> CreateString();
        Task<Dictionary<string, string>> CreateDictionary();
    }
}
