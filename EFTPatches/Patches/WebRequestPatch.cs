using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SPT.Reflection.Patching;
using UnityEngine.Networking;
// ReSharper disable InconsistentNaming

using BackResponse = GClass629;
using WebLogger = GClass630;
using System.IO.Compression;
using System.IO;
using System.Text;

namespace EFTPatches.Patches
{
    public class WebRequestPatch : ModulePatch
    {
        //System.Threading.Tasks.Task`1<GClass629> Class315::WaitResponse(System.String,System.Byte[],System.Collections.Generic.Dictionary`2<System.String,System.String>,System.Int32)
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Class315), nameof(Class315.WaitResponse));
        }

        [PatchPrefix]
        public static bool PatchPrefix(string url,
            byte[] data,
            Dictionary<string, string> headers,
            int timeoutSeconds,
            ref Task<BackResponse> __result)
        {
            var settings = PluginSettings.Instance;

            var logRequestsEnabled = settings.LogRequests.Value;

            if (logRequestsEnabled)
            {
                var logBuilder = new StringBuilder();

                logBuilder.AppendLine($"[UWR] Request URL: {url}");

                // Log Headers if enabled
                if (settings.LogHeaders.Value && headers != null && headers.Count > 0)
                {
                    var headerLog = HeadersToString(headers, "\n");
                    logBuilder.AppendLine($"Headers:\n{headerLog}");
                }

                // Log POST Data if enabled
                if (settings.LogPostData.Value)
                {
                    if (data != null && data.Length > 0 && data.Length > 2 && IsZLibCompressed(data))
                    {
                        try
                        {
                            var unZipped = DecompressZLib(data);
                            if (unZipped.Length > 0)
                            {
                                var maxLength = PluginSettings.Instance.MaxHexLogLength.Value;
                                var hex = unZipped.Take(maxLength).ToArray().ToHex();
                                var note = unZipped.Length > maxLength ? $" (first {maxLength} of {unZipped.Length} bytes)" : $" ({unZipped.Length} bytes)";
                                logBuilder.AppendLine($"POST Data (decompressed):{note}\n{hex}");
                            }
                            else
                                logBuilder.AppendLine("POST Data (decompressed): EMPTY");
                        }
                        catch (Exception e)
                        {
                            logBuilder.AppendLine($"Unable to decompress:\n{e}");
                        }
                    }
                    else
                    {
                        if (data != null && data.Length > 0)
                        {
                            var maxLength = PluginSettings.Instance.MaxHexLogLength.Value;
                            var hex = data.Take(maxLength).ToArray().ToHex();
                            var note = data.Length > maxLength ? $" (first {maxLength} of {data.Length} bytes)" : $" ({data.Length} bytes)";
                            logBuilder.AppendLine($"POST Data:{note}\n{hex}");
                        }
                        else
                            logBuilder.AppendLine("POST Data: EMPTY");
                    }
                }

                var finalLog = logBuilder.ToString();
                if (!string.IsNullOrEmpty(finalLog))
                {
                    EFTPatchesPlugin.PluginLogger.LogInfo(finalLog.TrimEnd());
                }
            }

            var r = RetryWaitResponse(url, data, headers, timeoutSeconds, EFTPatchesPlugin.WEBRequestRetries);
            __result = r;

            if (logRequestsEnabled && settings.LogResponses.Value)
            {
                // Fire-and-forget logging after response completes
                Task.Run(async () =>
                {
                    try
                    {
                        // Wait for the response to complete
                        BackResponse response = await r.ConfigureAwait(false);

                        if (response != null)
                        {
                            var logBuilder = new StringBuilder();

                            // Log response URL
                            logBuilder.AppendLine($"[UWR] Response URL: {url}");

                            // Log response headers
                            if (settings.LogHeaders.Value && response.responseHeaders != null && response.responseHeaders.Count > 0)
                            {
                                var headerLog = HeadersToString(response.responseHeaders, "\n");
                                logBuilder.AppendLine($"Response Headers:\n{headerLog}");
                            }

                            // Log response body (hex or text)
                            if (settings.LogPostData.Value)
                            {
                                if (response.responseData != null && response.responseData.Length > 0)
                                {
                                    byte[] responseData = response.responseData;
                                    bool isZlib = IsZLibCompressed(responseData);

                                    if (isZlib)
                                    {
                                        try
                                        {
                                            var unZipped = DecompressZLib(responseData);
                                            if (unZipped.Length > 0)
                                            {
                                                var maxLength = settings.MaxHexLogLength.Value;
                                                var hex = unZipped.Take(maxLength).ToArray().ToHex();
                                                var note = unZipped.Length > maxLength ? $" (first {maxLength} of {unZipped.Length} bytes)" : $" ({unZipped.Length} bytes)";
                                                logBuilder.AppendLine($"Response Data (decompressed):{note}\n{hex}");
                                            }
                                            else
                                            {
                                                logBuilder.AppendLine("Response Data (decompressed): EMPTY");
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            logBuilder.AppendLine($"Unable to decompress response:\n{e.Message}");
                                        }
                                    }
                                    else
                                    {
                                        var maxLength = settings.MaxHexLogLength.Value;
                                        var hex = responseData.Take(maxLength).ToArray().ToHex();
                                        var note = responseData.Length > maxLength ? $" (first {maxLength} of {responseData.Length} bytes)" : $" ({responseData.Length} bytes)";
                                        logBuilder.AppendLine($"Response Data:{note}\n{hex}");
                                    }
                                }
                                else
                                {
                                    logBuilder.AppendLine("Response Data: EMPTY");
                                }
                            }

                            // Also log status code or error info
                            if (response.responseCode >= 400 || !string.IsNullOrEmpty(response.errorText))
                            {
                                logBuilder.AppendLine($"Status Code: {response.responseCode}");
                                if (!string.IsNullOrEmpty(response.errorText))
                                    logBuilder.AppendLine($"Error: {response.errorText}");
                            }

                            EFTPatchesPlugin.PluginLogger.LogInfo(logBuilder.ToString().TrimEnd());
                        }
                    }
                    catch (Exception ex)
                    {
                        EFTPatchesPlugin.PluginLogger.LogError($"Failed to log response:\n{ex}");
                    }
                });
            }

            return false; // Skip the original method execution
        }

        private static async Task<BackResponse> RetryWaitResponse(
            string url,
            byte[] data,
            Dictionary<string, string> headers,
            int timeoutSeconds,
            int retries)
        {
            const int retryDelayMs = 500;
            BackResponse backResponse = null;
            var stopwatch = Stopwatch.StartNew();
            var logError = string.Empty;
            var webReqResponseCode = 0L;
            var webReqErrorText = string.Empty;

            for (var attempt = 0; attempt < retries; attempt++)
            {
                try
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

                        try
                        {
                            backResponse = await RetryRequestAsync(url, unityWebRequest, stopwatch);
                        }
                        catch (Exception)
                        {
                            backResponse = null;
                        }

                        if (backResponse == null)
                        {
                            webReqResponseCode = unityWebRequest.responseCode;
                            webReqErrorText = unityWebRequest.error;
                            var errorText = unityWebRequest.error;
                            errorText = errorText == "Unknown Error"
                                ? "Certificate validation error"
                                : errorText + "\n" + unityWebRequest.downloadHandler.text;
                            logError = $"<--- Error! HTTPS: {url}, isNetworkError:{unityWebRequest.isNetworkError}, isHttpError:{unityWebRequest.isHttpError}, responseCode:{(int)unityWebRequest.responseCode}\n responseHeaders:{HeadersToString(unityWebRequest.GetResponseHeaders(), "\n")}\nerror text: {errorText}";
                        }
                    }

                    // If the request succeeds, return the response
                    if (backResponse != null)
                    {
                        if (attempt > 0)
                            EFTPatchesPlugin.PluginLogger.LogInfo("Saved u from Cert Error, URL: " + url);
                        return backResponse;
                    }

                    // Log the failure and retry
                    EFTPatchesPlugin.PluginLogger.LogWarning($"Request failed on attempt {attempt + 1}. Retrying in {retryDelayMs}ms...");
                }
                catch (Exception ex)
                {
                    backResponse = new BackResponse(ex.Message);
                }

                // Delay before retrying
                if (attempt < retries)
                    await Task.Delay(retryDelayMs);
            }

            if (!string.IsNullOrEmpty(logError))
                WebLogger.Logger.LogError(logError);

            if (backResponse == null)
                backResponse = new BackResponse("Backend error: " + webReqErrorText, (int)webReqResponseCode);

            return backResponse;
        }

        private static async Task<BackResponse> RetryRequestAsync(string url, UnityWebRequest request,
        Stopwatch stopwatch)
        {
            var operation = request.SendWebRequest();

            // Wait for the request to complete
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // Check if the request succeeded
            if (!request.isNetworkError && !request.isHttpError)
            {
                var responseHeaders = request.GetResponseHeaders();
                var data = request.downloadHandler.data;
                var text = request.downloadHandler.text;
                var responseCode = (int)request.responseCode;

                return new BackResponse(responseCode, string.Empty, responseHeaders, data, data.Length, text,
                    stopwatch);
            }

            return null;
        }

        private static string HeadersToString(Dictionary<string, string> headers, string separator)
        {
            if (headers == null)
            {
                return string.Empty;
            }
            var text = string.Empty;
            var aggregator = new HeadersAggregator(separator);

            text = headers.Aggregate(text, aggregator.Aggregate);
            return text;
        }

        public class HeadersAggregator
        {
            public HeadersAggregator(string separator)
            {
                _separator = separator;
            }
            public string Aggregate(string current, KeyValuePair<string, string> item)
            {
                return string.Concat(current, "{", item.Key, ":", item.Value, "}", _separator);
            }

            private readonly string _separator;
        }

        public static bool IsZLibCompressed(byte[] data)
        {
            return data?.Length >= 2 && data[0] == 0x78 && (
                data[1] == 0x01 ||  // low compression
                data[1] == 0x5E ||  // medium compression
                data[1] == 0x9C ||  // high compression (default)
                data[1] == 0xDA);   // highest compression
        }

        public static byte[] DecompressZLib(byte[] zlibData)
        {
            // Skip the first 2 bytes (ZLib header) and last 4 bytes (checksum)
            int compressedLength = zlibData.Length - 6; // subtract header(2) + adler32(4)
            byte[] compressedBytes = new byte[compressedLength];
            Buffer.BlockCopy(zlibData, 2, compressedBytes, 0, compressedLength);

            using (var input = new MemoryStream(compressedBytes))
            using (var decompressor = new DeflateStream(input, CompressionMode.Decompress))
            using (var result = new MemoryStream())
            {
                decompressor.CopyTo(result);
                return result.ToArray();
            }
        }
    }
}
