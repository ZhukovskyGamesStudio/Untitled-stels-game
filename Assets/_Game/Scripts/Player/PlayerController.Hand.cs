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
        [Tooltip("Anything else in the hand slot (not the flashlight, not the map: they have their own), shown low in front of the view; every peer sees it. Its parent is the anchor that is shown and hidden.")]
        public MeshFilter HeldThing;
        [Tooltip("The right hand closed round what it holds, in the first-person view (owner only; a child of the held thing's anchor, its fist at the anchor).")]
        public GameObject HeldHand;
        [Tooltip("The items' meshes, colours and models (Data/ItemVisuals).")]
        public Items.ItemVisuals Visuals;
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

        int m_HeldShape = -1, m_HeldColor = -1;
        GameObject m_HeldModel;
        Quaternion? m_HeldRot;

        /// <summary>A mesh with no bounds is taken as this big, metres.</summary>
        const float HeldSize = 0.12f;
        /// <summary>The top of the closed fist over the anchor (the grip's axis), metres: a compact thing rests there.</summary>
        const float FistTop = 0.035f;

        /// <summary>
        /// Every peer: what the hand slot holds is in the hand — its own model if it has one (a key, the speaker, the
        /// screwdriver…), else its piece in its colour, sized to the hand. The flashlight and the map have their own.
        /// </summary>
        void UpdateHeldThing()
        {
            if (HeldThing == null || Visuals == null) return;
            var anchor = HeldThing.transform.parent != null ? HeldThing.transform.parent.gameObject : HeldThing.gameObject;
            var inv = GetComponent<PlayerInventory>();
            int hand = inv != null ? inv.HandIndex() : -1;
            int shape = -1, color = 0;
            if (hand >= 0 && !Downed && !InHatch && UsingTerminal == null && SeatIndex < 0)
            {
                var e = inv.Items[hand];
                if (e.Shape != Items.ItemCatalog.Flashlight && e.Shape != Items.ItemCatalog.ShelterMap) { shape = e.Shape; color = e.Color; }
            }
            bool show = shape >= 0;
            if (anchor.activeSelf != show) anchor.SetActive(show);
            if (HeldHand != null && HeldHand.activeSelf != (show && IsOwner)) HeldHand.SetActive(show && IsOwner);
            if (!show) return;
            if (shape != m_HeldShape || color != m_HeldColor) ShowHeld(shape, color);
            // a remote player's head does not pitch: the thing follows their look up and down
            if (!IsOwner) anchor.transform.localRotation = (m_HeldRot ??= anchor.transform.localRotation) * Quaternion.Euler(LookPitch * 0.6f, 0f, 0f);
        }

        void ShowHeld(int shape, int color)
        {
            m_HeldShape = shape;
            m_HeldColor = color;
            if (m_HeldModel != null) Destroy(m_HeldModel);
            m_HeldModel = null;
            var renderer = HeldThing.GetComponent<MeshRenderer>();
            var model = Visuals.Models != null && shape < Visuals.Models.Length ? Visuals.Models[shape] : null;
            Bounds b;
            if (model != null)
            {
                m_HeldModel = Instantiate(model, HeldThing.transform);
                m_HeldModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                HeldThing.sharedMesh = null;
                if (renderer != null) renderer.enabled = false;
                var size = Items.ItemCatalog.ModelSize(shape); // (a model lies along X, centred, on the floor)
                b = new Bounds(new Vector3(0f, size.y / 2f, 0f), size);
                var mode = IsOwner ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                foreach (var r in m_HeldModel.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
                foreach (var c in m_HeldModel.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            }
            else
            {
                var mesh = Visuals.Meshes[Mathf.Clamp(shape, 0, Visuals.Meshes.Length - 1)];
                HeldThing.sharedMesh = mesh;
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(color, 0, Visuals.ColorMaterials.Length - 1)];
                    renderer.shadowCastingMode = IsOwner ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
                b = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one * HeldSize);
            }
            // in the fist the right way round: a long thing points ahead along the anchor's +Z (its held end in the fist,
            // turned about that way), a compact one rests on the closed fingers
            var pose = Items.ItemCatalog.HeldPoseOf(shape);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float scale = longest > 1e-3f ? pose.Length / longest : 1f;
            var t = HeldThing.transform;
            t.localScale = Vector3.one * scale;
            if (pose.Compact)
            {
                t.localRotation = Quaternion.Euler(0f, -90f, 0f);
                t.localPosition = new Vector3(0f, FistTop + b.size.y * scale * 0.5f, 0f) - t.localRotation * (b.center * scale);
            }
            else
            {
                // model X (its length) onto +Z: yaw −90 when +X is ahead, +90 when −X is
                t.localRotation = Quaternion.Euler(0f, 0f, pose.Roll) * Quaternion.Euler(0f, pose.Forward > 0f ? -90f : 90f, 0f);
                var grip = new Vector3(b.center.x - pose.Forward * b.size.x * (0.5f - pose.Grip), b.center.y, b.center.z); // model space
                t.localPosition = -(t.localRotation * (grip * scale));
            }
            VisualStyles.Dress(HeldThing.gameObject);
        }

        /// <summary>Every peer: the beam where the holder looks.</summary>
        void UpdateFlashlightView()
        {
            UpdateHeldThing();
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
