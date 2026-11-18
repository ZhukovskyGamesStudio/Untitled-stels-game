using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// A safe on the level (prefab Props/Safe): it can be cracked with the dial minigame (<see cref="SafeLock"/>), then it
    /// gives valuable loot. Its keypad is red while locked and green once open. The server keeps the opened safes
    /// (<see cref="World"/>); every peer only shows them.
    /// </summary>
    public class SafeBox : MonoBehaviour, IReachTarget
    {
        public static readonly List<SafeBox> All = new();

        /// <summary>Stand this close to start cracking.</summary>
        public const float Reach = 1.8f;

        [Tooltip("The glowing keypad: red locked, green open.")]
        public Renderer Keypad;


        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color LockedColor = new(1f, 0.15f, 0.1f), OpenColor = new(0.25f, 1f, 0.4f);

        /// <summary>Index in <see cref="LevelLayout.Props"/>: the same on every peer, names the safe in requests.</summary>
        public int Index { get; private set; } = -1;
        public bool Opened => Index >= 0 && World.IsOpened(OpenKeys.Safe(Index));
        /// <summary>Seed of the combination (from the house seed and the index: the same on every peer).</summary>
        public int Seed { get; private set; }

        MaterialPropertyBlock m_Block;
        int m_Shown = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Configure(int index, int houseSeed)
        {
            Index = index;
            Seed = LevelRandom.Mix(houseSeed, Salt.SafeCode, index);
            Show(false); // a property block now also keeps the keypad out of the merged furniture: it changes colour
        }

        void Update() => Show(Opened);

        void Show(bool open)
        {
            int state = open ? 1 : 0;
            if (state == m_Shown || Keypad == null) return;
            m_Shown = state;
            m_Block ??= new MaterialPropertyBlock();
            var c = open ? OpenColor : LockedColor;
            Keypad.GetPropertyBlock(m_Block);
            m_Block.SetColor(BaseColorId, c);
            m_Block.SetColor(EmissionId, c * 2f);
            Keypad.SetPropertyBlock(m_Block);
        }

        /// <summary>The closed safe a player can crack from here (same floor), or null.</summary>
        public static SafeBox ClosedNear(Vector3 player, Vector3 hands, float reach = Reach, Player.Aim? aim = null)
            => ReachSearch.Nearest(All, player, hands, reach, aim);

        bool IReachTarget.CanReach(Vector3 player) => Index >= 0 && !Opened;
        (Vector3, Vector3, float) IReachTarget.AimSpan => (transform.position + Vector3.up * 0.15f, transform.position + Vector3.up * 0.9f, 0.45f);
        Vector3? IReachTarget.HandsTo => transform.position + Vector3.up * 0.5f;

        /// <summary>Where the loot falls out: in front of the safe, towards the middle of its room.</summary>
        public Vector3 LootSpot(int i)
        {
            var layout = ActiveLevel.Layout;
            var p = transform.position;
            var toRoom = layout != null ? layout.RoomCenterAt(p) - p : -transform.forward;
            toRoom.y = 0;
            var dir = toRoom.sqrMagnitude > 0.01f ? toRoom.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            return p + dir * 0.85f + side * (i - 0.5f) * 0.6f + Vector3.up * 0.3f;
        }
    }
}
