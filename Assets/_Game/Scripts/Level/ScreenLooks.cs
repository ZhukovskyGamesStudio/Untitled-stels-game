using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a screen with nothing to show looks like (a TV, a desk monitor): the house is empty at night.</summary>
    public enum ScreenLook { Off, Static, Cracked }

    /// <summary>
    /// The look of the furniture's screens (pure): a house's TVs and monitors are not all one glowing blue panel — most are
    /// switched off (dark glass), some are left on with nothing but static, some are broken (cracked glass). Chosen per
    /// piece from the house's seed, so every peer sees the same (LevelBuilder swaps the screen's material:
    /// <see cref="LevelArt.Screens"/>). Screens that are used — terminals, consoles — have their own pictures.
    /// </summary>
    public static class ScreenLooks
    {
        /// <summary>Of a hundred screens: this many are off, this many show static; the rest are cracked.</summary>
        public const int OffShare = 55, StaticShare = 25;
        /// <summary>A glowing part smaller than this (its longest side, metres) is a display on an appliance, not a screen: it keeps its dim glow.</summary>
        public const float SmallScreen = 0.2f;

        /// <summary>The look of a piece's screens, from its own stream of the house's seed.</summary>
        public static ScreenLook Pick(int seed)
        {
            int roll = (int)((uint)seed % 100u);
            return roll < OffShare ? ScreenLook.Off : roll < OffShare + StaticShare ? ScreenLook.Static : ScreenLook.Cracked;
        }

        /// <summary>Where a screen's static is on its texture at a step (pure): it jumps about, never drifting one way.</summary>
        public static Vector2 Jump(int step)
        {
            float x = step * 0.61803398f, y = step * 0.75487766f;
            return new Vector2(x - Mathf.Floor(x), y - Mathf.Floor(y));
        }
    }

    /// <summary>
    /// The screens showing static in the built house: their noise jumps a dozen times a second (a property block per
    /// screen — which also keeps them out of the merged furniture — only on the ones in view). Picture only, on every peer.
    /// </summary>
    public static class LiveScreens
    {
        /// <summary>How many times the noise texture repeats over a metre of screen, and how often it jumps, seconds.</summary>
        public const float Tiling = 4f, Interval = 0.08f;

        static readonly List<Renderer> s_Static = new();
        static readonly int StId = Shader.PropertyToID("_BaseMap_ST");
        static MaterialPropertyBlock s_Block;
        static float s_Next;
        static int s_Step;

        public static int Count => s_Static.Count;

        public static void Clear() => s_Static.Clear();

        public static void Add(Renderer r)
        {
            if (r == null) return;
            s_Static.Add(r);
            Set(r, ScreenLooks.Jump(s_Static.Count));
        }

        /// <summary>One frame (called by the level's light budget, which runs wherever a house stands).</summary>
        public static void Tick(float time)
        {
            if (s_Static.Count == 0 || (time >= s_Next - Interval && time < s_Next)) return;
            s_Next = time + Interval;
            s_Step++;
            for (int i = s_Static.Count - 1; i >= 0; i--)
            {
                var r = s_Static[i];
                if (r == null) { s_Static.RemoveAt(i); continue; }
                if (r.isVisible) Set(r, ScreenLooks.Jump(s_Step * 7 + i));
            }
        }

        static void Set(Renderer r, Vector2 at)
        {
            s_Block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(s_Block);
            s_Block.SetVector(StId, new Vector4(Tiling, Tiling, at.x, at.y));
            r.SetPropertyBlock(s_Block);
        }
    }
}
