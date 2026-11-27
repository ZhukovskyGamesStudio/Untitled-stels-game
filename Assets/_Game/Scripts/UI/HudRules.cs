using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.UI
{
    /// <summary>Everything the reticle and the hint stack depend on, gathered by the HUD each frame.</summary>
    public struct PromptContext
    {
        public bool Downed, MenuOpen, CursorMode, Cracking;
        /// <summary>Caught in a drone's net: E tears out of it.</summary>
        public bool Netted;
        public int Hp;
        public bool CanGetUp;
        /// <summary>Knocked out and looking through a teammate's eyes.</summary>
        public bool Watching;
        public bool CanRevive;
        public float ReviveProgress;
        /// <summary>A robot / device in reach to hack.</summary>
        public bool CanHack;
        /// <summary>The hack target is a robot and no keyboard is in the hand: E only searches it (what it carries falls off).</summary>
        public bool SearchOnly;
        /// <summary>The upgraded screwdriver: E at the device in reach takes it apart (not a hack).</summary>
        public bool Unscrew;
        /// <summary>The robot or sensor looked at is hacked only with the keyboard in the hand, and it is not there.</summary>
        public bool NeedsKeyboard;
        public float HackProgress;
        public bool Playing, NearHatch;
        /// <summary>The hatch in reach is the exit (the way down), not the entry.</summary>
        public bool NearExit;
        /// <summary>At a stash (a hatch of the house) or at the car.</summary>
        public bool NearTrade;
        public bool InHatch;
        /// <summary>A locked door in reach and how far its hack is (0..1).</summary>
        public bool LockedDoor;
        /// <summary>Its lock is the robots' electronic unit (hacked, not picked).</summary>
        public bool LockedByUnit;
        public float DoorProgress;
        /// <summary>A closed safe in reach.</summary>
        public bool SafeNear;
        /// <summary>A drawer / locker door looked at: what pressing E does with it (null if none).</summary>
        public string Storage;
        /// <summary>A terminal in reach (and whether it can do anything).</summary>
        public bool NearTerminal, TerminalUsable;
        /// <summary>Sitting at a terminal: which kind, and whether E hacks the robot on its screen now.</summary>
        public bool AtTerminal;
        public Level.TerminalKind SittingKind;
        public bool SittingCanHack;
        public int CarriedItems;
        /// <summary>Something to use with E (crate, med table, workbench, key lock, lever, car): its kind and short label.</summary>
        public bool HasUse;
        public Level.UseKind UseKind;
        public string UseText;
        public bool UseAvailable;
        public float UseProgress;
        /// <summary>Sitting in the car: at the wheel / as a passenger.</summary>
        public bool Driving, Passenger;
        public bool HasStun;
        /// <summary>What the hand key does now (the item in the hand slot), null if nothing.</summary>
        public string HandUse;
        /// <summary>A world item under the crosshair (it can be taken).</summary>
        public bool HoverItem;
        public bool BigMap;
    }

    /// <summary>The glyph in the middle of the screen: a dot, or what the thing in front of you is for.</summary>
    /// <summary>Small rules of the HUD (pure).</summary>
    public static class HudRules
    {
        /// <summary>The colour of a danger zone on the HUD and the map: green, yellow, red.</summary>
        public static Color ZoneColor(Level.DangerZone zone) => zone switch
        {
            Level.DangerZone.Red => UiPalette.Stamp,
            Level.DangerZone.Yellow => UiPalette.MarkYellow,
            _ => UiPalette.MarkGreen,
        };
    }

    /// <summary>What a knocked-out player reads on the screen (pure): where they are held.</summary>
    public static class DownedRules
    {
        public static string Key(Player.CaptiveKind kind) => kind switch
        {
            Player.CaptiveKind.Carried => "hud.carried",
            Player.CaptiveKind.Prison => "hud.prison",
            Player.CaptiveKind.Cocoon => "hud.cocoon",
            Player.CaptiveKind.Ward => "hud.ward",
            Player.CaptiveKind.Lair => "hud.lair",
            _ => "hud.out_cold",
        };
    }

    public enum ReticleIcon { Dot, Hand, Lock, Key, Hack, Revive, Heal, Search, Seat, Fuel, Terminal, Craft, Lever, Safe, Crate, Door, Vent }

    /// <summary>The reticle this frame: hidden (hands out, lying, in a menu), its glyph, dimmed when it can't be used now, the hold progress.</summary>
    public readonly struct Reticle
    {
        public readonly bool Visible;
        public readonly ReticleIcon Icon;
        public readonly bool Available;
        /// <summary>0..1 while holding E, negative — no ring.</summary>
        public readonly float Progress;

        public Reticle(ReticleIcon icon, bool available = true, float progress = -1f)
        {
            Visible = true;
            Icon = icon;
            Available = available;
            Progress = progress;
        }

        public static readonly Reticle Hidden = default;
    }

    /// <summary>One line of the hint stack (bottom right): one or two keys drawn as key caps and a short word.</summary>
    public readonly struct Hint
    {
        public readonly KeyBinding Key, Key2;
        public readonly string Text;
        /// <summary>Can't be done now (no key for the door): the line is dimmer.</summary>
        public readonly bool Dim;

        public Hint(KeyBinding key, string text, bool dim = false) : this(key, default, text, dim) { }

        public Hint(KeyBinding key, KeyBinding key2, string text, bool dim = false)
        {
            Key = key;
            Key2 = key2;
            Text = text;
            Dim = dim;
        }

        public override string ToString() => (Key.IsNone ? "" : $"[{Key}]") + (Key2.IsNone ? "" : $"[{Key2}]") + " " + Text;
    }

    /// <summary>
    /// What the reticle shows (pure, unit tested). The thing in front of you that E would act on, by priority:
    /// revive · hack · locked door · safe · something to use · terminal · an item to take; otherwise a dot.
    /// No text: the glyph says what it is, the hint stack names the key.
    /// </summary>
    /// <summary>Where the house's main job stands (<see cref="Game.GameManager.ObjectiveStage"/>).</summary>
    public struct ObjectiveState
    {
        public Level.ObjectiveKind Kind;
        /// <summary>The vault seen / the hostage spoken to.</summary>
        public bool Found;
        public int Solved, Locks;
        /// <summary>Somebody carries the cage's key.</summary>
        public bool KeyHeld;
        public bool Done;
    }

    /// <summary>When the screen stays black while a location is set up (pure, unit tested).</summary>
    public static class LoadingRules
    {
        /// <summary>Seconds after which the screen opens even if something never arrived (a lost message must not leave it black).</summary>
        public const float GiveUp = 10f;

        /// <summary>
        /// Black until this peer has its player, the player stands where the location put them (no jump from the network's
        /// spawn point to the real one) and, in the sewer, the car is there; <paramref name="waited"/> — seconds it has been black.
        /// </summary>
        public static bool StillLoading(bool hasPlayer, bool placed, bool inSewer, bool carHere, float waited) =>
            waited < GiveUp && (!hasPlayer || !placed || (inSewer && !carHere));
    }

    public static class ObjectiveRules
    {
        /// <summary>
        /// The job's line, one step at a time (pure). Vault: find it — unlock its door (k/3) — open its door — done.
        /// Hostage: find the hostage — take the key from the guard it points out — free the hostage; a key taken before
        /// the hostage is found keeps "find the hostage" until it is.
        /// </summary>
        public static (string text, bool done)? Step(in ObjectiveState s)
        {
            switch (s.Kind)
            {
                case Level.ObjectiveKind.Vault:
                    if (s.Done) return (Loc.T("hud.job.vault_done"), true);
                    if (!s.Found) return (Loc.T("hud.job.vault_find"), false);
                    if (s.Solved < s.Locks) return (Loc.F("hud.job.vault_unlock", s.Solved, s.Locks), false);
                    return (Loc.T("hud.job.vault_open"), false);
                case Level.ObjectiveKind.Hostage:
                    if (s.Done) return (Loc.T("hud.job.hostage_done"), true);
                    if (!s.Found) return (Loc.T("hud.job.hostage_find"), false);
                    if (!s.KeyHeld) return (Loc.T("hud.job.hostage_key"), false);
                    return (Loc.T("hud.job.hostage_free"), false);
            }
            return null;
        }

        /// <summary>The name of a main job (the tunnel's panel, the HUD), or null for none.</summary>
        public static string Name(Level.ObjectiveKind kind) => kind switch
        {
            Level.ObjectiveKind.Vault => Loc.T("objective.vault"),
            Level.ObjectiveKind.Hostage => Loc.T("objective.hostage"),
            _ => null,
        };

        /// <summary>
        /// The task list in the top left corner, in order (pure): in a house — find the exit first, then the house's main
        /// job; in the sewer — open the way for the car, fuel it, then choose a tunnel; in the run's first sewer (no gate, the
        /// first drive free: <paramref name="runStart"/>) only choose a tunnel. Each line with whether it is done.
        /// </summary>
        public static List<(string text, bool done)> Tasks(bool sewer, bool exitSeen, in ObjectiveState job, bool gateOpen, bool fueled, bool runStart = false)
        {
            var list = new List<(string, bool)>();
            if (sewer && runStart)
            {
                list.Add((Loc.T("hud.task.tunnel"), false));
                return list;
            }
            if (sewer)
            {
                list.Add((Loc.T("hud.task.gate"), gateOpen));
                list.Add((Loc.T("hud.task.fuel"), fueled));
                if (gateOpen && fueled) list.Add((Loc.T("hud.task.tunnel"), false));
                return list;
            }
            list.Add((Loc.T("hud.task.exit"), exitSeen));
            if (Step(job) is { } step) list.Add((Loc.F("hud.objective", step.text), step.done));
            return list;
        }

    }

    /// <summary>The centre of the screen: what E does with the thing under the crosshair (pure).</summary>
    public static class ReticleRules
    {
        public static Reticle Choose(in PromptContext c)
        {
            if (c.MenuOpen || c.CursorMode || c.Downed || c.Cracking || c.AtTerminal || c.InHatch) return Reticle.Hidden;
            if (c.Driving || c.Passenger) return new Reticle(ReticleIcon.Dot);
            if (c.CanRevive) return new Reticle(ReticleIcon.Revive, true, c.ReviveProgress);
            if (c.CanHack && c.NeedsKeyboard && !c.Unscrew) return new Reticle(ReticleIcon.Hack, false); // dim: not with these hands
            if (c.CanHack) return new Reticle(c.Unscrew ? ReticleIcon.Vent : c.SearchOnly ? ReticleIcon.Search : ReticleIcon.Hack, true, c.HackProgress);
            if (c.LockedDoor) return new Reticle(ReticleIcon.Lock, true, c.DoorProgress);
            if (c.SafeNear) return new Reticle(ReticleIcon.Safe);
            if (c.Storage != null) return new Reticle(ReticleIcon.Hand);
            if (c.HasUse) return new Reticle(IconOf(c.UseKind), c.UseAvailable, c.UseAvailable && c.UseProgress > 0f ? c.UseProgress : -1f);
            if (c.NearTerminal) return new Reticle(ReticleIcon.Terminal, c.TerminalUsable);
            if (c.HoverItem) return new Reticle(ReticleIcon.Hand);
            return new Reticle(ReticleIcon.Dot);
        }

        public static ReticleIcon IconOf(Level.UseKind kind) => kind switch
        {
            Level.UseKind.Crate or Level.UseKind.Mimic => ReticleIcon.Crate, // (a mimic passes for a crate)
            Level.UseKind.MedTable => ReticleIcon.Heal,
            Level.UseKind.Workbench => ReticleIcon.Craft,
            Level.UseKind.CarBench => ReticleIcon.Craft,
            Level.UseKind.KeyDoor => ReticleIcon.Key,
            Level.UseKind.GateLever => ReticleIcon.Lever,
            Level.UseKind.CarSeat => ReticleIcon.Seat,
            Level.UseKind.CarPanic => ReticleIcon.Hand,
            Level.UseKind.CarHopper => ReticleIcon.Fuel,
            Level.UseKind.CarCargo => ReticleIcon.Hand,
            Level.UseKind.VaultConsole => ReticleIcon.Terminal,
            Level.UseKind.Cage => ReticleIcon.Key,
            Level.UseKind.HandDryer => ReticleIcon.Hand,
            Level.UseKind.LightSwitch => ReticleIcon.Hand,
            Level.UseKind.ExitButton => ReticleIcon.Hand,
            Level.UseKind.PetBot => ReticleIcon.Hand,
            Level.UseKind.LiftCall or Level.UseKind.LiftFloor => ReticleIcon.Hand,
            Level.UseKind.VentGrille => ReticleIcon.Vent,
            Level.UseKind.VaultDoor => ReticleIcon.Lever,
            Level.UseKind.VentGrate => ReticleIcon.Vent,
            Level.UseKind.ShaftHatch => ReticleIcon.Lever,
            Level.UseKind.DoorLock => ReticleIcon.Lock,
            Level.UseKind.Door => ReticleIcon.Door,
            Level.UseKind.Safe => ReticleIcon.Safe,
            Level.UseKind.StoragePart => ReticleIcon.Search,
            _ => ReticleIcon.Dot,
        };
    }

    /// <summary>
    /// The hint stack in the bottom right corner (pure, unit tested): only the keys that are specific to this game and to
    /// this moment, one short word each — never the obvious ones (WASD to drive, the mouse to look). The last line is the
    /// most important (it sits at the bottom, next to the corner).
    /// </summary>
    public static class HintRules
    {
        static KeyBinding K(GameAction a) => Controls.Get(a);
        static readonly KeyBinding Lmb = KeyBinding.MouseButton(0), Rmb = KeyBinding.MouseButton(1), Wheel = new(KeyCapView.WheelCode);

        public static List<Hint> Collect(in PromptContext c, List<Hint> into)
        {
            into.Clear();
            if (c.MenuOpen) return into;
            if (c.Downed)
            {
                if (c.Watching) into.Add(new Hint(Lmb, Rmb, Loc.T("hint.watch_next")));
                if (c.Watching) into.Add(new Hint(K(GameAction.CallHelp), Loc.T("hint.call_help")));
                else if (c.Hp > 0 && c.CanGetUp) into.Add(new Hint(K(GameAction.Jump), Loc.T("hint.get_up")));
                return into;
            }
            if (c.Netted) into.Add(new Hint(K(GameAction.Interact), Loc.T("hint.struggle")));
            if (c.Cracking) return into; // the safe's own screen
            if (c.AtTerminal)
            {
                bool cams = c.SittingKind == Level.TerminalKind.Cameras;
                into.Add(new Hint(K(GameAction.Interact), Loc.T("hint.leave"))); // (a tap; Esc too)
                if (c.SittingKind == Level.TerminalKind.Map)
                {
                    into.Add(new Hint(Wheel, Loc.T("hint.map.floor"))); // (A / D and the mouse buttons too)
                    return into;
                }
                into.Add(new Hint(Lmb, Rmb, Loc.T(cams ? "hint.terminal.next_camera" : "hint.terminal.next_target")));
                if (cams) into.Add(new Hint(K(GameAction.Mark), Loc.T("hint.mark")));
                else if (c.SittingCanHack) into.Add(new Hint(K(GameAction.Interact), Loc.T("hint.hack_hold")));
                return into;
            }
            if (c.Driving || c.Passenger)
            {
                if (c.Driving) into.Add(new Hint(K(GameAction.Horn), Loc.T("hint.horn")));
                into.Add(new Hint(K(GameAction.Interact), Loc.T("hint.car.exit")));
                return into;
            }
            if (c.BigMap) into.Add(new Hint(Wheel, Loc.T("hint.map.floor")));
            if (c.CursorMode)
            {
                into.Add(new Hint(K(GameAction.Rotate), Loc.T("hint.rotate")));
                into.Add(new Hint(K(GameAction.Mirror), Loc.T("hint.mirror")));
            }
            if (c.HasStun && !c.CursorMode) into.Add(new Hint(K(GameAction.Gadget), Loc.T("hint.stun")));
            if (c.HandUse != null && !c.CursorMode) into.Add(new Hint(K(GameAction.UseHand), c.HandUse));
            if (c.InHatch)
            {
                into.Add(new Hint(K(GameAction.Hands), Loc.T("hint.stash")));
                if (c.CarriedItems > 0) into.Add(new Hint(K(GameAction.StashAll), Loc.T("hint.stash_all")));
                into.Add(new Hint(K(GameAction.Hatch), Loc.T("hint.hatch.out")));
                return into;
            }
            // at Linus with things on us: G puts everything in his backpack — with the cursor out too (the board has no button for it)
            if (c.NearTrade && c.CarriedItems > 0)
            {
                if (!c.CursorMode) into.Add(new Hint(K(GameAction.Hands), Loc.T("hint.stash")));
                into.Add(new Hint(K(GameAction.StashAll), Loc.T("hint.stash_all")));
            }
            if (c.Playing && c.NearHatch) into.Add(new Hint(K(GameAction.Hatch), Loc.T("hint.hatch.in")));
            var target = Target(c);
            if (target.Text != null) into.Add(target);
            return into;
        }

        /// <summary>The key line of what the reticle points at (the same priority as <see cref="ReticleRules"/>).</summary>
        static Hint Target(in PromptContext c)
        {
            var e = K(GameAction.Interact);
            if (c.CursorMode) return default;
            if (c.CanRevive) return new Hint(e, Loc.T("hint.revive"));
            if (c.CanHack && c.NeedsKeyboard && !c.Unscrew) return new Hint(e, Loc.T("hint.need_keyboard"), true);
            if (c.CanHack) return new Hint(e, Loc.T(c.Unscrew ? "hint.take_apart" : c.SearchOnly ? "hint.search" : "hint.hack"));
            if (c.LockedDoor) return new Hint(e, Loc.T(c.LockedByUnit ? "hint.hack" : "hint.pick_lock"));
            if (c.SafeNear) return new Hint(e, Loc.T("hint.safe"));
            if (c.Storage != null) return new Hint(e, c.Storage);
            if (c.HasUse && !string.IsNullOrEmpty(c.UseText)) return new Hint(e, c.UseText, !c.UseAvailable);
            if (c.NearTerminal) return new Hint(e, Loc.T("hint.terminal"), !c.TerminalUsable);
            if (c.HoverItem) return new Hint(Lmb, Loc.T("hint.take"));
            return default;
        }
    }

    /// <summary>
    /// The team's hack slots in the HUD, left to right: the free ones first (a blue bolt each), then what is switched off,
    /// oldest first (a grey picture of its kind) — the leftmost is always the next to go: a free slot is taken before
    /// anything wakes, then the oldest hack wakes first.
    /// </summary>
    public static class HackSlotRules
    {
        /// <summary>The kind shown in each of <paramref name="max"/> slots (-1 = free), from what is hacked oldest first.</summary>
        public static int[] Layout(int max, System.Collections.Generic.IReadOnlyList<int> hackedOldestFirst)
        {
            max = Mathf.Max(0, max);
            var slots = new int[max];
            int used = Mathf.Min(max, hackedOldestFirst?.Count ?? 0), free = max - used;
            for (int i = 0; i < max; i++) slots[i] = i < free ? -1 : hackedOldestFirst[hackedOldestFirst.Count - used + (i - free)];
            return slots;
        }
    }

    /// <summary>
    /// The house's clock in the HUD (after Risk of Rain's difficulty timer): it counts up from the moment the team is in
    /// the house and always shows when the next extra robot comes; the more have come, the redder it is — up to a point.
    /// </summary>
    /// <summary>The coffee's feel of speed (pure, unit tested): how strong the speed lines and the wider view are now.</summary>
    public static class RushRules
    {
        /// <summary>
        /// 0..1: nothing without the coffee or standing still, growing with the speed past a walk (<paramref name="walk"/>
        /// m/s) up to full at the coffee's run.
        /// </summary>
        public static float Strength(bool caffeinated, float speed, float walk) =>
            caffeinated ? Mathf.Clamp01((speed - walk * 0.8f) / Mathf.Max(0.1f, walk * 0.9f)) : 0f;

        /// <summary>Degrees the field of view widens at full strength.</summary>
        public const float FovKick = 9f;
    }

    /// <summary>The hacking terminal's list (pure, unit tested): which rows show, and the names numbered within a kind.</summary>
    public static class HackListRules
    {
        /// <summary>The first row shown so that the pick stays in view, a few rows from the top where it can.</summary>
        public static int FirstShown(int pick, int count, int shown)
        {
            if (count <= shown || pick < 0) return 0;
            return Mathf.Clamp(pick - shown / 3, 0, count - shown);
        }

        /// <summary>The row's name: the kind's name, numbered (1, 2…) when the list holds more than one of that kind.</summary>
        public static string Label<T, K>(string name, System.Collections.Generic.IReadOnlyList<T> list, int index, System.Func<T, K> kindOf)
        {
            var kind = kindOf(list[index]);
            int number = 0, total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!Equals(kindOf(list[i]), kind)) continue;
                total++;
                if (i <= index) number = total;
            }
            return total > 1 ? $"{name} {number}" : name;
        }
    }

    public static class HouseClockRules
    {
        /// <summary>"M:SS" of <paramref name="seconds"/> (whole seconds, never negative).</summary>
        public static string Format(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        /// <summary>How hot the clock is, 0..1: the share of <paramref name="hottestAt"/> reinforcements already come (capped).</summary>
        public static float Heat(int came, int hottestAt = 6) => Mathf.Clamp01(came / (float)Mathf.Max(1, hottestAt));

        static readonly Color Calm = new(0.22f, 0.55f, 0.85f), Warm = new(0.95f, 0.62f, 0.18f), Hot = new(0.86f, 0.24f, 0.2f);

        /// <summary>The clock's colour for a heat: calm blue, through amber, to a red that does not hurt the eyes.</summary>
        public static Color ColorOf(float heat) =>
            heat < 0.5f ? Color.Lerp(Calm, Warm, heat * 2f) : Color.Lerp(Warm, Hot, (heat - 0.5f) * 2f);
    }

    /// <summary>Banners shown one after another (pure, unit tested): at most four waiting, no immediate repeats.</summary>
    public sealed class BannerQueue
    {
        public const float Duration = 3.5f;
        public const int Capacity = 4;
        const float Fade = 0.25f;

        readonly List<(string text, string group, float duration)> m_Queue = new();
        float m_Start = float.NegativeInfinity;
        float m_Duration = Duration;
        string m_Group;

        public string Current { get; private set; }
        public int Waiting => m_Queue.Count;

        /// <summary>
        /// Queue a banner. Banners of a <paramref name="group"/> replace each other: a new one takes the place of the one
        /// on screen at once (no fade, its own short <paramref name="duration"/>) or of the one still waiting — pressing
        /// F8 ten times shows the last style for a moment, not ten banners in a row.
        /// </summary>
        public void Push(string text, string group = null, float now = float.NaN, float duration = Duration)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (group != null)
            {
                if (Current != null && m_Group == group && !float.IsNaN(now))
                {
                    Current = text;
                    m_Duration = duration;
                    m_Start = now - Fade; // fully visible right away
                    return;
                }
                m_Queue.RemoveAll(b => b.group == group);
            }
            if (m_Queue.Count >= Capacity || text == Current || m_Queue.Exists(b => b.text == text)) return;
            m_Queue.Add((text, group, duration));
        }

        public void Update(float now)
        {
            if (now - m_Start < m_Duration) return;
            if (m_Queue.Count == 0) { Current = null; m_Group = null; return; }
            (Current, m_Group, m_Duration) = m_Queue[0];
            m_Queue.RemoveAt(0);
            m_Start = now;
        }

        public float Alpha(float now) => Current == null ? 0f : UiMath.MessageAlpha(now - m_Start, m_Duration);
        public float Age(float now) => now - m_Start;
    }

    /// <summary>"+N" next to the quota after a deposit; deposits close together add up (pure, unit tested).</summary>
    public sealed class GainTracker
    {
        public const float MergeWindow = 1.5f;
        public const float ShowTime = 2f;

        int m_Last = -1;

        public int Amount { get; private set; }
        public float Time { get; private set; } = float.NegativeInfinity;

        public void Observe(int collected, float now)
        {
            if (m_Last >= 0 && collected > m_Last)
            {
                Amount = (now - Time < MergeWindow ? Amount : 0) + (collected - m_Last);
                Time = now;
            }
            m_Last = collected;
        }

        public bool Visible(float now) => now - Time < ShowTime;
    }

    /// <summary>Hit flash and direction of the last hit (pure).</summary>
    public sealed class HitFeedback
    {
        public const float ArcTime = 1.6f;

        public float Flash { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Time { get; private set; } = float.NegativeInfinity;

        public void Hit(Vector3 awayFromSource, float now)
        {
            Flash = 1f;
            Direction = -awayFromSource;
            Time = now;
        }

        public void Update(float dt) => Flash = Mathf.MoveTowards(Flash, 0f, dt * 1.5f);

        public float ArcAlpha(float now) => Mathf.Clamp01(ArcTime - (now - Time));
    }
}
