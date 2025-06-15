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
                if (index >= 24 && index <= 26) // Remove throw this.lastException
                    yield return new CodeInstruction(OpCodes.Nop);
                else if (index == 50 || index == 36) // Remove two calls of Debug.CheckAndThrow
                    yield return new CodeInstruction(OpCodes.Nop);
                if (index >= 37 && index <= 39) // Remove args to ValidateCertificate call
                    yield return new CodeInstruction(OpCodes.Nop);
                else if (index == 40) // Remove ValidateCertificate call
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                else
                    yield return instruction;
                index++;
            }
        }
    }
}