using Stealth.Configs;
using Stealth.Game;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// The player: the starting gear — the class and the extra item picked on the run's start screen (<see cref="Loadout"/>),
    /// the medkit and the instant coffee used from the hand, hard landings (the boots take them), the radio and the keyboard
    /// in the hand (see <see cref="PlayerController"/>).
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>The class picked on the run's start screen (index in <see cref="ItemCatalog.ClassTools"/>), server-decided, replicated.</summary>
        public int LoadoutClass { get; private set; }
        /// <summary>The extra item picked on the start screen (index in <see cref="ItemCatalog.StartItems"/>), replicated.</summary>
        public int LoadoutItem { get; private set; }
        /// <summary>Ready on the start screen, replicated.</summary>
        public bool LoadoutReady { get; private set; }

        /// <summary>The loadout as one number for the wire: class | item &lt;&lt; 4 | ready &lt;&lt; 8.</summary>
        public ushort LoadoutPacked => (ushort)((LoadoutClass & 15) | (LoadoutItem & 15) << 4 | (LoadoutReady ? 1 << 8 : 0));

        /// <summary>Server time the instant coffee wears off (0: none), replicated: the owner runs and works faster until then.</summary>
        public double CoffeeUntil { get; private set; }

        /// <summary>The coffee is working now (every peer).</summary>
        public bool Caffeinated => CoffeeUntil > Session.ServerTime;

        static GadgetSettings Gear => Tuning.Items.Gadgets;

        /// <summary>Every peer but the server: the server's word on the gear.</summary>
        public void ApplyGear(double coffeeUntil, ushort loadout)
        {
            CoffeeUntil = coffeeUntil;
            LoadoutClass = loadout & 15;
            LoadoutItem = loadout >> 4 & 15;
            LoadoutReady = (loadout & 1 << 8) != 0;
        }

        /// <summary>Owner: pick a class / an extra item / say ready on the start screen (the server checks it is up).</summary>
        public void ChooseLoadout(int cls, int item, bool ready)
        {
            cls = (cls % ItemCatalog.ClassTools.Length + ItemCatalog.ClassTools.Length) % ItemCatalog.ClassTools.Length;
            item = (item % ItemCatalog.StartItems.Length + ItemCatalog.StartItems.Length) % ItemCatalog.StartItems.Length;
            Link.SendLoadout((byte)cls, (byte)item, ready);
        }

        /// <summary>Server: a loadout request (only while the start screen is up).</summary>
        public void OnLoadoutRequest(byte cls, byte item, bool ready)
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.Picking) return;
            LoadoutClass = Mathf.Clamp(cls, 0, ItemCatalog.ClassTools.Length - 1);
            LoadoutItem = Mathf.Clamp(item, 0, ItemCatalog.StartItems.Length - 1);
            LoadoutReady = ready;
        }

        /// <summary>Server: the start screen is over — forget "ready" for the next run (the picks stay as the default).</summary>
        public void ClearReadyServer() => LoadoutReady = false;

        /// <summary>Server: the class's tool and the extra item into the backpack (the tool in the hand when it is free).</summary>
        public void GiveLoadoutServer()
        {
            var inv = GetComponent<PlayerInventory>();
            if (inv == null) return;
            byte tool = ItemCatalog.ClassTools[Mathf.Clamp(LoadoutClass, 0, ItemCatalog.ClassTools.Length - 1)];
            byte extra = ItemCatalog.StartItems[Mathf.Clamp(LoadoutItem, 0, ItemCatalog.StartItems.Length - 1)];
            if (!inv.GiveToHandServer(tool, ItemCatalog.FreshColor(tool, 0))) inv.GiveServer(tool, ItemCatalog.FreshColor(tool, 0));
            inv.GiveServer(extra, ItemCatalog.FreshColor(extra, 0));
        }

        /// <summary>Server, the hand key on a medkit (heals to full, used up), a bandage (heals a little) or the instant coffee (drunk: faster for a while).</summary>
        bool UseConsumableServer(PlayerInventory inv, InventoryEntry e)
        {
            if (e.Shape == ItemCatalog.Medkit)
            {
                if (Hp >= HealthState.MaxHp || !inv.TakeHeldServer(ItemCatalog.Medkit)) return true;
                HealServer(HealthState.MaxHp - Hp);
                Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Pickup : null, transform.position + Vector3.up, 0.8f, Audio.Sfx.Group.Items);
                return true;
            }
            if (e.Shape == ItemCatalog.Bandage)
            {
                if (Hp >= HealthState.MaxHp || !inv.TakeHeldServer(ItemCatalog.Bandage)) return true;
                HealServer(Mathf.Min(Gear.BandageHp, HealthState.MaxHp - Hp));
                Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Pickup : null, transform.position + Vector3.up, 0.8f, Audio.Sfx.Group.Items);
                return true;
            }
            if (e.Shape == ItemCatalog.Espresso) return DrinkEspressoServer(inv);
            if (e.Shape == ItemCatalog.Coffee)
            {
                if (!inv.TakeHeldServer(ItemCatalog.Coffee)) return true;
                CoffeeUntil = System.Math.Max(CoffeeUntil, Session.ServerTime) + Gear.CoffeeSeconds;
                return true;
            }
            return false;
        }

        float m_LandSlow = 1f, m_LandedAt = -100f;

        /// <summary>Owner: a landing from high up slows (and from higher, hurts) — not with the boots in the backpack.</summary>
        void HardLanding(float impactSpeed)
        {
            if (!IsOwner || Downed || SeatIndex >= 0 || Climbing || InHatch) return;
            float height = GearRules.FallHeight(impactSpeed, -Physics.gravity.y);
            var inv = GetComponent<PlayerInventory>();
            bool boots = inv != null && inv.Carries(ItemCatalog.Boots);
            var (slow, hurt) = GearRules.Landing(height, boots, Gear);
            if (slow >= 1f) return;
            m_LandSlow = slow;
            m_LandedAt = Time.time;
            Motor.AddTrauma(hurt ? 0.6f : 0.3f);
            if (hurt) Link.SendFallHurt();
        }

        /// <summary>Server: the owner fell from too high (movement is the owner's: its word is taken).</summary>
        public void OnFallHurtRequest()
        {
            var inv = GetComponent<PlayerInventory>();
            if (inv != null && inv.Carries(ItemCatalog.Boots)) return;
            GameManager.Instance?.NoteFallServer(this);
            HitServer(transform.position + Vector3.down);
        }

        bool m_PickingView;

        /// <summary>Owner, on the start screen: the camera stands before the row of the team and looks at it.</summary>
        void ShowPickingView()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.PickingCamera(out var at, out var rotation)) return;
            m_PickingView = true;
            Camera.transform.SetPositionAndRotation(at, rotation);
            Camera.fieldOfView = PickingRules.Fov; // (the motor sets the player's own again once the screen is over)
        }

        /// <summary>Owner, every frame: the coffee and a hard landing scale the walk; running on coffee widens the view.</summary>
        void UpdateGearMotion()
        {
            float scale = GearRules.SlowNow(m_LandSlow, Time.time - m_LandedAt, Gear.FallSlowSeconds);
            if (Caffeinated) scale *= Gear.CoffeeMoveScale;
            Motor.SpeedScale = scale;
            Rush = UI.RushRules.Strength(Caffeinated, Motor.HorizontalSpeed, Motor.WalkSpeed);
            Motor.FovBoost = Rush * UI.RushRules.FovKick;
        }

        /// <summary>Owner: 0..1, how fast it feels running on coffee now (the HUD's speed lines, the wider view).</summary>
        public float Rush { get; private set; }

        /// <summary>Held-E work (hacking, unscrewing, picking, searching) runs this many times faster now: the coffee.</summary>
        public float WorkScale => Caffeinated ? Gear.CoffeeWorkScale : 1f;

        /// <summary>The time a frame of held-E work counts for.</summary>
        float WorkDt => Time.deltaTime * WorkScale;

        /// <summary>The radio is in this player's hand: they hear the team from anywhere, and the radio sounds when they talk.</summary>
        public bool HoldsRadio => GetComponent<PlayerInventory>() is { } inv && inv.Holds(ItemCatalog.Radio);

        /// <summary>The keyboard is in this player's hand: robots can be hacked (devices need none).</summary>
        public bool HoldsKeyboard => GetComponent<PlayerInventory>() is { } inv && inv.Holds(ItemCatalog.Keyboard);
    }
}
