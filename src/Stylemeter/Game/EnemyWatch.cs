using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using Stylemeter.Core;
using UnityEngine;

namespace Stylemeter.Game
{
    // Enemy states that only show frame by frame: Needolin singers, stuns, shells, falls out of the room, Snitchflies
    // with Hornet's rosaries, and First Sinner's heal.
    internal static class EnemyWatch
    {
        private const float SongAfter = 2f;
        private const float LureAfter = 5f;
        private const float FallY = -2f;
        private const float ThiefRange = 4f;
        private const float ThiefMemory = 10f;
        private const float StunGrace = 0.3f;

        // The game's own list of Needolin reaction states (BlackThreadState).
        private static readonly HashSet<string> SingStates = new HashSet<string>
        {
            "Sing", "Pray", "Needolin", "Praying", "Sing Ground", "Sing Air",
        };

        private static readonly AccessTools.FieldRef<HealthManager, PlayMakerFSM> StunFsm =
            GameField.Find<HealthManager, PlayMakerFSM>("stunControlFsm");

        private sealed class Watched
        {
            public PlayMakerFSM[] Fsms;
            public bool Stunnable;
            public bool Stunned;
            public float StunnedUntil = float.NegativeInfinity;
            public int SongId;
            public float SangAt = float.NegativeInfinity;
            public float HitAt = float.NegativeInfinity;
            public bool Shelled;
            public bool Fell;
            public float ThiefAt = float.NegativeInfinity;
            public int LastHp = -1;
            public HashSet<string> HealStates;
            public HashSet<string> ChannelStates;
            public bool HitInChannel;
        }

        private static readonly Dictionary<HealthManager, Watched> All = new Dictionary<HealthManager, Watched>();
        private static int songId;
        private static bool singing;
        private static float songEndedAt = float.NegativeInfinity;
        private static int lastGeo = -1;

        public static void Tick(float now)
        {
            var song = HeroPerformanceRegion.IsPerforming;
            if (song && !singing)
                songId++;
            if (!song && singing)
                songEndedAt = now;
            singing = song;
            var listening = singing || now - songEndedAt <= SongAfter;
            var robbed = Robbed();
            var hero = HeroController.instance;
            foreach (var hm in HealthManager.EnumerateActiveEnemies())
            {
                if (!hm || hm.GetIsDead())
                    continue;
                var w = Of(hm);
                var info = EnemyRegistry.Get(hm);
                if (listening && IsSinging(w))
                {
                    w.SongId = songId;
                    w.SangAt = now;
                }
                if (w.Stunnable)
                    TickStun(hm, w, info, now);
                if (info.Species != null && Species.Shelled.Contains(info.Species)
                    && w.HitAt > float.NegativeInfinity && hm.IsInvincible)
                    w.Shelled = true;
                if (!w.Fell && hm.transform.position.y < FallY && now - w.HitAt <= Tuning.SpartaWindow)
                {
                    w.Fell = true;
                    EngineHost.EnemyFell(hm);
                }
                if (robbed && hero && info.Species == Species.Snitchfly
                    && Vector2.Distance(hero.transform.position, hm.transform.position) <= ThiefRange)
                    w.ThiefAt = now;
                if (info.BossKey == BossBonuses.FirstSinner)
                    TickSinner(hm, w, info);
            }
        }

        private static Watched Of(HealthManager hm)
        {
            if (All.TryGetValue(hm, out var w))
                return w;
            var stun = StunFsm != null ? StunFsm(hm) : null;
            w = new Watched
            {
                Fsms = hm.GetComponentsInChildren<PlayMakerFSM>(true).Where(f => f != stun).ToArray(),
                Stunnable = stun,
            };
            All[hm] = w;
            return w;
        }

        private static bool IsSinging(Watched w)
        {
            foreach (var fsm in w.Fsms)
            {
                if (fsm && fsm.enabled && SingStates.Contains(fsm.ActiveStateName))
                    return true;
            }
            return false;
        }

