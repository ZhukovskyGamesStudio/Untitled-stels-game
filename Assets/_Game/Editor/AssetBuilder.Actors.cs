using System;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Net;
using Stealth.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string RobotPrefabs = PrefabsDir + "/Robots";
        const string SensorPrefabs = PrefabsDir + "/Sensors";

        struct Actors
        {
            public GameObject Player, Item, Laser;
            /// <summary>Indexed by RobotKind.</summary>
            public GameObject[] Robots;
        }

        static Actors BuildActors()
        {
            var ripple = Prefab(PrefabsDir + "/FX/RadioRipple.prefab", BuildRadioRipple).GetComponent<RadioRipple>();
            s_StompRipple = Prefab(PrefabsDir + "/FX/StompRipple.prefab", BuildStompRipple).GetComponent<RadioRipple>();
            var visuals = DataAsset<ItemVisuals>(DataDir + "/ItemVisuals.asset", v =>
            {
                v.Meshes = ItemCatalog.Shapes.Select(s => ItemPieceMesh(s.Id)).ToArray();
            });
            // One material per configured colour (ItemsConfig can grow).
            visuals.ColorMaterials = Enumerable.Range(0, ItemCatalog.Colors.Length).Select(i => M($"Item_{i}")).ToArray();
            EditorUtility.SetDirty(visuals);
            FillToolVisuals(visuals);

            var a = new Actors
            {
                Item = Prefab(PrefabsDir + "/Items/WorldItem.prefab", () => BuildWorldItem(visuals)),
                Player = Prefab(PrefabsDir + "/Player/Player.prefab", BuildPlayer),
                Laser = Prefab(SensorPrefabs + "/LaserGate.prefab", BuildLaser),
                Robots = new GameObject[RobotRoster.KindCount],
            };

            void Robot(RobotKind kind, string path, Func<GameObject> build) => a.Robots[(int)kind] = Prefab(path, () =>
            {
                var go = build();
                go.GetComponent<Detector>().Kind = kind;
                return go;
            });

            Robot(RobotKind.Guard, RobotPrefabs + "/RobotGuard.prefab", () =>
            {
                var go = MobileRobot("RobotGuard", "robot_guard", "RobotBody", 0.4f, new Vector3(0, 1.84f, 0.3f), ripple, (s, t, anim) =>
                {
                    anim.Bob = 0.06f;
                    s.ViewDistance = 13f; s.ViewAngle = 100f; // deaf: the first houses are full of guards
                    t.PatrolSpeed = 1.8f; t.InvestigateSpeed = 2.8f; t.ChaseSpeed = 4f;
                    t.PatrolWaitMin = 6.25f; t.PatrolWaitMax = 12.5f; // stands at a point long enough to be read and slipped past
                });
                // what a guard may carry (the hostage's cage key) hangs at its belt behind, off to one side — the lower edge of
                // the torso (it starts at 1.5 m over the legs), where a player sneaking up sees it
                var rc = go.GetComponent<RobotController>();
                var cargo = Child(go.transform, "Cargo", new Vector3(0.17f, 1.47f, -0.13f));
                cargo.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                rc.CargoMesh = cargo.AddComponent<MeshFilter>();
                rc.CargoRenderer = cargo.AddComponent<MeshRenderer>();
                rc.CargoRenderer.enabled = false;
                rc.CargoVisuals = visuals;
                // no motor hum of the old guard: its own voice says what it does (GuardSounds)
                foreach (var loop in go.GetComponents<Stealth.Audio.LoopSound>())
                {
                    if (loop.Source != null) Object.DestroyImmediate(loop.Source, true);
                    Object.DestroyImmediate(loop, true);
                }
                // its own voice: charging on its dock, walking its rounds, chasing (GuardSounds)
                var voice = Child(go.transform, "GuardVoice", new Vector3(0f, 1.6f, 0f));
                var gs = go.AddComponent<Stealth.Audio.GuardSounds>();
                gs.Source = voice.AddComponent<AudioSource>();
                gs.Source.playOnAwake = false;
                gs.Charging = s_Bank.GuardCharge;
                gs.Patrolling = s_Bank.GuardPatrol;
                gs.Chasing = s_Bank.GuardChase;
                return go;
            });
            Robot(RobotKind.Listener, RobotPrefabs + "/RobotListener.prefab", () => MobileRobot("RobotListener", "robot_listener", "ListenerBody", 0.55f, new Vector3(0, 1.2f, 0f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 1.6f; s.ContactDistance = 1.4f;
                t.CanUseStairs = false; // wheels
                t.PatrolSpeed = 1.5f; t.InvestigateSpeed = 3.2f; t.ChaseSpeed = 4.5f; t.LoseTargetAfter = 3f;
            }));
            Robot(RobotKind.Drone, RobotPrefabs + "/RobotDrone.prefab", () => MobileRobot("RobotDrone", "robot_drone", "DroneBody", 0.45f, new Vector3(0, -0.1f, 0.25f), ripple, (s, t, anim) =>
            {
                anim.Lean = 14f;
                s.ViewDistance = 11f; s.ViewAngle = 120f; s.EyePitch = 35f; s.HearingMultiplier = 0.5f; // looks down-forward
                t.HoverHeight = 2.1f; t.AlarmOnDetect = true;
                t.PatrolSpeed = 2.6f; t.InvestigateSpeed = 3.6f; t.ChaseSpeed = 4.7f; t.AttackRange = 1.4f;
            }));
            Robot(RobotKind.Thumper, RobotPrefabs + "/RobotThumper.prefab", () => MobileRobot("RobotThumper", "robot_thumper", "ThumperBody", 0.7f, new Vector3(0, 0.3f, 0.8f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 0f; // deaf; it listens with its feet
                s.SeismicRange = 10f; s.SeismicSpeed = 0.8f; s.ContactDistance = 1.6f;
                t.CanUseStairs = false; // tracks
                t.PatrolSpeed = 1.2f; t.InvestigateSpeed = 2.2f; t.ChaseSpeed = 3f; t.AttackRange = 1.9f;
                t.SuspicionDecay = 0.12f; // waves come seconds apart: it keeps what it felt between them
            }));
            Robot(RobotKind.Hound, RobotPrefabs + "/RobotHound.prefab", () => MobileRobot("RobotHound", "robot_hound", "HoundBody", 0.4f, new Vector3(0, 0.9f, 0.9f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 0.6f; s.SmellRange = 3f; s.SmellSpeed = 0.8f; s.DetectionSpeed = 0.4f;
                t.PatrolSpeed = 1.5f; t.InvestigateSpeed = 2.3f; t.ChaseSpeed = 3.6f; t.LoseTargetAfter = 6f; t.WarningTime = 2.5f; t.SuspicionDecay = 0.5f; t.SniffEvery = 3.5f; t.SniffTime = 1.4f;
            }));
            Robot(RobotKind.Sorter, RobotPrefabs + "/RobotSorter.prefab", () => MobileRobot("RobotSorter", "robot_sorter", "SorterBody", 0.35f, new Vector3(0, 1.25f, 0.3f), ripple, (s, t, anim) =>
            {
                s.ColorSorter = true; s.ViewDistance = 16f; s.ViewAngle = 160f; s.DetectionSpeed = 0.9f; s.HearingMultiplier = 0.3f;
                t.PatrolSpeed = 1.7f; t.InvestigateSpeed = 2.6f; t.ChaseSpeed = 4.2f;
            }));
            Robot(RobotKind.Worker, RobotPrefabs + "/RobotWorker.prefab", () =>
            {
                var go = MobileRobot("RobotWorker", "robot_worker", "WorkerBody", 0.45f, new Vector3(0, 0.5f, 0.4f), ripple, (s, t, anim) =>
                {
                    s.ViewDistance = 10f; s.ViewAngle = 90f; s.DetectionSpeed = 0.5f; s.HearingMultiplier = 0.5f; s.ContactDistance = 0.9f;
                    t.CanAttack = false;    // harmless, but a snitch
                    t.AlarmRadius = 40f;
                    t.CanUseStairs = false; // wheels
                    t.PatrolSpeed = 1.6f; t.InvestigateSpeed = 1.8f; t.ChaseSpeed = 1.8f;
                    t.CargoChance = 0.6f;   // carries loot on its tray: hack it and it drops it
                    t.MovesLoot = true;     // lifts loot lying on the floor onto its tray, sets it down elsewhere
                });
                // The lift (Tools/Blender/worker_lift.py): arms on a pivot at their hinge, the tray on a pivot at the arms'
                // end pin; what it carries lies on the tray. RobotAnimator swings them (CargoMotion).
                var rc = go.GetComponent<RobotController>();
                var anim = go.GetComponent<RobotAnimator>();
                var lift = Model("worker_lift");
                lift.name = "Lift";
                // under the animated body: it leans and sags with it (the poses are the robot's own: the body sits at its origin)
                lift.transform.SetParent(anim.Model != null ? anim.Model : go.transform, false);
                Paint(lift, "WorkerBody", "RobotAccent", "SensorGlow");
                rc.Status.Dark = rc.Status.Dark.Concat(Parts(lift, "Eye")).ToArray(); // the lift's lamps: no sense of its own
                var armPivot = Child(lift.transform, "LiftArmPivot", CargoMotion.Hinge);
                var trayPivot = Child(lift.transform, "LiftTrayPivot", CargoMotion.PinRest);
                Find(lift, "AccentLiftArm").SetParent(armPivot.transform, true);
                Find(lift, "BodyLiftTray").SetParent(trayPivot.transform, true);
                anim.LiftArm = armPivot.transform;
                anim.LiftTray = trayPivot.transform;
                var cargo = Child(trayPivot.transform, "Cargo", new Vector3(0f, CargoMotion.OnTray, 0f));
                rc.CargoMesh = cargo.AddComponent<MeshFilter>();
                rc.CargoRenderer = cargo.AddComponent<MeshRenderer>();
                rc.CargoRenderer.enabled = false;
                rc.CargoVisuals = visuals;
                // the microphones stand on the mast's cap (the booms swing past the sides of the pod where the usual seat is):
                // SensorRig casts down from these spots onto the body of the current style
                var rig = go.GetComponent<SensorRig>();
                if (rig != null && rig.Microphones != null)
                    for (int i = 0; i < rig.Microphones.Length; i++)
                        if (rig.Microphones[i] != null)
                            rig.Microphones[i].transform.localPosition = new Vector3(i == 0 ? -0.17f : 0.17f, 0.55f, -0.25f);
                return go;
            });
            Robot(RobotKind.Sweeper, RobotPrefabs + "/RobotSweeper.prefab", () =>
            {
                var go = MobileRobot("RobotSweeper", "robot_sweeper", "SweeperBody", 0.4f, new Vector3(0, 0.5f, 0.35f), ripple, (s, t, anim) =>
                {
                    s.HasEyes = false; s.HearingMultiplier = 0f; s.ContactDistance = 0.9f; // blind and deaf: only the floor lidar
                    s.LidarRange = 6f; s.LidarHeight = 0.22f; s.LidarTurnSpeed = 140f; s.LidarRate = 15f;
                    t.CanUseStairs = false; // wheels
                    t.PatrolSpeed = 1.4f; t.InvestigateSpeed = 2.2f; t.ChaseSpeed = 3.1f; t.AttackRange = 1.3f;
                });
                var rc = go.GetComponent<RobotController>();
                var lidar = go.AddComponent<RobotLidar>();
                lidar.Robot = rc;
                lidar.Head = Find(go, "AccentLidar");
                lidar.Line = Beam(go, "LidarBeam", 0.025f);
                rc.Lidar = lidar;
                return go;
            });
            Robot(RobotKind.Hunter, RobotPrefabs + "/RobotHunter.prefab", () => MobileRobot("RobotHunter", "robot_hunter", "HunterBody", 0.45f, new Vector3(0, 1.9f, 0.3f), ripple, (s, t, anim) =>
            {
                // no sensors at all: it runs to whoever the other robots and cameras are seeing (RobotSenses.FollowsSightings)
                s.HasEyes = false; s.FollowsSightings = true; s.HearingMultiplier = 0f; s.SeismicRange = 0f; s.SmellRange = 0f;
                t.PatrolSpeed = 3.2f; t.InvestigateSpeed = 5.2f; t.ChaseSpeed = 6.2f; t.LoseTargetAfter = 3f; t.AttackCooldown = 1.5f; t.WarningTime = 0.3f;
            }));

            Robot(RobotKind.Warden, RobotPrefabs + "/RobotWarden.prefab", () => BuildWarden(ripple));
            Robot(RobotKind.Turret, RobotPrefabs + "/RobotTurret.prefab", () => Device<SentryTurret>("RobotTurret", "robot_turret", "TurretBody", false, false, (root, t) =>
            {
                t.Profile = Profile<TurretProfile>("RobotTurret", p => p.ForgetRate = 0.6f);
                t.Sight = Beam(root, "Sight", 0.025f);
                t.Head = Find(root, "AccentHead" + RobotAnimator.PivotSuffix);
                t.Point.localPosition = new Vector3(0, 1.35f, 0);
            }));
            // a sheet of the spider's web, the unit square along +Z and up +Y (stretched to each sheet), threads as geometry in
            // three variants (Web0..2, Tools/Blender/web.py sheet); SpiderBot shows one variant a sheet; no collider, no shadow
            var strand = Prefab(RobotPrefabs + "/SpiderStrand.prefab", () =>
            {
                var go = new GameObject("SpiderStrand");
                var model = Model("web_sheet");
                model.transform.SetParent(go.transform, false);
                var mat = WebMaterial();
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                return go;
            });
            Robot(RobotKind.Spider, RobotPrefabs + "/RobotSpider.prefab", () => Device<SpiderBot>("RobotSpider", "robot_spider", "SpiderBody", true, true, (root, s) =>
            {
                s.Profile = Profile<SpiderProfile>("RobotSpider");
                s.Strand = strand;
            }));
            Robot(RobotKind.Vacuum, RobotPrefabs + "/RobotVacuum.prefab", () => Device<CeilingVacuum>("RobotVacuum", "robot_vacuum", "VacuumBody", true, true, (root, v) =>
            {
                v.Profile = Profile<VacuumProfile>("RobotVacuum", p => p.ForgetRate = 0.6f);
                v.FanARenderer = LaserFan(root, "LaserFanA");
                v.FanBRenderer = LaserFan(root, "LaserFanB");
                v.FanA = v.FanARenderer.transform;
                v.FanB = v.FanBRenderer.transform;
            }));
            Robot(RobotKind.Lidar, SensorPrefabs + "/SensorLidar.prefab", () => Device<LidarScanner>("SensorLidar", "sensor_lidar", "TurretBody", false, false, (root, l) =>
            {
                l.Profile = Profile<LidarProfile>("SensorLidar", p => p.ForgetRate = 0.6f);
                l.Line = Beam(root, "LidarBeam", 0.02f);
                l.Head = Find(root, "AccentHead" + RobotAnimator.PivotSuffix);
            }));
            Robot(RobotKind.MotionSensor, SensorPrefabs + "/SensorMotion.prefab", () => Device<MotionSensor>("SensorMotion", "sensor_pir", "RobotBody", false, false, (root, m) =>
            {
                m.Profile = Profile<MotionSensorProfile>("SensorMotion", p => p.ForgetRate = 1f); // forgets twice as fast as the base
                Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // on the wall, nothing to bump into
            }));
            Robot(RobotKind.PressurePlate, SensorPrefabs + "/SensorPlate.prefab", () => Device<PressurePlate>("SensorPlate", "sensor_plate", "RobotBody", false, false, (root, p) =>
            {
                p.Profile = Profile<PressurePlateProfile>("SensorPlate", c => { c.ForgetRate = 0.4f; c.HalfSize = 0.55f; });
                Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // flush with the floor
                // A steel slab with a glowing border in the status colour and a small pool of light: seen by anyone who
                // watches where they walk (it lies in a doorway — jump over it or hack it).
                p.Status.Light.range = 1.4f;
                p.Status.Light.intensity = 0.8f;
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }));
            Robot(RobotKind.Camera, SensorPrefabs + "/SecurityCamera.prefab", BuildSecurityCamera);
            return a;
        }

        // ------------------------------------------------------------------ player and items

        static GameObject BuildWorldItem(ItemVisuals visuals)
        {
            var root = new GameObject("WorldItem");
            root.AddComponent<NetworkObject>();
            var mf = root.AddComponent<MeshFilter>();
            var mr = root.AddComponent<MeshRenderer>();
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            // real items (the shovel) fall: a solid box and a body, dynamic on the server only (WorldItem.ApplyPhysics);
            // the server's NetworkTransform shows where it landed on the clients
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = 2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var solid = Child(root.transform, "Solid").AddComponent<BoxCollider>();
            solid.enabled = false;
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            mf.sharedMesh = visuals.Meshes[0];
            mr.sharedMaterial = visuals.ColorMaterials[0];
            var item = root.AddComponent<WorldItem>();
            root.AddComponent<WorldItemNet>();
            item.Visuals = visuals;
            // the lure speaker's music (WorldItem plays it while the speaker is on)
            var music = Child(root.transform, "Music").AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 1f;
            music.rolloffMode = AudioRolloffMode.Linear;
            music.minDistance = 2f;
            music.maxDistance = 28f;
            music.dopplerLevel = 0f;
            item.Music = music;
            item.MeshFilter = mf;
            item.Renderer = mr;
            item.Collider = col;
            item.Body = body;
            item.Solid = solid;
            return root;
        }

        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = PlayerController.StandHeight;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, PlayerController.StandHeight / 2f, 0);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var model = Model("player");
            model.transform.SetParent(root.transform, false);
            Paint(model, "PlayerBody", "PlayerAccent", "PlayerGlasses");

            // Head pivot at the neck: teammates see where this player is looking.
            var headPivot = Child(model.transform, "HeadPivot", new Vector3(0, 1.3f, 0));
            foreach (var part in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "AccentHead" || t.name == "EyeGlasses").ToArray())
                part.SetParent(headPivot.transform, true);

            var head = Child(root.transform, "Head", new Vector3(0, 1.6f, 0));
            var cam = head.AddComponent<Camera>();
            ConfigureCamera(cam);
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.enabled = false;
            var listener = head.AddComponent<AudioListener>();
            listener.enabled = false;
            // the flashlight's beam from the head (on only while a charged flashlight in the hand is switched on);
            // robots see a player in it (LightSource), like in any lamp's light
            // the hand holding the flashlight in the view (Tools/Blender/poi.py view_flashlight; only its owner sees it)
            Mat("Player", "ViewSleeve", new Color(0.16f, 0.17f, 0.19f), smoothness: 0.2f);
            var view = Model("view_flashlight");
            view.name = "ViewFlashlight";
            view.transform.SetParent(head.transform, false);
            view.transform.localPosition = new Vector3(0.16f, -0.2f, 0.3f);
            view.transform.localRotation = Quaternion.Euler(-4f, -6f, 0f);
            view.transform.localScale = new Vector3(-1f, 1f, 1f); // the import mirrors Blender's X: mirrored back it is the right hand
            foreach (var r in view.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = M(n.StartsWith("AccentSleeve") ? "ViewSleeve" : n.StartsWith("AccentTorch") ? "FlashlightBody"
                    : n.StartsWith("AccentHead") ? "FlashlightMetal" : n.StartsWith("AccentLens") ? "FlashlightLens" : "PlayerAccent");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            view.SetActive(false);
            // the shelter map held open in front of the face (Tools/Blender/apartments.py held_map): the owner sees it in
            // the view, the others see it in front of the player — a thing in the world, not UI
            MapMaterials();
            var held = Model("held_map");
            held.name = "HeldMap";
            held.transform.SetParent(head.transform, false);
            held.transform.localPosition = new Vector3(0f, -0.2f, 0.42f);
            foreach (var r in held.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = M(n.StartsWith("AccentSleeve") ? "ViewSleeve" : n.StartsWith("AccentHand") ? "PlayerAccent"
                    : n.StartsWith("Trim") ? "MapInk" : "MapPaper");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            held.SetActive(false);
            // anything else held in the hand (PlayerController.HeldThing): its mesh or model, sized to the hand, low right in the view
            // where the flashlight's hand is: the fist's grip on the anchor, pointing ahead (+Z)
            var holder = Child(head.transform, "HeldAnchor", new Vector3(0.16f, -0.2f, 0.3f));
            holder.transform.localRotation = Quaternion.Euler(-4f, -6f, 0f);
            var thing = Child(holder.transform, "HeldThing", Vector3.zero);
            var thingMesh = thing.AddComponent<MeshFilter>();
            var thingRenderer = thing.AddComponent<MeshRenderer>();
            thingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            thingRenderer.receiveShadows = false;
            // the right hand closed round it (Tools/Blender/poi.py view_grip; only its owner sees it)
            var grip = Model("view_grip");
            grip.name = "HeldHand";
            grip.transform.SetParent(holder.transform, false);
            grip.transform.localScale = new Vector3(-1f, 1f, 1f);
            foreach (var r in grip.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = M(r.gameObject.name.StartsWith("AccentSleeve") ? "ViewSleeve" : "PlayerAccent");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            grip.SetActive(false);
            holder.SetActive(false);
            // the beam from the flashlight's lens in the view
            var beam = Lamp(head.transform, "Flashlight", new Vector3(0.15f, -0.19f, 0.48f), Quaternion.identity, LightType.Spot,
                new Color(1f, 0.93f, 0.8f), 6f, 16f, LightShadows.Soft);
            beam.spotAngle = 55f;
            beam.innerSpotAngle = 22f;
            beam.enabled = false;

            var motor = root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerInventory>();
            var pc = root.AddComponent<PlayerController>();
            root.AddComponent<PlayerNet>();
            root.AddComponent<InventoryNet>();
            pc.Motor = motor;
            pc.Head = head.transform;
            pc.Camera = cam;
            pc.Listener = listener;
            pc.Flashlight = beam;
            pc.ViewFlashlight = view;
            pc.HeldMap = held;
            pc.HeldThing = thingMesh;
            pc.HeldHand = grip;
            pc.Visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            pc.Model = model.transform;
            pc.HeadPivot = headPivot.transform;
            pc.BodyRenderers = model.GetComponentsInChildren<Renderer>(true);
            pc.Voice = AddVoice(root.transform);
            pc.Voice.Owner = pc;
            return root;
        }

        // ------------------------------------------------------------------ robots

        static GameObject NetworkedRoot(string name, bool moves, bool fullRotation)
        {
            var root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            if (!moves) return root;
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = fullRotation;
            nt.SyncRotAngleZ = fullRotation;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            return root;
        }

        /// <summary>
        /// Status light of a robot / device: a small spot with shadows (pitched <paramref name="pitch"/>° down from the robot's
        /// forward) — walls and slabs keep it in the room, it never glows through a floor.
        /// </summary>
        static Light StatusLight(Transform parent, Vector3 position, float range, float intensity, float pitch)
        {
            var go = Child(parent, "StatusLight", position);
            go.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 130f;
            light.innerSpotAngle = 60f;
            light.range = range;
            light.intensity = intensity;
            ShadowedLight(light, UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
            return light;
        }

        /// <summary>Legs cut at the knee (AccentLeg{n}Upper / Lower) walk by inverse kinematics.</summary>
        static void WalkingLegs(GameObject root, GameObject model)
        {
            var legs = root.AddComponent<ProceduralLegs>();
            if (!ProceduralLegs.Bake(root.transform, model.transform, legs)) Object.DestroyImmediate(legs);
        }

        /// <summary>Navmesh robot: NetworkObject + server NetworkTransform + NavMeshAgent + RobotController + animator.</summary>
        static GameObject MobileRobot(string name, string file, string bodyMat, float radius, Vector3 eyePos, RadioRipple ripple,
            Action<RobotSenses, RobotTraits, AnimationSettings> config)
        {
            var root = NetworkedRoot(name, true, false);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, bodyMat, "RobotAccent", "SensorGlow");
            var b = LocalBounds(root);
            RobotAnimator.BakePivots(model.transform);
            WalkingLegs(root, model);

            var col = root.AddComponent<CapsuleCollider>();
            col.height = Mathf.Max(b.size.y, radius * 2f);
            col.radius = radius;
            col.center = new Vector3(0, b.center.y, 0);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = radius;
            agent.height = Mathf.Max(0.5f, b.size.y);
            agent.angularSpeed = 360f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;

            var eye = Child(root.transform, "EyePoint", eyePos);
            var anim = root.AddComponent<RobotAnimator>();
            anim.Model = model.transform;
            var rc = root.AddComponent<RobotController>();
            root.AddComponent<DetectorNet>();
            rc.Point = eye.transform;
            rc.Eye = eye.transform;
            rc.Status.Renderers = Parts(model, "Eye");
            rc.Status.Light = StatusLight(root.transform, eyePos + new Vector3(0, 0.05f, 0.15f), 3.5f, 2.5f, 35f);
            rc.RadioRipplePrefab = ripple;
            rc.StompRipplePrefab = s_StompRipple;
            // All the numbers live in a profile next to the prefab (tuned by hand, never overwritten).
            rc.Profile = Profile<RobotProfile>(name, p => config(p.Senses, p.Traits, p.Animation));
            BuildSensorRig(root, model, rc, bodyMat, eyePos, radius);
            AddLoop(root, MotorFor(name, rc.Profile.Traits.HoverHeight > 0f), name == "RobotWorker" ? 1.3f : name == "RobotSorter" ? 1.2f : 1f);
            // what glows says what it senses: the eye mount's lens (blue: it sees), the body's own lamps (yellow: it hears),
            // never the nose or the ground probes (StatusLights, SecurityPalette.Glow)
            bool InMount(Renderer r, string mount)
            {
                for (var t = r.transform; t != null && t != model.transform; t = t.parent) if (t.name.StartsWith(mount)) return true;
                return false;
            }
            var glowing = Parts(model, "Eye");
            rc.Status.Renderers = glowing.Where(r => InMount(r, "SensorEye")).ToArray();
            rc.Status.Ears = glowing.Where(r => !InMount(r, "Sensor")).ToArray();
            rc.Status.Dark = glowing.Where(r => InMount(r, "Sensor") && !InMount(r, "SensorEye")).ToArray();
            return root;
        }

        /// <summary>Every kind of robot hums in its own way: joints of the legged ones, tracks, brush, turbine, rotor, wheels.</summary>
        static AudioClip MotorFor(string name, bool hover) => hover ? s_Bank.DroneRotor : name switch
        {
            "RobotGuard" or "RobotHound" => s_Bank.MotorServo,
            "RobotThumper" or "RobotWorker" => s_Bank.MotorTracks,
            "RobotSweeper" => s_Bank.MotorBrush,
            "RobotHunter" => s_Bank.MotorTurbine,
            _ => s_Bank.RobotMotor,
        };

        /// <summary>
        /// Every kind of sensor mount on the model (camera eye, two microphones, two ground probes, a snout);
        /// <see cref="SensorRig"/> keeps only those the robot's senses use.
        /// </summary>
        static void BuildSensorRig(GameObject root, GameObject model, RobotController rc, string bodyMat, Vector3 eye, float radius)
        {
            var rig = root.AddComponent<SensorRig>();
            rig.Robot = rc;
            rig.Model = model.transform; // in the game the mounts seat themselves on the body of the current style
            // Mounted where each robot's design expects them (the bestiary contracts) at their modelled size, painted like
            // the robot itself: its body colour, its accent, its lenses.
            float side = Mathf.Max(0.2f, radius * 0.75f);
            GameObject Mount(string file, string name, Vector3 position, Quaternion rotation)
            {
                var go = Model(file);
                go.name = name;
                go.transform.SetParent(model.transform, false);
                go.transform.localPosition = position;
                go.transform.localRotation = rotation;
                go.transform.localScale = Vector3.one;
                Paint(go, bodyMat, "RobotAccent", "SensorGlow");
                return go;
            }
            rig.Eyes = new[] { Mount("attach_eye", "SensorEye", eye + new Vector3(0, 0, 0.08f), Quaternion.Euler(rc.Profile.Senses.EyePitch, 0, 0)) };
            // no microphone stalks: from behind the two of them read as an odd pair of discs on the robot's back (playtest);
            // what a robot hears is in its description (RobotInfo)
            rig.Microphones = new GameObject[0];
            rig.GroundProbes = new[]
            {
                Mount("attach_seismic", "SensorGroundL", new Vector3(-side, 0, radius + 0.1f), Quaternion.identity),
                Mount("attach_seismic", "SensorGroundR", new Vector3(side, 0, radius + 0.1f), Quaternion.identity),
            };
            rig.Noses = new[] { Mount("attach_nose", "SensorNose", eye + new Vector3(0, -0.15f, 0.12f), Quaternion.identity) };
        }

        /// <summary>Robot / device with its own movement script (<see cref="SecurityDevice"/>).</summary>
        static GameObject Device<T>(string name, string file, string bodyMat, bool moves, bool fullRotation, Action<GameObject, T> config) where T : SecurityDevice
        {
            var root = NetworkedRoot(name, moves, fullRotation);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, bodyMat, "RobotAccent", "SensorGlow");
            var b = LocalBounds(root);
            RobotAnimator.BakePivots(model.transform);
            WalkingLegs(root, model);
            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;

            var eye = Child(root.transform, "EyePoint", b.center);
            root.AddComponent<RobotAnimator>().Model = model.transform;
            var device = root.AddComponent<T>();
            root.AddComponent<DetectorNet>();
            device.Point = eye.transform;
            device.Status.Renderers = Parts(model, "Eye");
            device.Status.Light = StatusLight(root.transform, b.center + Vector3.up * (b.extents.y + 0.1f), 3f, 2f, 90f);
            config(root, device);
            return root;
        }

        const string LaserFanMeshPath = Root + "/Art/Models/FX/LaserHalfFan.asset";

        /// <summary>
        /// Half of a laser fan: a right triangle with the apex at the origin, straight down 1 m and out 1 m along +X on the
        /// floor (scaled to the room each frame), drawn double-sided with the additive laser sheet material.
        /// </summary>
        static MeshRenderer LaserFan(GameObject root, string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LaserFanMeshPath);
            if (mesh == null)
            {
                EnsureFolder(Root + "/Art/Models/FX");
                mesh = new Mesh { name = "LaserHalfFan" };
                mesh.vertices = new[] { Vector3.zero, new Vector3(0f, -1f, 0f), new Vector3(1f, -1f, 0f) };
                mesh.uv = new[] { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
                mesh.triangles = new[] { 0, 2, 1 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, LaserFanMeshPath);
            }
            var go = Child(root.transform, name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = M("LaserSheet");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static LineRenderer Beam(GameObject root, string name, float width)
        {
            var lr = Child(root.transform, name).AddComponent<LineRenderer>();
            lr.sharedMaterial = M("Laser");
            lr.widthMultiplier = width;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            return lr;
        }

        static GameObject BuildSecurityCamera()
        {
            var root = NetworkedRoot("SecurityCamera", false, false);
            var model = Model("security_camera");
            model.transform.SetParent(root.transform, false);
            Paint(model, "RobotAccent", "RobotBody", "SensorGlow");
            // Only the head turns: the wall plate stays, the arm and housing swivel at the bracket.
            var pivot = Child(root.transform, "Pivot", new Vector3(0f, 0f, -0.24f));
            foreach (var part in model.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform).ToArray())
                if (part.name.StartsWith("Accent") || part.name.StartsWith("Eye")) part.SetParent(pivot.transform, true);

            var spotGo = Child(pivot.transform, "Spot", new Vector3(0, -0.02f, 0.49f));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            ShadowedLight(spot, UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium);

            var sc = root.AddComponent<SecurityCamera>();
            root.AddComponent<DetectorNet>();
            sc.Point = spotGo.transform;
            sc.Pivot = pivot.transform;
            sc.Spot = spot;
            sc.Profile = Profile<CameraProfile>("SecurityCamera", p => p.AlarmRadius = 35f);
            sc.Status.Renderers = Parts(model, "Eye");
            sc.Status.Emission = 3f;
            return root;
        }

        static GameObject BuildLaser()
        {
            var root = NetworkedRoot("LaserGate", false, false);
            Transform Emitter(string n, float yaw)
            {
                var holder = Child(root.transform, n);
                var model = Model("laser_emitter");
                model.transform.SetParent(holder.transform, false);
                model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                Paint(model, "LaserPost", "LaserPost", "Laser");
                return holder.transform;
            }
            var gate = root.AddComponent<LaserGate>();
            gate.Profile = Profile<LaserGateProfile>("LaserGate");
            root.AddComponent<LaserGateNet>();
            AddLoop(root, s_Bank.LaserHum);
            gate.EmitterA = Emitter("EmitterA", 90f);
            gate.EmitterB = Emitter("EmitterB", -90f);
            gate.Beams = Enumerable.Range(0, 3)
                .Select(i => Cube(root.transform, $"Beam{i}", new Vector3(0, 1f, 0), new Vector3(1.8f, LaserGate.BeamThickness, LaserGate.BeamThickness), M("Laser"), false).transform)
                .ToArray();
            return root;
        }

        static RadioRipple s_StompRipple;

        /// <summary>The floor wave of a seismic stomp: a wide ring running out along the floor (radius set per robot).</summary>
        static GameObject BuildStompRipple()
        {
            var go = BuildRadioRipple();
            go.name = "StompRipple";
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = M("Stomp");
            lr.positionCount = 64;
            var r = go.GetComponent<RadioRipple>();
            r.Duration = 1.1f;
            r.StartRadius = 0.6f;
            r.EndRadius = 10f;
            r.Height = 0.04f;
            r.Width = 0.14f;
            return go;
        }

        static GameObject BuildRadioRipple()
        {
            var go = new GameObject("RadioRipple");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = M("Radio");
            lr.loop = true;
            lr.positionCount = 32;
            lr.widthMultiplier = 0.04f;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<RadioRipple>();
            return go;
        }
    }
}
