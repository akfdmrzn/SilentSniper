using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public enum ObjectiveKind
    {
        Eliminate,      // kill
        Neutralize,     // stop: kill, put down, sedate or disarm (arm hit)
        Wound,          // hit a limb, he must live
        Sedate,         // tranquilizer dart, he must live
        Protect,        // keep alive (until the script or his arrival completes it)
        Disable,        // shoot a prop out of order (engine, generator, lights)
        Custom          // scripted condition
    }

    public enum ObjectiveState { Active, Done, Failed }

    /// <summary>One line of the contract (HUD objective list, briefing). Created by the mission script at setup.</summary>
    public class Objective
    {
        public ObjectiveKind kind;
        public string text;
        public NPC npc;
        public BodyZone zone = BodyZone.Leg;    // Wound: which limb (Arm or Leg)
        public bool anyLimb;                    // Wound: arm or leg will do
        public Shootable prop;
        public Func<string> liveText;           // dynamic line text ("Tetikçileri durdur 1/2"); null = text
        public Func<bool> doneWhen;             // Custom / Protect: completes when true
        public Func<bool> failWhen;             // Custom: fails when true
        public string failText;
        public bool optional;                   // bonus line: never fails the contract, never blocks completion
        public bool hidden;                     // revealed later by the script (Reveal)
        public bool walksToSafety;              // Protect with its own completion: still lost when he can no longer walk
        public ObjectiveState State;
        public float changedAt;

        public bool Active => State == ObjectiveState.Active && !hidden;
        public string Text => liveText != null ? liveText() : text;

        public string Icon
        {
            get
            {
                switch (kind)
                {
                    case ObjectiveKind.Wound: return "hand";
                    case ObjectiveKind.Sedate: return "breath";
                    case ObjectiveKind.Protect: return "shield";
                    case ObjectiveKind.Disable: return "crosshair";
                    case ObjectiveKind.Neutralize: return "guard";
                    case ObjectiveKind.Custom: return "flag";
                    default: return "target";
                }
            }
        }
    }

    public enum ChallengeKind { Complete, NoCivilians, AllHeadshots, NoAlarm, TimeUnder, MaxShots, LongShot, NoGuards, NoKills, OnlyTargets, Custom, NoDamage, Unseen }

    /// <summary>A star challenge of a contract (three per contract; each counts once, over all runs).</summary>
    public class ChallengeDef
    {
        public ChallengeKind kind;
        public string text;
        public float value;
        public Func<MissionRuntime, bool> check;

        public ChallengeDef(ChallengeKind k, string t, float v = 0f, Func<MissionRuntime, bool> c = null) { kind = k; text = t; value = v; check = c; }

        public bool Evaluate(MissionRuntime rt)
        {
            if (rt == null || !rt.Success) return false;
            switch (kind)
            {
                case ChallengeKind.Complete: return true;
                case ChallengeKind.NoCivilians: return rt.CiviliansKilled == 0 && rt.CiviliansHurt == 0;
                case ChallengeKind.AllHeadshots: return rt.TargetsKilled > 0 && rt.AllTargetHeadshots;
                case ChallengeKind.NoAlarm: return !rt.AlarmEver;
                case ChallengeKind.TimeUnder: return rt.Elapsed <= value;
                case ChallengeKind.MaxShots: return rt.Shots <= Mathf.RoundToInt(value);
                case ChallengeKind.LongShot: return rt.LongestKill >= value;
                case ChallengeKind.NoGuards: return rt.GuardsKilled == 0;
                case ChallengeKind.NoKills: return rt.Kills == 0;
                case ChallengeKind.OnlyTargets: return rt.Kills == rt.TargetsKilled && rt.CiviliansHurt == 0;
                case ChallengeKind.NoDamage: return !rt.Flag("hit");
                case ChallengeKind.Unseen: return !rt.Flag("located");
                default: return check != null && check(rt);
            }
        }

        /// <summary>Live state while playing: false once it can no longer be met (HUD shows it crossed out).</summary>
        public bool StillPossible(MissionRuntime rt)
        {
            if (rt == null) return true;
            switch (kind)
            {
                case ChallengeKind.NoCivilians: return rt.CiviliansKilled == 0 && rt.CiviliansHurt == 0;
                case ChallengeKind.AllHeadshots: return rt.AllTargetHeadshots;
                case ChallengeKind.NoAlarm: return !rt.AlarmEver;
                case ChallengeKind.TimeUnder: return rt.Elapsed <= value;
                case ChallengeKind.MaxShots: return rt.Shots <= Mathf.RoundToInt(value);
                case ChallengeKind.NoGuards: return rt.GuardsKilled == 0;
                case ChallengeKind.NoKills: return rt.Kills == 0;
                case ChallengeKind.OnlyTargets: return rt.Kills == rt.TargetsKilled && rt.CiviliansHurt == 0;
                case ChallengeKind.NoDamage: return !rt.Flag("hit");
                case ChallengeKind.Unseen: return !rt.Flag("located");
                default: return true;
            }
        }

        public static List<ChallengeDef> Defaults() => new List<ChallengeDef>
        {
            new ChallengeDef(ChallengeKind.Complete, "Sözleşmeyi tamamla"),
            new ChallengeDef(ChallengeKind.NoCivilians, "Sivillere zarar verme"),
            new ChallengeDef(ChallengeKind.AllHeadshots, "Hedefi kafadan vur"),
        };
    }
}
