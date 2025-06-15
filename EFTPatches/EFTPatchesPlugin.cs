using BepInEx;
using BepInEx.Logging;
using EFTPatches.Patches;
using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0051 // Remove unused private members

namespace EFTPatches
{
    [BepInPlugin("com.silve.eftpatches", "EFTPatches", "1.2")]
    public class EFTPatchesPlugin : BaseUnityPlugin
    {
        public static EFTPatchesPlugin Instance { get; private set; }
        public static ManualLogSource PluginLogger => Instance.Logger;
        public void Awake()
        {
            Instance = this;
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", true);
            PluginSettings.Create(Config);
            new TLSPatch1().Enable();
            new TLSPatch2().Enable();
            new WebRequestPatch().Enable();
            new MongoIDPatch().Enable();
        }
    }
}
