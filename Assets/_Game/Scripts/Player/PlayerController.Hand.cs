using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>The player: the hand: the flashlight and the held map (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        // ------------------------------------------------------------------ the hand: flashlight

        [Tooltip("The flashlight's beam (off unless a charged flashlight in the hand is switched on).")]
        public Light Flashlight;
        [Tooltip("The hand holding the flashlight, seen in the first-person view while a flashlight is in the hand slot (owner only).")]
        public GameObject ViewFlashlight;
        [Tooltip("The shelter map held open in front of the face while it is in the hand slot (every peer sees it: a thing in the world, not UI).")]
        public GameObject HeldMap;
        /// <summary>The flashlight in the hand shines (server-decided, replicated).</summary>
        public bool FlashlightOn { get; private set; }
        float m_Drain;

        /// <summary>Owner: the hand key — use what the hand holds.</summary>
        void UpdateHand()
        {
            if (!Controls.Pressed(GameAction.UseHand) || CursorMode || Downed) return;
            Link.SendUseHand();
        }

        /// <summary>
        /// Server: the hand key. A flashlight switches on (if it has charge) or off; a battery goes into the first flashlight
        /// in the backpack that is not full.
        /// </summary>
        public void UseHandServer()
        {
            var inv = GetComponent<PlayerInventory>();
            int hand = inv.HandIndex();
            if (Downed || hand < 0) return;
            var e = inv.Items[hand];
            if (e.Shape == Items.ItemCatalog.Flashlight)
            {
                FlashlightOn = !FlashlightOn && e.Color > 0;
                return;
            }
            if (e.Shape == Items.ItemCatalog.Battery)
                for (int i = 0; i < inv.Items.Count; i++)
                    if (inv.CanRecharge(i)) { inv.RechargeServer(hand, i); return; }
        }

        /// <summary>Server: the flashlight drains while it shines; it goes out when empty, put away or when its holder falls.</summary>
        void UpdateFlashlightServer(float dt)
        {
            if (!FlashlightOn) { m_Drain = 0f; return; }
            var inv = GetComponent<PlayerInventory>();
            int hand = inv.HandIndex();
            if (Downed || InHatch || hand < 0 || inv.Items[hand].Shape != Items.ItemCatalog.Flashlight || inv.Items[hand].Color == 0)
            {
                FlashlightOn = false;
                return;
            }
            float seconds = Mathf.Max(1f, Configs.Tuning.Items.Gadgets.FlashlightSeconds);
            m_Drain += dt * Items.ItemCatalog.FullCharge / seconds;
            if (m_Drain < 1f) return;
            int used = Mathf.FloorToInt(m_Drain);
            m_Drain -= used;
            int charge = Mathf.Max(0, inv.Items[hand].Color - used);
            inv.SetChargeServer(hand, (byte)charge);
            if (charge == 0) FlashlightOn = false;
        }

        /// <summary>Every peer: the beam where the holder looks.</summary>
        void UpdateFlashlightView()
        {
            if (HeldMap != null)
            {
                var inv = GetComponent<PlayerInventory>();
                int hand = inv != null ? inv.HandIndex() : -1;
                bool show = hand >= 0 && inv.Items[hand].Shape == Items.ItemCatalog.ShelterMap && !Downed && !InHatch && UsingTerminal == null;
                if (HeldMap.activeSelf != show) HeldMap.SetActive(show);
                // a remote player's head does not pitch: the map follows their look up and down
                if (show && !IsOwner) HeldMap.transform.localRotation = (m_HeldMapRot ??= HeldMap.transform.localRotation) * Quaternion.Euler(LookPitch * 0.6f, 0f, 0f);
            }
            if (ViewFlashlight != null)
            {
                var inv = GetComponent<PlayerInventory>();
                int hand = inv != null ? inv.HandIndex() : -1;
                bool show = IsOwner && hand >= 0 && inv.Items[hand].Shape == Items.ItemCatalog.Flashlight && !Downed && !InHatch && UsingTerminal == null;
                if (ViewFlashlight.activeSelf != show) ViewFlashlight.SetActive(show);
            }
            if (Flashlight == null) return;
            bool on = FlashlightOn && !Downed && !InHatch;
            if (Flashlight.enabled != on) Flashlight.enabled = on;
            if (on && !IsOwner) Flashlight.transform.localRotation = Quaternion.Euler(LookPitch, 0f, 0f); // the head of a remote player does not pitch
            if (!on) return;
            // close to a surface the lamp dims (no white blown-out spot): the nearest thing in front that is not this player
            if (m_FlashBase < 0f)
            {
                // the intensity as built, without the style's visual scale (LightSource writes that over the light)
                var source = Flashlight.GetComponent<LightSource>();
                m_FlashBase = source != null ? source.RawIntensity : Flashlight.intensity;
            }
            var t = Flashlight.transform;
            float d = FlashNear;
            int n = Physics.RaycastNonAlloc(t.position, t.forward, s_FlashHits, FlashNear, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (s_FlashHits[i].distance < d && !s_FlashHits[i].collider.transform.IsChildOf(transform)) d = s_FlashHits[i].distance;
            Flashlight.intensity = m_FlashBase * PlayerRules.FlashlightNear(d, FlashNear);
        }

        Quaternion? m_HeldMapRot;              // the held map's pose as built (a remote holder's pitch is added to it)
        const float FlashNear = 1.6f;          // closer than this the flashlight dims (metres)
        float m_FlashBase = -1f;               // its intensity as built
        static readonly RaycastHit[] s_FlashHits = new RaycastHit[8];
    }
}
