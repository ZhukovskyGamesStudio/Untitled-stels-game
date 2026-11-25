using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Tuning of how far a player looks round with the body held (Data/Config/Player → Look): on the shaft's ladder, in a seat of the car.</summary>
    [System.Serializable]
    public class LookSettings
    {
        [Tooltip("On the ladder the body faces the rungs, hands and feet on them: the head turns this far to either side, degrees.")]
        public float LadderHead = 100f;
        [Tooltip("In a seat of the car: the head turns this far to either side, degrees ...")]
        public float SeatHead = 95f;
        [Tooltip("... and the sitting body this much more (it shifts on the seat), degrees.")]
        public float SeatBody = 15f;
    }

    /// <summary>
    /// A look round with the body held (pure, tested): on the ladder or in a seat the body keeps facing its anchor (the
    /// rungs, the road), the mouse turns the look — the head up to its limit, the body a share of it up to its own — and
    /// no further, never round the back. The look is kept as degrees off the anchor.
    /// </summary>
    public static class LookLimits
    {
        /// <summary>The look turned by <paramref name="delta"/> degrees, held within the head's and the body's limits together.</summary>
        public static float Turn(float look, float delta, float head, float body)
        {
            float max = Mathf.Max(0f, head) + Mathf.Max(0f, body);
            return Mathf.Clamp(look + delta, -max, max);
        }

        /// <summary>Of a look <paramref name="look"/> degrees off the anchor, the body's turn (its share, up to its own limit); the rest is the head's.</summary>
        public static float Body(float look, float head, float body)
        {
            float h = Mathf.Max(0f, head), b = Mathf.Max(0f, body);
            return h + b <= 0f ? 0f : look * b / (h + b);
        }

        /// <summary>Taking hold: the look the body brought (its yaw off the anchor, any angle), held within the limits.</summary>
        public static float Take(float bodyYaw, float anchorYaw, float head, float body) => Turn(Mathf.DeltaAngle(anchorYaw, bodyYaw), 0f, head, body);
    }
}
