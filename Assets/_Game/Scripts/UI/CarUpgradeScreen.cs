using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The garage's car upgrade bench, open (E at it: <see cref="PlayerController.AtUpgrades"/>): a card per upgrade
    /// (<see cref="CarUpgradeCardView"/>) — the three levelled ones on top, the unique ones under them — showing at once what
    /// each gives, how far it is and what its next level takes this run (<see cref="GameManager.UpgradeBook"/>); its button
    /// asks the server (<see cref="PlayerInventory.RequestCarUpgrade"/>). Everything is pre-built in the prefab: this fills it.
    /// </summary>
    public class CarUpgradeScreen : MonoBehaviour
    {
        public GameObject Root;
        [Tooltip("The cards in CarUpgrade order.")]
        public CarUpgradeCardView[] Cards;
        public Button Close;
        [Tooltip("The things' pictures (Data/ItemVisuals Icons).")]
        public ItemVisuals Visuals;
        [Tooltip("The tick and the cross over a recipe's thing (have it / not).")]
        public Sprite Tick, Cross;

        bool m_Wired;
        readonly List<bool> m_Have = new();

        void Wire()
        {
            if (m_Wired) return;
            m_Wired = true;
            for (int i = 0; i < Cards.Length; i++)
            {
                var u = (CarUpgrade)i;
                if (Cards[i] != null && Cards[i].Raise != null) Cards[i].Raise.onClick.AddListener(() => Raise(u));
            }
            if (Close != null) Close.onClick.AddListener(() => PlayerController.Local?.CloseUpgrades());
        }

        static void Raise(CarUpgrade u)
        {
            var me = PlayerController.Local;
            if (me == null || !me.TryGetComponent<PlayerInventory>(out var inv)) return;
            inv.RequestCarUpgrade(u);
            Audio.Sfx.Ui(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Pickup : null, 0.6f, Audio.Sfx.Group.Items);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var me = PlayerController.Local;
            bool on = gm != null && me != null && me.AtUpgrades && !GameMenu.IsOpen;
            Ui.Show(Root, on);
            if (!on) return;
            Wire();
            me.TryGetComponent<PlayerInventory>(out var inv);
            for (int i = 0; i < Cards.Length && i < CarUpgrades.Count; i++)
                if (Cards[i] != null) Fill(Cards[i], (CarUpgrade)i, gm, inv);
        }

        void Fill(CarUpgradeCardView v, CarUpgrade u, GameManager gm, PlayerInventory inv)
        {
            int level = gm.Upgrades.Level(u), max = CarUpgrades.MaxLevel(u);
            bool planned = CarUpgrades.IsPlanned(u), top = !planned && level >= max;
            string key = GameManager.UpgradeKey(u);
            Ui.Set(v.Name, Loc.T(key), UiPalette.Ink);
            // the level boxes: as many as the upgrade has, ticked up to its level
            for (int b = 0; v.Boxes != null && b < v.Boxes.Length; b++)
            {
                Ui.Show(v.Boxes[b], b < max);
                if (v.Ticks != null && b < v.Ticks.Length) Ui.Show(v.Ticks[b], b < level);
            }
            var s = Tuning.Run.Upgrades;
            Ui.Set(v.What, u == CarUpgrade.Engine ? Loc.F(key + ".what", Mathf.RoundToInt(s.EngineCutPerLevel * 100f)) : Loc.T(key + ".what"), UiPalette.InkSoft);
            Ui.Set(v.Now, NowText(u, level, s), UiPalette.Ink);
            Ui.Show(v.Planned, planned);
            Ui.Show(v.Done, top);
            var recipe = planned || top ? null : gm.UpgradeBook.Recipe(u, level + 1);
            Ui.Show(v.Recipe, recipe != null);
            bool have = false;
            if (recipe != null)
            {
                if (inv != null) inv.RecipeHave(recipe, m_Have); else m_Have.Clear();
                have = m_Have.Count == recipe.Length && !m_Have.Contains(false);
                for (int k = 0; k < v.Things.Length; k++)
                {
                    bool shown = k < recipe.Length;
                    Ui.Show(v.Things[k], shown);
                    if (k < v.Marks.Length) Ui.Show(v.Marks[k], shown);
                    if (!shown) continue;
                    var sprite = Visuals != null && Visuals.Icons != null && recipe[k] < Visuals.Icons.Length ? Visuals.Icons[recipe[k]] : null;
                    if (v.Things[k].sprite != sprite) v.Things[k].sprite = sprite;
                    bool got = k < m_Have.Count && m_Have[k];
                    if (k < v.Marks.Length && v.Marks[k] != null)
                    {
                        var mark = got ? Tick : Cross;
                        if (v.Marks[k].sprite != mark) v.Marks[k].sprite = mark;
                        v.Marks[k].color = got ? UiPalette.Good : UiPalette.Stamp;
                    }
                }
            }
            Ui.Show(v.Raise, recipe != null);
            if (v.Raise != null) v.Raise.interactable = have;
            if (v.RaiseLabel != null) v.RaiseLabel.text = Loc.T(have ? "upgrade.raise" : "upgrade.missing");
        }

        /// <summary>What the upgrade has given so far.</summary>
        static string NowText(CarUpgrade u, int level, CarUpgradeSettings s)
        {
            string key = GameManager.UpgradeKey(u) + ".now";
            return u switch
            {
                CarUpgrade.Engine => Loc.F(key, Mathf.RoundToInt((1f - CarUpgrades.FuelFactor(level, s)) * 100f)),
                CarUpgrade.Trunk => Loc.F(key, level),
                CarUpgrade.Kennel => Loc.F(key, CarUpgrades.StashSize(level, s).x, CarUpgrades.StashSize(level, s).y),
                CarUpgrade.Teleport => Loc.T(level > 0 ? key : key + ".none"),
                CarUpgrade.Radio => level == 0 ? "" : GameManager.Instance != null && GameManager.Instance.Cassette >= 0
                    ? Loc.F(key, Loc.T(Items.ItemCatalog.CassetteKey(GameManager.Instance.Cassette))) : Loc.T(key + ".none"),
                _ => "",
            };
        }
    }
}
