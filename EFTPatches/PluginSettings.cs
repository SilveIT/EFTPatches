#if !UNITY_EDITOR

namespace EFTPatches
{
    using BepInEx.Configuration;
    using System.Diagnostics.CodeAnalysis;
    internal class PluginSettings
        {
            public static PluginSettings Instance { get; private set; }

            // UnityWebRequest Fix settings
            public readonly ConfigEntry<bool> LogUrls;
            public readonly ConfigEntry<bool> LogPostData;

            private const string UnityWebSection = "UnityWebRequest Fix";

            [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
            private PluginSettings(ConfigFile configFile)
            {
                // Bind settings under "UnityWebRequest Fix" section
                LogUrls = configFile.Bind(
                    UnityWebSection,
                    "Log URLs",
                    false,
                    new ConfigDescription("Logs all URLs requested via UnityWebRequest.")
                );

                LogPostData = configFile.Bind(
                    UnityWebSection,
                    "Log POST Data",
                    false,
                    new ConfigDescription("Logs the body/data sent in POST requests via UnityWebRequest.")
                );
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