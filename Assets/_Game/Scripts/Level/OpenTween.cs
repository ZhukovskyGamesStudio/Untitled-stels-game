using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a step of an <see cref="OpenTween"/> did.</summary>
    public enum OpenMove : byte { None, StartedOpening, StartedShutting }

    /// <summary>
    /// How far a lid, a gate, a grate or a door is open (0 shut … 1 open), following the server's word at its own pace:
    /// the one rule every such part moves by (pure). The first step snaps to the word (a house built with it already open
    /// shows it open, silently); after that a change of the word starts a move — the moment its sound plays.
    /// </summary>
    public struct OpenTween
    {
        public float Value;
        bool m_Started, m_Target;

        /// <summary>The value eased in and out (for an angle or a slide).</summary>
        public readonly float Smooth => Mathf.SmoothStep(0f, 1f, Value);
        /// <summary>Not moving: all the way open or shut.</summary>
        public readonly bool Settled => Value <= 0f || Value >= 1f;

        /// <summary>One frame: towards open (or shut), taking <paramref name="seconds"/> for the whole way.</summary>
        public OpenMove Step(bool open, float dt, float seconds)
        {
            if (!m_Started)
            {
                m_Started = true;
                m_Target = open;
                Value = open ? 1f : 0f;
                return OpenMove.None;
            }
            var move = OpenMove.None;
            if (open != m_Target)
            {
                m_Target = open;
                move = open ? OpenMove.StartedOpening : OpenMove.StartedShutting;
            }
            Value = Mathf.MoveTowards(Value, open ? 1f : 0f, seconds > 0f ? dt / seconds : 1f);
            return move;
        }
    }
}
