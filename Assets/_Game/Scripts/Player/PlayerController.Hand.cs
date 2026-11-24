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
        [Tooltip("A faint light round the eyes, only its owner's (the style sets its strength: StyleWorld.EyeGlow).")]
        public Light EyeGlow;
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
            // the tablet opens and closes its map on this peer only
            var inv = GetComponent<PlayerInventory>();
            if (inv != null && inv.Holds(Items.ItemCatalog.Tablet)) { TabletOpen = !TabletOpen; return; }
            if (UseCraftedInHand(inv)) return;
            Link.SendUseHand();
        }

        /// <summary>Owner: the tablet is raised to the eyes (the hand key toggles it while the tablet is held).</summary>
        public bool TabletOpen { get; private set; }

        bool m_HasScreen;              // the held model has a screen (the tablet): its box in the held thing's space
        Bounds m_ScreenBox;
        float m_Raise;                 // 0 the tablet low in the hand .. 1 up before the eyes
        Vector3 m_HolderPos;           // the hand's anchor as built
        Quaternion m_HolderRot;
        bool m_HolderKnown;

        /// <summary>
        /// Owner: the tablet in the hand — where its screen face is (<paramref name="parent"/>: the held thing, whose space
        /// is the model's: X along it, +Y up; <paramref name="box"/>: the screen face's box there). False without one.
        /// </summary>
        public bool HeldScreen(out Transform parent, out Bounds box)
        {
            parent = HeldThing != null ? HeldThing.transform : null;
            box = m_ScreenBox;
            return IsOwner && m_HasScreen && parent != null && parent.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Owner, every frame: the hand's anchor between its place low on the right and up before the eyes (the tablet
        /// raised: its screen centred in the view, facing it).
        /// </summary>
        void UpdateRaise(GameObject anchor)
        {
            if (!m_HolderKnown) { m_HolderPos = anchor.transform.localPosition; m_HolderRot = anchor.transform.localRotation; m_HolderKnown = true; }
            if (!m_HasScreen) TabletOpen = false;
            m_Raise = Mathf.MoveTowards(m_Raise, TabletOpen && m_HasScreen ? 1f : 0f, Time.deltaTime * 4f);
            float k = m_Raise * m_Raise * (3f - 2f * m_Raise);
            var t = anchor.transform;
            if (k <= 0f) { t.SetLocalPositionAndRotation(m_HolderPos, m_HolderRot); return; }
            // the screen's centre in the anchor's own space; raised, it sits RaisedAt before the eyes, facing them
            var centre = new Vector3(m_ScreenBox.center.x, m_ScreenBox.max.y, m_ScreenBox.center.z);
            var inAnchor = t.InverseTransformPoint(HeldThing.transform.TransformPoint(centre));
            var up = Quaternion.Euler(-35f, 0f, 0f); // the tablet tilts 55° to the eyes on the fingers: 35° more faces them
            var upPos = RaisedAt - up * inAnchor;
            t.SetLocalPositionAndRotation(Vector3.Lerp(m_HolderPos, upPos, k), Quaternion.Slerp(m_HolderRot, up, k));
        }

        /// <summary>Where the raised tablet's screen is, in the head's space (metres before the eyes, a touch low).</summary>
        static readonly Vector3 RaisedAt = new(0f, -0.012f, 0.17f);

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
            if (UseConsumableServer(inv, e)) return;
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
        Vector3? m_HeldPos;

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
            if (!show && IsOwner && m_HolderKnown && (m_Raise > 0f || TabletOpen))
            {
                m_Raise = 0f;
                TabletOpen = false;
                anchor.transform.SetLocalPositionAndRotation(m_HolderPos, m_HolderRot);
            }
            if (HeldHand != null && HeldHand.activeSelf != (show && IsOwner)) HeldHand.SetActive(show && IsOwner);
            if (!show) return;
            if (shape != m_HeldShape || color != m_HeldColor) ShowHeld(shape, color);
            if (IsOwner) UpdateRaise(anchor);
            // a remote player's head does not pitch: the thing follows their look up and down
            // (with a rigged human the rig then carries it in its right palm, after the animation: HumanRig.PlaceHeld)
            if (!IsOwner)
                anchor.transform.SetLocalPositionAndRotation(m_HeldPos ??= anchor.transform.localPosition,
                    (m_HeldRot ??= anchor.transform.localRotation) * Quaternion.Euler(LookPitch * 0.6f, 0f, 0f));
        }

        void ShowHeld(int shape, int color)
        {
            m_HeldShape = shape;
            m_HeldColor = color;
            if (m_HeldModel != null) Destroy(m_HeldModel);
            m_HeldModel = null;
            m_HasScreen = false;
            var renderer = HeldThing.GetComponent<MeshRenderer>();
            var model = Visuals.Models != null && shape < Visuals.Models.Length ? Visuals.Models[shape] : null;
            Bounds b;
            if (model != null)
            {
                m_HeldModel = Instantiate(model, HeldThing.transform);
                m_HeldModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                HeldThing.sharedMesh = null;
                if (renderer != null) renderer.enabled = false;
                b = ModelBounds(shape);
                var mode = IsOwner ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                foreach (var r in m_HeldModel.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
                foreach (var c in m_HeldModel.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                m_HasScreen = ScreenBox(m_HeldModel.transform, out m_ScreenBox);
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
            HeldLocal(shape, b, out var position, out var rotation, out float scale);
            var t = HeldThing.transform;
            t.localScale = Vector3.one * scale;
            t.SetLocalPositionAndRotation(position, rotation);
            VisualStyles.Dress(HeldThing.gameObject);
        }

        /// <summary>The bounds a held model is fitted by: its catalogue size (a model lies along X, centred, on the floor).</summary>
        public static Bounds ModelBounds(int shape)
        {
            var size = Items.ItemCatalog.ModelSize(shape);
            return new Bounds(new Vector3(0f, size.y / 2f, 0f), size);
        }

        /// <summary>
        /// How a thing of <paramref name="shape"/> (its mesh or model within <paramref name="b"/>) sits in the fist, in the
        /// hand's anchor (+Z ahead, the grip at the origin): a long thing points ahead along +Z with its held end in the fist
        /// (turned about that way), a compact one rests on the closed fingers (<see cref="Items.ItemCatalog.HeldPoseOf"/>).
        /// The first-person hand and another player's rigged hand (HumanRig.PlaceHeld) hold it the same way.
        /// </summary>
        public static void HeldLocal(int shape, Bounds b, out Vector3 position, out Quaternion rotation, out float scale)
        {
            var pose = Items.ItemCatalog.HeldPoseOf(shape);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            scale = longest > 1e-3f ? pose.Length / longest : 1f;
            if (pose.Compact)
            {
                // on the fingers: rolled (stood up), turned, then tilted (−: its far edge up, towards the eyes)
                rotation = Quaternion.Euler(pose.Pitch, 0f, 0f) * Quaternion.Euler(0f, pose.Yaw, 0f) * Quaternion.Euler(0f, 0f, pose.Roll);
                var e = b.extents * scale; // its half height as it now stands, resting on the fist
                float halfUp = Mathf.Abs((rotation * Vector3.right).y) * e.x + Mathf.Abs((rotation * Vector3.up).y) * e.y + Mathf.Abs((rotation * Vector3.forward).y) * e.z;
                position = new Vector3(0f, FistTop + halfUp, 0f) - rotation * (b.center * scale);
            }
            else
            {
                // model X (its length) onto +Z: yaw −90 when +X is ahead, +90 when −X is; rolled about it, tipped up by the pitch
                rotation = Quaternion.Euler(-pose.Pitch, 0f, 0f) * Quaternion.Euler(0f, 0f, pose.Roll)
                           * Quaternion.Euler(0f, pose.Forward > 0f ? -90f : 90f, 0f);
                var grip = new Vector3(b.center.x - pose.Forward * b.size.x * (0.5f - pose.Grip), b.center.y, b.center.z); // model space
                position = -(rotation * (grip * scale));
            }
        }

        /// <summary>The box of a model's screen face (its EyeScreen part, the tablet's) in the model's own space; false without one.</summary>
        static bool ScreenBox(Transform model, out Bounds box)
        {
            box = default;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.gameObject.name.StartsWith("EyeScreen") || f.sharedMesh == null) continue;
                var b = f.sharedMesh.bounds;
                bool first = true;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = model.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                    else box.Encapsulate(p);
                }
                return true;
            }
            return false;
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
            if (EyeGlow != null)
            {
                // what is right before the eyes is never pitch black (a drawer's inside, a corner in one's own shadow): the
                // picture's only — robots see by LightSource lights, and no other player draws this one
                var air = VisualStyles.World;
                bool glow = IsOwner && air.EyeGlow > 0f && !Downed && Watching == null;
                if (EyeGlow.enabled != glow) EyeGlow.enabled = glow;
                if (glow) { EyeGlow.intensity = air.EyeGlow; EyeGlow.range = air.EyeGlowRange; }
            }
            if (Flashlight == null) return;
            bool on = FlashlightOn && !Downed && !InHatch;
            if (Flashlight.enabled != on) Flashlight.enabled = on;
            if (on && !IsOwner) Flashlight.transform.localRotation = Quaternion.Euler(LookPitch, 0f, 0f); // the head of a remote player does not pitch
            if (!on) return;
            if (IsOwner && !m_FlashSharp)
            {
                // one's own beam: its shadows are right before the eyes (4 mm steps of a 512² map showed on every cabinet)
                m_FlashSharp = true;
                if (Flashlight.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(out var data))
                    data.additionalLightsShadowResolutionTier = UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
            }
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
        bool m_FlashSharp;
        float m_FlashBase = -1f;               // its intensity as built
        static readonly RaycastHit[] s_FlashHits = new RaycastHit[8];
    }
}
