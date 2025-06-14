using System.Collections.Generic;
using System.Threading.Tasks;
using BackResponse = GClass629;

namespace EFTPatches.WebRequests
{
    public interface IWebClient
    {
        Task<BackResponse> SendRequestAsync(string url, byte[] data, Dictionary<string, string> headers, int timeoutSeconds);
    }
}