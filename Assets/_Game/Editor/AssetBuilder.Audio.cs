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
            if (clip == null) Debug.LogWarning($"[Builder] Missing sound {path}: run Tools/Audio/import_cc0_sounds.py or generate_sounds.py");
            return clip;
        }

        /// <summary>Import settings: short sounds decompressed in memory, loops and music streamed / compressed.</summary>
        static void ConfigureAudioImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                if (path == VoiceFlatPath) continue; // stays raw PCM (VoiceFlatClip)
                bool music = path.Contains("/Music/");
                bool longLoop = music || path.Contains("office_hum") || path.Contains("gas_wind");
                // short loops (the car's engine, robots' motors and rotors, hums) stay uncompressed: Vorbis pads a clip's
                // end, and the seam of a loop under a second long clicked at every wrap
                bool seamless = !longLoop && ShortLoop(path);
                var settings = importer.defaultSampleSettings;
                var wanted = longLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                var format = seamless ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
                // short sounds are loaded with the scene: played on demand, a clip whose data was not loaded yet failed
                // in the player ("Cannot create FMOD::Sound instance … Unsupported file or audio format")
                bool preload = !longLoop;
                if (settings.loadType == wanted && settings.compressionFormat == format && settings.preloadAudioData == preload && importer.forceToMono) continue;
                settings.loadType = wanted;
                settings.compressionFormat = format;
                settings.preloadAudioData = preload;
                settings.quality = music ? 0.6f : 0.8f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = music;
                importer.SaveAndReimport();
            }
        }

        /// <summary>A clip that plays as a short loop (an engine, a motor, a rotor, a hum): kept as PCM for a seamless wrap.</summary>
        static bool ShortLoop(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name == "car_engine" || name.StartsWith("motor_") || name == "robot_motor" || name == "drone_rotor" || name.EndsWith("_hum") || name == "grind"
                || name.StartsWith("guard_") || name == "alarm";
        }

        static SoundBank BuildSoundBank()
        {
            ConfigureAudioImport();
            s_Bank = DataAsset<SoundBank>(DataDir + "/SoundBank.asset", b =>
            {
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
                FillRecordedClips(b);
            });
            if (RecordedClipsMissing(s_Bank))
            {
                FillRecordedClips(s_Bank);
                EditorUtility.SetDirty(s_Bank);
            }
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
            if (s_Bank.HandDryer == null)
            {
                s_Bank.HandDryer = Clip("Environment/hand_dryer");
                EditorUtility.SetDirty(s_Bank);
            }
            if (s_Bank.GasBreath == null || s_Bank.HostageVoice == null || s_Bank.HostageVoice.Length != 5)
            {
                s_Bank.GasBreath = Clip("Player/gas_breath");
                s_Bank.HostageVoice = new[] { "help", "key", "out", "quiet", "hurry" }.Select(n => Clip($"Voices/hostage_{n}")).ToArray();
                EditorUtility.SetDirty(s_Bank);
            }
            return s_Bank;
        }

        static AudioClip[] Takes(string path, int count) => Enumerable.Range(0, count).Select(i => Clip($"{path}_{i}")).ToArray();

        static bool RecordedClipsMissing(SoundBank b) =>
            new[] { b.StepConcrete, b.StepTile, b.StepWood, b.StepSoft, b.StepCarpet, b.StepMetal, b.StepWet, b.GasCough, b.WorkHack, b.WorkPick, b.WorkScrew, b.WorkUnit, b.LadderFoot, b.LadderHand }
                .Any(a => a == null || a.Length == 0 || a.Any(c => c == null))
            || new[] { b.UnlockUnit, b.DoorHeavyOpen, b.DoorHeavyShut, b.DrawerOpen, b.DrawerClose, b.SteelDrawerOpen, b.SteelDrawerClose, b.CupboardOpen, b.CupboardClose,
                b.SteelCupboardOpen, b.SteelCupboardClose, b.SwitchOn, b.SwitchOff, b.HatchOpen, b.GrilleOff, b.SafeTick, b.Clank, b.Latch, b.TerminalKey, b.Bandage, b.Drink,
                b.TerminalGranted, b.TerminalHackDone, b.ConsoleSolved, b.VaultUnlocked }
                .Any(c => c == null);

        /// <summary>The clips cut from recordings (Tools/Audio/recorded.py) that have no older generated namesake.</summary>
        static void FillRecordedClips(SoundBank b)
        {
            b.StepConcrete = Takes("Player/step_concrete", 4);
            b.StepTile = Takes("Player/step_tile", 4);
            b.StepWood = Takes("Player/step_wood", 4);
            b.StepSoft = Takes("Player/step_soft", 4);
            b.StepCarpet = Takes("Player/step_carpet", 4);
            b.StepMetal = Takes("Player/step_metal", 4);
            b.StepWet = Takes("Player/step_wet", 4);
            b.GasCough = Takes("Player/gas_cough", 3);
            b.WorkHack = Takes("Items/work_hack", 4);
            b.WorkPick = Takes("Items/work_pick", 4);
            b.WorkScrew = Takes("Items/work_screw", 3);
            b.WorkUnit = Takes("Items/work_unit", 3);
            b.UnlockUnit = Clip("Items/unlock_unit");
            b.LadderFoot = Takes("Player/ladder_foot", 4);
            b.LadderHand = Takes("Player/ladder_hand", 4);
            b.TerminalGranted = Clip("Items/terminal_granted");
            b.TerminalHackDone = Clip("Items/terminal_hack_done");
            b.ConsoleSolved = Clip("Items/console_solved");
            b.VaultUnlocked = Clip("Items/vault_unlocked");
            b.DoorHeavyOpen = Clip("Environment/door_heavy_open");
            b.DoorHeavyShut = Clip("Environment/door_heavy_shut");
            b.DrawerOpen = Clip("Environment/drawer_open");
            b.DrawerClose = Clip("Environment/drawer_close");
            b.SteelDrawerOpen = Clip("Environment/steel_drawer_open");
            b.SteelDrawerClose = Clip("Environment/steel_drawer_close");
            b.CupboardOpen = Clip("Environment/cupboard_open");
            b.CupboardClose = Clip("Environment/cupboard_close");
            b.SteelCupboardOpen = Clip("Environment/steel_cupboard_open");
            b.SteelCupboardClose = Clip("Environment/steel_cupboard_close");
            b.SwitchOn = Clip("Environment/switch_on");
            b.SwitchOff = Clip("Environment/switch_off");
            b.HatchOpen = Clip("Environment/hatch_open");
            b.GrilleOff = Clip("Environment/grille_off");
            b.SafeTick = Clip("Items/safe_tick");
            b.Clank = Clip("Items/clank");
            b.Latch = Clip("Items/latch");
            b.TerminalKey = Clip("Items/terminal_key");
            b.Bandage = Clip("Items/bandage");
            b.Drink = Clip("Items/drink");
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
            root.AddComponent<VoiceCapture>(); // this peer's microphone (voice chat)
            return root;
        });

        const string VoiceFlatPath = AudioDir + "/Voice/voice_flat.wav";

        /// <summary>
        /// A short loop of a constant full-scale signal (raw 16-bit PCM, not compressed): the voice of a player is
        /// multiplied into it on the audio thread (<see cref="VoicePlayback"/>), so Unity pans and spreads the voice in 3D
        /// as it does any clip.
        /// </summary>
        static AudioClip VoiceFlatClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceFlatPath);
            if (clip != null) return clip;
            const int rate = 48000, samples = 4800;
            var full = System.IO.Path.GetFullPath(VoiceFlatPath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            using (var w = new System.IO.BinaryWriter(System.IO.File.Create(full)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + samples * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(samples * 2);
                for (int i = 0; i < samples; i++) w.Write(short.MaxValue);
            }
            AssetDatabase.ImportAsset(VoiceFlatPath);
            if (AssetImporter.GetAtPath(VoiceFlatPath) is AudioImporter importer)
            {
                var s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.PCM;
                s.preloadAudioData = true;
                importer.defaultSampleSettings = s;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceFlatPath);
        }

        /// <summary>The mouth of a player: plays their voice chat in 3D with the filters that place it in the house.</summary>
        static VoicePlayback AddVoice(Transform parent)
        {
            var go = Child(parent, "Voice", new Vector3(0f, 1.5f, 0.12f));
            var source = go.AddComponent<AudioSource>();
            source.clip = VoiceFlatClip();
            source.loop = true;
            source.playOnAwake = false; // VoicePlayback starts it once it can multiply the voice in
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.priority = 16; // voices are never the ones Unity drops
            source.rolloffMode = AudioRolloffMode.Custom; // the distance gain is VoiceAcoustics.DistanceGain
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
            source.minDistance = 1f;
            source.maxDistance = 60f;
            var voice = go.AddComponent<VoicePlayback>(); // before the filters: they process what it writes
            var low = go.AddComponent<AudioLowPassFilter>();
            low.cutoffFrequency = 22000f;
            low.lowpassResonanceQ = 1f;
            var echo = go.AddComponent<AudioEchoFilter>();
            echo.enabled = false;
            var reverb = go.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Off;
            reverb.enabled = false;
            voice.Source = source;
            voice.LowPass = low;
            voice.Echo = echo;
            voice.Reverb = reverb;
            return voice;
        }

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
