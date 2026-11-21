using Stealth.Configs;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>How a player's microphone opens: by voice (like Discord's voice activity) or while a key is held.</summary>
    public enum VoiceMode : byte { VoiceActivity, PushToTalk, Off }

    /// <summary>Levels of PCM frames (mono, −1..1).</summary>
    public static class VoiceLevel
    {
        public const float Silence = -90f;

        public static float RmsDb(float[] samples, int offset, int count)
        {
            if (count <= 0) return Silence;
            double sum = 0;
            for (int i = offset; i < offset + count; i++) sum += samples[i] * (double)samples[i];
            return ToDb((float)System.Math.Sqrt(sum / count));
        }

        public static float ToDb(float amplitude) => amplitude <= 1e-5f ? Silence : Mathf.Max(Silence, 20f * Mathf.Log10(amplitude));
        public static float FromDb(float db) => Mathf.Pow(10f, db / 20f);
    }

    /// <summary>
    /// Opens the microphone for 20 ms frames. Push-to-talk: while the key is held. Voice activity: a frame the denoiser
    /// thinks is voice and that is loud enough for the player's sensitivity opens it, and it stays open
    /// <see cref="VoiceSettings.Hangover"/> after the last such frame so word endings are not cut off.
    /// </summary>
    public sealed class VoiceGate
    {
        float m_Hold;

        public bool IsOpen { get; private set; }

        /// <summary>The level a frame must reach at this sensitivity (0 = only loud speech, 1 = picks up a whisper).</summary>
        public static float ThresholdDb(float sensitivity, VoiceSettings s) => s.GateDb + (0.5f - Mathf.Clamp01(sensitivity)) * 40f;

        public bool Step(VoiceMode mode, bool keyHeld, float voiceProbability, float levelDb, float sensitivity, float dt, VoiceSettings s)
        {
            switch (mode)
            {
                case VoiceMode.Off:
                    m_Hold = 0f;
                    IsOpen = false;
                    break;
                case VoiceMode.PushToTalk:
                    m_Hold = 0f;
                    IsOpen = keyHeld;
                    break;
                default:
                    bool voiced = voiceProbability >= s.VoiceProbability && levelDb >= ThresholdDb(sensitivity, s);
                    if (voiced) m_Hold = s.Hangover;
                    else m_Hold -= dt;
                    IsOpen = voiced || m_Hold > 0f;
                    break;
            }
            return IsOpen;
        }

        public void Reset() { m_Hold = 0f; IsOpen = false; }
    }

    /// <summary>
    /// Automatic gain: brings speech towards <see cref="VoiceSettings.TargetDb"/>, slowly up and quickly down, only while
    /// the player talks (it does not pump up the room's hiss between words), then a soft limiter so nothing clips.
    /// The range is limited, so a whisper stays a little quieter than a shout.
    /// </summary>
    public sealed class VoiceGain
    {
        public float GainDb { get; private set; }

        public void Process(float[] frame, int offset, int count, float levelDb, bool voiced, float dt, VoiceSettings s)
        {
            if (voiced && levelDb > VoiceLevel.Silence + 1f)
            {
                float want = Mathf.Clamp(s.TargetDb - levelDb, -s.MaxCutDb, s.MaxGainDb);
                float tau = want > GainDb ? s.GainRise : s.GainFall;
                GainDb += (want - GainDb) * (1f - Mathf.Exp(-dt / Mathf.Max(0.001f, tau)));
            }
            float g = VoiceLevel.FromDb(GainDb);
            for (int i = offset; i < offset + count; i++) frame[i] = Limit(frame[i] * g);
        }

        /// <summary>Linear below 0.8, then bends smoothly towards ±1 (never clips).</summary>
        public static float Limit(float x)
        {
            float a = Mathf.Abs(x);
            if (a <= 0.8f) return x;
            float over = (a - 0.8f) / 0.2f;
            return Mathf.Sign(x) * (0.8f + 0.2f * (over / (1f + over)));
        }
    }

    /// <summary>How the robots hear a talking player.</summary>
    public static class VoiceNoise
    {
        /// <summary>
        /// Noise radius (m) of a frame at this raw microphone level: nothing below a whisper, then whisper → speech → shout
        /// radii in between (the level before the gain: whispering is quieter for the robots too).
        /// </summary>
        public static float Radius(float rawDb, VoiceSettings s)
        {
            if (rawDb <= s.WhisperDb - 12f) return 0f;
            if (rawDb < s.WhisperDb) return Mathf.Lerp(0f, s.WhisperRadius, (rawDb - (s.WhisperDb - 12f)) / 12f);
            if (rawDb < s.SpeechDb) return Mathf.Lerp(s.WhisperRadius, s.SpeechRadius, Mathf.InverseLerp(s.WhisperDb, s.SpeechDb, rawDb));
            return Mathf.Lerp(s.SpeechRadius, s.ShoutRadius, Mathf.InverseLerp(s.SpeechDb, s.ShoutDb, rawDb));
        }
    }
}
