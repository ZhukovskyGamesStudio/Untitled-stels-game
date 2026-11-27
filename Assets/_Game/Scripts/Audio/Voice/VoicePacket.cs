using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// One 20 ms voice frame on the wire: a version byte, a sequence number (wraps at 65536), the speaker's raw microphone
    /// level (for the robots: a whisper is quiet even after the gain brings it up) and the Opus payload. The server
    /// relays it to the other peers with the speaker's client id in front (<see cref="WriteRelay"/>).
    /// </summary>
    public static class VoicePacket
    {
        public const byte Version = 1;
        public const int Header = 4;
        public const int RelayHeader = 8 + Header;
        /// <summary>Bigger than any 20 ms Opus frame at the bitrates we use (32 kbit/s ≈ 80 bytes).</summary>
        public const int MaxPayload = 400;
        public const int MaxSize = RelayHeader + MaxPayload;

        const float FloorDb = -90f;

        /// <summary>dBFS → one byte (−90 … 0 dB).</summary>
        public static byte PackLevel(float db) => (byte)Mathf.RoundToInt(Mathf.Clamp01((db - FloorDb) / -FloorDb) * 255f);
        public static float UnpackLevel(byte b) => FloorDb + b / 255f * -FloorDb;

        /// <summary>Writes a frame into <paramref name="buffer"/>; returns its length.</summary>
        public static int Write(byte[] buffer, ushort sequence, float levelDb, byte[] payload, int payloadLength)
        {
            if (payloadLength < 0 || payloadLength > MaxPayload) throw new ArgumentOutOfRangeException(nameof(payloadLength));
            buffer[0] = Version;
            buffer[1] = (byte)sequence;
            buffer[2] = (byte)(sequence >> 8);
            buffer[3] = PackLevel(levelDb);
            Buffer.BlockCopy(payload, 0, buffer, Header, payloadLength);
            return Header + payloadLength;
        }

        /// <summary>Reads a frame at <paramref name="offset"/>; false if it is not one of ours.</summary>
        public static bool Read(byte[] buffer, int offset, int length, out ushort sequence, out float levelDb,
            out int payloadOffset, out int payloadLength)
        {
            sequence = 0; levelDb = FloorDb; payloadOffset = 0; payloadLength = 0;
            if (length < Header || buffer[offset] != Version) return false;
            payloadLength = length - Header;
            if (payloadLength > MaxPayload) return false;
            sequence = (ushort)(buffer[offset + 1] | buffer[offset + 2] << 8);
            levelDb = UnpackLevel(buffer[offset + 3]);
            payloadOffset = offset + Header;
            return true;
        }

        /// <summary>The server's copy for the other peers: the speaker's id, then the frame as it came.</summary>
        public static int WriteRelay(byte[] buffer, ulong speaker, byte[] frame, int offset, int length)
        {
            for (int i = 0; i < 8; i++) buffer[i] = (byte)(speaker >> (8 * i));
            Buffer.BlockCopy(frame, offset, buffer, 8, length);
            return 8 + length;
        }

        public static ulong ReadSpeaker(byte[] buffer, int offset)
        {
            ulong id = 0;
            for (int i = 0; i < 8; i++) id |= (ulong)buffer[offset + i] << (8 * i);
            return id;
        }

        /// <summary>A relay's call: the relay's id, the frame count, then each frame (as it came) with its length in front.</summary>
        public const int LureHeader = 10;

        /// <summary>Bytes of a relay's call carrying these frames (<see cref="WriteLure"/>).</summary>
        public static int LureSize(IReadOnlyList<ArraySegment<byte>> frames)
        {
            int n = LureHeader;
            foreach (var f in frames) n += 2 + f.Count;
            return n;
        }

        /// <summary>The server's message to every peer: play these recorded frames from relay <paramref name="relay"/>.</summary>
        public static int WriteLure(byte[] buffer, ulong relay, IReadOnlyList<ArraySegment<byte>> frames)
        {
            for (int i = 0; i < 8; i++) buffer[i] = (byte)(relay >> (8 * i));
            buffer[8] = (byte)frames.Count;
            buffer[9] = (byte)(frames.Count >> 8);
            int p = LureHeader;
            foreach (var f in frames)
            {
                buffer[p] = (byte)f.Count;
                buffer[p + 1] = (byte)(f.Count >> 8);
                Buffer.BlockCopy(f.Array, f.Offset, buffer, p + 2, f.Count);
                p += 2 + f.Count;
            }
            return p;
        }

        /// <summary>Reads a relay's call; the segments point into <paramref name="buffer"/>. False if it is broken.</summary>
        public static bool ReadLure(byte[] buffer, int offset, int length, out ulong relay, List<ArraySegment<byte>> frames)
        {
            frames.Clear();
            relay = 0;
            if (length < LureHeader) return false;
            relay = ReadSpeaker(buffer, offset);
            int count = buffer[offset + 8] | buffer[offset + 9] << 8;
            int p = offset + LureHeader, end = offset + length;
            for (int i = 0; i < count; i++)
            {
                if (p + 2 > end) return false;
                int n = buffer[p] | buffer[p + 1] << 8;
                p += 2;
                if (n > MaxSize || p + n > end) return false;
                frames.Add(new ArraySegment<byte>(buffer, p, n));
                p += n;
            }
            return true;
        }

        /// <summary>How far <paramref name="a"/> is ahead of <paramref name="b"/>, across the wrap (−32768 … 32767).</summary>
        public static int Ahead(ushort a, ushort b) => (short)(a - b);
    }
}
