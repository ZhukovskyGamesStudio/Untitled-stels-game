using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Petting the storage mule (on its prefab): look at the raccoon standing on the floor and press E — it sits back,
    /// leans its head into the hand and wags (<see cref="StorageBot.PetServer"/>, seen by every peer). It has no
    /// collider: it is aimed at by its head's place, like every <see cref="Interactable"/>.
    /// </summary>
    public class StorageBotPet : Interactable
    {
        StorageBot m_Bot;

        public override UseKind Kind => UseKind.PetBot;
        public override float Reach => 1.6f;
        /// <summary>Its head and back (the body is under a metre long, its head about half a metre up).</summary>
        public override Vector3 Point => transform.position + transform.up * 0.45f + transform.forward * 0.12f;
        public override float AimRadius => 0.35f;
        public override Transform ReachRoot => transform;

        void Awake()
        {
            m_Bot = GetComponent<StorageBot>();
            Index = 0;
        }

        public override bool Usable(PlayerController player) => m_Bot != null && StorageBotRules.CanPet(m_Bot.Mode, m_Bot.Motion) && base.Usable(player);

        public override string Prompt(PlayerController player, out bool available)
        {
            available = m_Bot != null && StorageBotRules.CanPet(m_Bot.Mode, m_Bot.Motion);
            return Loc.T("use.pet_bot");
        }
    }
}
