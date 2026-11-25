using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// The game's sound (prefab Audio/AudioDirector, in the scene): music with crossfades (menu, exploring, chase),
    /// ambience (office hum inside, gas wind outside), the alarm siren, a pool of 3D voices for one-shots and a 2D
    /// voice for the interface. Watches the replicated game state and voices it on every peer — footsteps and jumps of
    /// all players, robots noticing / losing someone, hacks, knockouts, the quota filling up, banners, run end.
    /// One-shots from game code go through <see cref="Sfx"/>. Volumes: Data/Config/Audio + the menu sliders.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        public SoundBank Bank;
        public AudioSource MusicA, MusicB;
        public AudioSource AmbienceInside, AmbienceOutside;
        public AudioSource AlarmSource;
        public AudioSource Flat;
        [Tooltip("Pooled 3D voices for one-shots in the world.")]
        public AudioSource[] Voices;

        /// <summary>One-shots played so far (autopilot checks).</summary>
        public int Played { get; private set; }
        /// <summary>Robot voice lines played so far (autopilot checks).</summary>
        public int VoiceLines { get; private set; }
        public MusicTrack Track => m_Track;

        AudioMix m_Mix;
        int m_NextVoice;
        AudioSource m_Music; // the one fading in
        // Crossfade weights (0..1): the settings' volume multiplies them, so a slider acts at once.
        float m_WeightA, m_WeightB, m_Inside, m_Outside, m_Siren;
        Vector3 m_Listener;
        bool m_InHouse;
        MusicTrack m_Track;
        float m_LastDanger = float.NegativeInfinity;

        int m_LastCollected = -1, m_LastLevel = -1;
        RunPhase m_LastPhase = RunPhase.Playing;
        float m_BannerMuteUntil;
        bool m_WasCursor;

        class Walker { public Vector3 Pos; public float NextStep; public float Climb; public bool Climbing; public readonly AirState Air = new(); public bool Downed; public float NextGas; }
        float m_NextHostage;
        int m_HostageSaid, m_HostageLast = -1;
        class Watched
        {
            public int Level; public bool Hacked; public float Cooldown; public Vector3 Pos; public float NextStep;
            public RobotState Robot; public float VoiceFree; public AudioClip Queued; public float NextBeep;
            public bool Legs; public int FootFrame = -1;
        }

        readonly Dictionary<PlayerController, Walker> m_Players = new();
        readonly Dictionary<Detector, Watched> m_Detectors = new();
        readonly List<Object> m_Gone = new();

        void Awake()
        {
            GameSettings.EnsureLoaded(); // the saved volumes from the very first frame of the menu, not after opening the settings
            Instance = this;
            m_Mix = Tuning.Audio.Mix;
            foreach (var v in Voices)
            {
                v.spatialBlend = 1f;
                v.rolloffMode = AudioRolloffMode.Linear;
                v.minDistance = m_Mix.MinDistance;
                v.maxDistance = m_Mix.MaxDistance;
                v.dopplerLevel = 0f;
                v.playOnAwake = false;
            }
            foreach (var s in new[] { MusicA, MusicB, AmbienceInside, AmbienceOutside, AlarmSource, Flat })
            {
                s.spatialBlend = 0f;
                s.playOnAwake = false;
            }
            Loop(MusicA, null);
            Loop(MusicB, null);
            Loop(AmbienceInside, Bank.OfficeHum);
            Loop(AmbienceOutside, Bank.GasWind);
            Loop(AlarmSource, Bank.Alarm);
            GameManager.BannerShown += OnBanner;
            ProceduralLegs.FootDown += OnFootDown;
        }

        void OnDestroy()
        {
            GameManager.BannerShown -= OnBanner;
            ProceduralLegs.FootDown -= OnFootDown;
            if (Instance == this) Instance = null;
        }

        static void Loop(AudioSource s, AudioClip clip)
        {
            s.loop = true;
            s.volume = 0f;
            s.clip = clip;
            if (clip != null) s.Play();
        }

        // ------------------------------------------------------------------ one-shots

        public void PlayAt(AudioClip clip, Vector3 position, float volume, Sfx.Group group, float pitch)
        {
            if (Voices.Length == 0) return;
            // Round robin, skipping voices still busy if a free one is near.
            AudioSource voice = Voices[m_NextVoice];
            for (int i = 0; i < Voices.Length; i++)
            {
                var v = Voices[(m_NextVoice + i) % Voices.Length];
                if (!v.isPlaying) { voice = v; m_NextVoice = (m_NextVoice + i) % Voices.Length; break; }
            }
            m_NextVoice = (m_NextVoice + 1) % Voices.Length;
            voice.transform.position = position;
            voice.pitch = pitch;
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume * GroupVolume(group) * Muffle(position));
            voice.Play();
            Played++;
        }

        public void PlayFlat(AudioClip clip, float volume, Sfx.Group group)
        {
            Flat.PlayOneShot(clip, Mathf.Clamp01(volume * GroupVolume(group)));
            Played++;
        }

        /// <summary>
        /// How much of a sound at <paramref name="position"/> reaches the listener through the floors of the house
        /// (1 on the same floor; slabs between them swallow most of it). Robots above or below you are muffled, not loud.
        /// </summary>
        public float Muffle(Vector3 position) =>
            m_InHouse ? AudioRules.FloorFactor(m_Listener.y, position.y, m_Mix.ThroughFloor) : 1f;

        float GroupVolume(Sfx.Group g) => g switch
        {
            Sfx.Group.Items => m_Mix.Items,
            Sfx.Group.Footsteps => m_Mix.Footsteps,
            Sfx.Group.Ui => m_Mix.Ui,
            _ => m_Mix.Robots,
        };

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            AudioListener.volume = DevFlags.Mute ? 0f : GameSettings.MasterVolume;
            var me = PlayerController.Local;
            var gm = GameManager.Instance;
            bool inGame = me != null && gm != null && ActiveLevel.Layout != null;
            m_InHouse = inGame && gm.Phase == RunPhase.Playing;
            m_Listener = ListenerPosition(me);

            UpdateMusic(me, gm, me != null && gm != null); // the sewer is part of the game too (not the menu track)
            UpdateAmbience(me, gm, inGame);
            // people are heard wherever they are: in the sewer too, where no house stands yet (or any more)
            if (me != null && gm != null) UpdatePlayers(me);
            else m_Players.Clear();
            if (!inGame)
            {
                m_Detectors.Clear();
                m_LastCollected = -1;
                m_LastLevel = -1;
                return;
            }
            UpdateRun(gm);
            UpdateHostage(me);
            UpdateDetectors();
            UpdateBackpack(me);
        }

        void UpdateMusic(PlayerController me, GameManager gm, bool inGame)
        {
            if (inGame && (gm.AlarmActive || InDanger(me))) m_LastDanger = Time.time;
            var track = AudioRules.ChooseMusic(inGame, Time.time - m_LastDanger, m_Mix.ChaseHold, m_Mix.LevelMusic);
            if (track != m_Track)
            {
                m_Track = track;
                var next = m_Music == MusicA ? MusicB : MusicA;
                next.clip = track switch { MusicTrack.Menu => Bank.MenuMusic, MusicTrack.Chase => Bank.ChaseMusic, MusicTrack.Explore => Bank.ExploreMusic, _ => null };
                if (next.clip != null) { next.time = 0f; next.Play(); }
                m_Music = next;
            }
            // Only the change of track fades; the volume from the settings applies immediately.
            float target = m_Mix.Music * GameSettings.MusicVolume * (track == MusicTrack.Explore ? m_Mix.ExploreMusic : 1f);
            if (UI.StudioSplash.Holding) target = 0f; // the studio's splash has its own sound
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, m_Mix.Crossfade);
            m_WeightA = Mathf.MoveTowards(m_WeightA, m_Music == MusicA ? 1f : 0f, step);
            m_WeightB = Mathf.MoveTowards(m_WeightB, m_Music == MusicB ? 1f : 0f, step);
            MusicA.volume = m_WeightA * target;
            MusicB.volume = m_WeightB * target;
            if (m_Music != MusicA && m_WeightA <= 0f && MusicA.isPlaying) MusicA.Stop();
            if (m_Music != MusicB && m_WeightB <= 0f && MusicB.isPlaying) MusicB.Stop();
        }

        /// <summary>Where this peer's ears are now (voice chat measures walls and distance from here).</summary>
        public static Vector3 Ears => Instance != null ? Instance.m_Listener : ListenerPosition(PlayerController.Local);

        /// <summary>Where the ears are: the local player's head (or the teammate they watch), else the menu camera.</summary>
        static Vector3 ListenerPosition(PlayerController me)
        {
            if (me != null && me.Listener != null && me.Listener.isActiveAndEnabled) return me.Listener.transform.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        static bool InDanger(PlayerController me)
        {
            foreach (var d in Detector.All)
                if (!d.IsHacked && d.FocusClient == me.ClientId && (d.IsChasing || d.Awareness >= 0.999f)) return true;
            return false;
        }

        void UpdateAmbience(PlayerController me, GameManager gm, bool inGame)
        {
            bool house = inGame && gm.Phase == RunPhase.Playing;
            bool inside = house && ActiveLevel.Layout.IsInside(me.transform.position);
            float step = Time.deltaTime * 0.5f;
            m_Inside = Mathf.MoveTowards(m_Inside, inside ? 1f : 0f, step);
            m_Outside = Mathf.MoveTowards(m_Outside, house && !inside ? 1f : 0f, step);
            m_Siren = Mathf.MoveTowards(m_Siren, house && gm.AlarmActive ? 1f : 0f, Time.deltaTime * 2f);
            AmbienceInside.volume = m_Inside * m_Mix.Ambience;
            AmbienceOutside.volume = m_Outside * m_Mix.Ambience;
            AlarmSource.volume = m_Siren * m_Mix.Alarm;
        }

        void UpdateRun(GameManager gm)
        {
            if (m_LastCollected >= 0 && gm.Collected > m_LastCollected) Sfx.Ui(Bank.Sell, 1f, Sfx.Group.Items);
            m_LastCollected = gm.Collected;
            if (m_LastLevel >= 0 && gm.Level.Index > m_LastLevel) { Sfx.Ui(Bank.LevelDone); m_BannerMuteUntil = Time.time + 1f; }
            if (m_LastLevel >= 0 && gm.Level.Index == 0 && m_LastLevel > 0) m_BannerMuteUntil = Time.time + 1f; // new run
            m_LastLevel = gm.Level.Index;
            if (gm.Phase == RunPhase.Failed && m_LastPhase != RunPhase.Failed) { Sfx.Ui(Bank.RunFailed); m_BannerMuteUntil = Time.time + 1f; }
            m_LastPhase = gm.Phase;
        }

        void OnBanner(string text)
        {
            if (Time.time >= m_BannerMuteUntil) Sfx.Ui(Bank.Banner, 0.8f);
        }

        /// <summary>Footsteps, jumps, landings, knockouts and revives of every player, from their replicated motion.</summary>
        void UpdatePlayers(PlayerController me)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var p in PlayerController.All)
            {
                var pos = p.transform.position;
                if (!m_Players.TryGetValue(p, out var w))
                {
                    m_Players[p] = new Walker { Pos = pos, Downed = p.Downed, NextStep = Time.time + 0.3f };
                    continue;
                }
                var delta = pos - w.Pos;
                w.Pos = pos;
                if (delta.magnitude > 3f) continue; // teleported
                float vy = delta.y / dt;
                delta.y = 0f;
                float speed = delta.magnitude / dt;
                float volume = p == me ? 0.8f : 1f; // your own steps a touch quieter

                if (p.Downed != w.Downed)
                {
                    Sfx.At(p.Downed ? Bank.Downed : Bank.Revive, pos + Vector3.up, 1f, Sfx.Group.Items, p.Downed ? PeopleSounds.VoicePitch(p.Department) : 1f);
                    w.Downed = p.Downed;
                }
                if (p.Downed) continue;

                // the gas: heavy breathing, then coughing ever harder (everyone hears it, from the replicated share)
                var (gasClip, gasVolume, gasGap) = PeopleSounds.Gas(p.GasShare, p.GasLaughShare);
                if (gasClip > -2 && Time.time >= w.NextGas)
                {
                    var clip = gasClip < 0 ? Bank.GasBreath : Bank.GasCough != null && gasClip < Bank.GasCough.Length ? Bank.GasCough[gasClip] : null;
                    if (clip != null)
                    {
                        float voice = PeopleSounds.VoicePitch(p.Department);
                        Sfx.At(clip, pos + Vector3.up * 1.6f, volume * gasVolume, Sfx.Group.Footsteps, voice);
                        w.NextGas = Time.time + clip.length / voice + gasGap;
                    }
                }
                // riding in the car (or on a ladder) the body moves without its feet: no steps, no jumps
                if (p.Pose == Stealth.Player.BodyPose.Ladder)
                {
                    // on a ladder: a boot on the rung as the climber's foot gets there, the soft pat of a hand as it
                    // closes on its own — by the body's own take, not by the metres (softer on the way down)
                    float phase = p.ClimbPhase;
                    if (!w.Climbing) { w.Climb = phase; w.Climbing = true; }
                    var (feet, hands) = Stealth.Player.HumanMath.ClimbContacts(w.Climb, phase);
                    float way = phase < w.Climb ? m_Mix.LadderDown : 1f;
                    w.Climb = phase;
                    for (; feet > 0; feet--)
                        Sfx.At(SoundBank.Any(Bank.LadderFoot), pos + Vector3.up * 0.25f, volume * m_Mix.LadderFeet * way, Sfx.Group.Footsteps,
                            0.95f + 0.1f * ((Time.frameCount + feet) % 3) / 2f);
                    for (; hands > 0; hands--)
                        Sfx.At(SoundBank.Any(Bank.LadderHand), pos + Vector3.up * 1.7f, volume * m_Mix.LadderHands * way, Sfx.Group.Footsteps,
                            0.95f + 0.1f * ((Time.frameCount + hands + 1) % 3) / 2f);
                }
                else w.Climbing = false;
                if (p.SeatIndex >= 0 || p.Pose == Stealth.Player.BodyPose.Ladder) { w.NextStep = Time.time + 0.3f; continue; }
                switch (w.Air.Step(vy, dt, Time.time))
                {
                    case AirEvent.Jump: Sfx.At(Bank.Jump, pos, volume * 0.8f, Sfx.Group.Footsteps); break;
                    case AirEvent.Land:
                        Sfx.At(Bank.Land, pos, volume, Sfx.Group.Footsteps);
                        Sfx.At(SoundBank.Any(Bank.Steps(StepSurfaces.At(pos))), pos, volume * 0.8f, Sfx.Group.Footsteps, 0.86f); // on what they came down
                        w.NextStep = Time.time + 0.25f;
                        break;
                }
                if (w.Air.Airborne) continue;

                float interval = AudioRules.StepInterval(speed, m_Mix.StepLength);
                if (float.IsInfinity(interval)) { w.NextStep = Mathf.Max(w.NextStep, Time.time + 0.12f); continue; }
                if (Time.time < w.NextStep) continue;
                w.NextStep = Time.time + interval;
                Sfx.At(SoundBank.Any(Bank.Steps(StepSurfaces.At(pos))), pos, volume * AudioRules.StepVolume(speed, p.Crouching, m_Mix), Sfx.Group.Footsteps, Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio).Range(0.92f, 1.08f));
            }
            Prune(m_Players, PlayerController.All);
        }

        /// <summary>The hostage calls to this player while it sees the cage close by (every peer for its own player).</summary>
        void UpdateHostage(PlayerController me)
        {
            if (me == null || Bank.HostageVoice == null || Bank.HostageVoice.Length == 0) return;
            // the hostage sees a player who came up to the cage — not one looking at it through a camera from a terminal
            if (me.UsingTerminal != null || me.Watching != null || me.Downed) return;
            foreach (var i in Interactable.All)
            {
                if (i is not Cage cage || cage.Freed || cage.Hostage == null || !cage.Hostage.activeInHierarchy) continue;
                var head = cage.transform.position + Vector3.up * 1.0f;
                var eye = me.Head != null ? me.Head.position : me.transform.position + Vector3.up * 1.6f; // the body's eyes, wherever the camera is
                float dist = Vector3.Distance(eye, head);
                bool sight = dist <= PeopleSounds.HostageReach && Stealth.Enemies.Perception.ReachClear(eye, head, cage.transform);
                if (!PeopleSounds.HostageSpeaks(dist, sight, Time.time, m_NextHostage)) continue;
                var rng = Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio);
                bool keyHeld = GameManager.Instance != null && GameManager.Instance.ObjectiveStage.KeyHeld;
                int line = Mathf.Clamp(PeopleSounds.HostageLine(m_HostageSaid, m_HostageLast, keyHeld, rng.Next(1000)), 0, Bank.HostageVoice.Length - 1);
                var clip = Bank.HostageVoice[line];
                if (clip == null) continue;
                Sfx.At(clip, head + Vector3.up * 0.3f, 1f, Sfx.Group.Items);
                m_HostageSaid++;
                m_HostageLast = line;
                m_NextHostage = Time.time + clip.length + rng.Range(6f, 11f);
                return;
            }
        }

        /// <summary>Robots and devices: beeps when they notice / lose someone, a glitch when hacked, steps of legged robots.</summary>
        void UpdateDetectors()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var d in Detector.All)
            {
                int level = d.IsHacked ? 0 : AudioRules.AlertLevel(d.Awareness, d.IsInvestigating, d.IsChasing);
                if (!m_Detectors.TryGetValue(d, out var w))
                {
                    m_Detectors[d] = new Watched
                    {
                        Level = level, Hacked = d.IsHacked, Pos = d.transform.position,
                        Robot = d is RobotController rc ? rc.State : RobotState.Patrol,
                        Legs = d.GetComponent<ProceduralLegs>() != null,
                    };
                    continue;
                }
                if (d.IsHacked && !w.Hacked) Sfx.At(Bank.Hacked, d.Position, AudioRules.HackedVolume);
                w.Hacked = d.IsHacked;
                var robot = d as RobotController;
                var cue = AudioRules.Cue(w.Level, level);
                if (cue != AlertCue.None) w.Level = level;
                if (robot != null && cue == AlertCue.Alert) cue = AlertCue.None; // robots say it instead
                if (robot != null) UpdateVoice(robot, w);
                if (d is SecurityCamera) UpdateCameraBeep(d, w);
                if (cue != AlertCue.None && Time.time >= w.Cooldown)
                {
                    Sfx.At(cue == AlertCue.Alert ? Bank.Alert : cue == AlertCue.Suspicious ? Bank.Suspicious : Bank.Lost, d.Position, AudioRules.StingerVolume);
                    w.Cooldown = Time.time + 1.2f;
                }

                // Robots with walking legs sound each foot as it lands (OnFootDown); the rest step on a timer.
                if (!w.Legs && d is RobotController r && r.Profile != null && r.Traits.CanUseStairs && r.Traits.HoverHeight <= 0f)
                {
                    var pos = r.transform.position;
                    var delta = pos - w.Pos;
                    w.Pos = pos;
                    delta.y = 0f;
                    float speed = delta.magnitude / dt;
                    float interval = AudioRules.StepInterval(speed, m_Mix.StepLength * 1.2f);
                    if (!float.IsInfinity(interval) && Time.time >= w.NextStep && speed < 15f)
                    {
                        w.NextStep = Time.time + interval;
                        Sfx.At(Bank.RobotStep, pos, 0.6f, Sfx.Group.Robots, Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio).Range(0.9f, 1.1f));
                    }
                }
            }
            Prune(m_Detectors, Detector.All);
        }

        /// <summary>A foot of a walking robot touched the surface: one step sound per robot per frame (a group lands together).</summary>
        void OnFootDown(ProceduralLegs legs, Vector3 foot)
        {
            var d = legs.Detector;
            if (d == null || !m_InHouse || !m_Detectors.TryGetValue(d, out var w)) return;
            if (w.FootFrame == Time.frameCount) return;
            w.FootFrame = Time.frameCount;
            var (clip, volume) = d.Kind switch
            {
                RobotKind.Spider => (Bank.SpiderStep, 0.35f),
                RobotKind.Hound => (Bank.StepLight, 0.45f),
                _ => (Bank.RobotStep, 0.6f),
            };
            Sfx.At(clip != null ? clip : Bank.RobotStep, foot, volume, Sfx.Group.Robots, Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio).Range(0.92f, 1.08f));
        }

        /// <summary>A camera that is noticing someone beeps faster and faster until it raises the alarm.</summary>
        void UpdateCameraBeep(Detector camera, Watched w)
        {
            float a = camera.IsHacked ? 0f : camera.Awareness;
            float interval = AudioRules.CameraBeepInterval(a);
            if (float.IsInfinity(interval)) { w.NextBeep = 0f; return; }
            if (Time.time < w.NextBeep) return;
            if (w.NextBeep > 0f || a > 0.05f)
                Sfx.At(Bank.CameraBeep, camera.Position, AudioRules.CameraBeepVolume, Sfx.Group.Robots, AudioRules.CameraBeepPitch(a));
            w.NextBeep = Time.time + interval;
        }

        /// <summary>A robot's voice lines; one line at a time per robot (the next one waits for the current to finish).</summary>
        void UpdateVoice(RobotController robot, Watched w)
        {
            if (robot.IsHacked)
            {
                // Hacked mid-chase: silence, not «устранён».
                w.Robot = RobotState.Patrol;
                w.Queued = null;
                return;
            }
            var state = robot.State;
            var voice = AudioRules.Voice(w.Robot, state);
            w.Robot = state;
            var clip = voice switch
            {
                RobotVoice.Spotted => Bank.VoiceSpotted,
                RobotVoice.Chase => Bank.VoiceChase,
                RobotVoice.Lost => Bank.VoiceLost,
                RobotVoice.Eliminated => Bank.VoiceEliminated,
                _ => null,
            };
            if (voice == RobotVoice.Spotted) w.Queued = null; // a new warning replaces anything still waiting
            if (clip != null) w.Queued = clip;
            if (w.Queued == null || Time.time < w.VoiceFree) return;
            Sfx.At(w.Queued, robot.Position, 1f, Sfx.Group.Robots, AudioRules.VoicePitch(robot.Kind));
            VoiceLines++;
            w.VoiceFree = Time.time + w.Queued.length - 0.1f;
            w.Queued = null;
        }

        void UpdateBackpack(PlayerController me)
        {
            // Tab: the backpack opens with a zip (not while an item is held from the world).
            bool cursor = me.CursorMode && !PlayerController.HoldCursor;
            if (cursor && !m_WasCursor) Sfx.Ui(Bank.BackpackOpen, 0.6f);
            m_WasCursor = cursor;
        }

        void Prune<TKey, TValue>(Dictionary<TKey, TValue> map, ICollection<TKey> alive) where TKey : Object
        {
            if (map.Count == alive.Count) return;
            m_Gone.Clear();
            foreach (var k in map.Keys) if (k == null || !alive.Contains(k)) m_Gone.Add(k);
            foreach (var k in m_Gone) map.Remove((TKey)k);
        }
    }
}
