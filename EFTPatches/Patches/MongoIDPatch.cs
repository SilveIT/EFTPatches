using EFT;
using SPT.Reflection.Patching;
using System.Reflection;
using System.Text.RegularExpressions;

namespace EFTPatches.Patches
{
    public class MongoIDPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(MongoID).GetConstructor(new[] { typeof(string) });

        [PatchPrefix]
        public static bool PatchPrefix(ref string id)
        {
            if (string.IsNullOrEmpty(id))
                return true; // Let original logic throw

            // If ID is already valid, do nothing
            if (id.Length == 24 && Regex.IsMatch(id, "^[a-fA-F0-9]{24}$"))
                return true;

            // Try to extract a valid 24-character hex ID from beginning of string
            if (id.Length >= 24)
            {
                var candidate = id.Substring(0, 24);
                if (Regex.IsMatch(candidate, "^[a-fA-F0-9]{24}$"))
                {
                    Logger.LogInfo($"Fixed incorrect MongoID: {id} to {candidate}");
                    id = candidate; // Replace input with clean ID
                    return true;
                }
            }

            // Optional: Log invalid ID
            Logger.LogError($"Invalid or malformed MongoID encountered: {id}");
            return true; // Still run constructor, will throw as normal if still invalid
        }
    }
}