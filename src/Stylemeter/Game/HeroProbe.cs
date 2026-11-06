using System.Collections.Generic;
using Stylemeter.Core;
using UnityEngine;
using Gameplay = GlobalSettings.Gameplay;

namespace Stylemeter.Game
{
    // Reads what the engine needs about Hornet straight from the game.
    internal static class HeroProbe
    {
        public static bool TryRead(out HeroState state)
        {
            state = default;
            var hero = HeroController.instance;
            if (!hero)
                return false;
            var pd = hero.playerData ?? PlayerData.instance;
            if (pd == null)
                return false;
            var cs = hero.cState;
            var body = hero.Body;
            var crest = pd.CurrentCrestID;
            state = new HeroState
            {
                OnGround = cs.onGround,
                OnWall = cs.wallClinging || cs.wallSliding,
                VelocityX = body ? body.linearVelocity.x : 0f,
                VelocityY = body ? body.linearVelocity.y : 0f,
                Health = pd.health,
                MaxHealth = pd.maxHealth,
                Silk = pd.silk,
                SilkMax = pd.CurrentSilkMax,
                SkillCost = pd.SilkSkillCost,
                Dead = cs.dead,
                Crest = crest,
                HunterStage = HunterStage(hero, crest),
                InRage = hero.WarriorState.IsInRageMode,
                InReaperMode = hero.ReaperState.IsInReaperMode,
                BarbedBracelet = IsEquipped(Gameplay.BarbedWireTool),
            };
            return true;
        }

        // The meter does not drain in menus, cutscenes, dialogues, scene transitions or at a bench (MECHANICS.md §2).
        public static bool DrainActive()
        {
            var hero = HeroController.instance;
            var gm = GameManager.instance;
            if (!hero || !gm || gm.isPaused || Time.timeScale <= 0f)
                return false;
            var cs = hero.cState;
            var atBench = hero.playerData != null && hero.playerData.atBench;
            return !cs.transitioning && !cs.dead && !hero.controlReqlinquished && !atBench;
        }

        // Thread sources Hornet has: four needle sources, each equipped attack tool and skill, Clawline once learned.
        public static int SourceCount()
        {
            var count = 4;
            foreach (var tool in ToolItemManager.GetAllTools())
            {
                if (tool && tool.IsEquipped && (tool.Type == ToolItemType.Red || tool.Type == ToolItemType.Skill))
                    count++;
            }
            var pd = PlayerData.instance;
            if (pd != null && pd.hasHarpoonDash)
                count++;
            return count;
        }

        // Internal names of the equipped attack (red) tools, for TOOLBOX.
        public static string[] AttackTools()
        {
            var names = new List<string>();
            foreach (var tool in ToolItemManager.GetAllTools())
            {
                if (tool && tool.IsEquipped && tool.Type == ToolItemType.Red)
                    names.Add(tool.name);
            }
            return names.ToArray();
        }

        // Hunter focus (DamageEnemies): the upgraded crest has one stage, the second upgrade two.
        private static int HunterStage(HeroController hero, string crest)
        {
            var focus = hero.HunterUpgState;
            if (crest == Crests.HunterV2)
                return focus.CurrentMeterHits >= Gameplay.HunterComboHits ? 1 : 0;
            if (crest != Crests.HunterV3)
                return 0;
            if (focus.IsComboMeterAboveExtra)
                return 2;
            return focus.CurrentMeterHits >= Gameplay.HunterCombo2Hits ? 1 : 0;
        }

        private static bool IsEquipped(ToolItem tool) => tool && tool.IsEquipped;
    }
}
