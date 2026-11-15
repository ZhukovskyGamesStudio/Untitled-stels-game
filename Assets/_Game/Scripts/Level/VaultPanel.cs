using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The vault door's plate over the doorway, on both faces of the wall (Prefabs/Level/VaultPanel): three lamps, one per
    /// lock panel — red until that lock is solved, then green. Shows the team how far they got.
    /// </summary>
    public class VaultPanel : MonoBehaviour
    {
        public Renderer[] Lamps;

        /// <summary>How many of the vault's locks are solved (set by the game).</summary>
        public static System.Func<int> Solved = () => 0;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color Locked = new(1f, 0.15f, 0.08f), Open = new(0.2f, 1f, 0.35f);
        MaterialPropertyBlock m_Block;
        int m_Shown = -1;

        void LateUpdate()
        {
            int solved = Solved();
            if (solved == m_Shown || Lamps == null) return;
            m_Shown = solved;
            m_Block ??= new MaterialPropertyBlock();
            for (int i = 0; i < Lamps.Length; i++)
            {
                if (Lamps[i] == null) continue;
                var c = i < solved ? Open : Locked * 0.6f;
                Lamps[i].GetPropertyBlock(m_Block);
                m_Block.SetColor(BaseColorId, c);
                m_Block.SetColor(EmissionId, c * (i < solved ? 2.5f : 1.2f));
                Lamps[i].SetPropertyBlock(m_Block);
            }
        }
    }
}
