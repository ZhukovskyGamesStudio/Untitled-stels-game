using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// Plays one player's voice from their mouth (a child of the Player prefab): the jitter buffer puts the frames in
    /// order, Opus decodes them (and rebuilds lost ones), and the audio thread multiplies a looping flat clip by the voice —
    /// so Unity pans it in 3D like any sound — then the low-pass, echo and reverb filters after it make it sound where it
    /// is: duller and quieter behind walls and slabs, ringing in a tiled toilet, booming in the sewer
    /// (<see cref="VoiceAcoustics"/>). The owner never hears their own voice.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoicePlayback : MonoBehaviour
    {
        [Tooltip("Plays the flat clip the voice is multiplied into (3D, flat rolloff: the gain is ours).")]
        public AudioSource Source;
        [Tooltip("Dulls the voice behind walls.")]
        public AudioLowPassFilter LowPass;
        [Tooltip("Slap-back echo in tunnels.")]
        public AudioEchoFilter Echo;
        [Tooltip("The ring of the speaker's room.")]
        public AudioReverbFilter Reverb;
        public PlayerController Owner;

        VoiceJitter m_Jitter;
        VoiceDecoder m_Decoder;
        VoiceRing m_Ring;
        VoiceResampler m_Resampler;
        readonly float[] m_Pcm = new float[VoiceNative.Frame];
        float[] m_Out;
        readonly byte[] m_Packet = new byte[VoicePacket.MaxPayload];
        int m_OutRate;
        float m_NextAcoustics;
        volatile float m_Gain;
        float m_GainNow; // audio thread
        VoiceSpace m_Space = (VoiceSpace)255;
        float m_Decay = -1f;

        /// <summary>Loudness of what is playing now (0..1, smoothed): the HUD shows who is talking.</summary>
        public float Level { get; private set; }

        void Awake()
        {
            if (!VoiceNative.Available)
            {
                if (Source != null) Source.enabled = false; // the flat clip must never reach the speakers on its own
                enabled = false;
                return;
            }
            m_OutRate = AudioSettings.outputSampleRate;
            m_Jitter = new VoiceJitter();
            m_Decoder = new VoiceDecoder();
            m_Ring = new VoiceRing(m_OutRate / 2);
            m_Resampler = new VoiceResampler(VoiceNative.SampleRate, m_OutRate);
            m_Out = new float[m_Resampler.MaxOut(VoiceNative.Frame)];
        }

        void OnEnable()
        {
            if (Source != null && m_Ring != null) { Source.enabled = true; if (!Source.isPlaying) Source.Play(); }
        }

        void OnDisable()
        {
            if (Source != null) Source.enabled = false; // without this filter the flat clip would play as is
        }

        void OnDestroy() => m_Decoder?.Dispose();

        /// <summary>A frame of this player's voice arrived.</summary>
        public void Push(ushort sequence, byte[] buffer, int offset, int length)
        {
            if (!enabled || m_Jitter == null || (Owner != null && Owner.IsOwner)) return;
            m_Jitter.Push(sequence, buffer, offset, length);
        }

        void Update()
        {
            var s = Tuning.Audio.Voice;
            // keep ~60 ms decoded ahead of the audio thread (more at a low frame rate)
            int want = Mathf.Max(m_OutRate * 3 / 50, (int)(m_OutRate * Time.unscaledDeltaTime * 2.5f));
            float peak = 0f;
            for (int guard = 0; guard < 8 && m_Ring.Count < want; guard++)
            {
                var kind = m_Jitter.Pop(m_Packet, out int length, s.JitterFrames, s.JitterMax);
                if (kind == VoiceFrame.None) break;
                int n = kind switch
                {
                    VoiceFrame.Packet => m_Decoder.Decode(m_Packet, length, m_Pcm, false),
                    VoiceFrame.Fec => m_Decoder.Decode(m_Packet, length, m_Pcm, true),
                    _ => m_Decoder.Decode(null, 0, m_Pcm, false),
                };
                if (n <= 0) continue;
                for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(m_Pcm[i]));
                int o = m_Resampler.Process(m_Pcm, 0, n, m_Out, 0);
                m_Ring.Write(m_Out, 0, o);
            }
            Level = Mathf.Max(peak, Level - Time.unscaledDeltaTime * 2f);

            if (Time.unscaledTime >= m_NextAcoustics)
            {
                m_NextAcoustics = Time.unscaledTime + s.AcousticsEvery;
                UpdateAcoustics(s);
            }
        }

        void UpdateAcoustics(VoiceSettings s)
        {
            Vector3 mouth = transform.position, ears = AudioDirector.Ears;
            float distance = Vector3.Distance(mouth, ears);
            float transmission = distance < 0.3f ? 1f : Perception.SoundTransmission(mouth, ears, Tuning.Perception.Hearing);
            VoiceAcoustics.Blocked(transmission, s, out float blocked, out float cutoff);
            m_Gain = VoiceAcoustics.DistanceGain(distance, s) * blocked * s.Volume * VoicePrefs.Volume;
            if (LowPass != null) LowPass.cutoffFrequency = cutoff;

            VoiceSpace space;
            float area = 20f, height = LevelMetrics.FloorHeight;
            var layout = ActiveLevel.Layout;
            if (mouth.y < LevelLayout.Underground) { space = VoiceSpace.Tunnel; area = 60f; height = 4f; }
            else if (layout == null) space = VoiceSpace.Outdoors;
            else
            {
                var room = layout.RoomAt(mouth + Vector3.up * 0.3f);
                if (room == null) space = VoiceSpace.Outdoors;
                else
                {
                    space = VoiceAcoustics.SpaceOf(room.Type);
                    area = room.Area;
                    height = Mathf.Max(1, room.Levels) * LevelMetrics.FloorHeight;
                }
            }
            var r = VoiceAcoustics.ReverbOf(space, area, height, s.Reverb);
            if (space == m_Space && Mathf.Abs(r.Decay - m_Decay) < 0.05f) return;
            m_Space = space;
            m_Decay = r.Decay;
            Apply(r);
        }

        void Apply(VoiceReverb r)
        {
            if (Reverb != null)
            {
                Reverb.enabled = !r.IsDry;
                if (!r.IsDry)
                {
                    Reverb.reverbPreset = AudioReverbPreset.User;
                    Reverb.dryLevel = 0f;
                    Reverb.room = r.Room;
                    Reverb.roomHF = r.RoomHF;
                    Reverb.roomLF = 0f;
                    Reverb.decayTime = r.Decay;
                    Reverb.decayHFRatio = 0.6f;
                    Reverb.reflectionsLevel = r.Reflections;
                    Reverb.reflectionsDelay = 0.01f;
                    Reverb.reverbLevel = r.Reverb;
                    Reverb.reverbDelay = 0.02f;
                    Reverb.diffusion = r.Diffusion;
                    Reverb.density = r.Density;
                }
            }
            if (Echo != null)
            {
                Echo.enabled = r.EchoDelay > 0f;
                if (Echo.enabled)
                {
                    Echo.delay = r.EchoDelay * 1000f;
                    Echo.decayRatio = r.EchoDecay;
                    Echo.wetMix = r.EchoWet;
                    Echo.dryMix = 1f;
                }
            }
        }

        // The audio thread: the source plays a constant 1, already panned and spread by Unity; multiplying it by the voice
        // gives the voice where the speaker stands. Runs before the filters below it on the object.
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (m_Ring == null)
            {
                System.Array.Clear(data, 0, data.Length);
                return;
            }
            float target = m_Gain, g = m_GainNow;
            for (int i = 0; i < data.Length; i += channels)
            {
                m_Ring.Read(out float v);
                g += (target - g) * 0.002f; // ~10 ms glide: no clicks when a door closes between us
                v *= g;
                for (int c = 0; c < channels; c++) data[i + c] *= v;
            }
            m_GainNow = g;
        }
    }
}
