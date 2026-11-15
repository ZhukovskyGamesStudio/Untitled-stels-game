using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>
        /// The furniture of the themed rooms (Tools/Blender/rooms_*.py → Art/Models/Props): kind, model file and the
        /// materials of its parts by role — Body, Accent, Trim (defaults to Accent), Eye (glows). Placed by the Furnisher.
        /// </summary>
        static readonly (PropKind kind, string file, string body, string accent, string trim, string eye)[] RoomPropTable =
        {
            (PropKind.KitchenSink, "kitchen_sink", "PropBody", "Metal", "Wood", "Metal"),
            (PropKind.Stove, "stove", "Ceiling", "Metal", "Metal", "PropScreen"),
            (PropKind.Microwave, "microwave", "Ceiling", "Metal", "Metal", "PropScreen"),
            (PropKind.WallCabinet, "wall_cabinet", "PropBody", "Metal", "Metal", "Metal"),
            (PropKind.Urinal, "urinal", "Ceiling", "Metal", "Metal", "Metal"),
            (PropKind.MirrorSink, "mirror_sink", "Ceiling", "Metal", "Metal", "Mirror"),
            (PropKind.HandDryer, "hand_dryer", "Ceiling", "Metal", "Metal", "LampBulb"),
            (PropKind.Bench, "bench", "Wood", "Metal", "Metal", "Metal"),
            (PropKind.ShowerStall, "shower_stall", "Ceiling", "Metal", "Metal", "StairGlass"),
            (PropKind.Treadmill, "treadmill", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.ExerciseBike, "exercise_bike", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.WeightBench, "weight_bench", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.DumbbellRack, "dumbbell_rack", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.PunchingBag, "punching_bag", "Rubber", "Vending", "Metal", "Metal"),
            (PropKind.HospitalBed, "hospital_bed", "Ceiling", "Fabric", "Metal", "Metal"),
            (PropKind.MedicineCabinet, "medicine_cabinet", "Ceiling", "Metal", "PropAccent", "StairGlass"),
            (PropKind.PrivacyScreen, "privacy_screen", "Metal", "Fabric", "Fabric", "Metal"),
            (PropKind.SleepPod, "sleep_pod", "Ceiling", "Fabric", "Fabric", "LampBulb"),
            (PropKind.Hammock, "hammock", "Metal", "Fabric", "Fabric", "Metal"),
            (PropKind.TallBookcase, "tall_bookcase", "Wood", "Folders", "PropAccent", "Folders"),
            (PropKind.ReadingTable, "reading_table", "Wood", "Metal", "Folders", "LampBulb"),
            (PropKind.Globe, "globe", "Wood", "Metal", "PropAccent", "Metal"),
            (PropKind.Copier, "copier", "Ceiling", "Rack", "Rack", "PropScreen"),
            (PropKind.PaperStack, "paper_stack", "Cardboard", "Ceiling", "Wood", "Ceiling"),
            (PropKind.MailShelf, "mail_shelf", "Wood", "Ceiling", "Cardboard", "Ceiling"),
            (PropKind.Shredder, "shredder", "Rack", "Metal", "Metal", "LampBulb"),
            (PropKind.MonitorWall, "monitor_wall", "Rack", "Rack", "Rack", "PropScreen"),
            (PropKind.ConsoleDesk, "console_desk", "PropBody", "Rack", "Rack", "PropScreen"),
            (PropKind.MixingDesk, "mixing_desk", "Wood", "Rack", "Metal", "PropScreen"),
            (PropKind.MicStand, "mic_stand", "Rack", "Metal", "Fabric", "Metal"),
            (PropKind.AcousticPanel, "acoustic_panel", "Fabric", "Rack", "Rack", "Rack"),
            (PropKind.DrumKit, "drum_kit", "Ceiling", "Metal", "Metal", "Metal"),
            (PropKind.PoolTable, "pool_table", "Wood", "Felt", "PropAccent", "Felt"),
            (PropKind.Foosball, "foosball", "Wood", "Metal", "PropAccent", "Metal"),
            (PropKind.DartBoard, "dart_board", "Wood", "PropAccent", "Rack", "PropAccent"),
            (PropKind.BarCounter, "bar_counter", "Wood", "Metal", "Rack", "Metal"),
            (PropKind.BarStool, "bar_stool", "Metal", "Fabric", "Fabric", "Metal"),
            (PropKind.LabBench, "lab_bench", "PropBody", "Rack", "Metal", "PropScreen"),
            (PropKind.PartsCabinet, "parts_cabinet", "Metal", "PropAccent", "Rack", "PropAccent"),
            (PropKind.RobotArm, "robot_arm", "BeanBag", "Rack", "Rack", "LampBulb"), // the arm in safety orange
        };

        /// <summary>Kinds hung on a wall or standing on a counter: no collider (nothing walks into them, nothing stands on them).</summary>
        static readonly HashSet<PropKind> NoCollider = new() { PropKind.Microwave, PropKind.HandDryer, PropKind.AcousticPanel, PropKind.DartBoard };

        /// <summary>Cupboards whose doors open (Items.Storage, models with BodyDoor{n} parts): loot may hide behind them.</summary>
        static readonly HashSet<PropKind> Opens = new() { PropKind.WallCabinet, PropKind.MedicineCabinet, PropKind.PartsCabinet, PropKind.MailShelf };

        /// <summary>Prefabs of the themed rooms' furniture whose models exist, into <see cref="LevelArt.RoomProps"/>.</summary>
        static void BuildRoomProps(LevelArt art)
        {
            string p = PropPrefabs + "/";
            var list = new List<PropPrefab>();
            foreach (var (kind, file, body, accent, trim, eye) in RoomPropTable)
            {
                if (!HasModel(file)) { Debug.LogWarning($"[Builder] no model {file} yet: {kind} is left out"); continue; }
                var prefab = Prefab(p + kind + ".prefab", () =>
                {
                    var go = PropObject(kind.ToString(), file, body, accent, eye, collider: !NoCollider.Contains(kind));
                    PaintTrim(go, trim);
                    if (kind == PropKind.HandDryer) DressDryer(go);
                    if (Opens.Contains(kind)) go.AddComponent<Stealth.Items.Storage>();
                    return go;
                });
                list.Add(new PropPrefab { Kind = kind, Prefab = prefab });
            }
            if (art.RoomProps != null && art.RoomProps.Length == list.Count && art.RoomProps.Zip(list, (a, b) => a.Kind == b.Kind && a.Prefab == b.Prefab).All(x => x)) return;
            art.RoomProps = list.ToArray();
            EditorUtility.SetDirty(art);
        }

        static bool HasModel(string file)
        {
            try { return AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(file)) != null; }
            catch (System.IO.FileNotFoundException) { return false; }
        }

        /// <summary>Parts named Trim* take their own material (PropObject paints them as the body).</summary>
        static void PaintTrim(GameObject go, string trim)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name.StartsWith("Trim")) r.sharedMaterial = M(trim);
        }

        /// <summary>The dryer: E switches it on (HandDryer), its roar is a 3D sound heard down the corridor, the LED lights.</summary>
        static void DressDryer(GameObject go)
        {
            var dryer = go.AddComponent<HandDryer>();
            var sound = go.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.loop = false;
            sound.spatialBlend = 1f;
            sound.rolloffMode = AudioRolloffMode.Linear;
            sound.minDistance = 2f;
            sound.maxDistance = 22f;
            sound.volume = 0.9f;
            dryer.Sound = sound;
            dryer.Led = go.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye"));
            if (dryer.Led != null) dryer.Led.enabled = false;
        }

        /// <summary>Floors of the themed rooms in the storybook style (Blue Prince): tiles where water is, rubber, carpet, lino.</summary>
        static RoomFloor[] StorybookRoomFloors()
        {
            RoomFloor F(RoomType room, string mat, float tile) => new() { Room = room, Material = K(mat), Tile = tile };
            return new[]
            {
                F(RoomType.Kitchen, "Floor_Checker", 1.2f),
                F(RoomType.Restroom, "Floor_SmallTiles", 1f),
                F(RoomType.LockerRoom, "Floor_SmallTiles", 1f),
                F(RoomType.Gym, "Floor_Rubber", 1f),
                F(RoomType.Library, "Floor_CarpetGreen", 1f),
                F(RoomType.NapRoom, "Floor_CarpetBlue", 1f),
                F(RoomType.Studio, "Floor_CarpetCharcoal", 1f),
                F(RoomType.GameRoom, "Floor_CarpetRed", 1f),
                F(RoomType.Infirmary, "Floor_Lino", 1.2f),
                F(RoomType.Lab, "Floor_Lino", 1.2f),
                F(RoomType.Server, "Floor_Lino", 1.2f),
                F(RoomType.CopyRoom, "Floor_CarpetCharcoal", 1f),
                F(RoomType.ControlRoom, "Floor_CarpetCharcoal", 1f),
            };
        }
    }
}
