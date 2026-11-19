using System.Collections.Generic;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>
        /// The special places of a house (Tools/Blender/poi.py): a crate to search, the med table, the workbench, the robot
        /// programming console, the illusion wall of a hidden room. Filled into an existing LevelArt without overwriting it.
        /// </summary>
        static void BuildFeatureArt(LevelArt art)
        {
            string p = PropPrefabs + "/";
            var crate = Prefab(p + "Crate.prefab", () =>
            {
                var go = PropObject("Crate", "crate", "Wood", "Metal", "Metal");
                go.AddComponent<SearchCrate>().Lid = Find(go, "AccentLid");
                return go;
            });
            var med = Prefab(p + "MedTable.prefab", () =>
            {
                var go = PropObject("MedTable", "med_table", "Ceiling", "Metal", "PropScreen");
                go.AddComponent<MedTable>();
                return go;
            });
            var bench = Prefab(p + "Workbench.prefab", () =>
            {
                var go = PropObject("Workbench", "workbench", "Wood", "Metal", "LampBulb");
                go.AddComponent<Workbench>();
                return go;
            });
            var programmer = Prefab(p + "Programmer.prefab", () => PropObject("Programmer", "programmer_console", "PropBody", "PropAccent", "PropScreen"));
            var illusion = Prefab($"{LevelPrefabs}/IllusionBlock.prefab", () =>
            {
                // a wall block to look at, nothing to bump into; robots keep out (they would give it away)
                var go = Block("IllusionBlock", M("Wall"), collider: false);
                var volume = go.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = new Vector3(1.6f, 1f, 1.6f);
                volume.area = 1; // Not Walkable
                return go;
            });
            var console = Prefab(p + "VaultConsole.prefab", BuildVaultConsole);
            var cage = Prefab(p + "Cage.prefab", BuildCage);
            var vaultPanel = Prefab($"{LevelPrefabs}/VaultPanel.prefab", BuildVaultPanel);
            var vaultDoor = Prefab($"{LevelPrefabs}/VaultDoor.prefab", BuildVaultDoor);
            var exteriorDoor = Prefab($"{LevelPrefabs}/ExteriorDoor.prefab", BuildExteriorDoor);
            Link(art, ref art.VaultDoor, vaultDoor);
            Link(art, ref art.ExteriorDoor, exteriorDoor);
            Link(art, ref art.Crate, crate);
            Link(art, ref art.MedTable, med);
            Link(art, ref art.Workbench, bench);
            Link(art, ref art.Programmer, programmer);
            Link(art, ref art.IllusionBlock, illusion);
            Link(art, ref art.VaultConsole, console);
            Link(art, ref art.Cage, cage);
            Link(art, ref art.VaultPanel, vaultPanel);
        }

        /// <summary>
        /// A lock panel of the vault on a wall (the root on the wall's face, +Z into the room), one look per minigame: a round
        /// safe dial, a box of toggle switches, a grid of glowing buttons; the lamp over it is red, green once solved.
        /// No collider: it hangs flat on the wall.
        /// </summary>
        static GameObject BuildVaultConsole()
        {
            var root = new GameObject("VaultConsole");
            // The lamp over the panel: red until solved, then green.
            Cube(root.transform, "LampBase", new Vector3(0f, 0.36f, 0.02f), new Vector3(0.2f, 0.08f, 0.04f), M("Metal"), false);
            var screen = Cube(root.transform, "EyeScreen", new Vector3(0f, 0.36f, 0.045f), new Vector3(0.16f, 0.05f, 0.012f), M("LockLamp"), false);
            var console = root.AddComponent<VaultConsole>();
            console.Screen = screen.GetComponent<Renderer>();
            console.LockedPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_vault_locked.png");
            console.OpenPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_vault_open.png");
            console.Faces = new GameObject[3];
            GameObject Face(int i, string name, Material body, Vector3 size)
            {
                var f = new GameObject(name);
                f.transform.SetParent(root.transform, false);
                f.AddComponent<KeepUnmerged>(); // one of the three shows (by the panel's game)
                Cube(f.transform, "Body", new Vector3(0f, 0f, size.z / 2f), size, body, false);
                console.Faces[i] = f;
                return f;
            }
            // The dial: a round steel safe door — a big knob with a pointer and a ring of ticks.
            var dial = Face(0, "FaceDial", M("Metal"), new Vector3(0.5f, 0.6f, 0.08f));
            for (int k = 0; k < 2; k++)
                Cube(dial.transform, "Ring", new Vector3(0f, -0.02f, 0.082f), new Vector3(0.36f, 0.36f, 0.006f), M("PropAccent"), false).transform.localRotation = Quaternion.Euler(0, 0, k * 45f);
            for (int k = 0; k < 2; k++)
                Cube(dial.transform, "Knob", new Vector3(0f, -0.02f, 0.1f), new Vector3(0.2f, 0.2f, 0.04f), M("Metal"), false).transform.localRotation = Quaternion.Euler(0, 0, 22.5f + k * 45f);
            Cube(dial.transform, "Pointer", new Vector3(0f, 0.07f, 0.122f), new Vector3(0.025f, 0.08f, 0.01f), M("Keypad"), false);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                Cube(dial.transform, "Tick", new Vector3(Mathf.Sin(a) * 0.15f, -0.02f + Mathf.Cos(a) * 0.15f, 0.087f), new Vector3(0.012f, 0.03f, 0.006f), M("Ceiling"), false)
                    .transform.localRotation = Quaternion.Euler(0, 0, -k * 30f);
            }
            Cube(dial.transform, "Handle", new Vector3(0.19f, -0.02f, 0.11f), new Vector3(0.04f, 0.2f, 0.05f), M("PropAccent"), false);
            // The switches: an orange-striped electrical box, four big toggles with a lamp over each.
            var sw = Face(1, "FaceSwitches", M("PropAccent"), new Vector3(0.56f, 0.5f, 0.09f));
            Cube(sw.transform, "Stripe", new Vector3(0f, -0.21f, 0.092f), new Vector3(0.56f, 0.05f, 0.004f), M("BeanBag"), false);
            for (int k = 0; k < 4; k++)
            {
                float x = -0.195f + k * 0.13f;
                Cube(sw.transform, "Socket", new Vector3(x, -0.03f, 0.1f), new Vector3(0.08f, 0.16f, 0.025f), M("Metal"), false);
                Cube(sw.transform, "Toggle", new Vector3(x, 0.02f, 0.13f), new Vector3(0.03f, 0.1f, 0.05f), M("Ceiling"), false)
                    .transform.localRotation = Quaternion.Euler(-25f, 0, 0);
                Cube(sw.transform, "Lamp", new Vector3(x, 0.13f, 0.095f), new Vector3(0.05f, 0.05f, 0.012f), M("Keypad"), false);
            }
            // The sequence: a white panel with a 3 x 3 grid of glowing buttons (the pattern to repeat).
            var seq = Face(2, "FaceSequence", M("PropBody"), new Vector3(0.46f, 0.56f, 0.07f));
            Cube(seq.transform, "Bezel", new Vector3(0f, -0.02f, 0.072f), new Vector3(0.36f, 0.36f, 0.006f), M("Rack"), false);
            string[] lit = { "LockLamp", "Keypad", "LampBulb" };
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    Cube(seq.transform, "Button", new Vector3((x - 1) * 0.11f, -0.02f + (1 - y) * 0.11f, 0.082f), new Vector3(0.08f, 0.08f, 0.02f), M(lit[(x + y) % 3]), false);
            return root;
        }

        /// <summary>
        /// The vault's round bank door (+Z into the vault): the steel ring framing the doorway on the outer face, the round
        /// door on its hinge against the inner face (its wheel and bolts towards the corridor), swinging into the vault once
        /// the three locks are solved (<see cref="AutoDoor.Swing"/>). Models: Tools/Blender/rooms2_special.py.
        /// </summary>
        static GameObject BuildVaultDoor()
        {
            var root = new GameObject("VaultDoor");
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            float h = LevelMetrics.DoorHeight - 0.02f, w = LevelMetrics.ClearWidth(LevelMetrics.VaultDoorWidth), wall = LevelMetrics.WallThickness;
            var door = root.AddComponent<AutoDoor>();
            door.Width = w;
            // the steel ring through the wall: a collar on the corridor's face, the sleeve lining the bore, rivets
            var frame = Model("vault_frame");
            frame.transform.SetParent(root.transform, false);
            Paint(frame, "Metal", "PropAccent", "PropAccent");
            // the corners between the round bore and the rectangular doorway are wall: the bare wall's material here,
            // the style's wall finish once StyleDecor dresses them (their +Z looks out of the wall)
            var corners = WallCornerMeshes(w, LevelMetrics.DoorHeight, VaultBoreCentre, VaultBoreRadius + 0.005f, wall);
            door.WallParts = new MeshRenderer[2];
            for (int k = 0; k < 2; k++)
            {
                var part = Child(root.transform, k == 0 ? "WallFront" : "WallBack", new Vector3(0f, 0f, k == 0 ? -wall / 2f : wall / 2f));
                if (k == 0) part.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                part.AddComponent<MeshFilter>().sharedMesh = corners.face;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterial = M("Wall");
                door.WallParts[k] = r;
            }
            // and solid like a wall: the faces and the bore between them
            root.AddComponent<MeshCollider>().sharedMesh = corners.collider;
            // the hinge at the doorway's +X edge, on the vault's side of the wall; the leaf turned to show its front to the
            // corridor, grown so its rim covers the bore and the sleeve (the model's disc, r 1.10, is smaller than the bore,
            // r 1.135: a ring of light showed round it), its face flat against the wall just clear of the sleeve's lip
            const float grow = VaultDoorScale;
            var hinge = Child(root.transform, "Hinge", new Vector3(VaultDoorOffAxis * grow, VaultBoreCentre * (1f - grow), wall / 2f + VaultDoorFront + 0.015f));
            var leaf = Model("vault_door");
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            leaf.transform.localScale = new Vector3(grow, grow, 1f);
            Paint(leaf, "Metal", "PropAccent", "PropAccent");
            PaintTrimParts(leaf, "Metal");
            door.Swing = hinge.transform;
            door.SwingAngle = 92f;
            // the wheel spins about the disc's axis (the hinge's local Z through the disc's centre) before the door moves
            var wheel = Child(hinge.transform, "Wheel", new Vector3(-VaultDoorOffAxis * grow, VaultBoreCentre * grow, 0f));
            var spokes = Find(leaf, "TrimWheel");
            if (spokes != null) spokes.SetParent(wheel.transform, true);
            else Debug.LogWarning("[Builder] vault_door has no TrimWheel: run Tools/Blender/rooms2_special.py --only vault_door");
            door.Wheel = wheel.transform;
            root.AddComponent<VaultHandle>().Door = door;
            door.Blocker = DoorBlocker(root, 0f, 0f, w, h, wall + 0.1f);
            door.LockLamps = new Renderer[0];
            door.Padlocks = new Transform[0];
            Muffles(root, SoundMaterial.Door);
            return root;
        }

        /// <summary>The vault door's round bore through the wall (Tools/Blender/rooms2_special.py vault_frame): centre height and radius.</summary>
        const float VaultBoreCentre = 1.15f, VaultBoreRadius = 1.135f;
        /// <summary>The round door's model (rooms2_special.py vault_door): disc centre off the hinge axis, its face in front of the hinge.</summary>
        const float VaultDoorOffAxis = 1.1f, VaultDoorFront = 0.185f;
        /// <summary>The door's disc grown from r 1.10 to 1.166: over the bore (1.135) and its sleeve (1.15).</summary>
        const float VaultDoorScale = 1.06f;

        /// <summary>
        /// The wall round a round bore in a rectangular doorway (width × height, bottom centre at the origin): the face
        /// (plane z = 0, looking along +Z; rectangle minus disc, a fan of quads from the circle to the rectangle with its
        /// corners on the rays) and a collider (both faces at z = ±depth/2 and the bore's band between them).
        /// </summary>
        static (Mesh face, Mesh collider) WallCornerMeshes(float width, float height, float cy, float radius, float depth)
        {
            const int seg = 64;
            float hw = width / 2f;
            var angles = new List<float>();
            for (int k = 0; k < seg; k++) angles.Add(2f * Mathf.PI * (k + 0.5f) / seg);
            foreach (var c in new[] { new Vector2(hw, height), new Vector2(-hw, height), new Vector2(-hw, 0f), new Vector2(hw, 0f) })
                angles.Add(Mathf.Repeat(Mathf.Atan2(c.y - cy, c.x), 2f * Mathf.PI));
            angles.Sort();
            Vector2 OnRect(float a)
            {
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float t = float.MaxValue;
                if (Mathf.Abs(d.x) > 1e-6f) t = Mathf.Min(t, hw / Mathf.Abs(d.x));
                if (d.y > 1e-6f) t = Mathf.Min(t, (height - cy) / d.y);
                if (d.y < -1e-6f) t = Mathf.Min(t, cy / -d.y);
                return new Vector2(0f, cy) + d * t;
            }
            Vector2 OnCircle(float a, float r) => new Vector2(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);

            void Tri(List<Vector3> v, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                int i = v.Count;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) (b, c) = (c, b);
                v.Add(a); v.Add(b); v.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            void Face(List<Vector3> v, List<int> tris, float z, Vector3 n, float r)
            {
                for (int k = 0; k < angles.Count; k++)
                {
                    float a0 = angles[k], a1 = angles[(k + 1) % angles.Count];
                    Vector2 c0 = OnCircle(a0, r), c1 = OnCircle(a1, r);
                    Vector2 b0 = OnRect(a0), b1 = OnRect(a1);
                    Vector3 P(Vector2 p) => new Vector3(p.x, p.y, z);
                    Tri(v, tris, P(c0), P(c1), P(b1), n);
                    Tri(v, tris, P(c0), P(b1), P(b0), n);
                }
            }

            // the face: flat, UVs in metres (the finish replaces them by the wall's projection)
            var fv = new List<Vector3>();
            var ft = new List<int>();
            Face(fv, ft, 0f, Vector3.forward, radius);
            var face = new Mesh { name = "vault_wall" };
            face.SetVertices(fv);
            face.SetTriangles(ft, 0);
            face.SetNormals(fv.ConvertAll(_ => Vector3.forward));
            face.SetUVs(0, fv.ConvertAll(p => new Vector2(p.x, p.y)));
            face.RecalculateTangents();
            face.RecalculateBounds();

            // the collider: both faces and the band round the bore (the passage's edge)
            var cv = new List<Vector3>();
            var ct = new List<int>();
            Face(cv, ct, -depth / 2f, Vector3.back, radius - 0.005f);
            Face(cv, ct, depth / 2f, Vector3.forward, radius - 0.005f);
            for (int k = 0; k < angles.Count; k++)
            {
                float a0 = angles[k], a1 = angles[(k + 1) % angles.Count];
                float r = radius - 0.005f;
                Vector3 Q(float a, float z) => new Vector3(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, z);
                var inward = -new Vector3(Mathf.Cos((a0 + a1) / 2f), Mathf.Sin((a0 + a1) / 2f), 0f);
                Tri(cv, ct, Q(a0, -depth / 2f), Q(a1, -depth / 2f), Q(a1, depth / 2f), inward);
                Tri(cv, ct, Q(a0, -depth / 2f), Q(a1, depth / 2f), Q(a0, depth / 2f), inward);
            }
            var collider = new Mesh { name = "vault_wall_collider" };
            collider.SetVertices(cv);
            collider.SetTriangles(ct, 0);
            collider.RecalculateNormals();
            collider.RecalculateBounds();

            EnsureFolder(Root + "/Art/Models/Level");
            return (SaveMesh(face, Root + "/Art/Models/Level/vault_wall.asset"), SaveMesh(collider, Root + "/Art/Models/Level/vault_wall_collider.asset"));
        }

        /// <summary>A generated mesh as an asset at a fixed path (rewritten in place: references keep their GUID).</summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>
        /// A door of the facade (+Z out of the house, LevelBuilder turns it so): two heavy leaves
        /// (Tools/Blender/rooms2_special.py exterior_door_panel: wired glass, a push bar inside, a pull handle outside) hinged
        /// at the jambs and swinging out to the street, in a steel frame that lines the doorway exactly — jambs and head on the
        /// opening's reveals, casings over both faces of the wall round it — so no wall shows between the frame and the leaves.
        /// </summary>
        static GameObject BuildExteriorDoor()
        {
            var root = new GameObject("ExteriorDoor");
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            float clear = LevelMetrics.ClearWidth(LevelMetrics.DoorWidth), height = LevelMetrics.DoorHeight, wall = LevelMetrics.WallThickness;
            const float lining = 0.06f, proud = 0.02f, casing = 0.12f, casingDepth = 0.03f;
            var door = root.AddComponent<AutoDoor>();
            door.Width = clear;
            // the frame: a lining on the reveals (2 cm proud of both faces) and casings on both faces, all over the wall's cut edge
            var steel = M("Metal");
            float depth = wall + 2f * proud;
            foreach (float side in new[] { -1f, 1f })
            {
                Cube(root.transform, "Jamb", new Vector3(side * (clear / 2f - lining / 2f), height / 2f, 0f), new Vector3(lining, height, depth), steel, false);
                foreach (float face in new[] { -1f, 1f })
                    Cube(root.transform, "Casing", new Vector3(side * (clear / 2f - lining + casing / 2f), (height + casing - lining) / 2f, face * (wall / 2f + casingDepth / 2f)),
                        new Vector3(casing, height + casing - lining, casingDepth), steel, false);
            }
            Cube(root.transform, "Head", new Vector3(0f, height - lining / 2f, 0f), new Vector3(clear, lining, depth), steel, false);
            foreach (float face in new[] { -1f, 1f })
                Cube(root.transform, "CasingHead", new Vector3(0f, height - lining + casing / 2f, face * (wall / 2f + casingDepth / 2f)),
                    new Vector3(clear + 2f * (casing - lining), casing, casingDepth), steel, false);
            Cube(root.transform, "Sill", new Vector3(0f, 0.01f, 0f), new Vector3(clear, 0.02f, depth), M("PropAccent"), false);
            // the leaves: hinged at the inner edges of the jambs, against the outer face, their glass and handles as modelled
            // (0.9 x 2.28 x 0.09) scaled to the opening; turned so the push bar faces in and the pull handle out at the meeting stiles
            float leafW = (clear - 2f * lining) / 2f, leafH = height - lining - 0.01f, hingeZ = wall / 2f + proud - 0.05f;
            Transform Leaf(string name, float side)
            {
                var hinge = Child(root.transform, name, new Vector3(side * (clear / 2f - lining), 0f, hingeZ)).transform;
                var leaf = Model("exterior_door_panel");
                leaf.transform.SetParent(hinge, false);
                leaf.transform.localPosition = new Vector3(-side * leafW / 2f, 0.005f, 0f);
                leaf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                leaf.transform.localScale = new Vector3((side < 0f ? 1f : -1f) * leafW / 0.9f, leafH / 2.28f, 1f);
                Paint(leaf, "PropAccent", "Metal", "StairGlass");
                return hinge;
            }
            door.Swing = Leaf("LeafL", -1f);   // the left leaf turns by −SwingAngle about Y: its free edge (+X) out to +Z
            door.SwingB = Leaf("LeafR", 1f);
            door.SwingAngle = -100f;
            door.SwingTime = 0.55f;
            door.Blocker = DoorBlocker(root, 0f, 0f, clear, height, 0.14f);
            door.LockLamps = new Renderer[0];
            door.Padlocks = new Transform[0];
            Muffles(root, SoundMaterial.Door);
            return root;
        }

        /// <summary>Parts named Trim* take this material (PropObject-style painting leaves them as the body).</summary>
        static void PaintTrimParts(GameObject go, string trim)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name.StartsWith("Trim")) r.sharedMaterial = M(trim);
        }

        /// <summary>The vault door's plate over the doorway (+Z out of the wall): three lamps, one per lock panel.</summary>
        static GameObject BuildVaultPanel()
        {
            var root = new GameObject("VaultPanel");
            Cube(root.transform, "Plate", new Vector3(0f, 0f, 0.01f), new Vector3(0.74f, 0.2f, 0.02f), M("Metal"), false);
            var panel = root.AddComponent<VaultPanel>();
            panel.Lamps = new Renderer[3];
            for (int i = 0; i < 3; i++)
                panel.Lamps[i] = Cube(root.transform, $"Lamp{i}", new Vector3((i - 1) * 0.22f, 0f, 0.025f), new Vector3(0.14f, 0.1f, 0.02f), M("LockLamp"), false).GetComponent<Renderer>();
            return root;
        }

        /// <summary>
        /// The hostage's cage, 2 x 2 m (its door on +Z): a floor plate, corner posts, bars on every side and over the top,
        /// the door's bars a part of their own (hidden once open), a person inside; box colliders keep everyone out. The
        /// storybook style (Blue Prince) has a model of its own (Tools/Blender/rooms2_special.py cage_storybook: an ornate
        /// brass birdcage on a wooden plinth, a padlock, a tied-up hostage sitting inside), switched by <see cref="StyleParts"/>.
        /// </summary>
        static GameObject BuildCage()
        {
            var root = new GameObject("Cage");
            float half = HouseFeatures.CageSize / 2f, h = 2.2f;
            var steel = M("Metal");
            Transform Group(Transform parent, string name)
            {
                var g = new GameObject(name);
                g.transform.SetParent(parent, false);
                return g.transform;
            }
            var frame = Group(root.transform, "Base");
            Cube(frame, "Floor", new Vector3(0f, 0.03f, 0f), new Vector3(2f * half, 0.06f, 2f * half), steel, false);
            foreach (var (x, z) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
                Cube(frame, "Post", new Vector3(x * (half - 0.04f), h / 2f, z * (half - 0.04f)), new Vector3(0.08f, h, 0.08f), steel, false);
            Cube(frame, "Top", new Vector3(0f, h, 0f), new Vector3(2f * half, 0.05f, 2f * half), M("DoorFrame"), false);
            const float step = 0.18f;
            int bars = Mathf.FloorToInt((2f * half - 0.2f) / step);
            for (int i = 0; i <= bars; i++)
            {
                float t = -half + 0.1f + i * (2f * half - 0.2f) / bars;
                Cube(frame, "BarBack", new Vector3(t, h / 2f, -half + 0.04f), new Vector3(0.03f, h, 0.03f), steel, false);
                Cube(frame, "BarLeft", new Vector3(-half + 0.04f, h / 2f, t), new Vector3(0.03f, h, 0.03f), steel, false);
                Cube(frame, "BarRight", new Vector3(half - 0.04f, h / 2f, t), new Vector3(0.03f, h, 0.03f), steel, false);
            }
            var door = new GameObject("Door");
            door.transform.SetParent(root.transform, false);
            door.AddComponent<KeepUnmerged>();
            var baseDoor = Group(door.transform, "BaseDoor");
            for (int i = 0; i <= bars; i++)
            {
                float t = -half + 0.1f + i * (2f * half - 0.2f) / bars;
                Cube(baseDoor, "Bar", new Vector3(t, h / 2f, half - 0.04f), new Vector3(0.03f, h, 0.03f), steel, false);
            }
            Cube(baseDoor, "Lock", new Vector3(half - 0.35f, 1.1f, half - 0.02f), new Vector3(0.16f, 0.2f, 0.08f), M("LockLamp"), false);
            // the walls of bars and the door are solid
            void Wall(GameObject on, Vector3 c, Vector3 size)
            {
                var b = on.AddComponent<BoxCollider>();
                b.center = c;
                b.size = size;
            }
            Wall(root, new Vector3(0f, h / 2f, -half + 0.04f), new Vector3(2f * half, h, 0.1f));
            Wall(root, new Vector3(-half + 0.04f, h / 2f, 0f), new Vector3(0.1f, h, 2f * half));
            Wall(root, new Vector3(half - 0.04f, h / 2f, 0f), new Vector3(0.1f, h, 2f * half));
            Wall(root, new Vector3(0f, h, 0f), new Vector3(2f * half, 0.08f, 2f * half));
            var doorCollider = door.AddComponent<BoxCollider>();
            doorCollider.center = new Vector3(0f, h / 2f, half - 0.04f);
            doorCollider.size = new Vector3(2f * half, h, 0.1f);
            // the person inside: a player's model, standing at the back, facing the door
            var hostage = new GameObject("Hostage");
            hostage.transform.SetParent(root.transform, false);
            hostage.AddComponent<KeepUnmerged>();
            var person = Model("player");
            person.name = "BaseHostage";
            person.transform.SetParent(hostage.transform, false);
            person.transform.localPosition = new Vector3(0f, 0.06f, -0.25f);
            Paint(person, "PlayerBody", "PlayerAccent", "PlayerGlasses");

            // the storybook model: its frame, its door and its hostage go under the same groups as the base parts
            Mat("Props", "CageWood", new Color(0.3f, 0.18f, 0.1f), smoothness: 0.45f);
            Mat("Props", "CageBrass", new Color(0.78f, 0.6f, 0.3f), smoothness: 0.65f);
            Mat("Props", "HostageCoat", new Color(0.16f, 0.3f, 0.34f), smoothness: 0.2f);
            Mat("Props", "HostageSkin", new Color(0.86f, 0.68f, 0.56f), smoothness: 0.3f);
            Mat("Props", "HostageHair", new Color(0.18f, 0.12f, 0.08f), smoothness: 0.25f);
            Mat("Props", "Rope", new Color(0.66f, 0.55f, 0.36f), smoothness: 0.1f);
            var story = Model("cage_storybook");
            story.name = "Storybook";
            story.transform.SetParent(root.transform, false);
            var storyDoor = Group(door.transform, "StoryDoor");
            var storyHostage = Group(hostage.transform, "StoryHostage");
            var materials = new Dictionary<string, string>
            {
                ["Body"] = "CageWood", ["Accent"] = "CageBrass", ["DoorAccent"] = "CageBrass", ["DoorLock"] = "CageBrass", ["DoorEye"] = "LockLamp",
                ["HostageBody"] = "HostageCoat", ["HostageSkin"] = "HostageSkin", ["HostageHair"] = "HostageHair", ["HostageRope"] = "Rope",
            };
            foreach (var r in story.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = M(materials.TryGetValue(n, out var m) ? m : "CageBrass");
                if (n.StartsWith("Door")) r.transform.SetParent(storyDoor, true);
                else if (n.StartsWith("Hostage")) r.transform.SetParent(storyHostage, true);
            }
            var parts = root.AddComponent<StyleParts>();
            parts.Style = "S06_Storybook";
            parts.Own = new[] { story, storyDoor.gameObject, storyHostage.gameObject };
            parts.Default = new[] { frame.gameObject, baseDoor.gameObject, person };
            story.SetActive(false);
            storyDoor.gameObject.SetActive(false);
            storyHostage.gameObject.SetActive(false);

            var cage = root.AddComponent<Cage>();
            cage.Door = door;
            cage.DoorCollider = doorCollider;
            cage.Hostage = hostage;
            return root;
        }

        /// <summary>The tools among the items: their meshes (one per shape) and the lure speaker's own model.</summary>
        static void FillToolVisuals(ItemVisuals visuals)
        {
            var lure = Prefab($"{PrefabsDir}/Items/LureSpeakerModel.prefab", () =>
            {
                var m = Model("lure_speaker");
                m.name = "LureSpeakerModel";
                Paint(m, "PropBody", "Metal", "LampBulb");
                return m;
            });
            // the shovel: wood and steel (Tools/Blender/poi.py item_shovel_model), its backpack picture (Tools/Blender/item_icons.py)
            Mat("Items", "ShovelWood", new Color(0.42f, 0.27f, 0.14f), smoothness: 0.3f);
            Mat("Items", "ShovelSteel", new Color(0.5f, 0.52f, 0.55f), smoothness: 0.55f);
            var shovel = Prefab($"{PrefabsDir}/Items/ShovelModel.prefab", () =>
            {
                var m = Model("item_shovel_model");
                m.name = "ShovelModel";
                Paint(m, "ShovelWood", "ShovelSteel", "ShovelSteel");
                return m;
            });
            // the flashlight (Tools/Blender/poi.py item_flashlight_model) and its batteries (item_battery_model)
            Mat("Items", "FlashlightBody", new Color(0.09f, 0.09f, 0.1f), smoothness: 0.45f);
            Mat("Items", "FlashlightMetal", new Color(0.6f, 0.62f, 0.64f), smoothness: 0.7f);
            Mat("Items", "FlashlightLens", new Color(0.8f, 0.85f, 0.9f), smoothness: 0.95f);
            Mat("Items", "BatteryLabel", new Color(0.1f, 0.38f, 0.85f), smoothness: 0.5f);
            Mat("Items", "BatterySteel", new Color(0.7f, 0.72f, 0.74f), smoothness: 0.7f);
            var flashlight = Prefab($"{PrefabsDir}/Items/FlashlightModel.prefab", () =>
            {
                var m = Model("item_flashlight_model");
                m.name = "FlashlightModel";
                Paint(m, "FlashlightBody", "FlashlightMetal", "FlashlightMetal");
                foreach (var r in Parts(m, "AccentLens")) r.sharedMaterial = M("FlashlightLens");
                return m;
            });
            var battery = Prefab($"{PrefabsDir}/Items/BatteryModel.prefab", () =>
            {
                var m = Model("item_battery_model");
                m.name = "BatteryModel";
                Paint(m, "BatteryLabel", "BatterySteel", "BatterySteel");
                return m;
            });
            // the keys (Tools/Blender/rooms2_special.py): real keys like the shovel, a little larger than life to be found
            Mat("Items", "KeyBrass", new Color(0.86f, 0.66f, 0.25f), smoothness: 0.75f);
            Mat("Items", "KeyTag", new Color(0.85f, 0.15f, 0.12f), smoothness: 0.3f);
            Mat("Items", "CageIron", new Color(0.16f, 0.16f, 0.18f), smoothness: 0.45f);
            var key = Prefab($"{PrefabsDir}/Items/KeyModel.prefab", () =>
            {
                var root = new GameObject("KeyModel");
                var m = Model("item_key_model");
                m.transform.SetParent(root.transform, false);
                m.transform.localScale = Vector3.one * ItemCatalog.KeyScale;
                Paint(m, "KeyBrass", "ShovelSteel", "ShovelSteel");
                foreach (var r in m.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.StartsWith("Trim")) r.sharedMaterial = M("KeyTag");
                return root;
            });
            var cageKey = Prefab($"{PrefabsDir}/Items/CageKeyModel.prefab", () =>
            {
                var root = new GameObject("CageKeyModel");
                var m = Model("item_cagekey_model");
                m.transform.SetParent(root.transform, false);
                m.transform.localScale = Vector3.one * ItemCatalog.CageKeyScale;
                Paint(m, "CageIron", "KeyBrass", "KeyBrass");
                return root;
            });
            // the shelter map, folded (Tools/Blender/apartments.py item_map_model): aged paper tied with a string
            MapMaterials();
            var map = Prefab($"{PrefabsDir}/Items/ShelterMapModel.prefab", () =>
            {
                var m = Model("item_map_model");
                m.name = "ShelterMapModel";
                Paint(m, "MapPaper", "MapString", "MapInk");
                return m;
            });
            // the book, canned food, the screwdriver, the belt bag (Tools/Blender/poi.py item_<id>_model)
            Mat("Items", "BookCover", new Color(0.45f, 0.12f, 0.1f), smoothness: 0.25f);
            Mat("Items", "BookPages", new Color(0.92f, 0.88f, 0.76f), smoothness: 0.1f);
            Mat("Items", "CanTin", new Color(0.72f, 0.74f, 0.76f), smoothness: 0.75f);
            Mat("Items", "CanLabel", new Color(0.85f, 0.32f, 0.12f), smoothness: 0.35f);
            Mat("Items", "ScrewHandle", new Color(0.95f, 0.72f, 0.08f), smoothness: 0.6f);
            Mat("Items", "BagFabric", new Color(0.12f, 0.24f, 0.55f), smoothness: 0.3f);
            Mat("Items", "BagStrap", new Color(0.06f, 0.06f, 0.07f), smoothness: 0.3f);
            GameObject ItemModel(string file, string name, string body, string accent, string eye, (string part, string mat)? extra = null) => Prefab($"{PrefabsDir}/Items/{name}.prefab", () =>
            {
                var m = Model(file);
                m.name = name;
                Paint(m, body, accent, eye);
                if (extra is var (part, mat)) foreach (var r in Parts(m, part)) r.sharedMaterial = M(mat);
                return m;
            });
            // parts: book — Body cover, Accent pages, AccentLabel the gilt title; cans — Body labels, Accent the tins;
            // screwdriver — Body handle, Accent steel; belt bag — Body nylon, Accent strap and buckle, AccentZip the zips
            var book = ItemModel("item_book_model", "BookModel", "BookCover", "BookPages", "BookPages", ("AccentLabel", "KeyBrass"));
            var cans = ItemModel("item_cans_model", "CansModel", "CanLabel", "CanTin", "CanTin");
            var screwdriver = ItemModel("item_screwdriver_model", "ScrewdriverModel", "ScrewHandle", "ShovelSteel", "ShovelSteel");
            var beltBag = ItemModel("item_beltbag_model", "BeltBagModel", "BagFabric", "BagStrap", "ShovelSteel", ("AccentZip", "ShovelSteel"));
            var icons = new Sprite[ItemCatalog.Shapes.Length];
            icons[ItemCatalog.Book] = ItemIcon("book");
            icons[ItemCatalog.Cans] = ItemIcon("cans");
            icons[ItemCatalog.Screwdriver] = ItemIcon("screwdriver");
            icons[ItemCatalog.BeltBag] = ItemIcon("beltbag");
            icons[ItemCatalog.Shovel] = ItemIcon("shovel");
            icons[ItemCatalog.Flashlight] = ItemIcon("flashlight");
            icons[ItemCatalog.Battery] = ItemIcon("battery");
            // the tools and the crafted diamond (Tools/Blender/item_icons.py renders their models too)
            icons[ItemCatalog.Diamond] = ItemIcon("diamond");
            icons[ItemCatalog.Key] = ItemIcon("key");
            icons[ItemCatalog.CageKey] = ItemIcon("cagekey");
            icons[ItemCatalog.Stun] = ItemIcon("stun");
            icons[ItemCatalog.Speaker] = ItemIcon("speaker");
            icons[ItemCatalog.ShelterMap] = ItemIcon("map");
            var meshes = ItemCatalog.Shapes.Select(s =>
            {
                // the speaker's piece in a grid is a domino; the cage's key looks like the door keys; the shovel's piece in
                // the cart is item_shovel (its own model shows in the world: Models)
                return ItemPieceMesh(s.Id);
            }).ToArray();
            if (visuals.Meshes != null && visuals.Meshes.SequenceEqual(meshes) && visuals.Models != null && visuals.Models.Length == ItemCatalog.Shapes.Length
                && visuals.Models[ItemCatalog.Speaker] == lure && visuals.Models[ItemCatalog.Shovel] == shovel
                && visuals.Models[ItemCatalog.Flashlight] == flashlight && visuals.Models[ItemCatalog.Battery] == battery
                && visuals.Models[ItemCatalog.Key] == key && visuals.Models[ItemCatalog.CageKey] == cageKey && visuals.Models[ItemCatalog.ShelterMap] == map
                && visuals.Models[ItemCatalog.Book] == book && visuals.Models[ItemCatalog.Cans] == cans
                && visuals.Models[ItemCatalog.Screwdriver] == screwdriver && visuals.Models[ItemCatalog.BeltBag] == beltBag
                && visuals.Icons != null && visuals.Icons.SequenceEqual(icons)) return;
            visuals.Meshes = meshes;
            visuals.Models = new GameObject[ItemCatalog.Shapes.Length];
            visuals.Models[ItemCatalog.Speaker] = lure;
            visuals.Models[ItemCatalog.Shovel] = shovel;
            visuals.Models[ItemCatalog.Flashlight] = flashlight;
            visuals.Models[ItemCatalog.Battery] = battery;
            visuals.Models[ItemCatalog.Key] = key;
            visuals.Models[ItemCatalog.CageKey] = cageKey;
            visuals.Models[ItemCatalog.ShelterMap] = map;
            visuals.Models[ItemCatalog.Book] = book;
            visuals.Models[ItemCatalog.Cans] = cans;
            visuals.Models[ItemCatalog.Screwdriver] = screwdriver;
            visuals.Models[ItemCatalog.BeltBag] = beltBag;
            visuals.Icons = icons;
            EditorUtility.SetDirty(visuals);
        }

        /// <summary>
        /// The mesh of a shape's piece (tinted grid pieces, the cargo a robot carries): the speaker's is a domino, the keys'
        /// are their own models.
        /// </summary>
        static string ItemMeshFile(string id) => id switch
        {
            "speaker" => "item_domino",
            "key" => "item_key_model",
            "cagekey" => "item_cagekey_model",
            "map" => "item_map_model",
            _ => $"item_{id}",
        };

        /// <summary>
        /// The mesh of a shape's piece in grids (the cart, a robot's cargo): its model file's, and for the shelter map its
        /// folded model stretched over its 3 × 3 cells (the real packet is smaller than its place in the backpack).
        /// </summary>
        static Mesh ItemPieceMesh(string id)
        {
            if (id != "map") return AssetDatabase.LoadAllAssetsAtPath(ModelPath(ItemMeshFile(id))).OfType<Mesh>().First();
            string path = $"{Root}/Art/Models/Items/item_map_piece.asset";
            var piece = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = piece == null;
            if (fresh) piece = new Mesh();
            // a flat folded packet over its 3 × 3 cells: its top a grid of 6 × 6 quads, a little bowed along the folds
            const int n = 6;
            float side = 3f * StashCart.Cell * 0.96f, h = 0.035f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    float u = x / (float)n - 0.5f, v = z / (float)n - 0.5f;
                    verts.Add(new Vector3(u * side, h + 0.008f * Mathf.Cos(u * Mathf.PI * 3f), v * side));
                }
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int a = z * (n + 1) + x;
                    tris.AddRange(new[] { a, a + n + 1, a + 1, a + 1, a + n + 1, a + n + 2 });
                }
            // the four sides down to the floor
            int top = verts.Count;
            foreach (var (p0, p1) in new[] { (new Vector2(-1, -1), new Vector2(1, -1)), (new Vector2(1, -1), new Vector2(1, 1)), (new Vector2(1, 1), new Vector2(-1, 1)), (new Vector2(-1, 1), new Vector2(-1, -1)) })
            {
                int i = verts.Count;
                verts.Add(new Vector3(p0.x * side / 2f, 0f, p0.y * side / 2f));
                verts.Add(new Vector3(p1.x * side / 2f, 0f, p1.y * side / 2f));
                verts.Add(new Vector3(p1.x * side / 2f, h, p1.y * side / 2f));
                verts.Add(new Vector3(p0.x * side / 2f, h, p0.y * side / 2f));
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            piece.Clear();
            piece.name = "item_map_piece";
            piece.SetVertices(verts);
            piece.SetTriangles(tris, 0);
            piece.RecalculateNormals();
            piece.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(piece, path);
            else EditorUtility.SetDirty(piece);
            return piece;
        }

        /// <summary>The shelter map's paper, string and ink (its folded model and the one held open).</summary>
        static void MapMaterials()
        {
            Mat("Items", "MapPaper", new Color(0.86f, 0.8f, 0.64f), smoothness: 0.12f);
            Mat("Items", "MapString", new Color(0.45f, 0.22f, 0.14f), smoothness: 0.2f);
            Mat("Items", "MapInk", new Color(0.2f, 0.16f, 0.12f), smoothness: 0.1f);
        }

        /// <summary>A backpack picture of a real item (Art/Sprites/Items/&lt;id&gt;.png) as a sprite.</summary>
        static Sprite ItemIcon(string id)
        {
            var path = $"{SpritesDir}/Items/{id}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Builder] {path} missing: run Tools/Blender/item_icons.py");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>The warden: a tall sentinel on wheels with a floodlight head; it watches a doorway and raises the alarm.</summary>
        static GameObject BuildWarden(RadioRipple ripple)
        {
            Mat("Robots", "WardenBody", new Color(0.18f, 0.42f, 0.62f), smoothness: 0.45f);
            var go = MobileRobot("RobotWarden", "robot_warden", "WardenBody", 0.42f, new Vector3(0, 1.55f, 0.27f), ripple, (s, t, anim) =>
            {
                // sharp eyes on the doorway, keen ears (that is how it is drawn away), no fighting: it calls the others
                s.ViewDistance = 10f; s.ViewAngle = 110f; s.DetectionSpeed = 1.1f; s.PeripheralDistance = 3f;
                s.HearingMultiplier = 1.8f; s.ContactDistance = 1.2f;
                t.CanAttack = false;
                t.AlarmOnDetect = true;
                t.AlarmRadius = 30f;
                t.CanUseStairs = false;
                t.PatrolSpeed = 1.1f; t.InvestigateSpeed = 2.6f; t.ChaseSpeed = 2.6f;
                t.WarningTime = 0.8f;
                t.PatrolWaitMin = 1.5f; t.PatrolWaitMax = 3f;
            });
            // the floodlight and the beacon turn with the head
            var head = Find(go, "AccentHead" + RobotAnimator.PivotSuffix) ?? Find(go, "AccentHead");
            if (head != null)
                foreach (var n in new[] { "EyeLens", "EyeBeacon" })
                {
                    var part = Find(go, n);
                    if (part != null) part.SetParent(head, true);
                }
            return go;
        }
    }
}
