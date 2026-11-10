using UnityEngine;

namespace Stealth.Player
{
    /// <summary>The players are IT people from different departments; the slot of a player (client id) picks one.</summary>
    public enum Department { Backend, Qa, Sysadmin, Designer }

    /// <summary>
    /// How a player looks (pure): the department of a slot and the colour of every part of the player model. The team
    /// colour is kept to small things — the backpack, an armband, the lanyard or the scarf (part "Accent"); the clothes
    /// wear the department's colour ("Body"), the face is skin ("AccentHead"), hair, glasses and headphones are dark
    /// ("EyeGlasses"). The models of the departments come with the visual styles (Tools/Blender/players.py).
    /// </summary>
    public static class PlayerLooks
    {
        public const int Slots = 4;

        public static readonly Color[] TeamColors =
        {
            new(0.95f, 0.55f, 0.2f), new(0.3f, 0.7f, 0.95f), new(0.6f, 0.9f, 0.3f), new(0.9f, 0.4f, 0.8f),
        };

        /// <summary>Clothes of each department: a dark hoodie, a white shirt, a khaki vest, a warm beige jumper.</summary>
        public static readonly Color[] DepartmentColors =
        {
            new(0.22f, 0.24f, 0.3f), new(0.85f, 0.87f, 0.9f), new(0.45f, 0.43f, 0.32f), new(0.62f, 0.52f, 0.46f),
        };

        public static readonly Color[] SkinTones =
        {
            new(0.93f, 0.76f, 0.62f), new(0.78f, 0.58f, 0.44f), new(0.96f, 0.82f, 0.7f), new(0.55f, 0.38f, 0.28f),
        };

        public static readonly Color Dark = new(0.06f, 0.06f, 0.07f);

        public static readonly string[] DepartmentNames = { "Бэкенд", "Тестирование", "Сисадмины", "Дизайн" };

        public static int SlotOf(ulong clientId) => (int)(clientId % Slots);

        public static Department DepartmentOf(int slot) => (Department)(((slot % Slots) + Slots) % Slots);

        public static Color TeamColor(int slot) => TeamColors[((slot % Slots) + Slots) % Slots];

        /// <summary>The colour of a part of the player model (by the part's name), or false to keep the material's own.</summary>
        public static bool PartColor(string part, int slot, out Color color)
        {
            int s = ((slot % Slots) + Slots) % Slots;
            if (part.StartsWith("AccentHead")) { color = SkinTones[s]; return true; }
            if (part.StartsWith("Accent")) { color = TeamColors[s]; return true; }
            if (part.StartsWith("Eye")) { color = Dark; return true; }
            if (part.StartsWith("Body")) { color = DepartmentColors[(int)DepartmentOf(s)]; return true; }
            color = default;
            return false;
        }

        /// <summary>Colours every part of a player model for the slot (a material property block per renderer).</summary>
        public static void Paint(Transform model, int slot)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!PartColor(r.gameObject.name, slot, out var c)) continue;
                block.Clear();
                block.SetColor("_BaseColor", c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
