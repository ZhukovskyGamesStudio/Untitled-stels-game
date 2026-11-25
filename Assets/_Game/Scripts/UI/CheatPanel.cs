using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Cheats for testing (prefab UI/HUD, F4 — <see cref="GameAction.Cheats"/>), on five tabs: the cheats (switch off every
    /// robot, be invulnerable or invisible to the robots, fill the quota, jump to the hatch, see the whole map), start a house
    /// of any kind right now (the test rooms too), spawn any item in front of you, set any car upgrade to any level, pick the
    /// visual style (<see cref="StylePanel"/>). The server carries them out (<see cref="PlayerController.RequestCheat(Cheat,int)"/>);
    /// the panel frees the cursor while it is open.
    /// </summary>
    public class CheatPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static CheatPanel Instance { get; private set; }

        /// <summary>The tabs, in order (index into <see cref="Tabs"/> and <see cref="Pages"/>).</summary>
        public enum Tab { Cheats, Houses, Items, Car, Visual }

        public GameObject Root;
        [Header("Tabs")]
        [Tooltip("The tab buttons along the top (index = Tab)")]
        public Button[] Tabs = new Button[0];
        [Tooltip("The pages the tabs show (index = Tab)")]
        public GameObject[] Pages = new GameObject[0];
        [Tooltip("Tint of the open tab's button, the chosen colour and the current upgrade levels")]
        public Color CurrentColor = UiPalette.NoteYellow;

        [Header("Cheats")]
        public Button KillRobots;
        public Button Invulnerable, Invisible, Quota, ToHatch, RevealMap;

        [Header("Locations")]
        [Tooltip("Start a house of a kind now (index = HouseKind): for testing a kind without waiting for it.")]
        public Button[] Houses = new Button[0];

        [Header("Items")]
        [Tooltip("Spawn this item in front of you (index = ItemCatalog shape); the label is the item's name.")]
        public Button[] Items = new Button[0];
        [Tooltip("The colour the items spawn with (index = ItemCatalog colour; a blueprint: that recipe).")]
        public Button[] ItemColors = new Button[0];

        [Header("Car")]
        [Tooltip("A row per upgrade (index = CarUpgrade); hidden for the ones not designed yet.")]
        public GameObject[] UpgradeRows = new GameObject[0];
        [Tooltip("The level buttons, row by row: upgrade × (CarUpgrades.MaxLevelled + 1) + level; those above an upgrade's top are hidden.")]
        public Button[] UpgradeLevels = new Button[0];

        bool m_Invulnerable, m_Invisible;
        Tab m_Tab;
        int m_Color, m_LocVersion = -1, m_UpgradesVersion = -1;

        const int LevelsPerRow = CarUpgrades.MaxLevelled + 1;

        void Awake()
        {
            Instance = this;
            KillRobots.onClick.AddListener(() => Me?.RequestCheat(Cheat.KillRobots, true));
            Invulnerable.onClick.AddListener(() => { m_Invulnerable = !m_Invulnerable; Me?.RequestCheat(Cheat.Invulnerable, m_Invulnerable); });
            Invisible.onClick.AddListener(() => { m_Invisible = !m_Invisible; Me?.RequestCheat(Cheat.Invisible, m_Invisible); });
            Quota.onClick.AddListener(() => Me?.RequestCheat(Cheat.FillQuota, true));
            ToHatch.onClick.AddListener(() =>
            {
                var layout = ActiveLevel.Layout;
                if (Me != null && layout != null) Me.TeleportOwner(layout.HatchPosition + new Vector3(1.5f, 0.05f, 0f), Me.transform.rotation);
            });
            RevealMap.onClick.AddListener(() => GameManager.Instance?.Exploration?.RevealAll());
            for (int i = 0; i < Tabs.Length; i++)
            {
                var tab = (Tab)i;
                if (Tabs[i] != null) Tabs[i].onClick.AddListener(() => Select(tab));
            }
            for (int i = 0; i < Houses.Length; i++)
            {
                var kind = (HouseKind)i;
                if (Houses[i] != null) Houses[i].onClick.AddListener(() => Me?.RequestCheat(Cheat.StartHouse + (byte)kind, true));
            }
            for (int i = 0; i < Items.Length; i++)
            {
                int shape = i;
                if (Items[i] != null) Items[i].onClick.AddListener(() => Me?.RequestCheat(Cheat.SpawnItem, CheatArgs.Item(shape, m_Color)));
            }
            for (int i = 0; i < ItemColors.Length; i++)
            {
                int color = i;
                if (ItemColors[i] != null) ItemColors[i].onClick.AddListener(() => { m_Color = color; m_LocVersion = -1; });
            }
            for (int i = 0; i < UpgradeLevels.Length; i++)
            {
                var upgrade = (CarUpgrade)(i / LevelsPerRow);
                int level = i % LevelsPerRow;
                if (UpgradeLevels[i] != null) UpgradeLevels[i].onClick.AddListener(() => Me?.RequestCheat(Cheat.SetUpgrade, CheatArgs.Upgrade(upgrade, level)));
            }
            for (int u = 0; u < UpgradeRows.Length; u++)
            {
                int top = CarUpgrades.MaxLevel((CarUpgrade)u);
                if (UpgradeRows[u] != null) UpgradeRows[u].SetActive(top > 0);
                for (int level = 0; level < LevelsPerRow; level++)
                {
                    var b = LevelButton(u, level);
                    if (b != null) b.gameObject.SetActive(level <= top);
                }
            }
            Select(Tab.Cheats);
            Root.SetActive(false);
        }

        static PlayerController Me => PlayerController.Local;

        Button LevelButton(int upgrade, int level)
        {
            int i = upgrade * LevelsPerRow + level;
            return i < UpgradeLevels.Length ? UpgradeLevels[i] : null;
        }

        void OnDestroy()
        {
            IsOpen = false;
            if (Instance == this) Instance = null;
        }

        /// <summary>Test hook (autopilot): open or close the panel.</summary>
        public void SetOpen(bool open) => Show(open);

        /// <summary>Show a tab's page (the others hide).</summary>
        public void Select(Tab tab)
        {
            m_Tab = tab;
            for (int i = 0; i < Pages.Length; i++)
                if (Pages[i] != null && Pages[i].activeSelf != (i == (int)tab)) Pages[i].SetActive(i == (int)tab);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null && Tabs[i].image != null) Tabs[i].image.color = i == (int)tab ? CurrentColor : Color.white;
            m_LocVersion = -1;
            m_UpgradesVersion = -1;
        }

        /// <summary>
        /// The toggles of the panel onto the player (owner side): whatever differs is asked for again. The panel lives in
        /// the scene, the player object does not — after a new session or a reconnect a fresh player came without the
        /// cheats while the panel still showed them on, and they had to be switched off and on to work.
        /// </summary>
        public static void Sync(PlayerController me, bool invulnerable, bool invisible)
        {
            if (me == null) return;
            if (me.Invulnerable != invulnerable) me.RequestCheat(Cheat.Invulnerable, invulnerable);
            if (me.Invisible != invisible) me.RequestCheat(Cheat.Invisible, invisible);
        }

        void Update()
        {
            if (Me == null) { Show(false); return; }
            Sync(Me, m_Invulnerable, m_Invisible);
            if (!GameMenu.IsOpen && Controls.Pressed(GameAction.Cheats)) Show(!IsOpen);
            if (GameMenu.IsOpen && IsOpen) Show(false);
            if (!IsOpen) return;
            switch (m_Tab)
            {
                case Tab.Cheats:
                    Label(Invulnerable, Loc.F("cheats.invulnerable", Loc.T(m_Invulnerable ? "cheats.on" : "cheats.off")));
                    Label(Invisible, Loc.F("cheats.invisible", Loc.T(m_Invisible ? "cheats.on" : "cheats.off")));
                    break;
                case Tab.Items:
                    RefreshItems();
                    break;
                case Tab.Car:
                    RefreshUpgrades();
                    break;
            }
        }

        /// <summary>The items' names and the colours (in the player's language), the chosen colour highlighted.</summary>
        void RefreshItems()
        {
            if (m_LocVersion == Loc.Version) return;
            m_LocVersion = Loc.Version;
            for (int i = 0; i < Items.Length && i < ItemCatalog.Shapes.Length; i++)
                if (Items[i] != null) Label(Items[i], ItemCatalog.ShortName(i));
            for (int c = 0; c < ItemColors.Length; c++)
            {
                if (ItemColors[c] == null) continue;
                Label(ItemColors[c], c < ItemCatalog.Colors.Length ? ItemCatalog.ColorName(c) : c.ToString());
                if (ItemColors[c].image != null) ItemColors[c].image.color = c == m_Color ? CurrentColor : Color.white;
            }
        }

        /// <summary>Each upgrade's current level highlighted.</summary>
        void RefreshUpgrades()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Upgrades.Version == m_UpgradesVersion) return;
            m_UpgradesVersion = gm.Upgrades.Version;
            for (int u = 0; u < UpgradeRows.Length; u++)
                for (int level = 0; level < LevelsPerRow; level++)
                {
                    var b = LevelButton(u, level);
                    if (b != null && b.image != null) b.image.color = gm.Upgrades.Level((CarUpgrade)u) == level ? CurrentColor : Color.white;
                }
        }

        void Show(bool on)
        {
            IsOpen = on;
            if (Root.activeSelf != on) Root.SetActive(on);
            if (on) { m_LocVersion = -1; m_UpgradesVersion = -1; }
        }

        static void Label(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null && t.text != text) t.text = text;
        }
    }
}
