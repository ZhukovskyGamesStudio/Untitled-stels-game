using System.Linq;
using Stealth.Audio;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string AudioDir = Root + "/Art/Audio";

        static SoundBank s_Bank;
        static GameObject s_AudioDirector;

        static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{path}.wav");
            if (clip == null) Debug.LogWarning($"[Builder] Missing sound {path}: run Tools/Audio/generate_sounds.py");
            return clip;
        }

        /// <summary>Import settings: short sounds decompressed in memory, loops and music streamed / compressed.</summary>
        static void ConfigureAudioImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                bool music = path.Contains("/Music/");
                bool longLoop = music || path.Contains("office_hum") || path.Contains("gas_wind");
                var settings = importer.defaultSampleSettings;
                var wanted = longLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType == wanted && importer.forceToMono) continue;
                settings.loadType = wanted;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.6f : 0.8f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = music;
                importer.SaveAndReimport();
            }
        }

        static SoundBank BuildSoundBank()
        {
            ConfigureAudioImport();
            s_Bank = DataAsset<SoundBank>(DataDir + "/SoundBank.asset", b =>
            {
                b.Footsteps = Enumerable.Range(0, 4).Select(i => Clip($"Player/footstep_{i}")).ToArray();
                b.Jump = Clip("Player/jump");
                b.Land = Clip("Player/land");
                b.Hurt = Clip("Player/hurt");
                b.Downed = Clip("Player/downed");
                b.Revive = Clip("Player/revive");
                b.Pickup = Clip("Items/pickup");
                b.Drop = Clip("Items/drop");
                b.Sell = Clip("Items/sell");
                b.Stash = Clip("Items/stash");
                b.Click = Clip("UI/ui_click");
                b.Hover = Clip("UI/ui_hover");
                b.BackpackOpen = Clip("UI/ui_open");
                b.Banner = Clip("UI/banner");
                b.RunFailed = Clip("UI/fail");
                b.LevelDone = Clip("UI/level_done");
                b.Ping = Clip("UI/ping");
                b.Mark = Clip("UI/mark");
                b.Suspicious = Clip("Robots/robot_suspicious");
                b.Alert = Clip("Robots/robot_alert");
                b.Lost = Clip("Robots/robot_lost");
                b.Hacked = Clip("Robots/robot_hacked");
                b.Radio = Clip("Robots/radio");
                b.TurretShot = Clip("Robots/turret_shot");
                b.RobotStep = Clip("Robots/robot_step");
                b.RobotMotor = Clip("Robots/robot_motor");
                b.DroneRotor = Clip("Robots/drone_rotor");
                b.SeismicThump = Clip("Robots/seismic_thump");
                b.CameraBeep = Clip("Robots/camera_beep");
                b.VoiceSpotted = Clip("Robots/voice_spotted");
                b.VoiceChase = Clip("Robots/voice_chase");
                b.VoiceLost = Clip("Robots/voice_lost");
                b.VoiceEliminated = Clip("Robots/voice_eliminated");
                b.LaserHum = Clip("Environment/laser_hum");
                b.DoorSlide = Clip("Environment/door_slide");
                b.Alarm = Clip("Environment/alarm");
                b.OfficeHum = Clip("Environment/office_hum");
                b.GasWind = Clip("Environment/gas_wind");
                b.HandDryer = Clip("Environment/hand_dryer");
                b.MenuMusic = Clip("Music/music_menu");
                b.ExploreMusic = Clip("Music/music_explore");
                b.ChaseMusic = Clip("Music/music_chase");
                FillLateClips(b);
            });
            // Clips added after the bank was created (the bank itself is never overwritten without FORCE).
            if (s_Bank.Ping == null || s_Bank.Mark == null)
            {
                s_Bank.Ping = Clip("UI/ping");
                s_Bank.Mark = Clip("UI/mark");
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.CarEngine == null || s_Bank.SpiderStep == null || s_Bank.DoorOpen == null || s_Bank.DoorShut == null)
            {
                FillLateClips(s_Bank);
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.GuardCharge == null || s_Bank.GuardPatrol == null || s_Bank.GuardChase == null)
            {
                s_Bank.GuardCharge = Clip("Robots/guard_charge");
                s_Bank.GuardPatrol = Clip("Robots/guard_patrol");
                s_Bank.GuardChase = Clip("Robots/guard_chase");
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.Grind == null)
            {
                s_Bank.Grind = Clip("Car/grind");
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.WorkHack == null || s_Bank.WorkPick == null || s_Bank.WorkScrew == null)
            {
                s_Bank.WorkHack = Clip("Items/work_hack");
                s_Bank.WorkPick = Clip("Items/work_pick");
                s_Bank.WorkScrew = Clip("Items/work_screw");
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.HandDryer == null)
            {
                s_Bank.HandDryer = Clip("Environment/hand_dryer");
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.GasBreath == null || s_Bank.GasLaugh == null || s_Bank.GasLaugh.Length != 3 || s_Bank.HostageVoice == null || s_Bank.HostageVoice.Length != 5)
            {
                s_Bank.GasBreath = Clip("Player/gas_breath");
                s_Bank.GasLaugh = Enumerable.Range(0, 3).Select(i => Clip($"Player/gas_laugh_{i}")).ToArray();
                s_Bank.HostageVoice = new[] { "help", "key", "out", "quiet", "hurry" }.Select(n => Clip($"Voices/hostage_{n}")).ToArray();
                EditorUtility.SetDirty(s_Bank);
            }
            return s_Bank;
        }

        static void FillLateClips(SoundBank b)
        {
            b.StepLight = Clip("Robots/step_light");
            b.SpiderStep = Clip("Robots/spider_step");
            b.MotorServo = Clip("Robots/motor_servo");
            b.GuardCharge = Clip("Robots/guard_charge");
            b.GuardPatrol = Clip("Robots/guard_patrol");
            b.GuardChase = Clip("Robots/guard_chase");
            b.MotorTracks = Clip("Robots/motor_tracks");
            b.MotorBrush = Clip("Robots/motor_brush");
            b.MotorTurbine = Clip("Robots/motor_turbine");
            b.SpeakerMusic = Clip("Items/speaker_music");
            b.FuelGulp = Clip("Items/fuel_gulp");
            b.Heal = Clip("Items/heal");
            b.Craft = Clip("Items/craft");
            b.StunBlast = Clip("Items/stun_blast");
            b.CrateSearch = Clip("Items/crate_search");
            b.Unlock = Clip("Items/unlock");
            b.GateOpen = Clip("Environment/gate_open");
            b.CarEngine = Clip("Car/car_engine");
            b.CarStart = Clip("Car/car_start");
            b.CarStop = Clip("Car/car_stop");
            b.CarHorn = Clip("Car/car_horn");
            b.CarBump = Clip("Car/car_bump");
            b.DoorOpen = Clip("Environment/door_open");
            b.DoorShut = Clip("Environment/door_shut");
        }

        /// <summary>Music, ambience, alarm, a 2D voice and a pool of 3D voices; placed in the scene.</summary>
        static GameObject BuildAudioDirector(SoundBank bank) => Prefab(PrefabsDir + "/Audio/AudioDirector.prefab", () =>
        {
            var root = new GameObject("AudioDirector");
            var d = root.AddComponent<AudioDirector>();
            d.Bank = bank;
            AudioSource Source(string name) => Child(root.transform, name).AddComponent<AudioSource>();
            d.MusicA = Source("MusicA");
            d.MusicB = Source("MusicB");
            d.AmbienceInside = Source("AmbienceInside");
            d.AmbienceOutside = Source("AmbienceOutside");
            d.AlarmSource = Source("Alarm");
            d.Flat = Source("Flat");
            d.Voices = Enumerable.Range(0, 16).Select(i => Source($"Voice{i}")).ToArray();
            return root;
        });

        /// <summary>A looping 3D sound on a prefab (configured at runtime by <see cref="LoopSound"/>).</summary>
        static void AddLoop(GameObject root, AudioClip clip, float pitch = 1f)
        {
            var source = root.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = pitch;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;
            root.AddComponent<LoopSound>().Source = source;
        }
    }
}
