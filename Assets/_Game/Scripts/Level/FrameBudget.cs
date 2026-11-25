using System.Diagnostics;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// How much of a frame a piece-by-piece job (a house going up or coming down on the drive, the style put on it) may
    /// take before it waits for the next frame: steps ask <see cref="Over"/> and yield when it is true. Outside play mode
    /// (tests, the editor) the steps run through in one go anyway.
    /// </summary>
    public static class FrameBudget
    {
        /// <summary>Milliseconds of a frame such work may take (the drive goes on at 60+ fps around it).</summary>
        public const double Milliseconds = 6.0;

        /// <summary>What the stepped work is doing now (a development build's slow-step log names it: GameManager.Timed).</summary>
        public static string Step = "";

        static readonly Stopwatch s_Watch = Stopwatch.StartNew();
        static int s_Frame = -1;

        /// <summary>This frame's share is used up (the clock starts at the first question of a frame).</summary>
        public static bool Over
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != s_Frame)
                {
                    s_Frame = frame;
                    s_Watch.Restart();
                    return false;
                }
                return s_Watch.Elapsed.TotalMilliseconds > Milliseconds;
            }
        }
    }
}
