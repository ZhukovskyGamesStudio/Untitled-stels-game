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

        /// <summary>What other players see of this one: the style's rigged human if it has one, otherwise the model.</summary>
        public Transform Visual => m_Rig != null ? m_Rig.transform : Model;

        /// <summary>
        /// The style's rigged human (VisualStyles.PlayerRig) in place of the player model: swapped in and out when the style
        /// changes, painted for the slot, a shadow only for the owner (like the model), posed every frame. Purely visual.
        /// </summary>
        void UpdateRig()
        {
            var prefab = VisualStyles.PlayerRig;
            if (prefab != m_RigPrefab)
            {
                if (m_Rig != null) Destroy(m_Rig.gameObject);
                m_Rig = null;
                m_RigPrefab = prefab;
                if (Model != null)
                    foreach (var r in Model.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = prefab != null;
                if (prefab != null && Model != null)
                {
                    var go = Instantiate(prefab, Model.parent);
                    go.transform.SetLocalPositionAndRotation(Model.localPosition, Quaternion.identity);
                    m_Rig = go.GetComponent<HumanRig>();
                    PlayerLooks.Paint(go.transform, PlayerLooks.SlotOf(ClientId));
                    if (IsOwner)
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
            if (m_Rig == null) return;
            float speed = IsOwner ? Motor.HorizontalSpeed : m_RemoteSpeed;
            m_Rig.Pose(speed, Crouching, Downed, IsOwner ? Motor.Pitch : LookPitch);
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
