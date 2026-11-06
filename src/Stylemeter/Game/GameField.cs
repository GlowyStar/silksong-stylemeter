using HarmonyLib;

namespace Stylemeter.Game
{
    // A private game field read through a cached accessor. When a game update renames or retypes the field the
    // accessor is null, so the check that reads it fails inside its HookGuard and only that bonus is lost.
    internal static class GameField
    {
        public static AccessTools.FieldRef<TOwner, TValue> Find<TOwner, TValue>(string name)
        {
            var field = AccessTools.Field(typeof(TOwner), name);
            if (field != null && field.FieldType == typeof(TValue))
                return AccessTools.FieldRefAccess<TOwner, TValue>(field);
            Plugin.Log.LogWarning($"Stylemeter: {typeof(TOwner).Name}.{name} is not in this game version, "
                + "the bonus reading it is off");
            return null;
        }
    }
}
