using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// The player: what the things made at the car's workbench do (<see cref="CraftedRules"/>) — the jump boots and the gas
    /// mask while carried, the double espresso's ram, the upgraded screwdriver at a device, the hammer's hole in the floor and
    /// its tired arms, the robot sensor put down on the floor (see <see cref="PlayerController"/>).
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>Server time the double espresso wears off (0: none), replicated: running into a robot knocks it out until then.</summary>
        public double RamUntil { get; private set; }
        /// <summary>The double espresso works now (every peer).</summary>
        public bool Espresso => RamUntil > Session.ServerTime;

        /// <summary>The house (its seed) this player used a hammer in — their arms are tired there.</summary>
        int m_HammeredIn = int.MinValue;
        bool m_TiredReplicated;

        /// <summary>This player has used a hammer in this house: they cannot again (a teammate can), every peer.</summary>
        public bool TiredArms => IsServer ? GameManager.Instance != null && GameManager.Instance.Phase == RunPhase.Playing && m_HammeredIn == GameManager.Instance.Level.Seed : m_TiredReplicated;

        /// <summary>The gear's flags for the wire: 1 tired arms.</summary>
        public byte GearFlags => (byte)(TiredArms ? 1 : 0);

        static CraftedSettings Crafted => Tuning.Items.Crafted;

        /// <summary>
        /// Owner: the car's workbench is open (E at it): the backpack is open as with Tab, the bench beside it. Closes on E
        /// or Tab again, the menu, a fall, or walking away from the car.
        /// </summary>
        public bool AtBench { get; private set; }
        /// <summary>
        /// Owner: a shelf of the car's bed is open (E at it; its number, −1 none): the backpack open as with Tab, the shelf's
        /// grid beside it. Closes like the bench.
        /// </summary>
        public int AtShelf { get; private set; } = -1;
        bool m_BenchOpenedNow;

        /// <summary>Owner, every frame: the bench (or a shelf) closes when it should.</summary>
        void UpdateBench(bool menu)
        {
            if (!AtBench && AtShelf < 0) { m_BenchOpenedNow = false; return; }
            var gm = GameManager.Instance;
            bool inReach = gm != null && (AtBench ? gm.AtBench(transform.position) : gm.AtShelf(transform.position, AtShelf));
            bool shut = menu || Downed || !inReach || SeatIndex >= 0 || UsingTerminal != null;
            // (the press that opened it is not the press that shuts it)
            bool pressed = Controls.Pressed(GameAction.Interact);
            if (m_BenchOpenedNow && (pressed || Controls.Pressed(GameAction.Hands))) shut = true;
            m_BenchOpenedNow = true;
            if (!shut) return;
            AtBench = false;
            AtShelf = -1;
            m_BenchOpenedNow = false;
            if (pressed) m_UseWaitRelease = true; // (the press that shut it does not open it again)
        }

        /// <summary>Every peer but the server: the server's word on the gear.</summary>
        public void ApplyGear(double coffeeUntil, ushort loadout, double ramUntil, byte flags)
        {
            ApplyGear(coffeeUntil, loadout);
            RamUntil = ramUntil;
            m_TiredReplicated = (flags & 1) != 0;
        }

        /// <summary>Server, the hand key on a double espresso: the coffee's speed, and robots fall when run into.</summary>
        bool DrinkEspressoServer(PlayerInventory inv)
        {
            if (!inv.TakeHeldServer(ItemCatalog.Espresso)) return true;
            double now = Session.ServerTime;
            CoffeeUntil = System.Math.Max(CoffeeUntil, now) + Gear.CoffeeSeconds;
            RamUntil = System.Math.Max(RamUntil, now) + Gear.CoffeeSeconds;
            return true;
        }

        /// <summary>Owner, every frame: the jump boots and the gas mask work while they are anywhere on the player.</summary>
        void UpdateCrafted()
        {
            var inv = m_Inventory;
            bool boots = inv != null && inv.CarriesExactly(ItemCatalog.JumpBoots);
            Motor.JumpHeightOverride = boots ? CraftedRules.JumpHeight(0f, true, Crafted) : 0f;
            bool mask = inv != null && inv.CarriesExactly(ItemCatalog.GasMask);
            Gas.SetLimit(mask ? CraftedRules.GasLimit(Gas.Settings.Limit, true, Crafted) : 0f);
            UpdateRam();
        }

        float m_RamSentAt = -10f;

        /// <summary>Owner: running under a double espresso into a robot — ask the server to knock it out.</summary>
        void UpdateRam()
        {
            if (!Espresso || Downed || SeatIndex >= 0 || !IsSprinting || Time.time < m_RamSentAt + 0.3f) return;
            var me = transform.position;
            foreach (var d in Detector.All)
            {
                if (d is not RobotController || d.IsHacked) continue;
                var v = d.transform.position - me;
                if (Mathf.Abs(v.y) > 1.5f) continue;
                v.y = 0f;
                if (!CraftedRules.Rams(true, true, v.magnitude, Crafted)) continue;
                m_RamSentAt = Time.time;
                Link.SendGear(GearAction.Ram, d, d.transform.position);
                return;
            }
        }

        /// <summary>
        /// Owner, the hand key: the hammer breaks the floor under the aim, the robot sensor is put down before the feet.
        /// False when the hand holds neither (the key does what it usually does).
        /// </summary>
        bool UseCraftedInHand(PlayerInventory inv)
        {
            if (inv == null) return false;
            if (inv.HoldsExactly(ItemCatalog.Hammer))
            {
                if (!TiredArms && FloorUnderAim(out var point)) Link.SendGear(GearAction.Hammer, null, point);
                return true;
            }
            if (inv.HoldsExactly(ItemCatalog.RoboSensor))
            {
                var at = transform.position + transform.forward * 0.6f + Vector3.up * 0.5f;
                const int mask = ~(1 << LevelMetrics.CeilingLayer);
                if (Physics.Raycast(at, Vector3.down, out var hit, 2f, mask, QueryTriggerInteraction.Ignore)) at = hit.point + Vector3.up * 0.01f;
                else at = transform.position;
                inv.RequestDrop(inv.HandIndex(), at, transform.eulerAngles.y);
                return true;
            }
            return false;
        }

        /// <summary>Owner: the floor the view points at (in the hammer's reach), false if it points at none.</summary>
        bool FloorUnderAim(out Vector3 point)
        {
            point = default;
            const int mask = ~(1 << LevelMetrics.CeilingLayer);
            var ray = new Ray(Camera.transform.position, Camera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, Crafted.HoleReach, mask, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f) return false;
            point = hit.point;
            return true;
        }

        /// <summary>Server: a request of a thing made at the workbench — every condition is checked again here.</summary>
        public void OnGearRequest(GearAction action, Detector target, Vector3 point)
        {
            var gm = GameManager.Instance;
            var inv = m_Inventory;
            if (gm == null || inv == null || Downed) return;
            switch (action)
            {
                case GearAction.Ram:
                    if (!Espresso || target is not RobotController || target.IsHacked) return;
                    if (!ServerReach.WithinFlat(transform.position, target.transform.position, Crafted.RamReach)) return;
                    if (Mathf.Abs(target.transform.position.y - transform.position.y) > 1.5f) return;
                    gm.RamServer(this, target, Crafted.RamStun);
                    break;
                case GearAction.Unscrew:
                    if (target == null || !CanUnscrew(inv)) return; // a robot or a device
                    if (!target.CanBeHackedFrom(transform.position, m_Interaction.HackReach + 0.5f)) return;
                    if (!target.ReachableFrom(Hands)) return;
                    if (!gm.UnscrewServer(this, target)) return;
                    inv.SetHeldColorServer(CraftedRules.UsedIn(gm.Level.Seed));
                    break;
                case GearAction.Hammer:
                    if (TiredArms || !inv.HoldsExactly(ItemCatalog.Hammer)) return;
                    if (!ServerReach.Within(Head != null ? Head.position : transform.position + Vector3.up * 1.6f, point, Crafted.HoleReach)) return;
                    if (!gm.BreakFloorServer(this, point)) return;
                    m_HammeredIn = gm.Level.Seed;
                    break;
            }
        }

        /// <summary>The upgraded screwdriver in the hand, not yet used in this house: it can take a robot or a device apart.</summary>
        public bool CanUnscrew(PlayerInventory inv)
        {
            var gm = GameManager.Instance;
            if (inv == null || gm == null || !inv.HoldsExactly(ItemCatalog.ProScrewdriver)) return false;
            return !CraftedRules.IsUsedIn(inv.Items[inv.HandIndex()].Color, gm.Level.Seed);
        }
    }
}
