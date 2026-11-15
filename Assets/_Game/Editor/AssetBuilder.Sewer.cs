using System.Linq;
using Stealth.Items;
using Stealth.Level;
using Stealth.Net;
using Stealth.UI;
using Stealth.Vehicles;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string SewerMaterials = MaterialsDir + "/Sewer";
        const string SewerKit = PrefabsDir + "/Level/Sewer";
        const string CarMaterials = MaterialsDir + "/Car";

        /// <summary>
        /// The sewer: the kit pieces (Tools/Blender/sewer.py, `kit`), the Sewer prefab that assembles them by a
        /// <see cref="SewerPlan"/>, the drive scene and the team's car. Returns the car (a network prefab).
        /// </summary>
        static GameObject BuildSewerPrefab(LevelArt art, RouteSignView sign)
        {
            BuildSewerMaterials();
            BuildCarMaterials();
            var segments = Enumerable.Range(0, SewerPlan.Variants).Select(i => Prefab($"{SewerKit}/Segment{i}.prefab", () => KitPiece($"sewer_seg_{i}", true, KitYaw))).ToArray();
            var cuts = Enumerable.Range(0, SewerPlan.Variants).Select(i => Prefab($"{SewerKit}/Segment{i}Cut.prefab", () => KitPiece($"sewer_seg_{i}_cut", false))).ToArray();
            var cap = Prefab($"{SewerKit}/Cap.prefab", () => KitPiece("sewer_cap", true, KitYaw));
            var hub = Prefab($"{SewerKit}/Hub.prefab", () => KitPiece("sewer_hub", true, KitYaw));
            var lamp = Prefab($"{SewerKit}/Lamp.prefab", () =>
            {
                var root = new GameObject("SewerLamp");
                SewerLamp(root.transform, Vector3.zero, 0f, false);
                return root;
            });
            var barrel = Prefab($"{SewerKit}/Barrel.prefab", () =>
            {
                var b = KitPiece("sewer_barrel", false);
                var c = b.AddComponent<CapsuleCollider>();
                c.center = new Vector3(0f, 0.44f, 0f);
                c.radius = 0.3f;
                c.height = 0.88f;
                return b;
            });
            var crate = Prefab($"{SewerKit}/Crate.prefab", () =>
            {
                var b = KitPiece("sewer_crate", false);
                var c = b.AddComponent<BoxCollider>();
                c.center = new Vector3(0f, 0.3f, 0f);
                c.size = new Vector3(0.62f, 0.6f, 0.62f);
                return b;
            });
            var ladder = Prefab($"{SewerKit}/Ladder.prefab", () => KitPiece("sewer_ladder", true));
            var manhole = Prefab($"{SewerKit}/Manhole.prefab", () => KitPiece("sewer_manhole", false));
            var gate = Prefab($"{SewerKit}/Gate.prefab", BuildSewerGate);
            var branch = Prefab($"{SewerKit}/Branch.prefab", BuildSewerBranch);
            var flood = Prefab($"{SewerKit}/FloodWater.prefab", () =>
            {
                var w = Cube(null, "FloodWater", Vector3.zero, new Vector3(1f, 0.05f, 1f), SewerMat("SewerWater"), false);
                w.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                return w;
            });

            var sewer = Prefab($"{LevelPrefabs}/Sewer.prefab", () =>
            {
                var root = new GameObject("Sewer");
                var loc = root.AddComponent<SewerLocation>();
                root.AddComponent<SewerRoute>();
                loc.Segments = segments;
                loc.Cap = cap;
                loc.Hub = hub;
                loc.Lamp = lamp;
                loc.Barrel = barrel;
                loc.Crate = crate;
                loc.Ladder = ladder;
                loc.Manhole = manhole;
                loc.GatePrefab = gate.GetComponent<SewerGate>();
                loc.BranchPrefab = branch.GetComponent<SewerBranch>();
                loc.Flooding = flood;
                loc.SignPrefab = sign;
                return root;
            });

            var carModel = Prefab($"{PrefabsDir}/Vehicles/PickupModel.prefab", () =>
            {
                var m = Model("pickup");
                m.name = "PickupModel";
                PaintCar(m);
                return m;
            });
            var drive = Prefab($"{LevelPrefabs}/DriveScene.prefab", () => BuildDriveScene(segments, carModel));
            var piece = Prefab($"{PrefabsDir}/Vehicles/CargoPiece.prefab", () =>
            {
                var go = new GameObject("CargoPiece");
                var p = go.AddComponent<CargoPiece>();
                p.MeshFilter = go.AddComponent<MeshFilter>();
                p.Renderer = go.AddComponent<MeshRenderer>();
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true; // something to aim at, nothing to stumble over
                box.center = new Vector3(0f, 0.11f, 0f);
                box.size = new Vector3(0.3f, 0.24f, 0.3f);
                return go;
            });
            var car = Prefab($"{PrefabsDir}/Vehicles/Car.prefab", () => BuildCar(piece.GetComponent<CargoPiece>()));

            if (art.Sewer != sewer || art.DriveScene != drive)
            {
                art.Sewer = sewer;
                art.DriveScene = drive;
                EditorUtility.SetDirty(art);
            }
            return car;
        }

        static Material SewerMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{SewerMaterials}/{name}.mat");

        static void BuildSewerMaterials()
        {
            ConfigureExternalTextures();
            void Pbr(string name, string id, Color tint, float smooth, float tiling)
            {
                var m = LitAsset($"{SewerMaterials}/{name}.mat");
                SetPbr(m, $"{ExternalTextures}/{id}", tint, smooth, tiling);
                EditorUtility.SetDirty(m);
            }
            void Plain(string name, Color color, float smooth, float metal)
            {
                var m = LitAsset($"{SewerMaterials}/{name}.mat");
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                EditorUtility.SetDirty(m);
            }
            Pbr("SewerBrick", "large_red_bricks", new Color(0.62f, 0.52f, 0.46f), 0.55f, 0.5f);
            Pbr("SewerStone", "concrete_wall_006", new Color(0.62f, 0.6f, 0.56f), 0.4f, 0.5f);
            Pbr("SewerConcrete", "concrete_floor_worn_001", new Color(0.7f, 0.69f, 0.64f), 0.5f, 0.5f);
            Pbr("SewerRust", "green_metal_rust", new Color(0.85f, 0.8f, 0.75f), 0.45f, 1f);
            Pbr("SewerWood", "dark_planks", new Color(0.95f, 0.85f, 0.75f), 0.35f, 1f);
            Plain("SewerWater", new Color(0.06f, 0.12f, 0.1f), 0.96f, 0.35f);
            Plain("SewerMetal", new Color(0.22f, 0.23f, 0.24f), 0.62f, 0.7f);
            Plain("SewerDark", new Color(0.01f, 0.01f, 0.012f), 0f, 0f);
            Plain("SewerStripe", new Color(0.95f, 0.72f, 0.08f), 0.35f, 0.1f);
            Mat("Sewer", "SewerGlow", new Color(1f, 0.82f, 0.55f), new Color(3.2f, 2.4f, 1.3f));
            Mat("Sewer", "SewerZone", new Color(0.35f, 1f, 0.55f), new Color(0.5f, 2.2f, 0.9f));
            // the road: a steel grate the water shows through (alpha-clipped texture, 0.5 m per tile)
            var grate = LitAsset($"{SewerMaterials}/SewerGrate.mat");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Sewer/grate.png");
            grate.SetTexture("_BaseMap", tex);
            grate.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            grate.SetColor("_BaseColor", new Color(0.42f, 0.43f, 0.44f));
            grate.SetFloat("_Metallic", 0.75f);
            grate.SetFloat("_Smoothness", 0.5f);
            grate.SetFloat("_AlphaClip", 1f);
            grate.SetFloat("_Cutoff", 0.5f);
            grate.EnableKeyword("_ALPHATEST_ON");
            grate.SetFloat("_Cull", (float)CullMode.Off);
            grate.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(grate);
            AssetDatabase.SaveAssets();
        }

        static Material SewerPart(string n)
        {
            if (n.StartsWith("Grate")) return SewerMat("SewerGrate");
            if (n.StartsWith("Stripe") || n.StartsWith("AccentHandle")) return SewerMat("SewerStripe");
            if (n.StartsWith("Brick")) return SewerMat("SewerBrick");
            if (n.StartsWith("Stone") || n.StartsWith("Frame") || n.StartsWith("Curbs") || n.StartsWith("BasinRim")) return SewerMat("SewerStone");
            if (n.StartsWith("Concrete") || n.StartsWith("BasinBed") || n.StartsWith("TrenchBed")) return SewerMat("SewerConcrete");
            if (n.StartsWith("Water")) return SewerMat("SewerWater");
            if (n.StartsWith("Rust") || n.StartsWith("Cage")) return SewerMat("SewerRust");
            if (n.StartsWith("Wood")) return SewerMat("SewerWood");
            if (n.StartsWith("Dark") || n.StartsWith("Gutter")) return SewerMat("SewerDark");
            if (n.StartsWith("Glow")) return SewerMat("SewerGlow");
            return SewerMat("SewerMetal");
        }

        static GameObject SewerModel(string file, Transform parent, Vector3 pos, float yaw = 0f, bool solid = true, string name = null)
        {
            var m = Model(file);
            m.name = name ?? file;
            m.transform.SetParent(parent, false);
            m.transform.localPosition = pos;
            m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var r in m.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = SewerPart(n);
                bool water = n.StartsWith("Water"), glow = n.StartsWith("Glow");
                r.shadowCastingMode = water || glow ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                bool trim = n.StartsWith("Stone") || n.StartsWith("Curbs") || n.StartsWith("Frames") || n.StartsWith("Plates") || n.StartsWith("MetalFrame");
                if (solid && !water && !glow && !trim) r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
            }
            return m;
        }

        /// <summary>
        /// The drivable kit is modelled along Blender +X, and the FBX import turns Blender +X into Unity −X (and +Y into −Z).
        /// Turned half round, a piece runs along Unity +X from its origin — where <see cref="SewerLocation"/> puts the next one
        /// (Blender +Y becomes Unity +Z). The cut-away segments of the drive scene stay as imported: their open side faces its camera.
        /// </summary>
        const float KitYaw = 180f;

        /// <summary>A kit piece as a prefab of its own (the model at its origin).</summary>
        static GameObject KitPiece(string file, bool solid, float yaw = 0f)
        {
            var root = new GameObject(file);
            SewerModel(file, root.transform, Vector3.zero, yaw, solid, "Model");
            return root;
        }

        static Light SewerLamp(Transform parent, Vector3 pos, float wallYaw, bool shadows, float intensity = 5.5f, float range = 11f)
        {
            SewerModel("sewer_lamp", parent, pos, wallYaw, false);
            var away = Quaternion.Euler(0f, wallYaw, 0f) * Vector3.right;
            var light = Lamp(parent, "Light", pos + away * 0.25f, Quaternion.LookRotation(away + Vector3.down * 0.7f), LightType.Spot,
                new Color(1f, 0.78f, 0.5f), intensity, range, shadows ? LightShadows.Soft : LightShadows.None);
            light.spotAngle = 150f;
            light.innerSpotAngle = 80f;
            return light;
        }

        /// <summary>The gate: the frame, the shutter (a collider while it is down) and the lever on the exit's side.</summary>
        static GameObject BuildSewerGate()
        {
            var root = new GameObject("SewerGate");
            var gate = root.AddComponent<SewerGate>();
            SewerModel("sewer_gate_frame", root.transform, Vector3.zero, KitYaw, true, "Frame");
            var door = SewerModel("sewer_gate_door", root.transform, Vector3.zero, KitYaw, false, "Door");
            var box = door.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.65f, 0f);
            box.size = new Vector3(0.3f, 3.3f, 5.8f);
            gate.Door = door.transform;
            gate.DoorCollider = box;
            var lever = SewerModel("sewer_lever", root.transform, new Vector3(1.2f, 1.25f, 3.72f), 180f, false, "Lever");
            lever.AddComponent<GateLever>();
            gate.Handle = Find(lever, "AccentHandle");
            // a small spot over the lever (placed with it, SewerLocation.PlaceLever): red while the way is shut, so it is found in the dark
            gate.LeverLight = Lamp(root.transform, "LeverLight", new Vector3(1.2f, 2.1f, 3.2f), Quaternion.Euler(70f, 0f, 0f), LightType.Spot, new Color(1f, 0.25f, 0.15f), 4f, 3.2f);
            gate.LeverLight.spotAngle = 70f;
            gate.HazardLights = new[]
            {
                Lamp(root.transform, "HazardA", new Vector3(-0.7f, 3.6f, 2.6f), Quaternion.Euler(60f, 0f, 0f), LightType.Spot, new Color(1f, 0.55f, 0.1f), 0f, 8f),
                Lamp(root.transform, "HazardB", new Vector3(-0.7f, 3.6f, -2.6f), Quaternion.Euler(60f, 0f, 0f), LightType.Spot, new Color(1f, 0.55f, 0.1f), 0f, 8f),
            };
            return root;
        }

        /// <summary>A tunnel leading on: its bars, the zone and lights inside (shown while the way is open), the invisible wall.</summary>
        static GameObject BuildSewerBranch()
        {
            var root = new GameObject("SewerBranch");
            var b = root.AddComponent<SewerBranch>();
            SewerModel("sewer_branch", root.transform, Vector3.zero, KitYaw, true, "Tunnel");
            var bars = new GameObject("Bars");
            bars.transform.SetParent(root.transform, false);
            for (int i = 0; i < 12; i++)
                Cube(bars.transform, $"Bar{i}", new Vector3(0.35f, 1.9f, -2.2f + i * 0.4f), new Vector3(0.07f, 3.8f, 0.07f), SewerMat("SewerRust"), false);
            Cube(bars.transform, "Rail", new Vector3(0.35f, 3.3f, 0f), new Vector3(0.1f, 0.12f, 4.6f), SewerMat("SewerRust"), false);
            var stop = bars.AddComponent<BoxCollider>();
            stop.center = new Vector3(0.35f, 1.9f, 0f);
            stop.size = new Vector3(0.2f, 3.8f, 4.6f);
            b.Bars = bars;
            var open = new GameObject("WhenOpen");
            open.transform.SetParent(root.transform, false);
            b.WhenOpen = open;
            var zone = SewerMat("SewerZone");
            float x0 = SewerLocation.VoteFrom, x1 = SewerLocation.VoteTo, hw = SewerLocation.VoteHalfWidth;
            void Strip(string name, Vector3 c, Vector3 size, float yaw = 0f)
            {
                var go = Cube(open.transform, name, c, size, zone, false);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            const float y = 0.02f, line = 0.09f;
            Strip("ZoneNear", new Vector3(x0, y, 0f), new Vector3(line, 0.01f, hw * 2f));
            Strip("ZoneFar", new Vector3(x1, y, 0f), new Vector3(line, 0.01f, hw * 2f));
            Strip("ZoneLeft", new Vector3((x0 + x1) / 2f, y, -hw), new Vector3(x1 - x0, 0.01f, line));
            Strip("ZoneRight", new Vector3((x0 + x1) / 2f, y, hw), new Vector3(x1 - x0, 0.01f, line));
            for (int k = 0; k < 3; k++)
            {
                float x = x0 + 1.6f + k * 2.2f;
                Strip($"Chevron{k}L", new Vector3(x, y, -0.4f), new Vector3(1.1f, 0.01f, line * 1.6f), -40f); // the tips point into the tunnel
                Strip($"Chevron{k}R", new Vector3(x, y, 0.4f), new Vector3(1.1f, 0.01f, line * 1.6f), 40f);
            }
            foreach (var (x, power) in new[] { (5f, 3.5f), (16f, 2.6f), (28f, 2f) })
            {
                var l = Lamp(open.transform, "TunnelLight", new Vector3(x, 3.3f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(0.65f, 0.85f, 1f), power, 9f);
                l.spotAngle = 150f;
            }
            var wall = Child(root.transform, "StopWall", new Vector3(SewerLocation.StopAt, 1.8f, 0f)).AddComponent<BoxCollider>(); // invisible: the tunnel goes on as far as the eye sees
            wall.size = new Vector3(0.4f, 3.6f, 4.8f);
            return root;
        }

        // ------------------------------------------------------------------ the car

        static Material CarMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{CarMaterials}/{name}.mat");

        static void BuildCarMaterials()
        {
            Mat("Car", "CarBody", new Color(0.78f, 0.36f, 0.12f), smoothness: 0.55f);
            Mat("Car", "CarAccent", new Color(0.2f, 0.21f, 0.22f), smoothness: 0.6f);
            Mat("Car", "CarDark", new Color(0.06f, 0.06f, 0.065f), smoothness: 0.2f);
            Mat("Car", "CarTyre", new Color(0.09f, 0.09f, 0.095f), smoothness: 0.15f);
            Mat("Car", "CarSeat", new Color(0.32f, 0.22f, 0.15f), smoothness: 0.3f);
            Mat("Car", "CarHead", new Color(1f, 0.96f, 0.85f), new Color(4f, 3.7f, 3f));
            Mat("Car", "CarTail", new Color(1f, 0.15f, 0.1f), new Color(3f, 0.3f, 0.2f));
            Mat("Car", "CarSpot", new Color(1f, 0.85f, 0.5f), new Color(2.5f, 2.1f, 1.2f));
            Translucent(Mat("Car", "CarGlass", new Color(0.45f, 0.6f, 0.65f, 0.35f), smoothness: 0.95f));
        }

        static void PaintCar(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = n.StartsWith("AccentWheel") ? CarMat("CarTyre")
                    : n.StartsWith("EyeHead") ? CarMat("CarHead")
                    : n.StartsWith("EyeTail") ? CarMat("CarTail")
                    : n.StartsWith("Eye") ? CarMat("CarSpot")
                    : n.StartsWith("Glass") ? CarMat("CarGlass")
                    : n.StartsWith("Seat") ? CarMat("CarSeat")
                    : n.StartsWith("Dark") ? CarMat("CarDark")
                    : n.StartsWith("Accent") ? CarMat("CarAccent")
                    : CarMat("CarBody");
                if (n.StartsWith("Eye") || n.StartsWith("Glass")) r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// The team's pickup (network prefab): the painted model under a Body pivot that bounces, the wheels and the
        /// steering wheel found by name, four seats, the hopper on the hood, the bed, colliders for the chassis and the cab,
        /// headlights, the engine's loop.
        /// </summary>
        static GameObject BuildCar(CargoPiece piece)
        {
            var root = new GameObject("Car");
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var car = root.AddComponent<Car>();
            root.AddComponent<CarNet>();
            var body = Child(root.transform, "Body");
            var model = Model("pickup");
            model.transform.SetParent(body.transform, false);
            PaintCar(model);
            car.Body = body.transform;
            car.Model = model.transform;
            car.Wheels = new[] { "AccentWheelFL", "AccentWheelFR", "AccentWheelRL", "AccentWheelRR" }.Select(n => Find(model, n)).ToArray();
            car.Steering = Find(model, "AccentSteering");
            car.Grinder = Find(model, "AccentHopperGrinder");
            car.LightGlow = Parts(model, "EyeHead");
            car.Visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            car.CargoPrefab = piece;

            // the driver sits behind the model's steering wheel, the passenger beside (a left-hand car swaps them at runtime: CarLook)
            float driverX = car.Steering != null ? root.transform.InverseTransformPoint(car.Steering.position).x : -0.42f;
            var seatPoints = new[]
            {
                new Vector3(driverX, 0.75f, -0.05f), new Vector3(-driverX, 0.75f, -0.05f),
                new Vector3(-0.5f, 1.12f, -1.95f), new Vector3(0.5f, 1.12f, -1.95f),
            };
            car.Seats = new Transform[Car.SeatCount];
            for (int i = 0; i < Car.SeatCount; i++)
            {
                var s = Child(root.transform, $"Seat{i}", seatPoints[i]);
                var use = s.AddComponent<CarSeat>();
                use.Car = car;
                use.Seat = i;
                car.Seats[i] = s.transform;
            }
            var hopperAt = car.Grinder != null ? root.transform.InverseTransformPoint(car.Grinder.position) + Vector3.up * 0.15f : new Vector3(0f, 1.56f, 1.55f);
            Child(root.transform, "Hopper", hopperAt).AddComponent<CarHopper>();
            car.Bed = Child(root.transform, "Bed", new Vector3(0f, 0.86f, -2.35f)).transform;
            car.CargoRoot = Child(root.transform, "Cargo").transform;

            // solid: the chassis with the bed walls and the cab; the hull (for the walls ahead) is not a collider of its own
            var chassis = Child(root.transform, "Chassis").AddComponent<BoxCollider>();
            chassis.center = new Vector3(0f, 0.6f, 0f);
            chassis.size = new Vector3(1.95f, 0.75f, 4.85f);
            car.Chassis = chassis;
            var cab = Child(root.transform, "Cab").AddComponent<BoxCollider>();
            cab.center = new Vector3(0f, 1.45f, 0.7f);
            cab.size = new Vector3(1.8f, 0.95f, 1.9f);
            var hull = Child(root.transform, "Hull").AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, 0.95f, 0f);
            hull.size = new Vector3(2.0f, 1.5f, 4.95f);
            hull.enabled = false;
            car.Hull = hull;

            car.Headlights = new[]
            {
                Lamp(root.transform, "HeadlightL", new Vector3(-0.65f, 0.95f, 2.45f), Quaternion.Euler(6f, 0f, 0f), LightType.Spot, new Color(1f, 0.95f, 0.85f), 8f, 22f),
                Lamp(root.transform, "HeadlightR", new Vector3(0.65f, 0.95f, 2.45f), Quaternion.Euler(6f, 0f, 0f), LightType.Spot, new Color(1f, 0.95f, 0.85f), 8f, 22f),
            };
            foreach (var l in car.Headlights) { l.spotAngle = 70f; l.innerSpotAngle = 40f; }
            var engine = root.AddComponent<AudioSource>();
            engine.clip = s_Bank != null ? s_Bank.CarEngine : null;
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.rolloffMode = AudioRolloffMode.Linear;
            engine.minDistance = 3f;
            engine.maxDistance = 35f;
            engine.dopplerLevel = 0f;
            car.Engine = engine;
            return root;
        }

        /// <summary>The drive scene: the cut-away tunnel, the pickup's model, a side camera with a long lens, the engine.</summary>
        static GameObject BuildDriveScene(GameObject[] segments, GameObject carModel)
        {
            var root = new GameObject("DriveScene");
            var scene = root.AddComponent<DriveScene>();
            scene.Segments = segments; // whole segments: the camera stays inside the tunnel (DriveScene.Place)
            scene.CarModel = carModel;
            var camGo = Child(root.transform, "Camera", new Vector3(3.1f, 1.45f, 2.35f));
            camGo.transform.localRotation = Quaternion.Euler(8f, 245f, 0f);
            var cam = camGo.AddComponent<Camera>();
            ConfigureCamera(cam);
            cam.fieldOfView = 52f;
            cam.depth = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            scene.Camera = cam;
            var engine = root.AddComponent<AudioSource>();
            engine.spatialBlend = 0f;
            engine.loop = true;
            engine.playOnAwake = false;
            scene.Engine = engine;
            scene.Lamps = new[]
            {
                Lamp(root.transform, "Lamp0", new Vector3(-8f, 3.4f, -1.2f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.78f, 0.5f), 6f, 12f),
                Lamp(root.transform, "Lamp1", new Vector3(6f, 3.4f, -1.2f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.78f, 0.5f), 6f, 12f),
            };
            foreach (var l in scene.Lamps) l.spotAngle = 150f;
            // a soft key light from the camera's side, under the vault: the car reads as a shape, not a silhouette
            var key = Lamp(root.transform, "KeyLight", new Vector3(1.5f, 3.3f, 2.6f), Quaternion.Euler(35f, 235f, 0f), LightType.Spot, new Color(0.85f, 0.9f, 1f), 7f, 12f);
            key.spotAngle = 70f;
            key.innerSpotAngle = 40f;
            return root;
        }
    }
}
