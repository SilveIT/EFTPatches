#if !UNITY_EDITOR

namespace EFTPatches
{
    using BepInEx.Configuration;
    using System.Diagnostics.CodeAnalysis;
    internal class PluginSettings
    {
        public static PluginSettings Instance { get; private set; }

        // UnityWebRequest Fix settings
        public readonly ConfigEntry<bool> LogRequests;
        public readonly ConfigEntry<bool> LogPostData;
        public readonly ConfigEntry<bool> LogHeaders;
        public readonly ConfigEntry<bool> LogResponses;
        public readonly ConfigEntry<int> MaxHexLogLength;

        private const string UnityWebSection = "UnityWebRequest Fix";

        [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
        private PluginSettings(ConfigFile configFile)
        {
            // Bind settings under "UnityWebRequest Fix" section
            LogRequests = configFile.Bind(
                UnityWebSection,
                "Log UnityWebRequest-s",
                false,
                new ConfigDescription("Logs all URLs requested via UnityWebRequest.")
            );

            LogPostData = configFile.Bind(
                UnityWebSection,
                "Log POST Data",
                false,
                new ConfigDescription("Logs the data sent in POST requests via UnityWebRequest.")
            );

            LogHeaders = configFile.Bind(
                UnityWebSection,
                "Log Headers",
                true,
                new ConfigDescription("Logs HTTP headers of outgoing requests."));

            LogResponses = configFile.Bind(
                UnityWebSection,
                "Log Responses",
                true,
                new ConfigDescription("Logs HTTP responses received from the server."));

            MaxHexLogLength = configFile.Bind(
                    UnityWebSection,
                    "HEX Dump Limit",
                    512,
                    new ConfigDescription("Maximum number of bytes to show in hex dumps for request data.",
                        new AcceptableValueRange<int>(16, 1024 * 1024)));

        }

        public static PluginSettings Create(ConfigFile configFile)
        {
            if (Instance != null)
                return Instance;

            return Instance = new PluginSettings(configFile);
        }
    }
}

#endif