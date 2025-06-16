using System.Collections.Generic;
using SPT.Reflection.Patching;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
// ReSharper disable InconsistentNaming

namespace EFTPatches.Patches
{
    //System.Boolean Mono.Unity.UnityTlsContext::ProcessHandshake()
    public class BypassProcessHandshake : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method("Mono.Unity.UnityTlsContext:ProcessHandshake");

        [PatchTranspiler]
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var index = 0;
            foreach (var instruction in instructions)
            {
                if (index >= 21 && index <= 50) // Remove throws, calls to CheckAndThrow and ValidateCertificate
                    yield return new CodeInstruction(OpCodes.Nop) { labels = instruction.labels };
                else
                    yield return instruction;
                index++;
            }
        }
    }
}