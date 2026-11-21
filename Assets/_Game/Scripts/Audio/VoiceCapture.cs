using System;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// This peer's microphone (on the AudioDirector prefab): reads it in 10 ms blocks, resamples to 48 kHz, removes the
    /// noise (RNNoise, which also says whether a frame is voice), opens the gate by voice or by the push-to-talk key
    /// (<see cref="VoiceGate"/>), evens out the level (<see cref="VoiceGain"/>), encodes 20 ms Opus frames and hands them
    /// to <see cref="VoiceHub"/>. Runs while there is a local player, or while the settings page tests the microphone.
    /// </summary>
    public class VoiceCapture : MonoBehaviour
    {
        public static VoiceCapture Instance { get; private set; }

        /// <summary>The settings page is open: run the microphone for its level meter even in the menu.</summary>
        public static bool Testing { get; set; }

        /// <summary>The microphone's level after noise suppression, before gain (dBFS, smoothed for the meter).</summary>
        public float LevelDb { get; private set; } = VoiceLevel.Silence;
        /// <summary>The gate is open: this player is being heard.</summary>
        public bool Transmitting { get; private set; }
        public bool Running => m_Clip != null;

        string m_Device;
        int m_Version = -1;
        AudioClip m_Clip;
        int m_Rate, m_Block, m_ReadPos;
        float[] m_BlockData;
        float[] m_Resampled;
        VoiceResampler m_In;
        readonly float[] m_Acc = new float[4096];
        int m_AccCount;
        readonly float[] m_Half = new float[VoiceNative.DenoiseFrame];
        readonly float[] m_Probe = new float[VoiceNative.DenoiseFrame];
        readonly float[] m_Frame = new float[VoiceNative.Frame];
        int m_FrameFill;
        float m_FrameProb;
        readonly byte[] m_Opus = new byte[VoicePacket.MaxPayload];
        readonly byte[] m_Packet = new byte[VoicePacket.MaxSize];
        ushort m_Seq;
        VoiceDenoiser m_Denoiser;
        VoiceEncoder m_Encoder;
        readonly VoiceGate m_Gate = new();
        readonly VoiceGain m_Gain = new();

        void Awake() => Instance = this;

        void OnDestroy()
        {
            StopMic();
            m_Denoiser?.Dispose();
            m_Encoder?.Dispose();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            bool want = VoiceNative.Available && VoicePrefs.Mode != VoiceMode.Off
                && (Testing || PlayerController.Local != null) && HasMicrophone();
            if (!want)
            {
                StopMic();
                return;
            }
            if (m_Clip == null || m_Version != VoicePrefs.Version && m_Device != PickDevice()) StartMic();
            m_Version = VoicePrefs.Version;
            if (m_Clip == null) return;
            Pump();
        }

        static bool s_HasMic;
        static float s_NextMicCheck;

        /// <summary>Is any microphone plugged in (asked once a second: the list allocates).</summary>
        static bool HasMicrophone()
        {
            if (Time.unscaledTime >= s_NextMicCheck)
            {
                s_NextMicCheck = Time.unscaledTime + 1f;
                s_HasMic = Microphone.devices.Length > 0;
            }
            return s_HasMic;
        }

        static string PickDevice()
        {
            string d = VoicePrefs.Device;
            return !string.IsNullOrEmpty(d) && Array.IndexOf(Microphone.devices, d) >= 0 ? d : null;
        }

        void StartMic()
        {
            StopMic();
            m_Device = PickDevice();
            Microphone.GetDeviceCaps(m_Device, out int min, out int max);
            m_Rate = min == 0 && max == 0 ? VoiceNative.SampleRate : Mathf.Clamp(VoiceNative.SampleRate, min, max);
            m_Rate -= m_Rate % 100; // whole 10 ms blocks: they never straddle the end of the 1 s loop
            m_Clip = Microphone.Start(m_Device, true, 1, m_Rate);
            if (m_Clip == null) return;
            m_Rate = m_Clip.frequency;
            m_Block = m_Rate / 100;
            m_BlockData = new float[m_Block];
            m_In = new VoiceResampler(m_Rate, VoiceNative.SampleRate);
            m_Resampled = new float[m_In.MaxOut(m_Block)];
            m_ReadPos = 0;
            m_AccCount = m_FrameFill = 0;
            m_Denoiser ??= new VoiceDenoiser();
            var s = Tuning.Audio.Voice;
            m_Encoder ??= new VoiceEncoder(s.Bitrate, s.PacketLoss);
            m_Gate.Reset();
            Debug.Log($"[Voice] microphone '{m_Device ?? "default"}' at {m_Rate} Hz");
        }

        void StopMic()
        {
            if (m_Clip == null) return;
            Microphone.End(m_Device);
            m_Clip = null;
            Transmitting = false;
            LevelDb = VoiceLevel.Silence;
        }

        void Pump()
        {
            int pos = Microphone.GetPosition(m_Device);
            if (pos < 0) return;
            int samples = m_Clip.samples;
            int available = pos - m_ReadPos;
            if (available < 0) available += samples;
            if (available > samples / 2) // fell behind (a hitch): skip to now
            {
                m_ReadPos = (pos - pos % m_Block - m_Block + samples) % samples;
                available = m_Block;
            }
            while (available >= m_Block)
            {
                m_Clip.GetData(m_BlockData, m_ReadPos);
                m_ReadPos = (m_ReadPos + m_Block) % samples;
                available -= m_Block;
                int n = m_In.Process(m_BlockData, 0, m_Block, m_Resampled, 0);
                n = Math.Min(n, m_Acc.Length - m_AccCount);
                Array.Copy(m_Resampled, 0, m_Acc, m_AccCount, n);
                m_AccCount += n;
                while (m_AccCount >= VoiceNative.DenoiseFrame)
                {
                    Array.Copy(m_Acc, 0, m_Half, 0, VoiceNative.DenoiseFrame);
                    m_AccCount -= VoiceNative.DenoiseFrame;
                    Array.Copy(m_Acc, VoiceNative.DenoiseFrame, m_Acc, 0, m_AccCount);
                    Half();
                }
            }
        }

        void Half()
        {
            float prob;
            if (VoicePrefs.NoiseSuppression) prob = m_Denoiser.Process(m_Half);
            else
            {
                Array.Copy(m_Half, m_Probe, m_Half.Length);
                prob = m_Denoiser.Process(m_Probe); // only its voice detection
            }
            Array.Copy(m_Half, 0, m_Frame, m_FrameFill, m_Half.Length);
            m_FrameFill += m_Half.Length;
            m_FrameProb = Mathf.Max(m_FrameProb, prob);
            if (m_FrameFill < VoiceNative.Frame) return;
            Frame();
            m_FrameFill = 0;
            m_FrameProb = 0f;
        }

        void Frame()
        {
            const float dt = VoiceNative.Frame / (float)VoiceNative.SampleRate;
            var s = Tuning.Audio.Voice;
            float raw = VoiceLevel.RmsDb(m_Frame, 0, m_Frame.Length);
            LevelDb = raw > LevelDb ? raw : Mathf.Max(raw, LevelDb - 30f * dt);
            bool key = Controls.Held(GameAction.PushToTalk);
            bool open = m_Gate.Step(VoicePrefs.Mode, key, m_FrameProb, raw, VoicePrefs.Sensitivity, dt, s);
            m_Gain.Process(m_Frame, 0, m_Frame.Length, raw, open && m_FrameProb >= s.VoiceProbability, dt, s);
            var me = PlayerController.Local;
            Transmitting = open && me != null;
            if (!Transmitting) return;
            int n = m_Encoder.Encode(m_Frame, m_Opus);
            if (n <= 0 || n > VoicePacket.MaxPayload) return;
            int length = VoicePacket.Write(m_Packet, m_Seq++, raw, m_Opus, n);
            VoiceHub.SendLocal(m_Packet, length);
            me.HearOwnVoice(VoiceNoise.Radius(raw, s));
        }
    }
}
