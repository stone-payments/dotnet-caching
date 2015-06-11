using System;
using System.Threading.Tasks;

namespace HybridCaching.Interfaces
{
    public interface ISubscribable
    {
        Task SubscribeToDeleteAsync(Action<string, string> callback);
        Task SubscribeToUpdateTimeToLiveAsync(Action<string, string> callback);
    }
}
