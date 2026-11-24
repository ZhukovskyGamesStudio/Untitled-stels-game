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
    /// <summary>The player: the body other players see: the style's rig, the legs, the model's pose (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        /// <summary>How deep the model sits while hiding: only the head looks out of the hatch.</summary>
        public const float HatchDepth = 1.5f;
        float m_ModelSink;

        GameObject m_RigPrefab;
        HumanRig m_Rig;
        /// <summary>The department the model and the rig are dressed as now (<see cref="Department"/> follows the class).</summary>
        Department m_LookDepartment = (Department)(-1);

        /// <summary>
        /// The layer of the owner's own body (TagManager "OwnBody"): the owner's flashlight leaves it out (its culling mask,
        /// set in the prefab), so the beam from his head throws no shadows of his own legs, hair and sleeves before him.
        /// </summary>
        public const int OwnBodyLayer = 11;

        /// <summary>What other players see of this one: the style's rigged human if it has one, otherwise the model.</summary>
        public Transform Visual => m_Rig != null ? m_Rig.transform : Model;

        /// <summary>
        /// The style's rigged human (VisualStyles.PlayerRig) in place of the player model: swapped in and out when the style
        /// changes, painted for the slot, a shadow only for the owner (like the model), posed every frame. Purely visual.
        /// </summary>
        void UpdateRig()
        {
            // a class picked on the start screen dresses the player anew
            if (Department != m_LookDepartment)
            {
                ApplyColor();
                if (m_Rig != null) PlayerLooks.Paint(m_Rig.transform, PlayerLooks.SlotOf(ClientId), m_LookDepartment);
            }
            var prefab = VisualStyles.PlayerRigFor(m_LookDepartment);
            if (prefab != m_RigPrefab)
            {
                if (m_Rig != null) Destroy(m_Rig.gameObject);
                m_Rig = null;
                if (IsOwner) UpdateViewArms(null);
                m_RigPrefab = prefab;
                if (Model != null)
                    foreach (var r in Model.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = prefab != null;
                if (prefab != null && Model != null)
                {
                    var go = Instantiate(prefab, Model.parent);
                    go.transform.SetLocalPositionAndRotation(Model.localPosition, Quaternion.identity);
                    m_Rig = go.GetComponent<HumanRig>();
                    // the department's outfit (the person's own unless the config dresses them otherwise); later swaps repaint
                    m_BootsWorn = -1;
                    if (go.TryGetComponent<HumanWardrobe>(out var wardrobe))
                    {
                        m_Wardrobe = wardrobe;
                        wardrobe.Dress(VisualStyles.OutfitFor(m_LookDepartment), VisualStyles.Wardrobe);
                        wardrobe.Changed += DressRig;
                    }
                    DressRig();
                }
            }
            UpdateBodyPose();
            if (m_Rig == null) return;
            UpdateBootsWorn();
            UpdateOwnLook();
            UpdateViewArm();
            float speed = IsOwner ? Motor.HorizontalSpeed : m_RemoteSpeed;
            // the hand's thing: everyone sees it carried in the rig's right hand (the owner keeps the first-person one)
            var held = HeldThing != null ? HeldThing.transform.parent : null;
            bool holding = held != null && held.gameObject.activeSelf;
            m_Rig.Held = IsOwner ? null : held;
            m_Rig.Cupped = m_HeldShape >= 0 && Items.ItemCatalog.HeldPoseOf(m_HeldShape).Compact;
            // the owner looks out of his own body: it hangs from his eyes, and its right arm gives way to the view's hand
            bool inside = IsOwner && !m_BodyShown;
            m_Rig.Eyes = inside && Camera != null ? Camera.transform : null;
            m_Rig.HideRightArm = inside && ((HeldHand != null && HeldHand.activeSelf) || (ViewFlashlight != null && ViewFlashlight.activeSelf));
            HoldTheWheel();
            m_Rig.Pose(speed, Crouching, Downed, IsOwner ? Motor.Pitch : LookPitch, SeatIndex >= 0, Pose, m_ClimbSpeed, holding && !inside);
        }

        /// <summary>
        /// In the driver's seat: both hands on the steering wheel's rim, at ten to two, turning with the wheel
        /// (<see cref="Vehicles.CarLook.HandTurn"/>); every peer, by the car's own steering (the driver's, or the replicated one).
        /// </summary>
        void HoldTheWheel()
        {
            var car = Vehicles.Car.Active;
            if (SeatIndex != 0 || Downed || car == null || car.Steering == null)
            {
                m_Rig.LetGoOfWheel();
                return;
            }
            float turn = Vehicles.CarLook.HandTurn(car.WheelTurn), at = Vehicles.CarLook.HandsAt;
            var a = car.WheelGrip(-at, turn, out var hub, out var column);
            var b = car.WheelGrip(at, turn, out _, out _);
            var a0 = car.WheelGrip(-at, 0f, out _, out _);
            var b0 = car.WheelGrip(at, 0f, out _, out _);
            // (which point is the left hand's by where it is: the mirrored car has the wheel's own right on the driver's left)
            if (Vector3.Dot(a0 - hub, m_Rig.transform.right) > 0f) { (a, b) = (b, a); (a0, b0) = (b0, a0); }
            m_Rig.HoldWheel(a, b, a0, b0, hub, column);
        }

        HumanWardrobe m_Wardrobe;
        int m_BootsWorn = -1;

        /// <summary>
        /// Boots (plain or jump boots) anywhere on the player are on their feet: the wardrobe's boots made for this person;
        /// without them, the outfit's own shoes again (every peer: backpacks are replicated).
        /// </summary>
        void UpdateBootsWorn()
        {
            if (m_Wardrobe == null) return;
            int want = m_Inventory != null && m_Inventory.Carries(Items.ItemCatalog.Boots) ? 1 : 0;
            if (want == m_BootsWorn) return;
            m_BootsWorn = want;
            const string slot = "feet", boots = "boots";
            if (want == 1)
            {
                if (m_Wardrobe.ItemIn(slot) == boots) return;
                WearPiece piece = null;
                foreach (var p in VisualStyles.Wardrobe)
                    if (p != null && p.Who == m_Wardrobe.Who && p.Item == boots) { piece = p; break; }
                if (piece != null) m_Wardrobe.Wear(piece);
                else m_Wardrobe.WearOwn(slot); // (the person whose own shoes are the boots)
                return;
            }
            var outfit = VisualStyles.OutfitFor(m_LookDepartment);
            if (outfit != null && outfit.Count > 0) m_Wardrobe.Dress(outfit, VisualStyles.Wardrobe);
            else m_Wardrobe.WearOwn(slot);
        }

        /// <summary>
        /// The rig's renderers as they are now (a wardrobe swap brings new ones): coloured for the slot; the owner's own are
        /// on their own layer (his flashlight casts no shadows of them) and, while he looks out of the body, what is on its
        /// head is a shadow only (<see cref="HumanMath.OnTheHead"/>).
        /// </summary>
        void DressRig()
        {
            if (m_Rig == null) return;
            PlayerLooks.Paint(m_Rig.transform, PlayerLooks.SlotOf(ClientId), m_LookDepartment);
            if (!IsOwner) return;
            if (!m_ShownCalling) foreach (var r in m_Rig.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = OwnBodyLayer;
            m_OwnLook = -1;
            UpdateOwnLook();
            UpdateViewArms(m_Rig);
        }

        int m_OwnLook = -1;

        /// <summary>
        /// Owner: how the own body is drawn now. Looking out of it: no head (a shadow only) and the skin without one
        /// (<see cref="HumanRig.OwnSkin"/>); with the flashlight on, no shadow of it either — the beam starts at the head and
        /// threw the owner's own shadow before him (the culling mask does not keep casters out of it in Forward+).
        /// </summary>
        void UpdateOwnLook()
        {
            if (!IsOwner || m_Rig == null) return;
            int look = (m_BodyShown ? 1 : 0) | (FlashlightOn ? 2 : 0);
            if (look == m_OwnLook) return;
            m_OwnLook = look;
            bool shown = m_BodyShown, beam = FlashlightOn && !shown;
            foreach (var r in m_Rig.GetComponentsInChildren<Renderer>(true))
            {
                if (r == m_Rig.OwnSkin) { r.enabled = !shown; continue; }
                bool head = !shown && (HumanMath.OnTheHead(r.name) || (r == m_Rig.Skin && m_Rig.OwnSkin != null));
                r.forceRenderingOff = head && beam;
                r.shadowCastingMode = head ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : beam ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        GameObject m_ArmFist, m_ArmCup, m_ArmTorch;

        /// <summary>
        /// Owner: the right arm of the view is this person's own (<see cref="HumanRig.ViewFist"/>, <see cref="HumanRig.ViewCup"/>)
        /// in place of the plain hand of the prefab: round the held thing at its anchor, and round the flashlight.
        /// </summary>
        void UpdateViewArms(HumanRig rig)
        {
            foreach (var old in new[] { m_ArmFist, m_ArmCup, m_ArmTorch }) if (old != null) Destroy(old);
            m_ArmFist = m_ArmCup = m_ArmTorch = null;
            bool own = rig != null && rig.ViewFist != null && rig.ViewCup != null && HeldThing != null && HeldThing.transform.parent != null;
            // the plain hands step aside
            foreach (var plain in new[] { HeldHand, ViewFlashlight })
                if (plain != null)
                    foreach (var r in plain.GetComponentsInChildren<Renderer>(true))
                        if (plain == HeldHand || r.name.StartsWith("Body") || r.name.StartsWith("AccentSleeve")) r.enabled = !own;
            if (!own) return;
            var anchor = HeldThing.transform.parent;
            m_ArmFist = Arm(rig.ViewFist, anchor);
            m_ArmCup = Arm(rig.ViewCup, anchor);
            if (ViewFlashlight != null)
            {
                m_ArmTorch = Arm(rig.ViewFist, ViewFlashlight.transform.parent);
                m_ArmTorch.transform.SetLocalPositionAndRotation(ViewFlashlight.transform.localPosition, ViewFlashlight.transform.localRotation);
            }
            UpdateViewArm();

            GameObject Arm(GameObject prefab, Transform parent)
            {
                var go = Instantiate(prefab, parent, false);
                PlayerLooks.Paint(go.transform, PlayerLooks.SlotOf(ClientId), m_LookDepartment);
                return go;
            }
        }

        /// <summary>Which of the view's arms shows: under a compact thing the open palm, round anything else the fist.</summary>
        void UpdateViewArm()
        {
            if (m_ArmFist == null) return;
            bool cup = m_HeldShape >= 0 && Items.ItemCatalog.HeldPoseOf(m_HeldShape).Compact;
            if (m_ArmFist.activeSelf == cup) m_ArmFist.SetActive(!cup);
            if (m_ArmCup.activeSelf != cup) m_ArmCup.SetActive(cup);
            bool torch = ViewFlashlight != null && ViewFlashlight.activeSelf;
            if (m_ArmTorch != null && m_ArmTorch.activeSelf != torch) m_ArmTorch.SetActive(torch);
        }

        /// <summary>
        /// What the body is busy with (the owner decides and replicates it; <see cref="HumanRig"/> plays a take for it): on a
        /// ladder, at a terminal or a dial, the hands at work — a hold's progress growing (hacking, a lock, a grille,
        /// reviving a teammate, anything held with E).
        /// </summary>
        public BodyPose Pose { get; private set; }
        float m_ClimbSpeed, m_LastPoseY, m_WorkingUntil, m_RungCarry;
        Vector4 m_HoldWas;

        void UpdateBodyPose()
        {
            float y = transform.position.y, dt = Time.deltaTime;
            // (every peer measures the climb itself: the height is replicated with the position)
            float climb = dt > 0f ? Mathf.Clamp((y - m_LastPoseY) / dt, -4f, 4f) : 0f;
            m_LastPoseY = y;
            m_ClimbSpeed = Pose == BodyPose.Ladder ? Mathf.Lerp(m_ClimbSpeed, climb, 1f - Mathf.Exp(-12f * dt)) : 0f;
            if (!IsOwner) return;
            // a rung of a ladder is heard by the robots (the same rungs the others hear: AudioDirector)
            if (Pose != BodyPose.Ladder) m_RungCarry = 0f;
            else if (Audio.AudioRules.Rungs(ref m_RungCarry, climb * dt) > 0 && IsSpawned && Tuning.Player.Noise.LadderRadius > 0f) EmitNoise(Tuning.Player.Noise.LadderRadius);
            var hold = new Vector4(HackTarget != null ? HackProgress : 0f, DoorTarget != null ? DoorProgress : 0f,
                UseTarget != null ? UseProgress : 0f, ReviveTarget != null ? ReviveProgress : 0f);
            if (HumanMath.Growing(hold.x, m_HoldWas.x) || HumanMath.Growing(hold.y, m_HoldWas.y) || HumanMath.Growing(hold.z, m_HoldWas.z)
                || HumanMath.Growing(hold.w, m_HoldWas.w)) m_WorkingUntil = Time.time + 0.2f;
            m_HoldWas = hold;
            bool atTerminal = UsingTerminal != null || Cracking != null || AtConsole != null;
            Pose = Downed ? BodyPose.None : HumanMath.PoseOf(Climbing, atTerminal, Time.time < m_WorkingUntil);
        }

        bool m_BodyShown;

        /// <summary>
        /// Owner: the view looks out of the own body — what is on its head is a shadow only, the rest is seen under the eyes
        /// (the plain model, where a style has no rigged human, stays a shadow) — but watching a teammate while knocked out,
        /// it lies there to be seen whole, like anyone's.
        /// </summary>
        void ShowOwnBody(bool shown)
        {
            if (shown == m_BodyShown) return;
            m_BodyShown = shown;
            var mode = shown ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in BodyRenderers) if (r != null) r.shadowCastingMode = mode;
            UpdateOwnLook();
        }

        Transform m_LegL, m_LegR;
        Quaternion m_LegRestL, m_LegRestR;
        float m_StridePhase, m_LegSwing;

        /// <summary>
        /// The legs (parts BodyLegL / BodyLegR hanging from the hip) swing in step with the walk: one step per
        /// <see cref="StepLength"/> metres, wider when running, still when standing, crouched lower, none in the hatch or lying.
        /// </summary>
        void SwingLegs(float speed)
        {
            if (m_LegL == null && Model != null)
                foreach (var t in Model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "BodyLegL") { m_LegL = t; m_LegRestL = t.localRotation; }
                    else if (t.name == "BodyLegR") { m_LegR = t; m_LegRestR = t.localRotation; }
                }
            if (m_LegL == null || m_LegR == null) return;
            if (SeatIndex >= 0)
            {
                // sitting: thighs forward
                m_LegL.localRotation = Quaternion.Slerp(m_LegL.localRotation, m_LegRestL * Quaternion.Euler(-80f, 0f, 0f), 1f - Mathf.Exp(-10f * Time.deltaTime));
                m_LegR.localRotation = Quaternion.Slerp(m_LegR.localRotation, m_LegRestR * Quaternion.Euler(-80f, 0f, 0f), 1f - Mathf.Exp(-10f * Time.deltaTime));
                m_LegSwing = 0f;
                return;
            }
            bool still = Downed || InHatch || speed < 0.3f;
            float amplitude = still ? 0f : PlayerRules.LegSwing(speed, Crouching);
            m_LegSwing = Mathf.MoveTowards(m_LegSwing, amplitude, Time.deltaTime * 160f);
            if (!still) m_StridePhase += speed / StepLength * Mathf.PI * Time.deltaTime;
            else if (m_LegSwing <= 0.01f) m_StridePhase = 0f; // the next walk starts from the rest pose
            float angle = Mathf.Sin(m_StridePhase) * m_LegSwing;
            m_LegL.localRotation = m_LegRestL * Quaternion.Euler(angle, 0f, 0f);
            m_LegR.localRotation = m_LegRestR * Quaternion.Euler(-angle, 0f, 0f);
        }

        /// <summary>Metres per step of the walking animation (the footstep sounds use the same length).</summary>
        const float StepLength = 1.25f;

        void UpdateModelPose()
        {
            UpdateRig();
            if (Model == null) return;
            m_ModelSink = Mathf.MoveTowards(m_ModelSink, InHatch ? HatchDepth : 0f, Time.deltaTime * 4f);
            if (IsOwner) Model.localPosition = Vector3.down * m_ModelSink;
            float y = Downed ? 0.35f : (Crouching ? 0.65f : 1f);
            Model.localScale = new Vector3(1f, Mathf.MoveTowards(Model.localScale.y, y, Time.deltaTime * 4f), 1f);
            if (IsOwner) SwingLegs(Motor.HorizontalSpeed);
            if (IsOwner || Time.deltaTime <= 0f) { FollowRig(); return; }

            // Remote players: tilt the head to the synced pitch and bob a little while walking.
            if (HeadPivot != null)
                HeadPivot.localRotation = Quaternion.Slerp(HeadPivot.localRotation,
                    Quaternion.Euler(Mathf.Clamp(LookPitch, -60, 60), 0, 0), 1f - Mathf.Exp(-12f * Time.deltaTime));
            var pos = transform.position;
            var delta = pos - m_LastModelPos;
            m_LastModelPos = pos;
            delta.y = 0;
            float speed = delta.magnitude / Time.deltaTime;
            if (speed > 20f) speed = 0f; // teleport
            m_RemoteSpeed = Mathf.Lerp(m_RemoteSpeed, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
            float walk = Mathf.Clamp01(m_RemoteSpeed / 3.6f);
            // the bob follows the steps: up at mid-stride, down when a foot lands (two per leg cycle)
            Model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(m_StridePhase)) * 0.035f * walk - m_ModelSink, 0);
            Model.localRotation = Quaternion.Euler(Mathf.Clamp(m_RemoteSpeed * 0.6f, 0f, 4f), 0, Mathf.Sin(m_StridePhase) * 1.5f * walk);
            SwingLegs(m_RemoteSpeed);
            FollowRig();
        }

        /// <summary>The style's rigged human sinks into the hatch with the model (it is a sibling of the model, not its child).</summary>
        void FollowRig()
        {
            // in the car every peer puts the model right on its seat (the synced position lags behind the moving car)
            var car = Vehicles.Car.Active;
            int seat = SeatIndex;
            if (seat >= 0 && car.Seats[seat] != null && Model != null)
            {
                Model.SetPositionAndRotation(car.Seats[seat].position - Vector3.up * ModelUnderSeat, car.transform.rotation);
                Model.localScale = Vector3.one;
            }
            if (m_Rig != null && Model != null) m_Rig.transform.localPosition = Model.localPosition;
        }

        void SyncLookPitch()
        {
            m_PitchSyncTimer -= Time.deltaTime;
            if (m_PitchSyncTimer > 0f) return;
            m_PitchSyncTimer = 0.1f;
            var pitch = (sbyte)Mathf.Clamp(Mathf.RoundToInt(Motor.Pitch), -88, 88);
            if (Mathf.Abs(pitch - LookPitch) >= 2) LookPitch = pitch;
        }
    }
}
