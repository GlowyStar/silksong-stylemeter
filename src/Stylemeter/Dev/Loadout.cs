using System.Linq;
using System.Text;

namespace Stylemeter.Dev
{
    // Hornet's crest, tools and skills, set from code the way the inventory does it. Unlocking writes the save data
    // directly: the game's own Unlock awards achievements.
    internal static class Loadout
    {
        public static string Show()
        {
            var crestId = PlayerData.instance.CurrentCrestID;
            var crest = ToolItemManager.GetCrestByName(crestId);
            if (!crest)
                return "no crest " + crestId;
            var equipped = ToolItemManager.GetEquippedToolsForCrest(crestId);
            var text = new StringBuilder($"crest={crestId} slots={crest.Slots.Length}");
            for (var i = 0; i < crest.Slots.Length; i++)
            {
                var slot = crest.Slots[i];
                var tool = equipped != null && i < equipped.Count ? equipped[i] : null;
                text.Append($"\n#{i} {slot.Type}/{slot.AttackBinding}{(slot.IsLocked ? " locked" : "")} = "
                    + $"{(tool ? tool.name : "-")}");
            }
            // Vesticrest slots sit outside the crest and stay with every crest.
            var extra = PlayerData.instance.ExtraToolEquips;
            foreach (var name in extra.GetValidNames())
            {
                var extraTool = extra.GetData(name).EquippedTool;
                if (!string.IsNullOrEmpty(extraTool))
                    text.Append($"\nextra {name} = {extraTool}");
            }
            return text.ToString();
        }

        // equip Straight_Pin [slot]: into that slot index, or wherever the game's AutoEquip puts it.
        public static string Equip(string name, int slot = -1)
        {
            var tool = ToolItemManager.GetToolByName(name.Replace('_', ' '));
            if (!tool)
                return "no tool " + name;
            if (!tool.SavedData.IsUnlocked)
            {
                var data = tool.SavedData;
                data.IsUnlocked = true;
                tool.SavedData = data;
            }
            var crestId = PlayerData.instance.CurrentCrestID;
            if (slot < 0)
            {
                ToolItemManager.AutoEquip(tool);
            }
            else
            {
                var names = (ToolItemManager.GetEquippedToolsForCrest(crestId)
                    ?? new System.Collections.Generic.List<ToolItem>())
                    .Select(t => t ? t.name : null).ToList();
                var crest = ToolItemManager.GetCrestByName(crestId);
                while (names.Count < crest.Slots.Length)
                    names.Add(null);
                for (var i = 0; i < names.Count; i++)
                {
                    if (names[i] == tool.name)
                        names[i] = null;
                }
                names[slot] = tool.name;
                ToolItemManager.SetEquippedTools(crestId, names);
            }
            ToolItemManager.SendEquippedChangedEvent(true);
            return Show();
        }

        public static string Crest(string id)
        {
            var crest = ToolItemManager.GetCrestByName(id);
            if (!crest)
                return "no crest " + id;
            var data = crest.SaveData;
            if (!data.IsUnlocked)
            {
                data.IsUnlocked = true;
                crest.SaveData = data;
            }
            ToolItemManager.SetEquippedCrest(id);
            ToolItemManager.SendEquippedChangedEvent(true);
            return Show();
        }
    }
}
