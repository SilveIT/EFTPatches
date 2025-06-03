using BepInEx;
using BepInEx.Logging;
using EFTPatches.Patches;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0051 // Remove unused private members

namespace EFTPatches
{
    [BepInPlugin("com.silve.eftpatches", "EFTPatches", "1.2")]
    public class EFTPatchesPlugin : BaseUnityPlugin
    {
        public static EFTPatchesPlugin Instance { get; private set; }
        public const int WEBRequestRetries = 3;
        public static ManualLogSource PluginLogger => Instance.Logger;
        private void Awake()
        {
            Instance = this;
            PluginSettings.Create(Config);
            new WebRequestPatch().Enable();
            new MongoIDPatch().Enable();
        }
    }
}
