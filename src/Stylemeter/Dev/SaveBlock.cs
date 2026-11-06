using System;
using HarmonyLib;

namespace Stylemeter.Dev
{
    // While the sandbox slot is in play nothing reaches the disk or Steam: the slot file, its version backup and
    // restore points, the shared data of all slots, and achievements. Callbacks still report success, so the game does
    // not show "save failed" or wait forever. Without the bridge none of this does anything.
    [HarmonyPatch]
    internal static class SaveBlock
    {
        private static bool Blocks(int slot) =>
            Bridge.Enabled && (slot == Sandbox.Slot || Sandbox.CurrentProfile == Sandbox.Slot);

        private static bool Blocks() => Bridge.Enabled && Sandbox.CurrentProfile == Sandbox.Slot;

        private static bool Skip(string what, Action<bool> callback)
        {
            Plugin.Out("SANDBOX blocked " + what);
            if (callback != null)
                CoreLoop.InvokeSafe(() => callback(true));
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DesktopPlatform), nameof(DesktopPlatform.WriteSaveSlot))]
        private static bool WriteSaveSlot(int slotIndex, Action<bool> callback) =>
            !Blocks(slotIndex) || Skip($"save slot {slotIndex}", callback);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DesktopPlatform), nameof(DesktopPlatform.ClearSaveSlot))]
        private static bool ClearSaveSlot(int slotIndex, Action<bool> callback) =>
            !Blocks(slotIndex) || Skip($"clear slot {slotIndex}", callback);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Platform), nameof(Platform.CreateSaveRestorePoint))]
        private static bool CreateSaveRestorePoint(int slot, Action<bool> callback) =>
            !Blocks(slot) || Skip($"restore point of slot {slot}", callback);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Platform), nameof(Platform.WriteSaveBackup))]
        private static bool WriteSaveBackup(int slot, Action<bool> callback) =>
            !Blocks(slot) || Skip($"version backup of slot {slot}", callback);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Platform), nameof(Platform.DeleteRestorePointsForSlot))]
        private static bool DeleteRestorePoints(int slot, Action<bool> callback) =>
            !Blocks(slot) || Skip($"restore point delete of slot {slot}", callback);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Platform), nameof(Platform.DeleteVersionBackupsForSlot))]
        private static bool DeleteVersionBackups(int slot, Action<bool> callback) =>
            !Blocks(slot) || Skip($"version backup delete of slot {slot}", callback);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JsonSharedData), nameof(JsonSharedData.Save))]
        private static bool SharedSave() => !Blocks() || Skip("shared data", null);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DesktopPlatform), nameof(DesktopPlatform.PushAchievementUnlock))]
        private static bool PushAchievement(string achievementId) =>
            !Blocks() || Skip("achievement " + achievementId, null);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DesktopPlatform), nameof(DesktopPlatform.UpdateAchievementProgress))]
        private static bool AchievementProgress(string achievementId) =>
            !Blocks() || Skip("achievement progress " + achievementId, null);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AchievementHandler), nameof(AchievementHandler.AwardAchievementToPlayer))]
        private static bool AwardAchievement(string key) => !Blocks() || Skip("achievement award " + key, null);
    }
}
