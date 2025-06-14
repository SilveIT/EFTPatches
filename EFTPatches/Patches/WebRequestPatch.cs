using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFTPatches.WebRequests;
using SPT.Reflection.Patching;
using System.Text;
using BackResponse = GClass629;
using WebLogger = GClass630;

// ReSharper disable InconsistentNaming
// ReSharper disable RedundantAssignment

namespace EFTPatches.Patches
{
    public class WebRequestPatch : ModulePatch
    {
        private static IWebClient[] _webClients;

        private static IWebClient[] WebClients => _webClients ?? (_webClients = new IWebClient[]
        {
            new UnityWebClient(),
            new HttpClientWebClient()
        });

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
                    var headerLog = Utils.HeadersToString(headers, "\n");
                    logBuilder.AppendLine($"Headers:\n{headerLog}");
                }

                // Log POST Data if enabled
                if (settings.LogPostData.Value)
                {
                    if (data != null && data.Length > 0 && data.Length > 2 && Utils.IsZLibCompressed(data))
                    {
                        try
                        {
                            var unZipped = Utils.DecompressZLib(data);
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

            var r = RetryWaitResponse(url, data, headers, timeoutSeconds, PluginSettings.Instance.RetryCount.Value, WebClients);
            __result = r;

            if (logRequestsEnabled && settings.LogResponses.Value)
            {
                // Fire-and-forget logging after response completes
                Task.Run(async () =>
                {
                    try
                    {
                        // Wait for the response to complete
                        var response = await r.ConfigureAwait(false);

                        if (response != null)
                        {
                            var logBuilder = new StringBuilder();

                            // Log response URL
                            logBuilder.AppendLine($"[UWR] Response URL: {url}");

                            // Log response headers
                            if (settings.LogHeaders.Value && response.responseHeaders != null && response.responseHeaders.Count > 0)
                            {
                                var headerLog = Utils.HeadersToString(response.responseHeaders, "\n");
                                logBuilder.AppendLine($"Response Headers:\n{headerLog}");
                            }

                            // Log response body (hex or text)
                            if (settings.LogPostData.Value)
                            {
                                if (response.responseData != null && response.responseData.Length > 0)
                                {
                                    var responseData = response.responseData;
                                    var isZlib = Utils.IsZLibCompressed(responseData);

                                    if (isZlib)
                                    {
                                        try
                                        {
                                            var unZipped = Utils.DecompressZLib(responseData);
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
                                            logBuilder.AppendLine($"Unable to decompress response:\n{e}");
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
            int retries,
            params IWebClient[] clients)
        {
            const int retryDelayMs = 500;
            var stopwatch = Stopwatch.StartNew();
            var logError = string.Empty;
            var lastErrorCode = 0;
            var lastErrorText = string.Empty;

            for (var attempt = 0; attempt < retries; attempt++)
            {
                for (var i = 0; i < clients.Length; i++)
                {
                    var client = clients[i];
                    try
                    {
                        var backResponse = await client.SendRequestAsync(url, data, headers, timeoutSeconds);

                        if (backResponse != null && string.IsNullOrEmpty(backResponse.errorText))
                        {
                            if (attempt > 0 || i > 0)
                                EFTPatchesPlugin.PluginLogger.LogWarning("Saved u from request error, URL: " + url);

                            backResponse.stopwatch = stopwatch;
                            return backResponse;
                        }
                        else
                        {
                            if (backResponse != null)
                            {
                                lastErrorCode = backResponse.responseCode;
                                lastErrorText = backResponse.errorText;
                            }
                            logError =
                                $"<--- Error! {client.GetType().Name} failed on URL: {url}, Response Code: {lastErrorCode}, Error: {lastErrorText}";
                        }
                    }
                    catch (Exception ex)
                    {
                        lastErrorText = ex.ToString();
                        logError = $"<--- Exception in {client.GetType().Name}: {ex}";
                    }
                }

                EFTPatchesPlugin.PluginLogger.LogWarning($"Request failed on attempt {attempt + 1}. Retrying in {retryDelayMs}ms...");
                await Task.Delay(retryDelayMs);
            }

            WebLogger.Logger.LogError(logError);
            return new BackResponse("Backend error: " + lastErrorText, lastErrorCode);
        }
    }
}