        // A stun shows as a state named "Stun..." in the enemy's own FSMs (the stun-control FSM only counts damage).
        private static void TickStun(HealthManager hm, Watched w, EnemyInfo info, float now)
        {
            var stunned = false;
            foreach (var fsm in w.Fsms)
            {
                var state = fsm && fsm.enabled ? fsm.ActiveStateName : null;
                if (state != null && state.StartsWith("Stun"))
                {
                    stunned = true;
                    break;
                }
            }
            if (stunned)
                w.StunnedUntil = now + StunGrace;
            if (stunned && !w.Stunned)
                EngineHost.EnemyStunned(hm);
            w.Stunned = stunned;
        }

        // Hornet's rosaries went down this frame; next to a Snitchfly that counts as theft.
        private static bool Robbed()
        {
            var pd = PlayerData.instance;
            if (pd == null)
                return false;
            var geo = pd.geo;
            var robbed = lastGeo >= 0 && geo < lastGeo;
            lastGeo = geo;
            return robbed;
        }

        // First Sinner: her hp going up is a finished heal; her heal states are the ones holding an AddHP or SetHP
        // action, and the channel is every state that leads into one.
        private static void TickSinner(HealthManager hm, Watched w, EnemyInfo info)
        {
            if (w.HealStates == null)
                ScanHeal(w);
            if (w.LastHp >= 0 && hm.hp > w.LastHp)
                EngineHost.BossHealed(hm);
            w.LastHp = hm.hp;
            var inChannel = InStates(w, w.ChannelStates);
            if (w.HitInChannel && !inChannel)
            {
                w.HitInChannel = false;
                if (!InStates(w, w.HealStates))
                    EngineHost.BindBroken(hm);
            }
        }

        private static void ScanHeal(Watched w)
        {
            w.HealStates = new HashSet<string>();
            w.ChannelStates = new HashSet<string>();
            foreach (var fsm in w.Fsms)
            {
                if (!fsm)
                    continue;
                foreach (var state in fsm.FsmStates)
                {
                    if (state.Actions.Any(a => a is HutongGames.PlayMaker.Actions.AddHP
                        || a is HutongGames.PlayMaker.Actions.SetHP))
                        w.HealStates.Add(state.Name);
                }
                foreach (var state in fsm.FsmStates)
                {
                    if (!w.HealStates.Contains(state.Name)
                        && state.Transitions.Any(t => w.HealStates.Contains(t.ToState)))
                        w.ChannelStates.Add(state.Name);
                }
            }
            if (Plugin.LogStyle.Value)
                Plugin.Out($"SINNER heal={string.Join(",", w.HealStates)} channel={string.Join(",", w.ChannelStates)}");
        }

        private static bool InStates(Watched w, HashSet<string> states)
        {
            if (states == null || states.Count == 0)
                return false;
            foreach (var fsm in w.Fsms)
            {
                if (fsm && fsm.enabled && states.Contains(fsm.ActiveStateName))
                    return true;
            }
            return false;
        }

        // Hornet hit this enemy: windows for the pit, the shell, and a heal she may have broken.
        public static void HitByHornet(HealthManager hm, float now)
        {
            if (!hm)
                return;
            var w = Of(hm);
            w.HitAt = now;
            if (w.ChannelStates != null && InStates(w, w.ChannelStates))
                w.HitInChannel = true;
        }

        // The flags of HitEvent that come from here, read before the hit lands.
        public static void Read(HealthManager hm, float now, ref HitEvent hit)
        {
            if (!hm)
                return;
            var w = Of(hm);
            var species = hit.Enemy?.Species;
            hit.Stunned = w.Stunned || now <= w.StunnedUntil;
            hit.SongId = now - w.SangAt <= SongAfter ? w.SongId : 0;
            hit.Lured = species == Species.Gromling && (singing || now - songEndedAt <= LureAfter);
            hit.Exposed = species != null && Species.Shelled.Contains(species) && !w.Shelled;
            hit.Thief = species == Species.Snitchfly && now - w.ThiefAt <= ThiefMemory;
        }

        // A Snitchfly that hit Hornet is a thief too (her rosaries may fall where it can take them).
        public static void HurtBy(HealthManager hm, float now)
        {
            if (hm && EnemyRegistry.Get(hm).Species == Species.Snitchfly)
                Of(hm).ThiefAt = now;
        }

        public static void Clear()
        {
            All.Clear();
            lastGeo = -1;
        }
    }
}
