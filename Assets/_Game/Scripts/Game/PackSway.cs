using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The things strapped to Linus's pack (pure, unit tested): how a strap goes round a thing — across its shorter side,
    /// round its middle — and how a thing swings on it as he moves (a few degrees, each its own phase, about the strap's
    /// point on the pack).
    /// </summary>
    public static class PackSway
    {
        /// <summary>
        /// The strap across a thing of <paramref name="size"/> (its piece's axes, y out of the pack): the turn of the strap
        /// model (it spans its own X) and the span — the thing's shorter side, so the strap goes round its middle the short way.
        /// </summary>
        public static (Quaternion turn, float span) StrapAcross(Vector3 size) =>
            size.x >= size.z ? (Quaternion.Euler(0f, 90f, 0f), size.z) : (Quaternion.identity, size.x);

        /// <summary>How a strap hugs a thing: its middle (in the thing's piece space), half its span, its top, its sides' low end.</summary>
        public struct Strap
        {
            public Vector3 Middle;
            public float HalfWidth, Top, Low;
        }

        /// <summary>
        /// The strap round a thing's slice (pure): <paramref name="fit"/> = (middle across, half width, top, 1) of the slice in
        /// the laid thing's space; it runs across the length (<paramref name="lengthAlongX"/>: across is Z, else X), over the
        /// slice's top and down its sides into the pack — a third of the thing's <paramref name="thick"/>ness is pressed into
        /// it (PackWrap.Sink), so the sides end a little below that.
        /// </summary>
        public static Strap Hug(Vector4 fit, float thick, bool lengthAlongX)
        {
            float face = thick * PackWrap.Sink;
            return new Strap
            {
                Middle = lengthAlongX ? new Vector3(0f, 0f, fit.x) : new Vector3(fit.x, 0f, 0f),
                HalfWidth = Mathf.Max(0.004f, fit.y),
                Top = Mathf.Max(fit.z, face + 0.004f),
                Low = face - 0.015f,
            };
        }

        /// <summary>
        /// A thing's swing now, degrees about the pack face's two axes: two slow waves of its own phase
        /// (<paramref name="index"/>), never more than <paramref name="amplitude"/>.
        /// </summary>
        public static (float x, float z) Angles(float phase, int index, float amplitude)
        {
            float a = phase + index * 1.37f, b = phase * 0.83f + index * 2.11f;
            return (Mathf.Sin(a) * amplitude * 0.8f, Mathf.Sin(b) * amplitude * 0.6f);
        }
    }
}
