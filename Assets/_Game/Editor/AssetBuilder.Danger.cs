using System;
using System.IO;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The robots of the danger zones (warden, repairman, smoke machine, relay, nurse, mimic, salesman, armed guard,
    /// driller, exterminator, banshee, maniac), the patches they leave (goo, smoke, gas) and what a held player wears
    /// (a cocoon, a net). Models: Tools/Blender/danger_bots_*.py; until a model is exported its stand-in is used.
    /// </summary>
    public static partial class AssetBuilder
    {
        /// <summary>The model if it is there, else its stand-in (a new kind before its own model is exported).</summary>
        static string ModelOr(string file, string standIn)
        {
            foreach (var guid in AssetDatabase.FindAssets(file + " t:Model", new[] { ModelsDir }))
                if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == file) return file;
            Debug.LogWarning($"[Builder] Model {file}.fbx is not there yet: {standIn} stands in");
            return standIn;
        }

        static void DangerMaterials()
        {
            const string fx = "FX";
            Mat(fx, "Goo", new Color(0.36f, 0.62f, 0.22f), smoothness: 0.85f);
            Mat(fx, "Tracer", new Color(1.6f, 1.3f, 0.5f), unlit: true);
        }

        /// <summary>
        /// The material of a cloud's puffs: lit particles (the room's lamps light the smoke: in a dark room it is dark),
        /// alpha blended, the puff sheet as its picture; the colour comes from each particle.
        /// </summary>
        static Material CloudMaterial(string name)
        {
            var mat = LitAsset($"{MaterialsDir}/FX/{name}.mat");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (particles != null) mat.shader = particles;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpritesDir}/SmokePuff.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            Translucent(mat);
            return mat;
        }

        /// <summary>A patch a robot leaves: a puddle (the model scaled to its radius) or a cloud (particles).</summary>
        static HazardView BuildPatch(string name, HazardKind kind)
        {
            return Prefab($"{PrefabsDir}/FX/{name}.prefab", () =>
            {
                var go = new GameObject(name);
                var view = go.AddComponent<HazardView>();
                view.Kind = kind;
                if (kind == HazardKind.Goo)
                {
                    GameObject body;
                    if (ModelOr("goo_puddle", "") == "goo_puddle") body = Model("goo_puddle");
                    else
                    {
                        body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        body.transform.localScale = new Vector3(2f, 0.01f, 2f);
                        var holder = new GameObject("Model");
                        body.transform.SetParent(holder.transform, false);
                        body = holder;
                    }
                    body.name = "Body";
                    body.transform.SetParent(go.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                    foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    foreach (var r in body.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        r.sharedMaterial = M("Goo");
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    view.Body = body.transform;
                    return go;
                }
                // a cloud of billowing puffs (Tools/Art/generate_smoke.py: four shapes on a sheet), lit by the room's lamps:
                // born small and thin, each swells, turns slowly, drifts on a gentle turbulence and thins out; the smoke
                // rises a little and rolls along the ceiling, the gas hugs the floor
                bool smokeKind = kind == HazardKind.Smoke;
                var cloud = Child(go.transform, "Cloud", Vector3.up * (smokeKind ? 0.9f : 0.5f));
                var ps = cloud.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 5f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = smokeKind
                    ? new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.63f, 0.66f, 0.55f), new Color(0.42f, 0.43f, 0.46f, 0.7f))
                    : new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.9f, 0.36f, 0.4f), new Color(0.45f, 0.75f, 0.25f, 0.55f));
                main.gravityModifier = smokeKind ? -0.012f : 0.006f;
                main.maxParticles = 220;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                main.prewarm = false;
                var emission = ps.emission;
                emission.rateOverTime = smokeKind ? 22f : 16f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(smokeKind ? 26 : 18)) }); // the puff it starts with
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 2f;
                shape.radiusThickness = 1f;
                shape.scale = new Vector3(1f, smokeKind ? 0.55f : 0.3f, 1f); // a low dome, not a ball through the floor
                var fade = ps.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                var grow = ps.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f, 0f, 1.6f), new Keyframe(0.35f, 0.9f), new Keyframe(1f, 1.35f)));
                var spin = ps.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
                var drift = ps.noise;
                drift.enabled = true;
                drift.strength = 0.22f;
                drift.frequency = 0.35f;
                drift.scrollSpeed = 0.15f;
                drift.damping = true;
                var slow = ps.limitVelocityOverLifetime;
                slow.enabled = true;
                slow.limit = 0.45f;
                slow.dampen = 0.2f;
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); // one shape per puff, picked at birth
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.999f / 4f);
                var pr = cloud.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sortMode = ParticleSystemSortMode.Distance;
                pr.sharedMaterial = CloudMaterial(smokeKind ? "SmokeCloud" : "GasCloud");
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pr.receiveShadows = true;
                view.Cloud = ps;
                return go;
            }).GetComponent<HazardView>();
        }

        /// <summary>What a held player wears (a cocoon, a net): a model under the player's root, hidden until needed.</summary>
        static GameObject HeldView(GameObject player, string name, string model, string standIn, Vector3 at, Vector3 scale, Material material)
        {
            var view = Model(ModelOr(model, standIn));
            view.name = name;
            view.transform.SetParent(player.transform, false);
            view.transform.localPosition = at;
            view.transform.localScale = scale;
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in view.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.name.StartsWith("Accent")) r.sharedMaterial = material;
                else r.sharedMaterial = M("RobotAccent");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            view.SetActive(false);
            return view;
        }

        /// <summary>The cocoon and the net on the player prefab (PlayerController.CocoonView / NetView).</summary>
        static void AddHeldViews(GameObject player)
        {
            var pc = player.GetComponent<PlayerController>();
            pc.CocoonView = HeldView(player, "Cocoon", "cocoon", "web_sheet", Vector3.zero, Vector3.one, WebMaterial());
            pc.NetView = HeldView(player, "Net", "net", "web_sheet", Vector3.zero, Vector3.one, WebMaterial());
        }

        /// <summary>The sweeper leaves goo (its patch prefab); called for its prefab.</summary>
        static void GivePatch(GameObject robot, HazardView patch) => robot.GetComponent<RobotController>().PatchPrefab = patch;

        /// <summary>The new kinds' prefabs. <paramref name="robot"/>: registers a prefab for a kind (BuildActors' Robot).</summary>
        static void BuildDangerRobots(Action<RobotKind, string, Func<GameObject>> robot, RadioRipple ripple, HazardView goo, HazardView smoke, HazardView gas)
        {
            robot(RobotKind.Warden, RobotPrefabs + "/RobotWarden.prefab", () => MobileRobot("RobotWarden", ModelOr("robot_warden", "robot_worker"), "WorkerBody", 0.4f, new Vector3(0, 1.2f, 0.27f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 9f; s.ViewAngle = 90f; s.DetectionSpeed = 0.5f; s.HearingMultiplier = 0.4f;
                t.CanAttack = false; t.WarningTime = 3f; t.AlarmRadius = 35f; // staff: takes fright, then reports
                t.CanUseStairs = false;
                t.PatrolSpeed = 1.5f; t.InvestigateSpeed = 1.8f; t.ChaseSpeed = 1.8f;
                t.Role = RobotRole.Warden; t.RolePeriod = 12f; t.RoleRadius = 14f; t.RoleLimit = 3; // three locks set again per house
            }));
            robot(RobotKind.Repairman, RobotPrefabs + "/RobotRepairman.prefab", () => MobileRobot("RobotRepairman", ModelOr("robot_repairman", "robot_thumper"), "WorkerBody", 0.4f, new Vector3(-0.15f, 0.58f, 0.22f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 8f; s.ViewAngle = 90f; s.DetectionSpeed = 0.45f; s.HearingMultiplier = 0.4f;
                t.CanAttack = false; t.WarningTime = 3f; t.AlarmRadius = 35f;
                t.PatrolSpeed = 1.6f; t.InvestigateSpeed = 2f; t.ChaseSpeed = 2f;
                t.Role = RobotRole.Repairman; t.RolePeriod = 10f; t.RoleRadius = 18f;
            }));
            robot(RobotKind.Smoker, RobotPrefabs + "/RobotSmoker.prefab", () =>
            {
                var go = MobileRobot("RobotSmoker", ModelOr("robot_smoker", "robot_sweeper"), "SweeperBody", 0.45f, new Vector3(0, 0.8f, 0.3f), ripple, (s, t, anim) =>
                {
                    s.HasEyes = false; s.HearingMultiplier = 0f; // it senses nothing: it only smokes
                    t.CanAttack = false; t.CanUseStairs = false;
                    t.PatrolSpeed = 1.2f; t.InvestigateSpeed = 1.2f; t.ChaseSpeed = 1.2f;
                    t.PatrolWaitMin = 1f; t.PatrolWaitMax = 3f;
                    t.Role = RobotRole.Smoker; t.RolePeriod = 22f; t.RoleRadius = 3.5f; t.RoleDuration = 14f;
                });
                GivePatch(go, smoke);
                return go;
            });
            robot(RobotKind.Nurse, RobotPrefabs + "/RobotNurse.prefab", () => MobileRobot("RobotNurse", ModelOr("robot_nurse", "robot_sorter"), "RobotBody", 0.32f, new Vector3(0, 1.53f, 0.25f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 11f; s.ViewAngle = 110f; s.DetectionSpeed = 0.6f; s.HearingMultiplier = 0.6f;
                t.CanUseStairs = false;
                t.PatrolSpeed = 1.6f; t.InvestigateSpeed = 2.6f; t.ChaseSpeed = 3.6f;
                t.Attack = AttackKind.Sedate; t.CarriesPlayers = true; // the patient is put to sleep and taken to a bed
            }));
            robot(RobotKind.Mimic, RobotPrefabs + "/RobotMimic.prefab", () =>
            {
                var go = MobileRobot("RobotMimic", ModelOr("robot_mimic", "robot_thumper"), "WorkerBody", 0.42f, new Vector3(0, 0.49f, 0.29f), ripple, (s, t, anim) =>
                {
                    s.ViewDistance = 9f; s.ViewAngle = 120f; s.DetectionSpeed = 0.8f; s.HearingMultiplier = 0.4f;
                    t.PatrolSpeed = 1.8f; t.InvestigateSpeed = 3f; t.ChaseSpeed = 4.4f; t.LoseTargetAfter = 3f;
                    t.WarningTime = 0.5f; t.AlarmHuntTime = 6f; // it wakes fast and soon freezes again somewhere else
                    t.Role = RobotRole.Mimic;
                });
                go.AddComponent<MimicBait>().Robot = go.GetComponent<RobotController>();
                // painted as the crate it passes for (the Crate prop: wood and metal), not as a robot
                var crate = go.GetComponent<RobotAnimator>().Model;
                foreach (var r in crate.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name;
                    if (n.StartsWith("Sensor") || r.transform.parent != null && r.transform.parent.name.StartsWith("Sensor")) continue;
                    if (n.StartsWith("Accent")) r.sharedMaterial = M("Metal");
                    else if (n.StartsWith("Body")) r.sharedMaterial = M("Wood");
                }
                // a crate does not hum
                foreach (var loop in go.GetComponents<Stealth.Audio.LoopSound>())
                {
                    if (loop.Source != null) Object.DestroyImmediate(loop.Source, true);
                    Object.DestroyImmediate(loop, true);
                }
                return go;
            });
            robot(RobotKind.Salesman, RobotPrefabs + "/RobotSalesman.prefab", () => MobileRobot("RobotSalesman", ModelOr("robot_salesman", "robot_listener"), "ListenerBody", 0.4f, new Vector3(0, 1.52f, 0.18f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 12f; s.ViewAngle = 120f; s.DetectionSpeed = 0.9f; s.HearingMultiplier = 0.5f;
                t.CanUseStairs = false;
                t.PatrolSpeed = 1.6f; t.InvestigateSpeed = 3f; t.ChaseSpeed = 4.2f; t.LoseTargetAfter = 6f; t.WarningTime = 0.8f;
                t.Attack = AttackKind.Pester; t.AttackRange = 2.5f; t.AttackCooldown = 2.2f; t.RoleRadius = 14f; // how far its talk carries
            }));
            robot(RobotKind.Gunner, RobotPrefabs + "/RobotGunner.prefab", () =>
            {
                var go = MobileRobot("RobotGunner", ModelOr("robot_gunner", "robot_guard"), "RobotBody", 0.45f, new Vector3(0, 1.84f, 0.29f), ripple, (s, t, anim) =>
                {
                    anim.Bob = 0.06f;
                    s.ViewDistance = 15f; s.ViewAngle = 100f; s.DetectionSpeed = 0.7f;
                    t.PatrolSpeed = 1.8f; t.InvestigateSpeed = 2.8f; t.ChaseSpeed = 3.6f;
                    t.Attack = AttackKind.Shoot; t.AttackRange = 11f; t.AttackCooldown = 2.8f; // break the line of sight between shots
                    t.CarriesPlayers = true;
                });
                var rc = go.GetComponent<RobotController>();
                var muzzle = Child(go.transform, "Muzzle", new Vector3(0.5f, 1.1f, 0.99f));
                rc.Muzzle = muzzle.transform;
                var tracer = Child(muzzle.transform, "Tracer").AddComponent<LineRenderer>();
                tracer.sharedMaterial = M("Tracer");
                tracer.widthMultiplier = 0.03f;
                tracer.positionCount = 2;
                tracer.useWorldSpace = false;
                tracer.SetPosition(0, Vector3.zero);
                tracer.SetPosition(1, Vector3.forward * 11f);
                tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tracer.enabled = false;
                rc.Tracer = tracer;
                return go;
            });
            robot(RobotKind.Driller, RobotPrefabs + "/RobotDriller.prefab", () => MobileRobot("RobotDriller", ModelOr("robot_driller", "robot_thumper"), "ThumperBody", 0.6f, new Vector3(0, 1.15f, 0.23f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 10f; s.ViewAngle = 90f; s.DetectionSpeed = 0.6f; s.HearingMultiplier = 0.6f;
                t.CanUseStairs = false;
                t.PatrolSpeed = 1.2f; t.InvestigateSpeed = 2.4f; t.ChaseSpeed = 3.4f; t.AttackRange = 1.9f;
                t.Role = RobotRole.Driller; t.RoleRadius = 2.2f; t.RoleLimit = 4; // how far ahead a door or a wall is broken; walls per house
            }));
            robot(RobotKind.Exterminator, RobotPrefabs + "/RobotExterminator.prefab", () =>
            {
                var go = MobileRobot("RobotExterminator", ModelOr("robot_exterminator", "robot_thumper"), "SweeperBody", 0.5f, new Vector3(0, 1.04f, 0.27f), ripple, (s, t, anim) =>
                {
                    s.ViewDistance = 9f; s.ViewAngle = 110f; s.DetectionSpeed = 0.6f; s.HearingMultiplier = 0.4f;
                    t.CanUseStairs = false;
                    t.PatrolSpeed = 1.1f; t.InvestigateSpeed = 1.8f; t.ChaseSpeed = 2.4f;
                    t.Attack = AttackKind.Gas; t.AttackRange = 5f; t.AttackCooldown = 9f; t.RoleRadius = 5.5f; t.RoleDuration = 12f;
                });
                GivePatch(go, gas);
                return go;
            });
            robot(RobotKind.Banshee, RobotPrefabs + "/RobotBanshee.prefab", () => MobileRobot("RobotBanshee", ModelOr("robot_banshee", "robot_listener"), "ListenerBody", 0.33f, new Vector3(0, 1.8f, 0.22f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 1.4f; // blind: it comes to what it hears
                t.HoverHeight = 0.3f;
                t.PatrolSpeed = 1.4f; t.InvestigateSpeed = 3f; t.ChaseSpeed = 3.8f;
                t.Attack = AttackKind.Shriek; t.AttackRange = 6f; t.AttackCooldown = 7f; t.RoleRadius = 8f; t.RoleDuration = 3.5f;
            }));
            robot(RobotKind.Maniac, RobotPrefabs + "/RobotManiac.prefab", () => MobileRobot("RobotManiac", ModelOr("robot_maniac", "robot_guard"), "ThumperBody", 0.4f, new Vector3(0, 1.735f, 0.37f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 13f; s.ViewAngle = 110f; s.DetectionSpeed = 0.8f; s.HearingMultiplier = 0.5f;
                t.PatrolSpeed = 1.7f; t.InvestigateSpeed = 2.8f; t.ChaseSpeed = 4.3f; t.AttackRange = 1.6f;
                t.Role = RobotRole.Maniac; t.RolePeriod = 25f; t.RoleRadius = 6f; // how well it knows where its prey is
                t.CarriesPlayers = true;
            }));
            robot(RobotKind.Relay, SensorPrefabs + "/SensorRelay.prefab", () => Device<LureRelay>("SensorRelay", ModelOr("robot_relay", "sensor_lidar"), "TurretBody", false, false, (root, r) =>
            {
                r.Profile = Profile<RelayProfile>("SensorRelay", p => p.ForgetRate = 1f);
                // the loudspeaker plays a teammate's recorded words: a player's voice, made tinny (no lows, a little grit)
                r.Voice = AddVoice(root.transform);
                r.Voice.transform.localPosition = r.Point.localPosition;
                var tinny = r.Voice.gameObject.AddComponent<AudioHighPassFilter>();
                tinny.cutoffFrequency = 650f;
                tinny.highpassResonanceQ = 1.6f;
                r.Voice.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.3f;
            }));
        }
    }
}
