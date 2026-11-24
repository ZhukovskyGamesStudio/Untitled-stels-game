using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The level currently built on this peer (layout + scene root).</summary>
    public static class ActiveLevel
    {
        public static LevelLayout Layout { get; private set; }
        public static GameObject Root { get; private set; }
        /// <summary>Surfaces of the house (walls, floors, openings) that visual styles dress.</summary>
        public static LevelShell Shell { get; private set; }
        /// <summary>The prefabs the level was built from (the hammer's hole cuts its slab with them).</summary>
        public static LevelArt Art { get; private set; }
        /// <summary>The furniture placed in the house (one object per prop).</summary>
        public static IReadOnlyList<GameObject> Props => s_Props;
        static readonly List<GameObject> s_Props = new();
        static readonly List<Mesh> s_Meshes = new();

        /// <summary>The level stands whole (not being built a piece at a time).</summary>
        public static bool Ready { get; private set; }

        /// <summary>Builds <paramref name="layout"/> from the prefabs in <paramref name="art"/>, replacing the previous level.</summary>
        public static GameObject Load(LevelLayout layout, LevelArt art)
        {
            foreach (var _ in LoadSteps(layout, art)) { }
            return Root;
        }

        /// <summary>
        /// The same a piece at a time (<see cref="LevelBuilder.Steps"/>): the layout and the root are there from the first
        /// step, <see cref="Ready"/> only after the last.
        /// </summary>
        public static IEnumerable<bool> LoadSteps(LevelLayout layout, LevelArt art)
        {
            foreach (var step in ClearSteps()) yield return step; // the house before comes down a piece at a time (on the drive)
            Layout = layout;
            Art = art;
            var builder = new LevelBuilder(art, layout);
            Shell = builder.Shell;
            foreach (var _ in builder.Steps())
            {
                Root = builder.RootObject;
                yield return true;
            }
            Root = builder.RootObject;
            s_Props.AddRange(builder.Props);
            s_Meshes.AddRange(builder.Meshes);
            if (Application.isPlaying && Root.TryGetComponent<LightBudget>(out var budget)) budget.ApplyNow();
            Ready = true;
        }

        /// <summary>Children of the old root destroyed per step when it comes down a piece at a time.</summary>
        const int DismantlePerStep = 150;

        /// <summary>
        /// Takes the level away a piece at a time: at once it is no longer the level (no layout, no root — the next can be
        /// built meanwhile), its navmesh is gone at once, then its pieces are destroyed a batch a step (one destroy of a
        /// whole house was a long frame on the drive). Outside play mode, at once.
        /// </summary>
        public static IEnumerable<bool> ClearSteps()
        {
            var root = Root;
            if (root == null || !Application.isPlaying) { Clear(); yield break; }
            var meshes = new List<Mesh>(s_Meshes);
            s_Meshes.Clear();
            Root = null;
            Clear(); // (the rest of the state; the root is ours now)
            root.name += " (leaving)";
            if (root.TryGetComponent<Unity.AI.Navigation.NavMeshSurface>(out var surface)) surface.enabled = false; // its navmesh off now
            var t = root.transform;
            int n = 0;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                if (t == null) yield break;
                if (i >= t.childCount) continue;
                Object.Destroy(t.GetChild(i).gameObject);
                if (++n % DismantlePerStep == 0) yield return true;
            }
            if (root != null) Object.Destroy(root);
            foreach (var m in meshes)
                if (m != null) Object.Destroy(m);
        }

        public static void Clear()
        {
            Ready = false;
            if (Root != null)
            {
                if (Application.isPlaying) Object.Destroy(Root);
                else Object.DestroyImmediate(Root);
            }
            Root = null;
            Layout = null;
            Shell = null;
            s_Props.Clear();
            foreach (var m in s_Meshes)
                if (m != null)
                {
                    if (Application.isPlaying) Object.Destroy(m);
                    else Object.DestroyImmediate(m);
                }
            s_Meshes.Clear();
        }
    }

    /// <summary>
    /// The soft warm light of a house (Data/Config/Level → Building → Mood): its lamps' colours pulled to a warm tint and
    /// dimmed a little, the style's ambient (the picture's fill light) warmed and lifted so shadows are less harsh.
    /// </summary>
    [System.Serializable]
    public class LightMood
    {
        [Tooltip("The warm tint the lamps' colours are multiplied towards (an incandescent bulb).")]
        public Color Tint = new(1f, 0.82f, 0.62f);
        [Tooltip("How far the lamps go to the tint: 0 keeps their own colours, 1 multiplies by the tint in full.")]
        [Range(0f, 1f)] public float Warmth = 0.6f;
        [Tooltip("The lamps' brightness, share of their own: under 1 is softer. Robots see by it too.")]
        [Range(0.4f, 1.2f)] public float Brightness = 0.88f;
        [Tooltip("The tint of the ambient light (the picture only: robots do not see by it).")]
        public Color AmbientTint = new(1f, 0.86f, 0.7f);
        [Tooltip("How far the ambient goes to its tint.")]
        [Range(0f, 1f)] public float AmbientWarmth = 0.7f;
        [Tooltip("The ambient's strength, share of the style's own: over 1 fills the shadows (softer contrast).")]
        [Range(0.5f, 2f)] public float AmbientFill = 1.25f;

        /// <summary>A lamp's colour in this mood (pure): its own, multiplied towards the tint.</summary>
        public Color Lamp(Color own) => Mul(own, Color.Lerp(Color.white, Tint, Warmth));

        /// <summary>The ambient in this mood (pure): the style's own, warmed and lifted.</summary>
        public Color Ambient(Color own) => Mul(own, Color.Lerp(Color.white, AmbientTint, AmbientWarmth)) * AmbientFill;

        static Color Mul(Color a, Color b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
    }

    /// <summary>Light of a room by its type (pure, unit tested).</summary>
    public static class RoomLighting
    {
        /// <summary>Ceiling panels sit this far either side of the room centre (along x).</summary>
        public const float LampOffset = 2.5f;

        /// <summary>The soft warm light of this house, or null: the hospital and the abandoned station keep theirs (<see cref="HouseRules.WarmLight"/>).</summary>
        public static LightMood MoodOf(LevelLayout l) => l != null && l.Mood != null && HouseRules.Of(l).WarmLight ? l.Mood : null;

        /// <summary>A room's light in a house's mood (pure): <see cref="For(RoomType, bool, float)"/>, then warmer and softer.</summary>
        public static (Color color, float intensity) For(RoomType room, bool dark, float dimShare, LightMood mood)
        {
            var (color, intensity) = For(room, dark, dimShare);
            return mood == null ? (color, intensity) : (mood.Lamp(color), intensity * mood.Brightness);
        }

        public static (Color color, float intensity) For(RoomType room, bool dark, float dimShare = 0f)
        {
            if (dark && dimShare <= 0f) return (new Color(1f, 0.95f, 0.85f), 0f); // switched off: exit signs, lamps and the moon only
            if (dark) return (new Color(1f, 0.88f, 0.72f), LightMath.RoomIntensity * dimShare); // a dim lamp: a weak warm glow
            return room switch
            {
                RoomType.Server => (new Color(0.55f, 0.75f, 1f), LightMath.RoomIntensity * 0.6f),
                RoomType.Storage => (new Color(1f, 0.92f, 0.8f), LightMath.RoomIntensity * 0.75f),
                RoomType.Lobby => (new Color(0.92f, 1f, 0.95f), LightMath.RoomIntensity * 1.15f),
                // the shop: the hall under cold daylight tubes, the paint booth flooded white (colours must be judged true)
                RoomType.Workshop => (new Color(0.9f, 0.96f, 1f), LightMath.RoomIntensity * 1.1f),
                RoomType.PaintBooth => (new Color(0.95f, 0.98f, 1f), LightMath.RoomIntensity * 1.3f),
                RoomType.Utility => (new Color(1f, 0.9f, 0.75f), LightMath.RoomIntensity * 0.7f), // a bare bulb over the pipes
                // the hospital: theatres flooded cold white, imaging dimmed for the screens, wards warm
                RoomType.Operating => (new Color(0.92f, 0.97f, 1f), LightMath.RoomIntensity * 1.35f),
                RoomType.Imaging => (new Color(0.85f, 0.9f, 1f), LightMath.RoomIntensity * 0.7f),
                RoomType.Ward => (new Color(1f, 0.93f, 0.84f), LightMath.RoomIntensity * 0.9f),
                // the flats: warm ceiling lamps, a cooler bathroom, a dim hallway
                RoomType.Living or RoomType.Bedroom => (new Color(1f, 0.86f, 0.68f), LightMath.RoomIntensity * 0.85f),
                RoomType.FlatHall => (new Color(1f, 0.88f, 0.72f), LightMath.RoomIntensity * 0.7f),
                RoomType.Bathroom => (new Color(0.95f, 0.97f, 1f), LightMath.RoomIntensity * 0.9f),
                _ => (new Color(1f, 0.95f, 0.86f), LightMath.RoomIntensity),
            };
        }
    }

    /// <summary>
    /// Turns a <see cref="LevelLayout"/> into scene objects. Everything is instantiated from the prefabs of
    /// <see cref="LevelArt"/>: structure blocks are unit cubes scaled (and turned) to size, pieces (stairs, doors, vents,
    /// lights, lamps) and furniture are placed as they are. Walls follow the plan (<see cref="HousePlan.Walls"/>):
    /// straight, diagonal and arcs of short pieces, railings on the edges of galleries; slabs follow its raster.
    /// </summary>
    public sealed class LevelBuilder
    {
        readonly LevelArt m_Art;
        readonly LevelLayout m_Layout;
        readonly HousePlan m_Plan;
        Transform m_Root;
        int m_Floor;

        const float RailingHeight = 1.05f, RailingThickness = 0.08f;

        /// <summary>Surfaces recorded while building.</summary>
        public LevelShell Shell { get; } = new();
        /// <summary>The furniture placed (not static-batched: styles swap its meshes; <see cref="PropBatches"/> merges it).</summary>
        public List<GameObject> Props { get; } = new();
        /// <summary>Meshes made while building (the city's): destroyed with the level.</summary>
        public List<Mesh> Meshes { get; } = new();

        public LevelBuilder(LevelArt art, LevelLayout layout)
        {
            m_Art = art;
            m_Layout = layout;
            m_Plan = layout.Plan;
        }

        /// <summary>The level's root while it is being built (and after).</summary>
        public GameObject RootObject => m_Root != null ? m_Root.gameObject : null;

        /// <summary>Builds the whole level at once.</summary>
        public GameObject Build()
        {
            foreach (var _ in Steps()) { }
            return RootObject;
        }

        /// <summary>
        /// Builds the level a piece at a time (each step a few hundredths of a second): the drive to the next house builds it
        /// over several frames while the drive scene goes on (<see cref="ActiveLevel.LoadSteps"/>).
        /// </summary>
        /// <summary>
        /// A piece's screens with nothing to show (the models' glowing screen material): off, static, no signal, the boot menu
        /// or cracked, by whose screen it is and the house's seed (<see cref="ScreenLooks"/>) — before the style dresses the
        /// piece. Little displays keep their glow.
        /// </summary>
        void DressScreens(GameObject go, PropKind kind, int seed)
        {
            var screens = m_Art.Screens;
            if (screens == null || screens.Length == 0 || screens[0] == null || ScreenLooks.InUse(kind)) return;
            int picture = ScreenLooks.DisplayOf(kind);
            if (picture >= 0 && m_Art.Displays != null)
            {
                // an appliance's own little display: what such a thing shows, or dark
                bool dark = ScreenLooks.DisplayDark(seed);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.sharedMaterial != screens[0]) continue;
                    r.sharedMaterial = dark ? screens[1 + (int)ScreenLook.Off] : m_Art.Displays;
                    if (!dark) LiveScreens.AddDisplay(r, picture);
                }
                return;
            }
            var look = ScreenLooks.Pick(seed, ScreenLooks.KindOf(kind));
            var to = 1 + (int)look < screens.Length ? screens[1 + (int)look] : null;
            if (to == null) return;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial != screens[0]) continue;
                var size = r.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < ScreenLooks.SmallScreen) continue;
                r.sharedMaterial = to;
                LiveScreens.Add(r, look);
            }
        }

        /// <summary>The piece's own picture (<see cref="PropPictures"/>): its board's writing, its mirror's state.</summary>
        void DressPictures(GameObject go, PropKind kind, int seed)
        {
            if (m_Art.Pictures == null || !PropPictures.TryPick(kind, m_Layout.Kind, seed, out var pick) || (int)pick.Kind >= m_Art.Pictures.Length) return;
            var material = m_Art.Pictures[(int)pick.Kind];
            if (material == null) return;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (pick.Part != null ? !r.name.StartsWith(pick.Part, System.StringComparison.Ordinal) : r.sharedMaterial != m_Art.Mirror) continue;
                r.sharedMaterial = material;
                LiveScreens.AddPicture(r, pick.Faces);
            }
        }

        public IEnumerable<bool> Steps()
        {
            var l = m_Layout;
            var root = Object.Instantiate(m_Art.LevelRoot);
            root.name = $"Level_{l.Seed}";
            m_Root = root.transform;
            LiveScreens.Clear();

            float W = l.SizeX, H = l.SizeZ;
            // Ground under the building, slabs between floors (holes for stairs and atriums), the roof, the yard and its fence.
            for (int f = 0; f < l.Floors; f++) BuildSlab(f);
            BuildRoof();
            BuildGrates();
            float yard = CityPlan.Yard, far = 2f * CityPlan.Reach;
            // the ground outside is the street's level (a kerb under the house's floor); the pavements stand on it (BuildCity)
            // (with holes where the shafts go down — to the basement and from the exit hatch: pieces round them)
            float gy = CityPlan.RoadTop - 0.06f;
            var ground = Rect.MinMaxRect(W / 2 - (W + far) / 2, H / 2 - (H + far) / 2, W / 2 + (W + far) / 2, H / 2 + (H + far) / 2);
            foreach (var (a, b) in ArchitecturePlanner.CutHoles(ground.min, ground.max, l.ShaftHoles))
                Block(m_Art.OutsideGround, "OutsideGround", new Vector3((a.x + b.x) / 2f, gy, (a.y + b.y) / 2f), new Vector3(b.x - a.x, 0.1f, b.y - a.y));
            // the invisible wall round the yard; the street and the city beyond it are scenery (BuildCity)
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 4f, -yard - 0.5f), new Vector3(W + 2f * yard + 2f, 8f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 4f, H + yard + 0.5f), new Vector3(W + 2f * yard + 2f, 8f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(-yard - 0.5f, 4f, H / 2), new Vector3(1f, 8f, H + 2f * yard + 2f));
            Block(m_Art.Fence, "Fence", new Vector3(W + yard + 0.5f, 4f, H / 2), new Vector3(1f, 8f, H + 2f * yard + 2f));
            yield return true;

            m_WindowCuts.Clear();
            foreach (var cut in WindowCuts(l))
            {
                if (!m_WindowCuts.TryGetValue(cut.wall, out var at)) m_WindowCuts[cut.wall] = at = new List<float>();
                at.Add(cut.at);
            }
            for (int f = 0; f < l.Floors; f++)
            {
                m_Floor = f;
                BuildWalls(f);
                yield return true;
            }
            foreach (var r in l.Rooms) BuildLights(r);
            foreach (var s in l.Stairs) BuildStair(s);
            yield return true;
            for (int i = 0; i < l.AutoDoors.Count; i++)
            {
                var d = l.AutoDoors[i];
                bool vault = i == l.VaultDoor && m_Art.VaultDoor != null;
                var rot = d.rotation;
                if (vault && l.VaultRoom >= 0 && Vector3.Dot(l.Plan.Rooms[l.VaultRoom].Center - d.position, d.rotation * Vector3.forward) < 0f)
                    rot *= Quaternion.Euler(0f, 180f, 0f); // the round door's +Z into the vault
                // the kind of door (DoorRules: wood between rooms, a flat's padded steel, the hospital's laminate, glass sliding
                // doors where the tech is…), which way it opens (a hinged door's prefab opens to its +Z) and the hinges' jamb
                var spec = DoorRules.Of(l, i);
                bool hinged = DoorRules.Hinged(spec.Kind);
                if (hinged && spec.Back) rot *= Quaternion.Euler(0f, 180f, 0f);
                var prefab = vault ? m_Art.VaultDoor : m_Art.Door(spec.Kind, l.DoorWidthOf(i) < LevelMetrics.DoorWidth - 0.01f);
                var door = Place(prefab, vault ? "VaultDoor" : $"Door{spec.Kind}", d.position, rot).GetComponent<AutoDoor>();
                if (door != null)
                {
                    door.Spot = i;
                    if (hinged && spec.HingeRight) door.HingeRight();
                    if (door.Number != null) door.Number.text = FlatNumber(l, i);
                    bool keyed = false;
                    foreach (var k in l.KeyDoors) keyed |= k.Door == i;
                    bool storage = i == l.StorageDoor && StorageRooms.Locked(l);
                    var inward = Vector3.zero;
                    var room = default(Rect);
                    if (storage)
                    {
                        // which way the room is: across the doorway, towards the storage room's middle
                        var across = d.rotation * Vector3.forward;
                        var to = l.Plan.Rooms[l.StorageRoom].Center - d.position;
                        inward = Vector3.Dot(to, across) >= 0f ? across : -across;
                        inward.y = 0f;
                        inward.Normalize();
                        room = l.Plan.Rooms[l.StorageRoom].Bounds;
                    }
                    door.Configure(i, l.LockedDoors.Contains(i), keyed, i == l.VaultDoor, storage, inward, room, DoorRules.LockOf(l.PlanKind, spec.Kind));
                    if (storage && m_Art.ExitButton != null)
                    {
                        // the way out for whoever is locked in: a push button on the room's side of the wall, on the side of
                        // the doorway that is this room's wall and has no light switch
                        var along = Vector3.Cross(Vector3.up, inward);
                        float clear = LevelMetrics.ClearWidth(LevelMetrics.DoorWidth);
                        float side = 1f, best = float.MinValue;
                        foreach (float s in new[] { 1f, -1f })
                        {
                            var at = ExitButton.Spot(d.position, inward, along, clear, s);
                            float score = l.Plan.RoomAt(at + inward * 0.4f)?.Id == l.StorageRoom ? 10f : 0f;
                            float nearest = 3f;
                            foreach (var sw in UtilityPlan.SwitchSpots(l, Shell)) nearest = Mathf.Min(nearest, (sw.Position - at).magnitude);
                            score += nearest;
                            if (score > best) { best = score; side = s; }
                        }
                        var button = Place(m_Art.ExitButton, "ExitButton", ExitButton.Spot(d.position, inward, along, clear, side), Quaternion.LookRotation(inward));
                        if (button.TryGetComponent<ExitButton>(out var exit)) exit.Setup(0, i);
                    }
                    if (door.WallParts != null)
                        foreach (var part in door.WallParts)
                            if (part != null) Shell.WallParts.Add(part);
                }
                // what is mounted over the doorway stays in sight: the pipes along the wall keep off it (StyleDecor)
                var over = i == l.VaultDoor ? VaultSign : spec.Kind == DoorKind.Storage ? StorageSign : default;
                if (over.HalfWidth > 0f)
                {
                    over.Center = d.position + Vector3.up * over.Center.y;
                    over.Along = d.rotation * Vector3.right;
                    over.Floor = LevelLayout.FloorOf(d.position.y);
                    Shell.Signs.Add(over);
                }
                if (i == l.VaultDoor && m_Art.VaultPanel != null)
                {
                    // the vault's plate with its three lamps over the doorway, on both faces of the wall
                    var across = d.rotation * Vector3.forward;
                    foreach (float side in new[] { 1f, -1f })
                        Place(m_Art.VaultPanel, "VaultPanel", d.position + Vector3.up * VaultSign.Center.y + across * (side * (LevelMetrics.WallThickness / 2f + 0.01f)),
                            Quaternion.LookRotation(across * side));
                }
            }
            if (m_Art.VaultConsole != null)
                for (int i = 0; i < l.VaultConsoles.Count; i++)
                    Place(m_Art.VaultConsole, $"VaultConsole{i}", l.VaultConsoles[i].position, l.VaultConsoles[i].rotation).GetComponent<VaultConsole>()?.Configure(i);
            BuildFacade();
            yield return true;
            if (Application.isPlaying) CombineStatic(root);
            yield return true;
            BuildCity(W, H); // one mesh of its own: not static-batched
            yield return true;
            BuildUtilities();
            BuildSwitches();
            yield return true;

            for (int i = 0; i < l.Props.Count; i++)
            {
                var p = l.Props[i];
                var prefab = m_Art.Prop(p.Kind);
                if (prefab == null) continue;
                var go = Place(prefab, p.Kind.ToString(), p.Position, Quaternion.Euler(0, p.RotationY, 0));
                if (p.Scale > 0f) go.transform.localScale = Vector3.one * p.Scale;
                RecordProp(go, p.Kind, LevelLayout.FloorOf(p.Position.y));
                if (go.TryGetComponent<Items.SafeBox>(out var safe)) safe.Configure(i, l.Seed);
                if (go.TryGetComponent<Interactable>(out var use)) use.Configure(i);
                if (go.TryGetComponent<Items.Storage>(out var storage)) storage.Configure(i);
                if (go.TryGetComponent<PropVariants>(out var variants)) variants.Dress(LevelRandom.Mix(l.Seed, Salt.PropVariants, i)); // its coats, its books
                DressScreens(go, p.Kind, LevelRandom.Mix(l.Seed, Salt.Screens, i));
                DressPictures(go, p.Kind, LevelRandom.Mix(l.Seed, Salt.Pictures, i));
                if (go.TryGetComponent<SecurityTerminal>(out var terminal))
                    foreach (var t in l.Terminals)
                        if (t.prop == i) terminal.Configure(i, t.kind, l.KioskTerminals.Contains(i));
                Props.Add(go);
                if (i % 80 == 79) yield return true;
            }
            BuildBasement(); // after the props: the lid over the shaft is one
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The footprint of the placed model on the map (glowing bits such as lamp shades and screens are left out).</summary>
        void RecordProp(GameObject go, PropKind kind, int floor)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name.StartsWith("Eye")) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any || kind is PropKind.Hatch or PropKind.ShaftHatch) return;
            Shell.Props.Add(new MapProp { Floor = floor, Kind = kind, Area = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z) });
        }


        GameObject Place(GameObject prefab, string name, Vector3 position, Quaternion rotation)
        {
            var go = Object.Instantiate(prefab, position, rotation, m_Root);
            go.name = name;
            return go;
        }

        /// <summary>A unit-cube prefab stretched to <paramref name="size"/> (and turned by <paramref name="rotation"/>).</summary>
        GameObject Block(GameObject prefab, string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var go = Place(prefab, name, center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            return go;
        }

        // ------------------------------------------------------------------ slabs

        /// <summary>
        /// The slab under floor <paramref name="f"/> (the ground slab for floor 0): everywhere the floor has rooms or
        /// pockets, minus the holes of stairs arriving from below; atriums (voids) have none. Cells merge into rectangles.
        /// </summary>
        void BuildSlab(int f)
        {
            var plan = m_Plan;
            var holes = new List<Rect>();
            foreach (var s in m_Layout.Stairs) if (s.Floor + 1 == f) holes.Add(PlanGenerator.HoleOf(s));
            if (m_Layout.HasLift) holes.Add(m_Layout.LiftWell); // the lift's cab rides through every floor (a pit under the ground floor: its floor is the cab's)
            if (f == 0) // the shaft down to the basement, the exit hatch's well (3 x 3 cells each)
                holes.AddRange(m_Layout.ShaftHoles);
            bool Solid(int x, int z)
            {
                var k = plan.KindAt(f, x, z);
                if (k != CellKind.Room && k != CellKind.Pocket) return false;
                var c = HousePlan.CellCenter(x, z);
                foreach (var h in holes) if (h.Contains(c)) return false;
                return true;
            }
            float thick = f == 0 ? 0.2f : LevelMetrics.SlabThickness;
            float y = f == 0 ? -0.1f : LevelLayout.FloorY(f) - LevelMetrics.SlabThickness / 2f;
            // the ducts of the void under this floor run through it: a deck on top, a ceiling board under (a grate's hole in it)
            var net = f >= 1 && f - 1 < m_Layout.Vents.Count ? m_Layout.Vents[f - 1] : null;
            bool Duct(int x, int z) => net != null && net.Cells.Contains(new Vector2Int(x / 2, z / 2));
            foreach (var r in Rectangles(plan.NX, plan.NZ, (x, z) => Solid(x, z) && !Duct(x, z)))
                SlabPiece(f == 0 ? "Floor" : "Slab", m_Art.FloorSlab, r, y, thick, f, true);
            if (net != null) DuctPieces(m_Art.FloorSlab, net, (x, z) => Solid(x, z) && Duct(x, z), f, LevelLayout.FloorY(f));
        }

        void SlabPiece(string name, GameObject prefab, RectInt r, float y, float thick, int floor, bool floorFinish)
        {
            float x0 = r.xMin * HousePlan.Res, x1 = r.xMax * HousePlan.Res, z0 = r.yMin * HousePlan.Res, z1 = r.yMax * HousePlan.Res;
            Block(prefab, name, new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, thick, z1 - z0));
            if (floorFinish) Shell.Floors.Add(new FloorRect { Min = new Vector2(x0, z0), Max = new Vector2(x1, z1), Y = y + thick / 2f, Floor = floor });
        }

        /// <summary>
        /// Over the cells of a duct: the deck (the floor above, its top at <paramref name="top"/>) and the ceiling board of the
        /// room below — but where a grate is. Between them the duct's headroom (<see cref="LevelMetrics.VoidHeight"/>).
        /// </summary>
        void DuctPieces(GameObject prefab, VentNet net, System.Func<int, int, bool> cell, int floor, float top)
        {
            var plan = m_Plan;
            foreach (var r in Rectangles(plan.NX, plan.NZ, cell))
                SlabPiece("Deck", prefab, r, top - LevelMetrics.DeckThickness / 2f, LevelMetrics.DeckThickness, floor, floor < m_Layout.Floors);
            float ceiling = top - LevelMetrics.SlabThickness;
            bool Board(int x, int z) => cell(x, z) && !net.IsGrate(new Vector2Int(x / 2, z / 2));
            foreach (var r in Rectangles(plan.NX, plan.NZ, Board))
                SlabPiece("CeilingBoard", m_Art.FloorSlab, r, ceiling + LevelMetrics.CeilingBoard / 2f, LevelMetrics.CeilingBoard, floor, false);
            foreach (var g in net.Grates)
                Shell.CeilingHoles.Add(new CeilingHole { Floor = floor - 1, Area = new Rect(g.Cell.x * VentPlan.Res, g.Cell.y * VentPlan.Res, VentPlan.Res, VentPlan.Res) });
        }

        /// <summary>
        /// The basement the team arrives in (<see cref="Basement"/>), from the sewer's kit along the basement's frame: the way
        /// back behind an invisible wall (a few segments, then a cap), the car's stretch with the alcove and its shaft up to the
        /// lid, the shut gate and a segment beyond it closed by a cap; the sewer's lamps; the ladder's ends
        /// (<see cref="ShaftLadder"/>) and the storage robot's link up the shaft. (The street's lamps above, without shadows, are
        /// too high to reach its road: 11 m of range from about 5.5 m over the street, the road 4.9 m under it.)
        /// </summary>
        void BuildBasement()
        {
            if (m_Art.Sewer == null) return;
            var kit = m_Art.Sewer.GetComponent<SewerLocation>();
            if (kit == null || kit.Segments == null || kit.Segments.Length == 0) return;
            var frame = m_Layout.Cellar;
            GameObject Put(GameObject prefab, string name, Vector3 local, float yaw = 0f) =>
                prefab != null ? Place(prefab, name, frame.ToWorld(local), frame.Rotation * Quaternion.Euler(0f, yaw, 0f)) : null;
            const float seg = SewerPlan.SegmentLength;
            int seed = m_Layout.Seed & int.MaxValue;
            GameObject Segment(int i) => kit.Segments[(seed + i * 3) % kit.Segments.Length];
            // the way the car came by: seen, never walked or driven (the invisible wall across the tunnel), then a cap far in the dark
            var back = Put(kit.BackWall, "BasementBackWall", Vector3.zero);
            if (back != null) back.transform.localScale = new Vector3(1f, 0.75f, 1f); // up to the vault (4.5 m), not through the house's floor
            // (in the drive's pattern, DrivePath: the car arrives down this way from the endless tunnel, which carries on past
            // the cap while it arrives — the cap is hidden then)
            var pattern = kit.Tunnel != null && kit.Tunnel.Segments != null && kit.Tunnel.Segments.Length > 0 ? kit.Tunnel : null;
            for (int i = 1; i <= Basement.BackSegments; i++)
            {
                Put(pattern != null ? pattern.Segments[DrivePath.VariantOf(-i) % pattern.Segments.Length] : Segment(i + 5), $"BasementBack{i}", new Vector3(-i * seg, 0f, 0f));
                int side = DrivePath.LampSideOf(-i);
                var lamp = Put(kit.Lamp, $"BasementBackLamp{i}", new Vector3(-i * seg + seg / 2f, 2.3f, side * 3.55f), side > 0 ? 90f : -90f);
                SewerLamp.Set(lamp, DriveTunnel.LampColor, DriveTunnel.LampIntensity);
            }
            var cap = Put(kit.Cap, "BasementBackCap", new Vector3(-Basement.BackSegments * seg, 0f, 0f));
            if (pattern != null)
            {
                var approach = Put(pattern.gameObject, "BasementApproach", Vector3.zero).GetComponent<DriveTunnel>();
                approach.To = -Basement.BackSegments - 1;
                approach.HideWhileFollowing = cap;
            }
            // the car's stretch; its last segment has the alcove with the shaft and the ladder (its lid from below is the sewer's: here the real one is in the floor)
            for (int i = 0; i < Basement.CarSegments; i++)
            {
                bool alcove = i == Basement.CarSegments - 1 && kit.Alcove != null;
                var go = Put(alcove ? kit.Alcove : Segment(i), alcove ? "BasementAlcove" : $"BasementSegment{i}", new Vector3(i * seg, 0f, 0f));
                if (alcove) SetChild(go, "MetalShutLid", false);
            }
            // the gate the team opens from the far side after the house: shut here, its lever and the housing over the vault left out
            // (the housing would stand up through the house's floor)
            if (kit.GatePrefab != null)
            {
                var gate = Put(kit.GatePrefab.gameObject, "BasementGate", new Vector3(Basement.GateX, 0f, 0f));
                SetChild(gate, "ConcreteHousing", false);
                foreach (var lever in gate.GetComponentsInChildren<GateLever>(true)) lever.gameObject.SetActive(false);
                if (gate.TryGetComponent<SewerGate>(out var g) && g.LeverLight != null) g.LeverLight.gameObject.SetActive(false);
            }
            for (int i = 0; i < Basement.BeyondSegments; i++) Put(Segment(i + 2), $"BasementBeyond{i}", new Vector3(Basement.GateX + i * seg, 0f, 0f));
            Put(kit.Cap, "BasementFarCap", new Vector3(Basement.GateX + Basement.BeyondSegments * seg, 0f, 0f), 180f);
            // lamps: the car's stretch lit on alternate walls (opposite the alcove beside it), the way back dimmer and dimmer
            var warm = new Color(1f, 0.8f, 0.55f);
            void Lamp(string name, float x, int side, float share, float range)
            {
                SewerLamp.Scale(Put(kit.Lamp, name, new Vector3(x, 2.3f, side * 3.55f), side > 0 ? 90f : -90f), warm, share, range);
            }
            // (at the lamps' full strength: this is where the team arrives and finds the ladder)
            Lamp("BasementLamp", seg * 0.5f, 1, 1f, 12f);
            Lamp("BasementLamp", seg * 1.5f, -1, 1f, 12f);
            Lamp("BasementLamp", Basement.AlcoveX, -1, 1f, 12f);
            // the ladder's ends: under the lid in the lobby (built with it, as a prop) and at its foot
            var shaft = m_Layout.HatchPosition;
            var path = Basement.ClimbPath(frame, shaft, m_Layout.ShaftExit);
            if (m_Art.ShaftLadder != null)
            {
                var foot = Place(m_Art.ShaftLadder, "ShaftLadderFoot", Basement.LadderFoot(frame), Quaternion.identity);
                if (foot.TryGetComponent<ShaftLadder>(out var ladder)) ladder.Setup(path, shaft, OpenKeys.Shaft);
            }
            // the storage robot's way up and down: a navmesh link from the ladder's foot to the lobby's floor, open with the lid
            if (m_Art.ShaftLink != null)
            {
                var go = Place(m_Art.ShaftLink, "ShaftLink", Vector3.zero, Quaternion.identity);
                if (go.TryGetComponent<Unity.AI.Navigation.NavMeshLink>(out var link))
                {
                    link.startPoint = path[0];
                    link.endPoint = path[path.Length - 1];
                    link.UpdateLink();
                    foreach (var lid in m_Root.GetComponentsInChildren<ShaftHatch>(true)) if (!lid.Exit) lid.Link = link; // (the entry's)
                    link.activated = false;
                }
            }
        }

        /// <summary>Shows or hides a child (any depth) of a placed kit piece by its name.</summary>
        static void SetChild(GameObject go, string name, bool on)
        {
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == name) t.gameObject.SetActive(on);
        }

        /// <summary>The grates in the ceilings (the way into the ducts), every storey's.</summary>
        void BuildGrates()
        {
            if (m_Art.VentGrate == null) return;
            int index = 0;
            foreach (var net in m_Layout.Vents)
                foreach (var g in net.Grates)
                {
                    var go = Place(m_Art.VentGrate, $"VentGrate_{index}", g.Position, Quaternion.Euler(0f, VentPlan.LidYaw(g.Position, m_Layout.Doorways), 0f));
                    if (go.TryGetComponent<VentGrate>(out var grate)) grate.Setup(index, g.Room, g.Closed);
                    index++;
                }
        }

        /// <summary>The roof over the top floor (over its rooms, pockets and the top of atriums).</summary>
        void BuildRoof()
        {
            int top = m_Layout.Floors - 1;
            float y = LevelLayout.FloorY(top + 1) - LevelMetrics.SlabThickness / 2f;
            var net = top < m_Layout.Vents.Count ? m_Layout.Vents[top] : null;
            bool In(int x, int z) => x >= 0 && z >= 0 && x < m_Plan.NX && z < m_Plan.NZ && m_Plan.KindAt(top, x, z) != CellKind.Outside;
            bool Duct(int x, int z) => net != null && net.Cells.Contains(new Vector2Int(x / 2, z / 2));
            foreach (var r in Rectangles(m_Plan.NX, m_Plan.NZ, (x, z) => In(x, z) && !Duct(x, z)))
            {
                // the eaves: 0.2 m over the facade, only on the sides where the building ends
                bool Edge(int x0c, int x1c, int z0c, int z1c)
                {
                    for (int x = x0c; x <= x1c; x++) for (int z = z0c; z <= z1c; z++) if (In(x, z)) return false;
                    return true;
                }
                float ex0 = Edge(r.xMin - 1, r.xMin - 1, r.yMin, r.yMax - 1) ? 0.2f : 0f, ex1 = Edge(r.xMax, r.xMax, r.yMin, r.yMax - 1) ? 0.2f : 0f;
                float ez0 = Edge(r.xMin, r.xMax - 1, r.yMin - 1, r.yMin - 1) ? 0.2f : 0f, ez1 = Edge(r.xMin, r.xMax - 1, r.yMax, r.yMax) ? 0.2f : 0f;
                float x0 = r.xMin * HousePlan.Res - ex0, x1 = r.xMax * HousePlan.Res + ex1, z0 = r.yMin * HousePlan.Res - ez0, z1 = r.yMax * HousePlan.Res + ez1;
                Block(m_Art.Roof, "Roof", new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, LevelMetrics.SlabThickness, z1 - z0));
            }
            if (net != null) DuctPieces(m_Art.Roof, net, (x, z) => In(x, z) && Duct(x, z), m_Layout.Floors, LevelLayout.FloorY(top + 1));
        }

        /// <summary>Cells where <paramref name="solid"/> holds, merged into rectangles (widest run, then as tall as it stays).</summary>
        public static List<RectInt> Rectangles(int nx, int nz, System.Func<int, int, bool> solid)
        {
            var used = new bool[nx, nz];
            var list = new List<RectInt>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    if (used[x, z] || !solid(x, z)) continue;
                    int w = 0;
                    while (x + w < nx && !used[x + w, z] && solid(x + w, z)) w++;
                    int h = 1;
                    while (z + h < nz)
                    {
                        bool row = true;
                        for (int i = 0; i < w && row; i++) row = !used[x + i, z + h] && solid(x + i, z + h);
                        if (!row) break;
                        h++;
                    }
                    for (int i = 0; i < w; i++)
                        for (int j = 0; j < h; j++) used[x + i, z + j] = true;
                    list.Add(new RectInt(x, z, w, h));
                }
            return list;
        }

        // ------------------------------------------------------------------ walls

        /// <summary>Posts of the floor being built: by their place rounded to a centimetre (shared ends meet there), the exact place and the height.</summary>
        readonly Dictionary<Vector3, (Vector3 at, float height)> m_Posts = new();
        /// <summary>Where windows cut the outer walls: wall index → positions along it (<see cref="WindowCuts"/>).</summary>
        readonly Dictionary<int, List<float>> m_WindowCuts = new();
        /// <summary>Walls of the floor by their end points (cm keys): to find bent walls whose pieces meet at a shallow angle.</summary>
        readonly Dictionary<Vector3, List<int>> m_Ends = new();

        static Vector3 EndKey(Vector2 p) => new(Mathf.Round(p.x * 100f) / 100f, 0f, Mathf.Round(p.y * 100f) / 100f);

        /// <summary>
        /// The two walls meeting at <paramref name="p"/> continue each other at a shallow angle (the pieces of a chamfer or an
        /// arc, not a corner): their joint gets a post turned between them and decoration that meets at the joint.
        /// </summary>
        bool SmoothJoint(Vector2 p, out float yaw)
        {
            yaw = 0f;
            if (!m_Ends.TryGetValue(EndKey(p), out var walls) || walls.Count != 2) return false;
            var a = m_Plan.Walls[walls[0]];
            var b = m_Plan.Walls[walls[1]];
            if (a.Railing || b.Railing) return false;
            // directions pointing away from the joint
            var da = (EndKey(a.A) == EndKey(p) ? a.Dir : -a.Dir);
            var db = (EndKey(b.A) == EndKey(p) ? b.Dir : -b.Dir);
            float turn = 180f - Vector2.Angle(da, db); // 0 = straight on
            if (turn > 60f || turn < 1f) return false;
            var mean = (da - db).normalized; // along the wall through the joint
            yaw = Mathf.Atan2(mean.x, mean.y) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// Walls of a floor without overlapping geometry (overlaps z-fight and look like clipping): every piece stops half a
        /// wall thickness short of its ends, and every end gets one post. Openings are cut between the pieces; the lintel
        /// above a doorway fills exactly the gap. Railings edge the galleries over atriums.
        /// </summary>
        void BuildWalls(int f)
        {
            m_Posts.Clear();
            m_Ends.Clear();
            for (int i = 0; i < m_Plan.Walls.Count; i++)
            {
                var w = m_Plan.Walls[i];
                if (w.Floor != f || w.Railing) continue;
                foreach (var e in new[] { EndKey(w.A), EndKey(w.B) })
                {
                    if (!m_Ends.TryGetValue(e, out var l)) m_Ends[e] = l = new List<int>();
                    l.Add(i);
                }
            }
            for (int i = 0; i < m_Plan.Walls.Count; i++)
                if (m_Plan.Walls[i].Floor == f) Wall(i);
            const float t = LevelMetrics.WallThickness;
            float y = LevelLayout.FloorY(f);
            foreach (var kv in m_Posts)
            {
                // a post between two pieces of a bent wall turns with them: its faces run on with the wall's, no cube sticks out;
                // it stands at the walls' exact end (the rounded key moved it up to 5 mm out of the facade: a seam outside)
                var (p, h) = kv.Value;
                var at = new Vector2(p.x, p.z);
                var rot = SmoothJoint(at, out float yaw) ? Quaternion.Euler(0f, yaw, 0f) : (Quaternion?)null;
                Block(m_Art.WallBlock, "WallPost", p + Vector3.up * (y + h / 2f), new Vector3(t, h, t), rot);
                RecordPost(kv.Key, p + Vector3.up * y, h, rot ?? Quaternion.identity);
            }
        }

        /// <summary>The faces of a post that no wall goes on from (<see cref="LevelShell.Posts"/>).</summary>
        void RecordPost(Vector3 key, Vector3 at, float height, Quaternion rot)
        {
            const float half = LevelMetrics.WallThickness / 2f;
            foreach (var axis in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
            {
                var n = rot * axis;
                if (GoesOn(key, new Vector2(n.x, n.z))) continue;
                var along = rot * new Vector3(axis.z, 0f, -axis.x);
                Shell.Posts.Add(new WallFace { A = at - along * half, B = at + along * half, Y0 = 0f, Y1 = height, Normal = n, Floor = m_Floor });
            }
        }

        /// <summary>A wall leaves this end point in that direction (the post's face is inside it).</summary>
        bool GoesOn(Vector3 key, Vector2 way)
        {
            if (!m_Ends.TryGetValue(key, out var walls)) return false;
            foreach (int i in walls)
            {
                var w = m_Plan.Walls[i];
                if (Vector2.Dot(EndKey(w.A) == key ? w.Dir : -w.Dir, way) < 0.99f) continue;
                bool open = false;
                foreach (var op in m_Plan.OpeningsOf(i)) open |= op.Kind == EdgeType.Open;
                if (!open) return true;
            }
            return false;
        }

        /// <summary>
        /// Past the post at this end of the wall another wall runs straight on (a T-junction seen from its flat side, a
        /// wall split where the room behind it changes): decoration of the two meets in the middle of the post.
        /// </summary>
        bool RunsOn(int index, Vector2 end)
        {
            if (!m_Ends.TryGetValue(EndKey(end), out var walls)) return false;
            var w = m_Plan.Walls[index];
            foreach (int i in walls)
            {
                if (i == index) continue;
                var o = m_Plan.Walls[i];
                if (o.Railing || Mathf.Abs(Vector2.Dot(o.Dir, w.Dir)) < 0.999f) continue;
                bool open = false;
                foreach (var op in m_Plan.OpeningsOf(i)) if (op.Kind == EdgeType.Open) open = true;
                if (!open) return true; // (an open passage is no wall: the post there is a free end)
            }
            return false;
        }

        void Post(Vector2 p, float height)
        {
            var key = new Vector3(Mathf.Round(p.x * 100f) / 100f, 0f, Mathf.Round(p.y * 100f) / 100f);
            m_Posts[key] = m_Posts.TryGetValue(key, out var was) ? (was.at, Mathf.Max(was.height, height)) : (new Vector3(p.x, 0f, p.y), height);
        }

        void Wall(int index)
        {
            var w = m_Plan.Walls[index];
            var openings = m_Plan.OpeningsOf(index);
            foreach (var o in openings) if (o.Kind == EdgeType.Open) return; // an open passage: no wall at all
            float y = LevelLayout.FloorY(w.Floor);
            var dir = w.Dir;
            var a3 = new Vector3(w.A.x, y, w.A.y);
            var b3 = new Vector3(w.B.x, y, w.B.y);
            var d3 = new Vector3(dir.x, 0f, dir.y);
            if (w.Railing)
            {
                RailingPiece(a3, b3);
                return;
            }
            // an outer wall runs up to the next floor (the roof): the slab's edge between the floors is behind it, the facade is one plane
            float height = w.FullHeight || w.Exterior ? LevelMetrics.FloorHeight : LevelMetrics.WallHeight;
            Post(w.A, height);
            Post(w.B, height);
            const float t = LevelMetrics.WallThickness;
            float len = w.Length;
            if (height < LevelMetrics.FloorHeight)
                foreach (var (u0, u1) in StairBands(w, m_Layout.Stairs))
                {
                    // over the stairwell no slab fills the band up to the next floor: the wall does
                    WallPiece(m_Art.WallBlock, a3 + d3 * u0, a3 + d3 * u1, LevelMetrics.WallHeight, LevelMetrics.FloorHeight);
                    if (u0 <= t) Post(w.A, LevelMetrics.FloorHeight);
                    if (u1 >= len - t) Post(w.B, LevelMetrics.FloorHeight);
                }
            var inner = InnerNormals(w);
            float from = t / 2f;
            bool jointA = SmoothJoint(w.A, out _), jointB = SmoothJoint(w.B, out _);
            bool cornerA = !jointA;
            m_StraightA = RunsOn(index, w.A);
            m_StraightB = RunsOn(index, w.B);
            // the cuts along the wall in order: the plan's openings and the windows (a real hole with glass in it)
            var cuts = new List<(float at, int opening)>();
            for (int k = 0; k < openings.Count; k++) cuts.Add((openings[k].At, k));
            if (m_WindowCuts.TryGetValue(index, out var windows)) foreach (float at in windows) cuts.Add((at, -1));
            cuts.Sort((x, y) => x.at.CompareTo(y.at));
            foreach (var cut in cuts)
            {
                if (cut.opening < 0)
                {
                    // a window: wall up to the sill, the hole (the Window prefab's glass fills it), wall above
                    float w0 = cut.at - LevelMetrics.WindowWidth / 2f, w1 = cut.at + LevelMetrics.WindowWidth / 2f;
                    float sillY = LevelMetrics.WindowCenterY - LevelMetrics.WindowHeight / 2f, topY = sillY + LevelMetrics.WindowHeight;
                    WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * w0, 0f, height);
                    RecordFaces(a3 + d3 * from, a3 + d3 * w0, 0f, height, w.Exterior, inner, cornerA, false, jointA && cornerA == false && from <= t / 2f + 0.001f);
                    WallPiece(m_Art.WallBlock, a3 + d3 * w0, a3 + d3 * w1, 0f, sillY);
                    RecordFaces(a3 + d3 * w0, a3 + d3 * w1, 0f, sillY, w.Exterior, inner, false, false);
                    WallPiece(m_Art.WallBlock, a3 + d3 * w0, a3 + d3 * w1, topY, height);
                    RecordFaces(a3 + d3 * w0, a3 + d3 * w1, topY, height, w.Exterior, inner, false, false);
                    from = w1;
                    cornerA = false;
                    continue;
                }
                var o = openings[cut.opening];
                if (o.Kind == EdgeType.Illusion)
                {
                    // the entrance of a hidden room: a wall to look at (with all its decor), nothing to bump into
                    float h0 = o.At - LevelMetrics.ClearWidth(o.Width) / 2f, h1 = o.At + LevelMetrics.ClearWidth(o.Width) / 2f;
                    WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * h0, 0f, height);
                    RecordFaces(a3 + d3 * from, a3 + d3 * h0, 0f, height, w.Exterior, inner, cornerA, false, jointA && cornerA == false && from <= t / 2f + 0.001f);
                    WallPiece(m_Art.IllusionBlock != null ? m_Art.IllusionBlock : m_Art.WallBlock, a3 + d3 * h0, a3 + d3 * h1, 0f, height);
                    RecordFaces(a3 + d3 * h0, a3 + d3 * h1, 0f, height, w.Exterior, inner, false, false);
                    from = h1;
                    cornerA = false;
                    continue;
                }
                bool vent = o.Kind == EdgeType.Vent, squeeze = o.Kind == EdgeType.Squeeze;
                bool breach = o.Kind == EdgeType.Breach, hole = o.Kind == EdgeType.Hole, window = o.Kind == EdgeType.InnerWindow;
                bool vault = IsVaultDoorway(index, o) || o.Kind == EdgeType.Lift, booth = IsBoothDoorway(index, o); // (the lift's portal, like the vault's ring: its own frame)
                bool lift = o.Kind == EdgeType.Lift;
                float clear = LevelMetrics.ClearWidth(lift ? LevelMetrics.LiftDoorWidth : vault ? LevelMetrics.VaultDoorWidth : booth ? LevelMetrics.VehicleDoorWidth
                    : o.Kind == EdgeType.Door && o.Width > 0.5f && o.Width < LevelMetrics.DoorWidth - 0.01f ? o.Width : LevelMetrics.OpeningWidth(o.Kind));
                float o0 = o.At - clear / 2f, o1 = o.At + clear / 2f;
                float sill = window ? LevelMetrics.InnerWindowSill : o.High ? LevelMetrics.HighVentSill : LevelMetrics.VentSill;
                float openH = vent ? sill + LevelMetrics.VentHeight
                    : window ? sill + LevelMetrics.InnerWindowHeight
                    : hole ? LevelMetrics.HoleHeight : LevelMetrics.DoorHeight;
                WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * o0, 0f, height);
                RecordFaces(a3 + d3 * from, a3 + d3 * o0, 0f, height, w.Exterior, inner, cornerA, false, jointA && cornerA == false && from <= t / 2f + 0.001f);
                if (vent || window)
                {
                    // the duct's floor: a sill of wall under the hole
                    WallPiece(m_Art.WallBlock, a3 + d3 * o0, a3 + d3 * o1, 0f, sill);
                    RecordFaces(a3 + d3 * o0, a3 + d3 * o1, 0f, sill, w.Exterior, inner, false, false);
                }
                // over a street door the facade runs on (the door's frame paints the inside): no trim-coloured lintel outside
                WallPiece(w.Exterior ? m_Art.WallBlock : m_Art.TrimBlock, a3 + d3 * o0, a3 + d3 * o1, openH, height);
                RecordFaces(a3 + d3 * o0, a3 + d3 * o1, openH, height, w.Exterior, inner, false, false);
                var mid = a3 + d3 * o.At;
                // a window between rooms has a sill like a vent and a frame of its own; breaches and holes are ruins, no door trim
                Shell.Openings.Add(new WallOpening { Center = mid, Along = d3, Width = clear, Height = openH, Vent = vent || window, Exterior = w.Exterior,
                    Squeeze = squeeze || breach || hole, Vault = vault, Floor = m_Floor });
                // The vent frame and the exit sign are modelled along local X; turn them to the wall direction.
                var along = Quaternion.LookRotation(new Vector3(-d3.z, 0, d3.x));
                if (lift) PlaceLiftDoor(mid, d3, inner);
                else if (vent)
                {
                    // shut by its grille until someone works it loose (the grille knows its doorway)
                    var frame = Place(o.High && m_Art.HighVentFrame != null ? m_Art.HighVentFrame : m_Art.VentFrame, "VentFrame", mid, along);
                    if (frame.TryGetComponent<VentGrille>(out var grille)) grille.Configure(DoorwayIndex(mid));
                }
                else if (window) { if (m_Art.InnerWindowFrame != null) Place(m_Art.InnerWindowFrame, "InnerWindow", mid, along); }
                else if (breach || hole)
                {
                    // one of the ways it broke, by the house's seed; the plaster round it is wall (each room's own finish)
                    var prefab = breach ? m_Art.BreachFrame : m_Art.HoleFrame;
                    if (prefab != null && Place(prefab, breach ? "Breach" : "Hole", mid, along).TryGetComponent<RuinFrame>(out var ruin))
                        ruin.Show(LevelRandom.Mix(m_Layout.Seed, Salt.Ruins, Shell.Openings.Count), Shell.WallParts);
                }
                else if (w.Exterior)
                {
                    // the street door's +Z out of the house: its leaves swing outwards (the apartment block's: thick steel with an
                    // intercom, the shop's: steel, else glass leaves); a door-and-a-half's hinges on either jamb, by the house's seed
                    var outward = inner.Count > 0 ? -inner[0] : along * Vector3.forward;
                    if (m_Floor > 0)
                    {
                        // a balcony's door (glazed leaves opening into the room) and the balcony outside
                        if (m_Art.WoodDoubleDoor != null) OuterDoor(Place(m_Art.WoodDoubleDoor, "BalconyDoor", mid, Quaternion.LookRotation(-outward)));
                        if (m_Art.Balcony != null) Place(m_Art.Balcony, "Balcony", mid + outward * (LevelMetrics.WallThickness / 2f), Quaternion.LookRotation(outward));
                        from = o1;
                        cornerA = false;
                        continue;
                    }
                    var street = DoorRules.StreetKind(m_Layout);
                    var prefab = street.HasValue ? m_Art.Door(street.Value, false) : m_Art.ExteriorDoor;
                    var go = Place(prefab != null ? prefab : m_Art.AutoDoor, "ExteriorDoor", mid, Quaternion.LookRotation(outward));
                    OuterDoor(go);
                    if (street.HasValue && go.TryGetComponent<AutoDoor>(out var exit) && (LevelRandom.Mix(m_Layout.Seed, Salt.StreetDoors, index * 31 + cut.opening) & 1) == 1)
                        exit.HingeRight();
                }
                else if (squeeze) { if (m_Art.SqueezeFrame != null) Place(m_Art.SqueezeFrame, "SqueezeFrame", mid, along); }
                else if (m_Art.ExitSign != null && !vault && OnEscapeRoute(w)) Place(m_Art.ExitSign, "ExitSign", mid + d3 * (LampOver(mid) ? 0.36f : 0f) + Vector3.up * (LevelMetrics.DoorHeight + 0.14f), along); // (beside the door's lock lamp)
                from = o1;
                cornerA = false;
            }
            WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * (len - t / 2f), 0f, height);
            RecordFaces(a3 + d3 * from, a3 + d3 * (len - t / 2f), 0f, height, w.Exterior, inner, cornerA, !jointB,
                jointA && from <= t / 2f + 0.001f, jointB);
        }

        /// <summary>The door in this doorway may have a lock lamp over it: it is locked, or opens to a key.</summary>
        bool LampOver(Vector3 mid)
        {
            for (int i = 0; i < m_Layout.AutoDoors.Count; i++)
            {
                var at = m_Layout.AutoDoors[i].position;
                if (Mathf.Abs(at.y - mid.y) > 1f || new Vector2(at.x - mid.x, at.z - mid.z).sqrMagnitude > 0.04f) continue;
                if (m_Layout.LockedDoors.Contains(i)) return true;
                foreach (var k in m_Layout.KeyDoors) if (k.Door == i) return true;
            }
            return false;
        }

        int m_OuterDoors;

        /// <summary>
        /// Over the doorways, between their trim and the ceiling: the storage room's lit sign and the vault's plate of lamps
        /// (the middle's height over the floor, half the size; AssetBuilder builds them to these).
        /// </summary>
        public static readonly WallSign StorageSign = new() { Center = new Vector3(0f, LevelMetrics.WallHeight - 0.15f, 0f), HalfWidth = 0.55f, HalfHeight = 0.13f };
        public static readonly WallSign VaultSign = new() { Center = new Vector3(0f, LevelMetrics.WallHeight - 0.13f, 0f), HalfWidth = 0.37f, HalfHeight = 0.1f };

        /// <summary>An outer door (the street's, a balcony's): its spot comes after the inner doors', in the order they are built.</summary>
        void OuterDoor(GameObject go)
        {
            if (go != null && go.TryGetComponent<AutoDoor>(out var door)) door.Spot = m_Layout.AutoDoors.Count + m_OuterDoors++;
        }

        /// <summary>The doorway (index in <see cref="LevelLayout.Doorways"/>) whose opening is at <paramref name="at"/> on this floor.</summary>
        int DoorwayIndex(Vector3 at)
        {
            for (int i = 0; i < m_Layout.Doorways.Count; i++)
            {
                var d = m_Layout.Doorways[i];
                if (d.Floor == m_Floor && new Vector2(d.Position.x - at.x, d.Position.z - at.z).sqrMagnitude < 0.01f) return i;
            }
            return -1;
        }

        /// <summary>
        /// The number of the flat a door opens (its rooms' flat, counted from 1 on the ground floor up, as on a real block's
        /// doors), or empty for any other door.
        /// </summary>
        static string FlatNumber(LevelLayout l, int door)
        {
            var d = DoorRules.DoorwayAt(l, l.AutoDoors[door].position);
            foreach (int id in new[] { d.RoomA, d.RoomB })
                if (id >= 0 && l.Plan.Rooms[id].Flat >= 0) return (l.Plan.Rooms[id].Flat + 1).ToString();
            return "";
        }

        /// <summary>
        /// An exit sign goes only over the doorways on the way out, as in a real building: into a stairwell (the evacuation
        /// route) and from the lobby onto the corridors leading to the street door — not over every room's door.
        /// </summary>
        bool OnEscapeRoute(PlanWall w)
        {
            RoomType? Type(int id) => id >= 0 && id < m_Plan.Rooms.Count ? m_Plan.Rooms[id].Type : null;
            var a = Type(w.RoomA);
            var b = Type(w.RoomB);
            if (a == RoomType.Hall || b == RoomType.Hall) return true;
            return (a == RoomType.Lobby && b == RoomType.Corridor) || (b == RoomType.Lobby && a == RoomType.Corridor);
        }

        /// <summary>
        /// The lift's doors on this floor's landing (+Z out onto the landing, the room's side of the wall) and, on the ground
        /// floor, the cab in the well behind them (+Z towards the landings).
        /// </summary>
        void PlaceLiftDoor(Vector3 mid, Vector3 along, List<Vector3> inner)
        {
            var landing = inner.Count > 0 ? inner[0] : Quaternion.Euler(0f, -90f, 0f) * along;
            if (m_Art.LiftDoor != null)
                Place(m_Art.LiftDoor, $"LiftDoor{m_Floor}", mid, Quaternion.LookRotation(landing)).GetComponent<LiftDoor>()?.Configure(m_Floor);
            if (m_Floor != 0 || m_Art.LiftCab == null) return;
            var cab = m_Art.LiftCab.GetComponent<LiftCab>();
            float half = cab != null ? cab.HalfSize.y : 1.05f;
            // the cab's front a few centimetres off the shaft's wall, its middle in the well across
            var well = m_Layout.LiftWell;
            var at = new Vector3(well.center.x, 0f, well.center.y);
            var front = mid - landing * (LevelMetrics.WallThickness / 2f + 0.03f + half);
            at += Vector3.Project(front - at, landing);
            Place(m_Art.LiftCab, "LiftCab", at, Quaternion.LookRotation(landing)).GetComponent<LiftCab>()?.Configure(m_Layout.Floors);
        }

        /// <summary>
        /// The sides of a wall a room sees: both for a wall between rooms, only the room's side otherwise. A bent wall (a
        /// chamfer, an arc) has the sealed pocket on one side, and the room's cells next to it are pocket too (a margin):
        /// look a little further out before deciding.
        /// </summary>
        List<Vector3> InnerNormals(PlanWall w)
        {
            var list = new List<Vector3>();
            var d = w.Dir;
            var n = new Vector2(-d.y, d.x);
            var mid = w.At(w.Length / 2f);
            foreach (var side in new[] { n, -n })
            {
                var k = CellKind.Pocket;
                for (float s = HousePlan.Res * 0.5f; s <= 0.8f && k == CellKind.Pocket; s += HousePlan.Res * 0.5f)
                {
                    var c = HousePlan.CellOf(mid + side * s);
                    k = m_Plan.KindAt(w.Floor, c.x, c.y);
                }
                if (k == CellKind.Room || k == CellKind.Void) list.Add(new Vector3(side.x, 0f, side.y));
            }
            return list;
        }

        /// <summary>The vault's doorway (<see cref="LevelLayout.VaultDoorway"/>) — wider, for the round bank door.</summary>
        bool IsVaultDoorway(int wall, PlanOpening o)
        {
            int v = m_Layout.VaultDoorway;
            if (v < 0 || v >= m_Plan.Openings.Count || m_Art.VaultDoor == null) return false;
            var vo = m_Plan.Openings[v];
            return vo.Wall == wall && Mathf.Abs(vo.At - o.At) < 0.01f && o.Kind == EdgeType.Door;
        }

        /// <summary>The paint booth's doorway (<see cref="LevelLayout.BoothDoorway"/>) — a car goes through it.</summary>
        bool IsBoothDoorway(int wall, PlanOpening o)
        {
            int v = m_Layout.BoothDoorway;
            if (v < 0 || v >= m_Plan.Openings.Count) return false;
            var vo = m_Plan.Openings[v];
            return vo.Wall == wall && Mathf.Abs(vo.At - o.At) < 0.01f && o.Kind == EdgeType.Door;
        }

        bool m_StraightA, m_StraightB; // of the wall being built (see RunsOn)

        void RecordFaces(Vector3 a, Vector3 b, float y0, float y1, bool outside, List<Vector3> sides, bool cornerA, bool cornerB,
            bool jointA = false, bool jointB = false)
        {
            if ((b - a).magnitude < 0.05f) return;
            foreach (var side in sides)
                Shell.Walls.Add(new WallFace
                {
                    A = a, B = b, Y0 = y0, Y1 = y1, Normal = side, Floor = m_Floor, Exterior = outside,
                    CornerA = cornerA, CornerB = cornerB, JointA = jointA, JointB = jointB,
                    StraightA = cornerA && m_StraightA, StraightB = cornerB && m_StraightB,
                });
        }

        /// <summary>
        /// Where a wall runs along the hole in the slab above it that the stairs leaving its floor climb through: spans
        /// along the wall (metres from A). Elsewhere the slab fills the band between the wall's top and the next floor; over
        /// the hole nothing did, and by an outer wall a strip of sky showed between the floors.
        /// </summary>
        public static List<(float from, float to)> StairBands(PlanWall w, IReadOnlyList<Stair> stairs)
        {
            var spans = new List<(float, float)>();
            const float step = 0.1f, reach = LevelMetrics.WallThickness / 2f + 0.15f;
            if (w.Railing) return spans;
            foreach (var s in stairs)
            {
                if (s.Floor != w.Floor) continue;
                var hole = PlanGenerator.StairArea(s, reach);
                float from = -1f, to = -1f;
                for (float u = 0f; u <= w.Length + step - 0.001f; u += step)
                {
                    float at = Mathf.Min(u, w.Length);
                    if (!hole.Contains(w.At(at))) continue;
                    if (from < 0f) from = at;
                    to = at;
                }
                if (from >= 0f && to - from > 0.05f) spans.Add((Mathf.Max(0f, from - step), Mathf.Min(w.Length, to + step)));
            }
            return spans;
        }

        /// <summary>A wall block exactly from <paramref name="a"/> to <paramref name="b"/> (no overlap with neighbours), any direction.</summary>
        void WallPiece(GameObject prefab, Vector3 a, Vector3 b, float y0, float y1)
        {
            var len = (b - a).magnitude;
            if (len < 0.01f) return;
            const float t = LevelMetrics.WallThickness;
            var d = (b - a) / len;
            var centre = (a + b) / 2f + Vector3.up * ((y0 + y1) / 2f);
            if (Mathf.Abs(d.x) > 0.999f) Block(prefab, "Wall", centre, new Vector3(len, y1 - y0, t));
            else if (Mathf.Abs(d.z) > 0.999f) Block(prefab, "Wall", centre, new Vector3(t, y1 - y0, len));
            else Block(prefab, "Wall", centre, new Vector3(t, y1 - y0, len), Quaternion.LookRotation(d));
        }

        /// <summary>
        /// A railing on the edge of a gallery: an invisible waist-high collider the view and the light pass over, and bays of
        /// the house's railing look strung along it (posts and balusters, tubes, glass or a parapet — <see cref="StairLook"/>).
        /// </summary>
        void RailingPiece(Vector3 a, Vector3 b)
        {
            var len = (b - a).magnitude;
            if (len < 0.01f) return;
            var d = (b - a) / len;
            var centre = (a + b) / 2f + Vector3.up * (RailingHeight / 2f);
            var rot = Mathf.Abs(d.x) > 0.999f ? Quaternion.LookRotation(Vector3.right) : Mathf.Abs(d.z) > 0.999f ? Quaternion.identity : Quaternion.LookRotation(d);
            if (m_Art.RailingBay == null || m_Art.RailingBlock == null)
            {
                Block(m_Art.TrimBlock, "Railing", centre, new Vector3(RailingThickness, RailingHeight, len), rot);
                return;
            }
            Block(m_Art.RailingBlock, "Railing", centre, new Vector3(RailingThickness, RailingHeight, len), rot);
            int bays = Mathf.Max(1, Mathf.CeilToInt(len / 1.25f));
            var bayRot = Quaternion.LookRotation(Vector3.Cross(d, Vector3.up)); // local X along the railing
            int seed = LevelRandom.Mix(m_Layout.Seed, Salt.Railings, 7); // one look for every railing of the house
            for (int i = 0; i < bays; i++)
            {
                var go = Place(m_Art.RailingBay, "RailingBay", a + d * (len * (i + 0.5f) / bays), bayRot);
                go.transform.localScale = new Vector3(len / bays, 1f, 1f);
                if (go.TryGetComponent<StairLook>(out var look)) look.Configure(seed);
            }
        }

        // ------------------------------------------------------------------ light, stairs, facade

        /// <summary>Ceiling lights of a room: about one per 70 m², spread over its floor (clear of stair holes), under its own ceiling.</summary>
        void BuildLights(PlanRoom r)
        {
            var mood = RoomLighting.MoodOf(m_Layout); // warm and soft, but for the hospital and the abandoned station
            var (color, intensity) = RoomLighting.For(r.Type, r.Dark, m_Layout.DarkRoomsDim ? m_Layout.DimShare : 0f, mood);
            var (litColor, litIntensity) = RoomLighting.For(r.Type, false, 0f, mood); // a light switch turns a dark room on
            float ceiling = LevelLayout.FloorY(r.Floor + r.Levels - 1) + LevelMetrics.WallHeight;
            int k = 0;
            foreach (var p in LightSpots(m_Layout, r))
            {
                var go = Place(m_Art.RoomLight, $"Light_{r.Id}_{k}", new Vector3(p.x, ceiling, p.y), Quaternion.identity);
                go.GetComponent<RoomLight>().Init(color, intensity, r.Flicker, r.Id * 3.7f + k * 1.9f, r.Id, r.Dark, litColor, litIntensity);
                k++;
            }
        }

        /// <summary>Where the ceiling lights of a room hang (pure): a grid of spots on its floor, at least one.</summary>
        public static List<Vector2> LightSpots(LevelLayout l, PlanRoom r)
        {
            var list = new List<Vector2>();
            int n = Mathf.Max(1, Mathf.RoundToInt(r.Area / 70f));
            var b = r.Bounds;
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * b.width / Mathf.Max(1f, b.height))));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    var p = new Vector2(b.xMin + b.width * (i + 0.5f) / cols, b.yMin + b.height * (j + 0.5f) / rows);
                    var p3 = new Vector3(p.x, LevelLayout.FloorY(r.Floor), p.y);
                    if (l.Plan.RoomAt(p3) != r) continue;
                    bool hole = false;
                    for (int f = r.Floor; f < r.Floor + r.Levels; f++) hole |= l.NearStair(f, p3, 0.8f);
                    if (!hole) list.Add(p);
                }
            if (list.Count == 0) list.Add(new Vector2(r.Center.x, r.Center.z));
            // a duct grate that found no cell clear of the lamps (a narrow room): the lamp moves aside along the room
            var grates = new List<Vector2Int>();
            foreach (var net in l.Vents)
                if (net.Floor == r.Floor + r.Levels - 1)
                    foreach (var g in net.Grates)
                        if (g.Room == r.Id) grates.Add(g.Cell);
            for (int i = 0; i < list.Count && grates.Count > 0; i++)
            {
                bool Clear(Vector2 p)
                {
                    foreach (var c in grates) if (VentPlan.ClashesLamp(c, new List<Vector2> { p })) return false;
                    return true;
                }
                if (Clear(list[i])) continue;
                for (int s = 1; s <= 12; s++)
                {
                    float step = (s + 1) / 2 * 0.25f * (s % 2 == 0 ? -1f : 1f);
                    bool moved = false;
                    foreach (var dir in new[] { Vector2.right, Vector2.up, new Vector2(1f, 1f), new Vector2(1f, -1f) })
                    {
                        var p = list[i] + dir * step;
                        var p3 = new Vector3(p.x, LevelLayout.FloorY(r.Floor), p.y);
                        bool inside = l.Plan.RoomAt(p3) == r;
                        foreach (var (cx, cz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                            inside &= l.Plan.RoomAt(p3 + new Vector3(cx * VentPlan.LampHalf, 0f, cz * VentPlan.LampHalf)) == r;
                        if (!Clear(p) || !inside) continue;
                        list[i] = p;
                        moved = true;
                        break;
                    }
                    if (moved) break;
                }
            }
            return list;
        }

        void BuildStair(Stair s)
        {
            var (mirrored, yaw) = StairGeometry.Placement(s.Corner, s.Flip);
            var prefab = s.Stacked && m_Art.StairsStacked != null ? (mirrored ? m_Art.StairsStackedMirrored : m_Art.StairsStacked)
                : mirrored ? m_Art.StairsMirrored : m_Art.Stairs;
            var go = Place(prefab, $"Stairs_{s.Floor}", StairGeometry.Origin(s), Quaternion.Euler(0f, yaw, 0f));
            // the look: one per staircase shaft (the same on every floor), from the house seed
            if (go.TryGetComponent<StairLook>(out var look))
                look.Configure(LevelRandom.Mix(m_Layout.Seed, Salt.StairLooks, Mathf.RoundToInt(s.Origin.x * 4f) * 1009 + Mathf.RoundToInt(s.Origin.y * 4f)));
        }

        /// <summary>The house's services (<see cref="UtilityPlan"/>: ducts, pipes, panels, sockets): one mesh, a submesh per material.</summary>
        /// <summary>A light switch beside every doorway of a room (<see cref="UtilityPlan.SwitchSpots"/>), numbered in the plan's order on every peer.</summary>
        void BuildSwitches()
        {
            if (m_Art.LightSwitch == null) return;
            var spots = UtilityPlan.SwitchSpots(m_Layout, Shell);
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var room = m_Layout.Plan.Rooms.Find(r => r.Id == s.Room);
                var go = Place(m_Art.LightSwitch, $"LightSwitch_{i}", s.Position, Quaternion.LookRotation(s.Facing));
                if (go.TryGetComponent<LightSwitch>(out var sw)) sw.Setup(i, s.Room, room != null && room.Dark);
            }
        }

        void BuildUtilities()
        {
            if (m_Art.ElectricPanel != null)
                foreach (var (at, facing) in UtilityPlan.PanelSpots(m_Layout, Shell)) Place(m_Art.ElectricPanel, "ElectricPanel", at, Quaternion.LookRotation(facing));
            var mats = m_Art.UtilityMaterials;
            if (m_Art.CityPiece == null || mats == null || mats.Length == 0) return;
            var parts = UtilityPlan.Make(m_Layout, Shell);
            if (parts.Count == 0) return;
            int slots = System.Enum.GetValues(typeof(UtilitySlot)).Length;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>[slots];
            for (int i = 0; i < slots; i++) tris[i] = new List<int>();
            foreach (var p in parts) UtilityPlan.AddFaces(p, verts, normals, uvs, tris);
            var mesh = new Mesh { name = "Utilities", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            var used = new List<Material>();
            var subs = new List<List<int>>();
            for (int i = 0; i < slots; i++)
                if (tris[i].Count > 0 && i < mats.Length && mats[i] != null) { used.Add(mats[i]); subs.Add(tris[i]); }
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateTangents(); // (their surfaces have relief)
            mesh.RecalculateBounds();
            Meshes.Add(mesh);
            var go = Place(m_Art.CityPiece, "Utilities", Vector3.zero, Quaternion.identity);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = used.ToArray();
        }

        /// <summary>The city round the yard (<see cref="CityPlan"/>): one mesh, a submesh per material, and the street lamps' lights.</summary>
        void BuildCity(float w, float h)
        {
            var mats = m_Art.CityMaterials;
            if (m_Art.CityPiece == null || mats == null || mats.Length == 0) return;
            var house = new Rect(0f, 0f, w, h);
            var lobby = m_Layout.Lobby.Center;
            var city = CityPlan.Make(house, CityPlan.SideNearest(house, new Vector2(lobby.x, lobby.z)), m_Layout.Floors, m_Layout.Seed);
            // the house's own corners (an L, a courtyard): pavement too
            foreach (var r in Rectangles(m_Plan.NX, m_Plan.NZ, (x, z) => m_Plan.KindAt(0, x, z) == CellKind.Outside))
            {
                var box = new CityBox(new Vector3(r.xMin * HousePlan.Res, -0.3f, r.yMin * HousePlan.Res), new Vector3(r.xMax * HousePlan.Res, CityPlan.WalkTop, r.yMax * HousePlan.Res),
                    CitySlot.Sidewalk, CitySlot.Sidewalk);
                city.Boxes.Add(box);
                city.Ground.Add(box.Bounds);
            }
            // the shafts (down to the basement, down from the exit hatch) go through the asphalt under the block
            foreach (var hole in m_Layout.ShaftHoles) CityPlan.CutHole(city, hole);
            // what players reach (inside the invisible wall and a bit): the pavements to stand on, the buildings, cars, posts to bump into
            var reach = new Bounds(new Vector3(w / 2f, 0f, h / 2f), new Vector3(w + 2f * CityPlan.Yard + 2f, 100f, h + 2f * CityPlan.Yard + 2f));
            foreach (var b in city.Ground)
                if (b.Intersects(reach)) Block(m_Art.Fence, "Fence", b.center, b.size);
            foreach (var b in city.Solids)
                if (b.Intersects(reach)) Block(m_Art.Fence, "Fence", b.center, b.size);
            int slots = System.Enum.GetValues(typeof(CitySlot)).Length;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>[slots];
            for (int i = 0; i < slots; i++) tris[i] = new List<int>();
            foreach (var b in city.Boxes) CityPlan.AddFaces(b, verts, normals, uvs, tris);
            AddCityModels(city, verts, normals, uvs, tris);
            var mesh = new Mesh { name = "City", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            var used = new List<Material>();
            var subs = new List<List<int>>();
            for (int i = 0; i < slots; i++)
                if (tris[i].Count > 0 && i < mats.Length && mats[i] != null) { used.Add(mats[i]); subs.Add(tris[i]); }
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateBounds();
            Meshes.Add(mesh);
            var go = Place(m_Art.CityPiece, "City", Vector3.zero, Quaternion.identity);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = used.ToArray();
            if (m_Art.CityLamp != null)
                foreach (var p in city.Lamps) Place(m_Art.CityLamp, "CityLamp", p, Quaternion.identity);
        }

        /// <summary>
        /// Every placed city model (<see cref="City.Props"/>: street furniture, cars, trees, the facades' kit) into the city's
        /// vertex lists: each part of its <see cref="CityMesh"/> posed and drawn with its slot (Livery / Pane from the copy).
        /// The models' meshes are read once per build.
        /// </summary>
        void AddCityModels(City city, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int>[] tris)
        {
            if (m_Art.CityMeshes == null || m_Art.CityMeshes.Length == 0) return;
            var models = new Dictionary<CityPropKind, CityMesh>();
            foreach (var m in m_Art.CityMeshes)
                if (m != null && m.Meshes != null) models[m.Kind] = m;
            var cache = new Dictionary<Mesh, (Vector3[] v, Vector3[] n, int[] t)>();
            foreach (var p in city.Props)
            {
                if (!models.TryGetValue(p.Kind, out var model)) continue;
                var pose = p.Pose;
                for (int i = 0; i < model.Meshes.Length; i++)
                {
                    var mesh = model.Meshes[i];
                    if (mesh == null) continue;
                    if (!cache.TryGetValue(mesh, out var data))
                        cache[mesh] = data = (mesh.vertices, mesh.normals, mesh.triangles);
                    var slot = CityPlan.Resolve(i < model.Slots.Length ? model.Slots[i] : CitySlot.Metal, p);
                    var local = i < model.Locals.Length ? model.Locals[i] : Matrix4x4.identity;
                    CityPlan.AddModel(data.v, data.n, data.t, pose * local, slot, verts, normals, uvs, tris);
                }
            }
        }

        /// <summary>
        /// Windows in the outer walls of every floor: the wall has a real hole there (<see cref="Wall"/>), the prefab
        /// frames it with glass you see the yard, the gas and the sky through (it blocks bodies, not sight), blinds inside.
        /// </summary>
        void BuildFacade()
        {
            foreach (var (p, yaw) in WindowSpots(m_Layout))
            {
                Place(m_Art.Window, "Window", p, Quaternion.Euler(0f, yaw, 0f));
                Shell.Windows.Add((p, yaw));
            }
        }

        /// <summary>
        /// Windows along the straight outer walls (pure): every ~5 m where a window fits clear of the wall's ends and its
        /// openings; the yaw turns the window's interior side (local +Z) into the building.
        /// </summary>
        public static List<(Vector3 pos, float yaw)> WindowSpots(LevelLayout l)
        {
            var list = new List<(Vector3, float)>();
            foreach (var c in WindowCuts(l)) list.Add((c.pos, c.yaw));
            return list;
        }

        /// <summary>The windows as cuts in the walls (pure): where (centre, yaw) and in which wall, how far along it.</summary>
        public static List<(Vector3 pos, float yaw, int wall, float at)> WindowCuts(LevelLayout l)
        {
            var list = new List<(Vector3, float, int, float)>();
            var roomOf = new List<int>(); // the room each window lights (a kitchen keeps one when windows are few)
            var plan = l.Plan;
            const float spacing = 5f, clear = LevelMetrics.WindowWidth / 2f + 0.9f;
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (!w.Exterior || w.Shaped || w.Railing || w.Length < LevelMetrics.WindowWidth + 1.8f) continue;
                var d = w.Dir;
                var n = new Vector2(-d.y, d.x);
                var mid = w.At(w.Length / 2f);
                var c = HousePlan.CellOf(mid + n * (HousePlan.Res * 0.5f));
                var k = plan.KindAt(w.Floor, c.x, c.y);
                if (k != CellKind.Room && k != CellKind.Void) n = -n;
                int count = Mathf.Max(1, Mathf.FloorToInt((w.Length - 1.8f) / spacing));
                var openings = plan.OpeningsOf(i);
                for (int j = 0; j < count; j++)
                {
                    float at = w.Length * (j + 0.5f) / count;
                    bool free = at > clear && at < w.Length - clear;
                    foreach (var o in openings) if (Mathf.Abs(o.At - at) < o.Width / 2f + clear) free = false;
                    if (!free) continue;
                    var p = w.At(at);
                    // a window opens into a room (or an atrium), never into a pocket behind a slanted or round wall
                    var inside = HousePlan.CellOf(p + n * (HousePlan.Res * 0.5f));
                    var ik = plan.KindAt(w.Floor, inside.x, inside.y);
                    if (ik != CellKind.Room && ik != CellKind.Void) continue;
                    // never behind a staircase (its flights and balusters stood across the glass)
                    bool stair = false;
                    foreach (float u in new[] { -LevelMetrics.WindowWidth / 2f, 0f, LevelMetrics.WindowWidth / 2f })
                    {
                        var q = p + d * u + n * 0.3f;
                        stair |= l.NearStair(w.Floor, new Vector3(q.x, 0f, q.y), 0.2f);
                    }
                    if (stair) continue;
                    // nobody looks into a restroom, a changing room or the vault from the street
                    if (ik == CellKind.Room)
                    {
                        int id = plan.IdAt(w.Floor, inside.x, inside.y);
                        if (id == l.VaultRoom || (id >= 0 && id < plan.Rooms.Count && plan.Rooms[id].Type is RoomType.Restroom or RoomType.LockerRoom or RoomType.PaintBooth or RoomType.Workshop or RoomType.Utility or RoomType.Operating or RoomType.Imaging or RoomType.Bathroom or RoomType.FlatHall)) continue; // the hall's street wall is its doors; theatres and X-ray rooms are shut in
                    }
                    roomOf.Add(plan.IdAt(w.Floor, inside.x, inside.y));
                    list.Add((new Vector3(p.x, LevelLayout.FloorY(w.Floor) + LevelMetrics.WindowCenterY, p.y), Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg, i, at));
                }
            }
            if (l.WindowLimit >= 0 && list.Count > l.WindowLimit)
            {
                // a few windows only: which ones from the house's own stream, kept in facade order
                var rng = new System.Random(LevelRandom.Mix(l.Seed, (int)RandomChannel.Windows, 1));
                var keep = new List<int>();
                for (int i = 0; i < list.Count; i++) keep.Add(i);
                for (int i = keep.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (keep[i], keep[j]) = (keep[j], keep[i]); }
                // every kitchen keeps a window: the first of each in the shuffled order goes to the front
                var lit = new HashSet<int>();
                var first = new List<int>();
                foreach (int i in keep)
                {
                    int r = roomOf[i];
                    if (r >= 0 && r < plan.Rooms.Count && plan.Rooms[r].Type == RoomType.Kitchen && lit.Add(r)) first.Add(i);
                }
                keep.RemoveAll(first.Contains);
                keep.InsertRange(0, first);
                keep.RemoveRange(l.WindowLimit, keep.Count - l.WindowLimit);
                keep.Sort();
                var picked = new List<(Vector3, float, int, float)>();
                foreach (int i in keep) picked.Add(list[i]);
                return picked;
            }
            return list;
        }

        /// <summary>Static batching of everything that never moves (walls, slabs, stairs, windows). Doors stay dynamic.</summary>
        /// <summary>
        /// A renderer that moves or changes while the house is played (a door's leaves, the lift's doors and cab, a vent's
        /// grille, a cage's door): never batched — a batched mesh stays where it was merged, whatever its transform does.
        /// </summary>
        public static bool KeepsMoving(Renderer r) => r.GetComponentInParent<AutoDoor>(true) != null || r.GetComponentInParent<KeepUnmerged>(true) != null;

        static void CombineStatic(GameObject root)
        {
            var list = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (KeepsMoving(r)) continue;
                // the unchanging shell is merged per room cell instead (PropBatches): static batching would keep each renderer
                if (PropBatches.IsShell(r, root.transform) && MeshBatcher.CanMerge(r, out _)) continue;
                list.Add(r.gameObject);
            }
            StaticBatchingUtility.Combine(list.ToArray(), root);
        }
    }
}
