using System.Collections.Generic;
using Stealth.Audio;
using Stealth.Game;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The shredder on the car's hood, seen (every peer, by the server's clock: <see cref="Shredder"/>): the very things thrown
    /// in heap up in the hopper's mouth — a new one drops onto the pile — and the first one goes under the blades: it shakes,
    /// spins, sinks and shrinks away while chips fly and the blades whine (the car spins them fast meanwhile,
    /// <see cref="Car.Grinding"/>); a gulp when it is gone and its fuel is in the tank.
    /// </summary>
    public class CarShredder : MonoBehaviour
    {
        [Tooltip("The hopper's mouth: the pile sits on it, the blades are under it.")]
        public Transform Mouth;
        [Tooltip("A thing's look (the bed's piece prefab: mesh, colour or a tool's own model).")]
        public CargoPiece PiecePrefab;
        public ItemVisuals Visuals;
        [Tooltip("Chips flying out of the mouth while the blades chew.")]
        public ParticleSystem Chips;
        public Car Car;

        /// <summary>A thing in the mouth is at most this long (a shovel goes in shrunk), metres.</summary>
        const float Fit = 0.32f;

        readonly List<CargoPiece> m_Shown = new();
        readonly List<float> m_Arrived = new();
        int m_LastCount, m_LastVersion = -1;
        (byte, byte) m_LastFirst = (255, 255);

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || Mouth == null || PiecePrefab == null) return;
            var shredder = gm.Shredder;
            var pile = shredder.Pile;
            double now = Session.ServerTime;
            if (shredder.Version != m_LastVersion) Sync(pile);
            float progress = shredder.Progress(now);
            bool chewing = pile.Count > 0 && progress >= 0f;
            if (Car != null) Car.Grinding = pile.Count > 0;
            if (Chips != null)
            {
                var e = Chips.emission;
                e.rateOverTime = chewing ? 70f : 0f;
            }
            var up = Mouth.up;
            for (int i = 0; i < m_Shown.Count; i++)
            {
                var piece = m_Shown[i];
                var t = piece.transform;
                float drop = Mathf.Clamp01((Time.time - m_Arrived[i]) / Shredder.FallTime);
                drop = 1f - (1f - drop) * (1f - drop);
                float rest = 0.08f + 0.11f * i;                                   // its place on the pile
                var wobble = new Vector3(Mathf.Sin(Time.time * 7f + i * 2.1f), 0f, Mathf.Cos(Time.time * 5.3f + i)) * 0.012f;
                float scale = piece.FitScale;
                if (i == 0 && chewing)
                {
                    // under the blades: shaking, spinning, sinking, shrinking away
                    float shake = Mathf.Sin(Time.time * 55f) * 0.012f * (1f - progress);
                    t.position = Mouth.position + up * Mathf.Lerp(rest, -0.12f, progress) + Mouth.right * shake + wobble;
                    t.rotation = Mouth.rotation * Quaternion.Euler(progress * 90f, progress * 540f + i * 40f, Mathf.Sin(Time.time * 31f) * 12f * (1f - progress));
                    t.localScale = Vector3.one * scale * Mathf.Lerp(1f, 0.15f, progress * progress);
                }
                else
                {
                    t.position = Mouth.position + up * Mathf.Lerp(rest + 0.9f, rest, drop) + wobble;
                    t.rotation = Mouth.rotation * Quaternion.Euler(i * 23f, i * 67f, i * 11f);
                    t.localScale = Vector3.one * scale;
                }
            }
        }

        /// <summary>The pile changed: a piece per thing (new ones fall in), a crunch when the next goes under, a gulp when one is gone.</summary>
        void Sync(IReadOnlyList<(byte shape, byte color)> pile)
        {
            var gm = GameManager.Instance;
            m_LastVersion = gm.Shredder.Version;
            // gone from the front: those were chewed up
            int eaten = 0;
            while (m_Shown.Count > 0 && eaten < m_LastCount && (m_Shown.Count > pile.Count || !Same(m_Shown[0], pile, 0)))
            {
                Recycle(m_Shown[0]);
                m_Shown.RemoveAt(0);
                m_Arrived.RemoveAt(0);
                eaten++;
            }
            if (eaten > 0) Sfx.At(Sfx.Bank?.FuelGulp, Mouth.position, 0.8f, Sfx.Group.Items);
            while (m_Shown.Count > pile.Count) { Recycle(m_Shown[^1]); m_Shown.RemoveAt(m_Shown.Count - 1); m_Arrived.RemoveAt(m_Arrived.Count - 1); }
            for (int i = 0; i < pile.Count; i++)
            {
                if (i < m_Shown.Count)
                {
                    if (!Same(m_Shown[i], pile, i)) Dress(m_Shown[i], pile[i]);
                    continue;
                }
                var piece = Take();
                Dress(piece, pile[i]);
                m_Shown.Add(piece);
                m_Arrived.Add(Time.time + 0.06f * (i - m_LastCount)); // a heap thrown in lands one after another
            }
            m_LastCount = pile.Count;
            var first = pile.Count > 0 ? pile[0] : ((byte)255, (byte)255);
            if (pile.Count > 0 && (eaten > 0 || first != m_LastFirst)) Sfx.At(Sfx.Bank?.Grind, Mouth.position, 0.9f, Sfx.Group.Items);
            m_LastFirst = first;
        }

        bool Same(CargoPiece p, IReadOnlyList<(byte shape, byte color)> pile, int i) =>
            i < pile.Count && p.Item.Shape == pile[i].shape && p.Item.Color == pile[i].color;

        void Dress(CargoPiece p, (byte shape, byte color) item)
        {
            p.Show(-1, new CargoItem { Shape = item.shape, Color = item.color }, Visuals);
            var size = ItemCatalog.ModelSize(item.shape);
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            p.FitScale = longest > Fit ? Fit / longest : 1f;
        }

        readonly Stack<CargoPiece> m_Spare = new();

        CargoPiece Take()
        {
            var p = m_Spare.Count > 0 ? m_Spare.Pop() : Instantiate(PiecePrefab, transform);
            p.gameObject.SetActive(true);
            foreach (var c in p.GetComponentsInChildren<Collider>(true)) c.enabled = false; // only to look at: not picked up from the hopper
            return p;
        }

        void Recycle(CargoPiece p)
        {
            p.gameObject.SetActive(false);
            m_Spare.Push(p);
        }
    }
}
