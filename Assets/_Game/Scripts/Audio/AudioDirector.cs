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
        MusicTrack m_Track;
        float m_LastDanger = float.NegativeInfinity;

        int m_LastCollected = -1, m_LastLevel = -1;
        RunPhase m_LastPhase = RunPhase.Playing;
        float m_BannerMuteUntil;
        bool m_WasCursor;

        class Walker { public Vector3 Pos; public float NextStep; public float VSpeed; public bool Airborne; public bool Downed; }
        class Watched
        {
            public int Level; public bool Hacked; public float Cooldown; public Vector3 Pos; public float NextStep;
            public RobotState Robot; public float VoiceFree; public AudioClip Queued; public float NextBeep;
        }

        readonly Dictionary<PlayerController, Walker> m_Players = new();
        readonly Dictionary<Detector, Watched> m_Detectors = new();
        readonly List<Object> m_Gone = new();

        void Awake()
        {
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
        }

        void OnDestroy()
        {
            GameManager.BannerShown -= OnBanner;
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
            voice.volume = Mathf.Clamp01(volume * GroupVolume(group));
            voice.Play();
            Played++;
        }

        public void PlayFlat(AudioClip clip, float volume, Sfx.Group group)
        {
            Flat.PlayOneShot(clip, Mathf.Clamp01(volume * GroupVolume(group)));
            Played++;
        }

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

            UpdateMusic(me, gm, inGame);
            UpdateAmbience(me, gm, inGame);
            if (!inGame)
            {
                m_Players.Clear();
                m_Detectors.Clear();
                m_LastCollected = -1;
                m_LastLevel = -1;
                return;
            }
            UpdateRun(gm);
            UpdatePlayers(me);
            UpdateDetectors();
            UpdateBackpack(me);
        }

        void UpdateMusic(PlayerController me, GameManager gm, bool inGame)
        {
            if (inGame && (gm.AlarmActive || InDanger(me))) m_LastDanger = Time.time;
            var track = AudioRules.ChooseMusic(inGame, Time.time - m_LastDanger, m_Mix.ChaseHold);
            if (track != m_Track)
            {
                m_Track = track;
                var next = m_Music == MusicA ? MusicB : MusicA;
                next.clip = track switch { MusicTrack.Menu => Bank.MenuMusic, MusicTrack.Chase => Bank.ChaseMusic, MusicTrack.Explore => Bank.ExploreMusic, _ => null };
                if (next.clip != null) { next.time = 0f; next.Play(); }
                m_Music = next;
            }
            float target = m_Mix.Music * GameSettings.MusicVolume;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, m_Mix.Crossfade);
            foreach (var s in new[] { MusicA, MusicB })
            {
                s.volume = Mathf.MoveTowards(s.volume, s == m_Music ? target : 0f, step * Mathf.Max(target, 0.01f));
                if (s != m_Music && s.volume <= 0f && s.isPlaying) s.Stop();
            }
        }

        static bool InDanger(PlayerController me)
        {
            foreach (var d in Detector.All)
                if (!d.IsHacked && d.FocusClient == me.ClientId && (d.IsChasing || d.Awareness >= 0.999f)) return true;
            return false;
        }

        void UpdateAmbience(PlayerController me, GameManager gm, bool inGame)
        {
            bool inside = inGame && ActiveLevel.Layout.IsInside(me.transform.position);
            float step = Time.deltaTime * 0.5f;
            AmbienceInside.volume = Mathf.MoveTowards(AmbienceInside.volume, inGame && inside ? m_Mix.Ambience : 0f, step);
            AmbienceOutside.volume = Mathf.MoveTowards(AmbienceOutside.volume, inGame && !inside ? m_Mix.Ambience : 0f, step);
            AlarmSource.volume = Mathf.MoveTowards(AlarmSource.volume, inGame && gm.AlarmActive ? m_Mix.Alarm : 0f, Time.deltaTime * 2f);
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
                w.VSpeed = Mathf.Lerp(w.VSpeed, vy, 0.5f);
                delta.y = 0f;
                float speed = delta.magnitude / dt;
                float volume = p == me ? 0.8f : 1f; // your own steps a touch quieter

                if (p.Downed != w.Downed)
                {
                    Sfx.At(p.Downed ? Bank.Downed : Bank.Revive, pos + Vector3.up, 1f, Sfx.Group.Items);
                    w.Downed = p.Downed;
                }
                if (p.Downed) continue;

                if (!w.Airborne && w.VSpeed > 2.5f) { w.Airborne = true; Sfx.At(Bank.Jump, pos, volume * 0.8f, Sfx.Group.Footsteps); }
                else if (!w.Airborne && w.VSpeed < -4f) w.Airborne = true; // walked off an edge
                else if (w.Airborne && Mathf.Abs(w.VSpeed) < 0.5f)
                {
                    w.Airborne = false;
                    Sfx.At(Bank.Land, pos, volume, Sfx.Group.Footsteps);
                    w.NextStep = Time.time + 0.25f;
                }
                if (w.Airborne) continue;

                float interval = AudioRules.StepInterval(speed, m_Mix.StepLength);
                if (float.IsInfinity(interval)) { w.NextStep = Mathf.Max(w.NextStep, Time.time + 0.12f); continue; }
                if (Time.time < w.NextStep) continue;
                w.NextStep = Time.time + interval;
                var clips = Bank.Footsteps;
                if (clips == null || clips.Length == 0) continue;
                Sfx.At(clips[Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio).Next(clips.Length)], pos, volume * AudioRules.StepVolume(speed, p.Crouching, m_Mix), Sfx.Group.Footsteps, Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio).Range(0.92f, 1.08f));
            }
            Prune(m_Players, PlayerController.All);
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
                    };
                    continue;
                }
                if (d.IsHacked && !w.Hacked) Sfx.At(Bank.Hacked, d.Position);
                w.Hacked = d.IsHacked;
                var robot = d as RobotController;
                var cue = AudioRules.Cue(w.Level, level);
                if (cue != AlertCue.None) w.Level = level;
                if (robot != null && cue == AlertCue.Alert) cue = AlertCue.None; // robots say it instead
                if (robot != null) UpdateVoice(robot, w);
                if (d is SecurityCamera) UpdateCameraBeep(d, w);
                if (cue != AlertCue.None && Time.time >= w.Cooldown)
                {
                    Sfx.At(cue == AlertCue.Alert ? Bank.Alert : cue == AlertCue.Suspicious ? Bank.Suspicious : Bank.Lost, d.Position);
                    w.Cooldown = Time.time + 1.2f;
                }

                if (d is RobotController r && r.Profile != null && r.Traits.CanUseStairs && r.Traits.HoverHeight <= 0f)
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

        /// <summary>A camera that is noticing someone beeps faster and faster until it raises the alarm.</summary>
        void UpdateCameraBeep(Detector camera, Watched w)
        {
            float a = camera.IsHacked ? 0f : camera.Awareness;
            float interval = AudioRules.CameraBeepInterval(a);
            if (float.IsInfinity(interval)) { w.NextBeep = 0f; return; }
            if (Time.time < w.NextBeep) return;
            if (w.NextBeep > 0f || a > 0.05f)
                Sfx.At(Bank.CameraBeep, camera.Position, 0.8f, Sfx.Group.Robots, AudioRules.CameraBeepPitch(a));
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
            Sfx.At(w.Queued, robot.Position, 1f, Sfx.Group.Robots);
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
