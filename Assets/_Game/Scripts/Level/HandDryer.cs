using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The hand dryer on a restroom's wall (prefab Props/HandDryer): E switches it on for a few seconds — a loud roar
    /// every robot with ears within <see cref="Configs.InteractionSettings"/>' reach comes to check (the server emits the
    /// noise, <see cref="Game.GameManager.UseServer"/>). Running or not comes from the game (<see cref="World"/>),
    /// so every peer hears it.
    /// </summary>
    public class HandDryer : Interactable
    {
        [Tooltip("The roar (one shot, played while it runs).")]
        public AudioSource Sound;
        [Tooltip("The LED: lit while it runs.")]
        public Renderer Led;


        public override UseKind Kind => UseKind.HandDryer;
        public override Vector3 Point => transform.position + Vector3.up * 1.2f + transform.forward * 0.12f;
        public override float AimRadius => 0.35f;

        bool m_Was;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !World.IsOpened(OpenKeys.Dryer(Index));
            return available ? Loc.T("use.dryer") : null;
        }

        void Update()
        {
            if (Index < 0) return;
            bool on = World.IsOpened(OpenKeys.Dryer(Index));
            if (on == m_Was) return;
            m_Was = on;
            if (Sound != null)
            {
                if (on) { Sound.clip = Audio.Sfx.Bank != null ? Audio.Sfx.Bank.HandDryer : Sound.clip; Sound.Play(); }
                else Sound.Stop();
            }
            if (Led != null) Led.enabled = on;
        }
    }
}
