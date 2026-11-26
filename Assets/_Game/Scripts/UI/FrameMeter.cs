namespace Stealth.UI
{
    /// <summary>
    /// The frame rate and the frame's time as the player reads them on screen (pure): averaged over half a second, so the
    /// numbers stand still long enough to read — frames shown in that time, and the mean time of one.
    /// </summary>
    public sealed class FrameMeter
    {
        /// <summary>A fresh pair of numbers this often, seconds.</summary>
        public const float Window = 0.5f;

        float m_Time;
        int m_Count;

        /// <summary>Frames a second over the last window.</summary>
        public float Fps { get; private set; }
        /// <summary>The mean time of a frame over the last window, milliseconds.</summary>
        public float Milliseconds { get; private set; }

        /// <summary>A frame that took <paramref name="seconds"/>; true: a window closed, the numbers are new.</summary>
        public bool Add(float seconds)
        {
            if (seconds <= 0f) return false;
            m_Time += seconds;
            m_Count++;
            if (m_Time < Window) return false;
            Fps = m_Count / m_Time;
            Milliseconds = m_Time / m_Count * 1000f;
            m_Time = 0f;
            m_Count = 0;
            return true;
        }

        /// <summary>The window starts over (the meter was hidden).</summary>
        public void Reset()
        {
            m_Time = 0f;
            m_Count = 0;
        }
    }
}
