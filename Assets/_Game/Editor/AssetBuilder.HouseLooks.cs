using System;
using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The looks of the kinds of houses (Data/Styles/Houses/&lt;kind&gt;.asset, <see cref="HouseLook"/>): made once from the
    /// definitions below, edited by hand from then on. REBUILD_LOOKS=Hospital,Abandoned (or 1) makes them anew.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string HouseLooksDir = StylesDir + "/Houses";

        static HouseLook[] BuildHouseLooks()
        {
            EnsureFolder(HouseLooksDir);
            var again = (Environment.GetEnvironmentVariable("REBUILD_LOOKS") ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            var kinds = (HouseKind[])Enum.GetValues(typeof(HouseKind));
            var looks = new HouseLook[kinds.Length];
            foreach (var kind in kinds)
            {
                var path = $"{HouseLooksDir}/{kind}.asset";
                var look = AssetDatabase.LoadAssetAtPath<HouseLook>(path);
                if (look == null || again.Contains("1") || again.Contains(kind.ToString()))
                {
                    bool fresh = look == null;
                    var made = ScriptableObject.CreateInstance<HouseLook>();
                    made.name = kind.ToString();
                    made.Kind = kind;
                    DefineLook(made);
                    if (fresh) { AssetDatabase.CreateAsset(made, path); look = made; }
                    else { EditorUtility.CopySerialized(made, look); look.name = kind.ToString(); EditorUtility.SetDirty(look); }
                }
                if (look.Surfaces == null && SurfacesOf(kind) is { } surfaces) { look.Surfaces = surfaces; EditorUtility.SetDirty(look); }
                looks[(int)kind] = look;
            }
            return looks;
        }

        static RoomLook Room(RoomType room, string wall, float wallTile, string lower = null, float lowerHeight = 0f, string ceiling = null, float ceilingTile = 1.2f,
            float lowerTile = 1f, bool plain = true) => new()
        {
            Room = room, Wall = K(wall), WallTile = wallTile, Lower = lower != null ? K(lower) : null, LowerTile = lowerTile, LowerHeight = lowerHeight, Plain = plain,
            Ceiling = ceiling != null ? K(ceiling) : null, CeilingTile = ceilingTile,
        };

        static RoomFloor Under(RoomType room, string material, float tile) => new() { Room = room, Material = K(material), Tile = tile };

        /// <summary>Plain trim: door casings and skirting, no cornices, panels, rails or light slits.</summary>
        static void PlainTrim(HouseLook h, bool pipes, CeilingKind ceiling)
        {
            h.OwnTrim = true;
            h.Doors = DoorTrim.Casing;
            h.Baseboard = true;
            h.Pipes = pipes;
            h.Ceiling = ceiling;
        }

        static void DefineLook(HouseLook h)
        {
            var looks = new List<RoomLook>();
            var floors = new List<RoomFloor>();
            var types = (RoomType[])Enum.GetValues(typeof(RoomType));
            var scanned = new[] { ModelFamily.Realistic };
            switch (h.Kind)
            {
                case HouseKind.Office:
                    // the style's own: beige paint, carpet, acoustic ceilings
                    h.ShellIn = scanned;
                    break;

                case HouseKind.Business:
                    // a business centre that wants to please: pale plaster, dark wood, parquet where people are received
                    h.ShellIn = scanned;
                    h.Trim = K("Real_DarkWood");
                    looks.Add(Room(RoomType.Lobby, "Real_Plaster2", 2.5f, "Real_PaneledWood", 1.4f, "Ceiling_Plain", 2f, plain: false));
                    looks.Add(Room(RoomType.Meeting, "Real_Plaster2", 2.5f, "Real_PaneledWood", 0.9f, "Real_CeilingTiles", 1.2f, plain: false));
                    looks.Add(Room(RoomType.Office, "Real_Plaster", 2.5f, null, 0f, "Ceiling_Acoustic", 1.2f, plain: false));
                    floors.Add(Under(RoomType.Meeting, "Real_Herringbone", 2f));
                    floors.Add(Under(RoomType.Corridor, "Real_Laminate", 2f));
                    floors.Add(Under(RoomType.Hall, "Real_Marble", 2f));
                    h.Grade.Temperature = 4f;
                    h.Grade.Saturation = 3f;
                    break;

                case HouseKind.DataCenter:
                    // machines first: white panels, smooth grey floors, services in the open, cold light
                    h.ShellIn = scanned;
                    PlainTrim(h, pipes: true, CeilingKind.Trays);
                    h.Trim = K("Trim_Metal");
                    foreach (var t in new[] { RoomType.Server, RoomType.ControlRoom, RoomType.Corridor, RoomType.Hall, RoomType.Lab })
                    {
                        looks.Add(Room(t, "Wall_Panels", 2f, null, 0f, "Ceiling_Acoustic"));
                        floors.Add(Under(t, t is RoomType.Server or RoomType.ControlRoom ? "Real_ConcreteClean" : "Floor_Lino", 1.2f));
                    }
                    h.Grade.Temperature = -8f;
                    h.Grade.Saturation = -6f;
                    h.Air.AmbientTint = new Color(0.94f, 0.98f, 1.06f);
                    break;

                case HouseKind.Warehouse:
                    // painted block, bare concrete, corrugated deck, everything overhead in sight
                    h.ShellIn = scanned;
                    PlainTrim(h, pipes: true, CeilingKind.Trays);
                    h.Wall = K("Shop_Wall"); h.WallTile = 2f;
                    h.Floor = K("Real_HangarConcrete"); h.FloorTile = 3f;
                    h.Trim = K("Trim_Metal");
                    foreach (var t in new[] { RoomType.Storage, RoomType.Corridor, RoomType.Hall, RoomType.Lobby, RoomType.OpenSpace, RoomType.CopyRoom })
                    {
                        looks.Add(Room(t, "Shop_Wall", 2f, "Shop_WallLower", 1.2f, t is RoomType.Lobby or RoomType.Corridor ? "Ceiling_Concrete" : "Shop_Ceiling", 1.5f, 2f));
                        floors.Add(Under(t, "Real_HangarConcrete", 3f));
                    }
                    h.Grade.Saturation = -5f;
                    h.Grade.Contrast = 3f;
                    break;

                case HouseKind.Headquarters:
                    // the head office: stone underfoot, panelling, deep carpets
                    h.ShellIn = scanned;
                    h.Trim = K("Real_DarkWood");
                    looks.Add(Room(RoomType.Lobby, "Real_PaneledWood", 2f, null, 0f, "Ceiling_Plain", 2f, plain: false));
                    looks.Add(Room(RoomType.Meeting, "Real_PaneledWood", 2f, null, 0f, "Ceiling_Plain", 2f, plain: false));
                    looks.Add(Room(RoomType.Office, "Real_Plaster2", 2.5f, "Real_PaneledWood", 1.0f, "Ceiling_Plain", 2f, plain: false));
                    looks.Add(Room(RoomType.Corridor, "Real_Plaster2", 2.5f, "Real_PaneledWood", 1.0f, "Ceiling_Plain", 2f, plain: false));
                    floors.Add(Under(RoomType.Corridor, "Real_Marble", 2f));
                    floors.Add(Under(RoomType.Hall, "Real_Marble", 2f));
                    floors.Add(Under(RoomType.Meeting, "Floor_CarpetCharcoal", 1f));
                    floors.Add(Under(RoomType.Office, "Floor_CarpetCharcoal", 1f));
                    h.Grade.Temperature = 5f;
                    h.Grade.Contrast = 4f;
                    h.Air.Ambient = 0.9f;
                    break;

                case HouseKind.Hospital:
                    // washable paint over a wall-protection band to 1 m, acoustic tiles, sheet vinyl; the wards warmer; the
                    // theatres and imaging rooms in white panels over a seamless floor; the plant room and the stairs keep the style's
                    PlainTrim(h, pipes: false, CeilingKind.None);
                    h.KeepStyleRooms = false;
                    foreach (var t in types)
                    {
                        if (t is RoomType.Utility or RoomType.Hall) continue;
                        bool theatre = t is RoomType.Operating or RoomType.Imaging;
                        looks.Add(theatre ? Room(t, "Theatre_Wall", 2f, null, 0f, "Clinic_Ceiling")
                            : Room(t, t == RoomType.Ward ? "Clinic_WardWall" : "Clinic_Wall", 2f, "Clinic_Guard", 1f, "Clinic_Ceiling", 1.2f, 2f));
                        floors.Add(Under(t, theatre ? "Theatre_Floor" : "Clinic_Floor", 2f));
                    }
                    h.Grade.Temperature = -5f;
                    h.Grade.Saturation = -3f;
                    h.Air.AmbientTint = new Color(0.97f, 1f, 1.03f);
                    break;

                case HouseKind.Garage:
                    // the hall, the parts store and the mechanics' rooms in painted block with a dark band, epoxy, a metal deck;
                    // the booth in filter panels over a grating; the front rooms keep the style's
                    PlainTrim(h, pipes: true, CeilingKind.Trays);
                    foreach (var t in new[] { RoomType.Workshop, RoomType.Storage, RoomType.LockerRoom })
                    {
                        looks.Add(Room(t, "Shop_Wall", 2f, "Shop_WallLower", 1.2f, "Shop_Ceiling", 1f, 2f));
                        floors.Add(Under(t, "Shop_Floor", 2f));
                    }
                    looks.Add(Room(RoomType.PaintBooth, "Booth_Wall", 1f, null, 0f, "Booth_Ceiling", 1f));
                    floors.Add(Under(RoomType.PaintBooth, "Booth_Floor", 1f));
                    h.Grade.Contrast = 3f;
                    break;

                case HouseKind.Apartments:
                    // papered flats over parquet, painted kitchens over tiles, tiled bathrooms, the stairwell's oil-painted band
                    PlainTrim(h, pipes: false, CeilingKind.None);
                    h.KeepStyleRooms = false;
                    foreach (var t in types)
                    {
                        if (t == RoomType.Utility) continue; // the plant room keeps the style's
                        switch (t)
                        {
                            case RoomType.Living: case RoomType.Bedroom: case RoomType.FlatHall:
                                looks.Add(Room(t, t == RoomType.Living ? "Home_Wallpaper" : t == RoomType.Bedroom ? "Home_WallpaperBed" : "Home_WallpaperHall", 1.2f, null, 0f, "Home_Ceiling", 2f));
                                floors.Add(Under(t, "Home_Parquet", 1.5f));
                                break;
                            case RoomType.Kitchen:
                                looks.Add(Room(t, "Home_KitchenWall", 2f, "Home_KitchenTile", 1.5f, "Home_Ceiling", 2f, 1.2f));
                                floors.Add(Under(t, "Home_KitchenFloor", 1.2f));
                                break;
                            case RoomType.Bathroom:
                                looks.Add(Room(t, "Home_BathTile", 1.2f, null, 0f, "Home_Ceiling", 2f));
                                floors.Add(Under(t, "Home_BathFloor", 0.8f));
                                break;
                            default:
                                looks.Add(Room(t, "Home_StairWall", 2f, "Home_StairLower", 1.5f, "Home_Ceiling", 2f, 2f));
                                floors.Add(Under(t, "Home_StairFloor", 1.2f));
                                break;
                        }
                    }
                    h.Grade.Temperature = 5f;
                    h.Grade.Saturation = 4f;
                    h.Air.AmbientTint = new Color(1.04f, 1f, 0.95f);
                    break;

                case HouseKind.Abandoned:
                    // a dead research station: steel panels and floor plates, vented ceilings, rust, decals of decay, its own
                    // battered furniture, and the sick green light that is this house's alone
                    PlainTrim(h, pipes: true, CeilingKind.Trays);
                    h.KeepStyleRooms = false;
                    h.Wall = K("Dr_SteelPanels"); h.WallTile = 2.4f;
                    h.Floor = K("Dr_FloorPlate"); h.FloorTile = 2f;
                    h.CeilingFinish = K("Dr_CeilingVents"); h.CeilingTile = 2.4f;
                    h.Trim = h.Pipe = K("Dr_Rust");
                    h.Beam = K("Dr_PaintedMetal");
                    h.FloorDecay = KitDecals("Dc_floor_rust", "Dc_floor_oil", "Dc_floor_scorch", "Dc_floor_rubble");
                    h.WallDecay = KitDecals("Dc_wall_rust", "Dc_wall_rust", "Dc_wall_leak", "Dc_wall_mold", "Dc_wall_peel", "Dc_wall_scorch"); // rust twice as often
                    h.Posters = KitDecals(Enumerable.Range(0, 6).Select(i => $"Dc_poster_{i}").ToArray());
                    h.DecayDensity = 0.14f;
                    h.PosterChance = 0.25f;
                    foreach (var t in types)
                    {
                        bool wet = t is RoomType.Restroom or RoomType.LockerRoom or RoomType.Kitchen;
                        bool quarters = t is RoomType.Bedroom or RoomType.Lounge or RoomType.Library or RoomType.Office;
                        if (wet) looks.Add(Room(t, "Dr_SteelPanels", 2.4f, "Wall_TilesWhite", 1.4f));
                        if (quarters || wet || t is RoomType.Infirmary or RoomType.Lab) floors.Add(Under(t, "Dr_Vinyl", 2f));
                    }
                    h.Furniture = ModelFamily.Derelict;
                    h.FurnitureIn = scanned;
                    h.Grade.Exposure = -0.05f;
                    h.Grade.Saturation = -8f;
                    h.Grade.Tint = -8f;
                    h.Grade.ColorFilter = new Color(0.93f, 1f, 0.95f);
                    h.Grade.SplitShadows = new Color(0.38f, 0.52f, 0.47f);
                    h.Grade.SplitHighlights = new Color(0.53f, 0.54f, 0.46f);
                    h.Grade.Split = 0.8f;
                    h.Grade.FilmGrain = 0.12f;
                    h.Grade.Vignette = 0.06f;
                    h.Air.AmbientTint = new Color(0.9f, 1.04f, 0.97f);
                    h.Air.Fog = new Color(0.011f, 0.024f, 0.019f);
                    h.Air.FogColor = 1f;
                    h.Air.FogDensity = 1.35f;
                    break;
            }
            h.RoomLooks = looks.ToArray();
            h.RoomFloors = floors.ToArray();
        }
    }
}
