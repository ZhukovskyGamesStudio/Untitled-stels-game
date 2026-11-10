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
                b.MenuMusic = Clip("Music/music_menu");
                b.ExploreMusic = Clip("Music/music_explore");
                b.ChaseMusic = Clip("Music/music_chase");
            });
            // Clips added after the bank was created (the bank itself is never overwritten without FORCE).
            if (s_Bank.Ping == null || s_Bank.Mark == null)
            {
                s_Bank.Ping = Clip("UI/ping");
                s_Bank.Mark = Clip("UI/mark");
                EditorUtility.SetDirty(s_Bank);
            }
            return s_Bank;
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
        static void AddLoop(GameObject root, AudioClip clip)
        {
            var source = root.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;
            root.AddComponent<LoopSound>().Source = source;
        }
    }
}
