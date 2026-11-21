using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// The native voice plugin (Plugins/x86_64/StealthVoice.dll, built from Xiph's Opus and RNNoise by
    /// Tools/Voice/build_native.sh): the Opus codec and the RNNoise denoiser. Audio is mono float PCM at 48 kHz.
    /// </summary>
    public static class VoiceNative
    {
        const string Lib = "StealthVoice";
        public const int SampleRate = 48000;
        /// <summary>One codec frame: 20 ms.</summary>
        public const int Frame = 960;
        /// <summary>One denoiser frame: 10 ms.</summary>
        public const int DenoiseFrame = 480;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sv_version();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sv_encoder_create(int bitrate);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void sv_encoder_set_bitrate(IntPtr enc, int bitrate);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void sv_encoder_set_loss(IntPtr enc, int percent);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sv_encode(IntPtr enc, float[] pcm, int frame, byte[] output, int maxBytes);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void sv_encoder_destroy(IntPtr enc);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sv_decoder_create();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sv_decode(IntPtr dec, byte[] data, int length, float[] output, int frame, int fec);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void sv_decoder_destroy(IntPtr dec);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sv_denoise_create();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern float sv_denoise(IntPtr state, float[] frame);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void sv_denoise_destroy(IntPtr state);

        static int s_Available = -1;

        /// <summary>False when the plugin is missing (another platform): voice chat is then off, the game runs as before.</summary>
        public static bool Available
        {
            get
            {
                if (s_Available < 0)
                {
                    try { s_Available = sv_version() >= 1 ? 1 : 0; }
                    catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                    {
                        s_Available = 0;
                        Debug.LogWarning("[Voice] StealthVoice plugin not found: voice chat is off. " + e.Message);
                    }
                }
                return s_Available == 1;
            }
        }
    }

    /// <summary>Opus encoder of one microphone (VoIP mode, in-band FEC against lost packets).</summary>
    public sealed class VoiceEncoder : IDisposable
    {
        IntPtr m_Handle;

        public VoiceEncoder(int bitrate, int packetLoss)
        {
            m_Handle = VoiceNative.sv_encoder_create(bitrate);
            if (m_Handle == IntPtr.Zero) throw new InvalidOperationException("Opus encoder");
            VoiceNative.sv_encoder_set_loss(m_Handle, packetLoss);
        }

        /// <summary>Encodes one 20 ms frame; returns the packet's length (≤ 0 on error).</summary>
        public int Encode(float[] frame, byte[] output) =>
            m_Handle == IntPtr.Zero ? -1 : VoiceNative.sv_encode(m_Handle, frame, VoiceNative.Frame, output, output.Length);

        public void Dispose()
        {
            if (m_Handle != IntPtr.Zero) VoiceNative.sv_encoder_destroy(m_Handle);
            m_Handle = IntPtr.Zero;
        }
    }

    /// <summary>Opus decoder of one speaker.</summary>
    public sealed class VoiceDecoder : IDisposable
    {
        IntPtr m_Handle;

        public VoiceDecoder()
        {
            m_Handle = VoiceNative.sv_decoder_create();
            if (m_Handle == IntPtr.Zero) throw new InvalidOperationException("Opus decoder");
        }

        /// <summary>
        /// One 20 ms frame into <paramref name="output"/>: the packet itself, the lost frame before it from its redundancy
        /// (<paramref name="fec"/>), or a concealed frame when <paramref name="data"/> is null. Returns the samples written.
        /// </summary>
        public int Decode(byte[] data, int length, float[] output, bool fec) =>
            m_Handle == IntPtr.Zero ? -1 : VoiceNative.sv_decode(m_Handle, data, data == null ? 0 : length, output, VoiceNative.Frame, fec ? 1 : 0);

        public void Dispose()
        {
            if (m_Handle != IntPtr.Zero) VoiceNative.sv_decoder_destroy(m_Handle);
            m_Handle = IntPtr.Zero;
        }
    }

    /// <summary>RNNoise: a small neural network that removes noise (fans, keyboards, the street) and says how sure it is that a 10 ms frame is voice.</summary>
    public sealed class VoiceDenoiser : IDisposable
    {
        IntPtr m_Handle;

        public VoiceDenoiser()
        {
            m_Handle = VoiceNative.sv_denoise_create();
            if (m_Handle == IntPtr.Zero) throw new InvalidOperationException("RNNoise");
        }

        /// <summary>Denoises 480 samples in place; returns the voice probability 0..1.</summary>
        public float Process(float[] frame) => m_Handle == IntPtr.Zero ? 1f : VoiceNative.sv_denoise(m_Handle, frame);

        public void Dispose()
        {
            if (m_Handle != IntPtr.Zero) VoiceNative.sv_denoise_destroy(m_Handle);
            m_Handle = IntPtr.Zero;
        }
    }
}
