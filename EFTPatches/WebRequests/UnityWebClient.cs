using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine.Networking;
using BackResponse = GClass629;

namespace EFTPatches.WebRequests
{
    public class UnityWebClient : IWebClient
    {
        public async Task<BackResponse> SendRequestAsync(string url, byte[] data, Dictionary<string, string> headers, int timeoutSeconds)
        {
            using (var unityWebRequest = new UnityWebRequest(url, "POST"))
            {
                unityWebRequest.uploadHandler = new UploadHandlerRaw(data);
                unityWebRequest.downloadHandler = new DownloadHandlerBuffer();
                unityWebRequest.certificateHandler = new SslCertPatchClass();
                unityWebRequest.timeout = timeoutSeconds;
                unityWebRequest.SetRequestHeader("Content-Type", "application/json");

                foreach (var header in headers)
                    unityWebRequest.SetRequestHeader(header.Key, header.Value);

                var operation = unityWebRequest.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (!unityWebRequest.isNetworkError && !unityWebRequest.isHttpError)
                {
                    return new BackResponse(
                        (int)unityWebRequest.responseCode,
                        null,
                        unityWebRequest.GetResponseHeaders(),
                        unityWebRequest.downloadHandler.data,
                        unityWebRequest.downloadHandler.data.Length,
                        unityWebRequest.downloadHandler.text,
                        Stopwatch.StartNew());
                }

                return null;
            }
        }
    }
}