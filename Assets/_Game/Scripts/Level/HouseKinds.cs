using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What kind of building a house is: it changes the program of rooms and the shapes, not the rules.</summary>
    public enum HouseKind : byte { Office, Business, DataCenter, Warehouse, Headquarters }

    /// <summary>
    /// House kinds (pure): which one a house is (from its seed: every peer and the route panel agree without sending
    /// anything) and what it does to the building settings — room mix, room sizes, corridors, atriums, rotundas,
    /// cut corners, darkness. The first house is always an office.
    /// </summary>
    public static class HouseKinds
    {
        public static HouseKind Of(int seed, int house)
        {
            if (house <= 0) return HouseKind.Office;
            var rng = new System.Random(LevelRandom.Mix(seed, 0x4B1D, 0));
            int roll = rng.Next(100);
            return roll < 34 ? HouseKind.Office : roll < 52 ? HouseKind.Business : roll < 68 ? HouseKind.DataCenter
                 : roll < 84 ? HouseKind.Warehouse : HouseKind.Headquarters;
        }

        public static string Name(HouseKind kind) => kind switch
        {
            HouseKind.Business => "Бизнес-центр",
            HouseKind.DataCenter => "Дата-центр",
            HouseKind.Warehouse => "Склад",
            HouseKind.Headquarters => "Штаб-квартира",
            _ => "Офис",
        };

        static readonly string[] LegalForms = { "ООО", "ОАО", "АО", "ЗАО", "ПАО", "ГК" };
        static readonly string[] NameStarts =
        {
            "Рога", "Вектор", "Гипер", "Мега", "Техно", "Кибер", "Альфа", "Нео", "Инфо", "Сигма", "Байт", "Пиксель", "Квант", "Облако",
            "Север", "Протон", "Стек", "Бит", "Терра", "Спектр",
        };
        static readonly string[] NameEnds =
        {
            "Софт", "Системс", "Лаб", "Групп", "Плюс", "Интеграция", "Консалтинг", "Логистик", "Девелопмент", "Холдинг",
            "Решения", "Сервис", "Технологии", "Трейд", "Капитал", "Инвест", "Дата", "Сеть", "Нова",
        };

        /// <summary>
        /// The name on the HUD: the kind and a made-up company from the house's seed, e.g. «Офис ООО «Рога и Копыта»»
        /// (every peer and every visit to the same house agree).
        /// </summary>
        public static string Title(int seed, HouseKind kind)
        {
            var rng = new System.Random(LevelRandom.Mix(seed, 0x4E41, 1));
            string form = LegalForms[rng.Next(LegalForms.Length)];
            string start = NameStarts[rng.Next(NameStarts.Length)];
            string end = NameEnds[rng.Next(NameEnds.Length)];
            string company = start == "Рога" ? "Рога и Копыта" : $"{start}-{end}";
            return $"{Name(kind)} {form} «{company}»";
        }

        /// <summary>A copy of the settings with this kind's character (the office keeps them as they are).</summary>
        public static BuildingSettings Apply(HouseKind kind, BuildingSettings b)
        {
            if (kind == HouseKind.Office) return b;
            var s = (BuildingSettings)b.Clone();
            float Mul(RoomType t) => kind switch
            {
                HouseKind.Business => t switch { RoomType.OpenSpace => 2f, RoomType.Meeting => 1.6f, RoomType.Lounge => 1.6f, RoomType.Storage => 0.5f, RoomType.Server => 0.6f, _ => 1f },
                HouseKind.DataCenter => t switch { RoomType.Server => 4f, RoomType.Storage => 1.5f, RoomType.OpenSpace => 0.5f, RoomType.Lounge => 0.3f, RoomType.Kitchen => 0.6f, _ => 1f },
                HouseKind.Warehouse => t switch { RoomType.Storage => 5f, RoomType.OpenSpace => 0.6f, RoomType.Meeting => 0.4f, RoomType.Lounge => 0.5f, _ => 1f },
                HouseKind.Headquarters => t switch { RoomType.Office => 2.2f, RoomType.Meeting => 2f, RoomType.Lounge => 1.3f, RoomType.Storage => 0.5f, _ => 1f },
                _ => 1f,
            };
            var weights = new RoomWeight[b.RoomWeights.Length];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = new RoomWeight(b.RoomWeights[i].Room, Mathf.RoundToInt(b.RoomWeights[i].Weight * Mul(b.RoomWeights[i].Room)));
            s.RoomWeights = weights;
            switch (kind)
            {
                case HouseKind.Business:
                    s.AtriumChance = Mathf.Min(1f, b.AtriumChance * 2f);
                    s.RotundaChance = b.RotundaChance * 1.6f;
                    s.RoomSizeScale = b.RoomSizeScale * 1.3f;
                    break;
                case HouseKind.DataCenter:
                    s.CorridorChance = 1f;
                    s.AtriumChance = b.AtriumChance * 0.3f;
                    s.RotundaChance = b.RotundaChance * 0.3f;
                    s.CornerCutChance = b.CornerCutChance * 0.4f;
                    s.DarkChance = b.DarkChance + 0.12f;
                    s.RoomSizeScale = b.RoomSizeScale * 0.85f;
                    break;
                case HouseKind.Warehouse:
                    s.CorridorChance = b.CorridorChance * 0.35f;
                    s.AtriumChance = Mathf.Min(1f, b.AtriumChance * 1.8f);
                    s.RotundaChance = 0f;
                    s.CornerCutChance = b.CornerCutChance * 0.3f;
                    s.RoomSizeScale = b.RoomSizeScale * 1.7f;
                    break;
                case HouseKind.Headquarters:
                    s.RotundaChance = b.RotundaChance * 3f;
                    s.CornerCutChance = Mathf.Min(1f, b.CornerCutChance * 1.6f);
                    s.RoomSizeScale = b.RoomSizeScale * 0.9f;
                    break;
            }
            return s;
        }
    }
}
