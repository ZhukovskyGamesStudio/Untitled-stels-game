using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A lock panel of the vault, on a wall next to it (<see cref="LevelLayout.VaultConsoles"/>; its index is the
    /// <see cref="VaultGame"/>): E opens its minigame on the screen (<see cref="PlayerController.AtConsole"/>, the dial is
    /// the safe's), solved it reports to the server (<see cref="Game.GameManager.UseServer"/>). Its screen glows red until
    /// solved, then green; all three — the vault's door opens.
    /// </summary>
    public class VaultConsole : Interactable
    {
        [Tooltip("The panel's screen: red while locked, green once solved.")]
        public Renderer Screen;
        [Tooltip("Pictures on the status strip: a shut padlock (red) until solved, an open one (green) after.")]
        public Texture2D LockedPicture, OpenPicture;
        [Tooltip("The panel's face per minigame (Dial, Switches, Sequence): only its own shows.")]
        public GameObject[] Faces;


        public override UseKind Kind => UseKind.VaultConsole;
        public override float Reach => 2.0f;
        public override Vector3 Point => transform.position + transform.forward * 0.08f;
        public override float AimRadius => 0.4f;
        public VaultGame Game => (VaultGame)Mathf.Clamp(Index, 0, 2);
        public bool Solved => Index >= 0 && World.IsOpened(OpenKeys.Console(Index));

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor"),
            BaseMapId = Shader.PropertyToID("_BaseMap"), EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly Color Locked = new(1f, 0.15f, 0.08f), Open = new(0.2f, 1f, 0.35f);
        MaterialPropertyBlock m_Block;
        int m_Shown = -1;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !Solved;
            return available ? Loc.T("use.vault_console") : null;
        }

        void LateUpdate()
        {
            if (Faces != null)
                for (int i = 0; i < Faces.Length; i++)
                    if (Faces[i] != null && Faces[i].activeSelf != (i == (int)Game)) Faces[i].SetActive(i == (int)Game);
            int state = Solved ? 1 : 0;
            if (state == m_Shown || Screen == null) return;
            m_Shown = state;
            m_Block ??= new MaterialPropertyBlock();
            var c = state == 1 ? Open : Locked;
            Screen.GetPropertyBlock(m_Block);
            var picture = state == 1 ? OpenPicture : LockedPicture;
            if (picture != null)
            {
                m_Block.SetTexture(BaseMapId, picture);
                m_Block.SetTexture(EmissionMapId, picture);
            }
            m_Block.SetColor(BaseColorId, c);
            m_Block.SetColor(EmissionId, c * 2.2f);
            Screen.SetPropertyBlock(m_Block);
        }
    }
}
