using System;
using System.Linq;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace EFTPatches.Patches
{
    /// <summary>
    /// Base class for TLS check patches
    /// </summary>
    public abstract class TLSPatchBase : ModulePatch
    {
        private const string TargetTypeName = "Mono.Unity.Debug";
        private const string MethodName = "CheckAndThrow";

        protected abstract int ExpectedParameterCount { get; }

        protected override MethodBase GetTargetMethod()
        {
            // Step 1: Find the System.dll assembly
            var systemAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "System");

            if (systemAssembly == null)
            {
                Debug.LogError("Failed to find 'System' assembly.");
                return null;
            }

            // Step 2: Find the Mono.Unity.Debug type
            var debugType = systemAssembly.GetType(TargetTypeName);
            if (debugType == null)
            {
                Debug.LogError($"Failed to find '{TargetTypeName}' type.");
                return null;
            }

            // Step 3: Get all methods named CheckAndThrow
            var methods = debugType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(m => m.Name == MethodName)
                .ToArray();

            if (methods.Length == 0)
            {
                Debug.LogError($"No methods found matching '{MethodName}' in '{TargetTypeName}'.");
                return null;
            }

            // Step 4: Filter by expected parameter count
            foreach (var method in methods)
            {
                var paramCount = method.GetParameters().Length;
                if (paramCount == ExpectedParameterCount)
                {
                    return method;
                }
            }

            Debug.LogError($"Could not find overload of '{MethodName}' with {ExpectedParameterCount} parameters.");
            return null;
        }
    }

    /// <summary>
    /// Patch for the 3-parameter overload of CheckAndThrow
    /// </summary>
    public class TLSPatch1 : TLSPatchBase
    {
        protected override int ExpectedParameterCount => 3;
        [PatchPrefix]
        public static bool Prefix()
        {
            // Skip original logic - effectively disable SSL checks
            return false;
        }
    }

    /// <summary>
    /// Patch for the 4-parameter overload of CheckAndThrow
    /// </summary>
    public class TLSPatch2 : TLSPatchBase
    {
        protected override int ExpectedParameterCount => 4;
        [PatchPrefix]
        public static bool Prefix()
        {
            // Skip original logic - effectively disable SSL checks
            return false;
        }
    }
}