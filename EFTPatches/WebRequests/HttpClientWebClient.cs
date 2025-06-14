using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using System.Net;
using BackResponse = GClass629;

namespace EFTPatches.WebRequests
{
    public class HttpClientWebClient : IWebClient
    {
        private static readonly CookieContainer CookieContainer = new CookieContainer();
        private static readonly HttpClient HttpClient;

        static HttpClientWebClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

            // Set proxy to Fiddler running locally
            //var proxy = new WebProxy("http://127.0.0.1:8888", false);
            //handler.Proxy = proxy;
            //handler.UseProxy = true;
            handler.CookieContainer = CookieContainer;

            HttpClient = new HttpClient(handler);
        }

        public async Task<BackResponse> SendRequestAsync(string url, byte[] data, Dictionary<string, string> headers, int timeoutSeconds)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new ByteArrayContent(data)
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

                // Use the Content-Type from headers, or default to application/json
                var contentType = headers.ContainsKey("Content-Type") ? headers["Content-Type"] : "application/json";
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                if (headers.TryGetValue("Cookie", out var cookieHeaderValue))
                {
                    headers.Remove("Cookie"); // Prevent duplicate cookie headers

                    var uri = new Uri(url);
                    CookieContainer.SetCookies(uri, cookieHeaderValue); // Will overwrite existing
                }

                // Copy over custom headers from input
                foreach (var header in headers)
                {
                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // must be set on the content object, not the request headers
                        continue;
                    }

                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        // For cases where headers need to go on the content (e.g., Cookie)
                        request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var response = await HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                var responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
                var responseBody = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                return new BackResponse(
                    (int)response.StatusCode,
                    null,
                    responseHeaders,
                    responseBody,
                    responseBody.Length,
                    Encoding.UTF8.GetString(responseBody), null);
            }
            catch (OperationCanceledException ex)
            {
                // This includes both timeout and manual cancellation
                return ex.CancellationToken == default ?
                    // Operation was manually canceled
                    new BackResponse("Request was canceled.", -1) :
                    // Timeout occurred
                    new BackResponse("The request timed out.", 408); // HTTP 408 Request Timeout
            }
            catch (HttpRequestException ex)
            {
                // Network-level issues (DNS failure, unreachable server, etc.)
                return new BackResponse($"Network error: {ex}", -1);
            }
            catch (Exception ex)
            {
                // Other unexpected errors
                return new BackResponse($"Unexpected error: {ex}", -1);
            }
        }
    }
}