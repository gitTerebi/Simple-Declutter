using SPT.Reflection.Patching;
using EFT;
using System.Reflection;
using HarmonyLib;


namespace SimpleDeclutter
{
    // DontSpawnShellsFiringPatch removes the spawning of spent shell casings
    // when firing a gun. Very cool, but it has an expensive update cycle
    // in GameWorld to clean them up.
    class OnAfterGameStarted : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.AfterGameStarted));

        }

        [PatchPostfix]
        public static bool Postfix()
        {
            Plugin.LogSource.LogInfo($"The game has started!");

            return false;
        }
    }
}