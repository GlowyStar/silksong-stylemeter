using HarmonyLib;
using UnityEngine;

namespace Stylemeter.Game
{
    // Short text descriptions of game objects for the development logs.
    internal static class Describe
    {
        public static string PathOf(GameObject go)
        {
            if (!go)
                return "-";
            var path = go.name;
            for (var t = go.transform.parent; t; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }

        public static string JournalName(HealthManager hm)
        {
            var effects = hm ? hm.GetComponent<EnemyDeathEffects>() : null;
            if (!effects)
                return "-";
            var record = Traverse.Create(effects).Field("journalRecord").GetValue<EnemyJournalRecord>();
            return record ? record.name : "-";
        }

        public static string Enemy(HealthManager hm)
        {
            var t = Traverse.Create(hm);
            return $"{hm.name} journal={JournalName(hm)} hp={hm.hp}/{t.Field("initHp").GetValue<int>()} "
                + $"type={hm.EnemyType} size={t.Field("enemySize").GetValue()} "
                + $"pos=({hm.transform.position.x:0.0},{hm.transform.position.y:0.0})";
        }
    }
}
