using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The lift's doors on one landing (prefab Level/LiftDoor, in the doorway <see cref="EdgeType.Lift"/>; +Z out onto the
    /// landing): two steel panels that slide apart only while the cab stands at this floor (<see cref="LiftRules.DoorOpen"/>)
    /// and stay open there, the call button beside them (<see cref="Call"/>), a lamp over them lit while the cab is here and
    /// arrows while it rides. Shut, the panels block the way, sight and sound into the empty shaft.
    /// </summary>
    public class LiftDoor : MonoBehaviour
    {
        public static readonly List<LiftDoor> All = new();

        public Transform PanelA, PanelB;
        public BoxCollider Blocker;
        [Tooltip("The call button on the landing beside the doors.")]
        public LiftButton Call;
        [Tooltip("Lit while the cab stands at this floor.")]
        public Renderer HereLamp;
        [Tooltip("The arrows over the doors: lit while the cab rides up / down.")]
        public Renderer UpLamp, DownLamp;
        [Tooltip("The clear opening, metres (the panels part to its sides).")]
        public float Width = LevelMetrics.ClearWidth(LevelMetrics.LiftDoorWidth);

        public int Floor { get; private set; } = -1;

        static float FloorTime => Configs.Tuning.Level.Building.LiftFloorTime;
        static float DoorTime => Configs.Tuning.Level.Building.LiftDoorTime;

        float m_ShownOpen = -1f;
        int m_ShownLamps = -1;

        public void Configure(int floor)
        {
            Floor = floor;
            if (Call != null) Call.Configure(floor);
        }

        /// <summary>A point in the doorway, between the landing and the cab (the doors must not shut on it).</summary>
        public bool InDoorway(Vector3 p)
        {
            var local = transform.InverseTransformPoint(p);
            return Mathf.Abs(local.y) < 1.2f && Mathf.Abs(local.x) < Width / 2f + 0.15f && local.z > -0.7f && local.z < 0.45f;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (Floor < 0) return;
            var s = LiftCab.State();
            double now = LiftCab.Now();
            float open = LiftRules.DoorOpen(s, Floor, now, FloorTime, DoorTime);
            if (!Mathf.Approximately(open, m_ShownOpen))
            {
                if (m_ShownOpen >= 0f && (m_ShownOpen <= 0f || m_ShownOpen >= 1f) && open > 0f && open < 1f)
                    Audio.Sfx.At(Audio.Sfx.Bank?.DoorSlide, transform.position + Vector3.up * 1.2f, Configs.Tuning.Audio.Mix.Doors, Audio.Sfx.Group.Items, 0.85f);
                m_ShownOpen = open;
                float slide = Width / 2f * Mathf.SmoothStep(0f, 1f, open);
                if (PanelA != null) PanelA.localPosition = new Vector3(-Width / 4f - slide, PanelA.localPosition.y, PanelA.localPosition.z);
                if (PanelB != null) PanelB.localPosition = new Vector3(Width / 4f + slide, PanelB.localPosition.y, PanelB.localPosition.z);
                if (Blocker != null) Blocker.enabled = open < 0.95f;
            }
            var phase = LiftRules.Phase(s, now, FloorTime, DoorTime);
            bool here = LiftRules.StandsAt(s, now, FloorTime, DoorTime) == Floor;
            int lamps = (here ? 1 : 0) | (phase == LiftPhase.Riding && s.To > s.From ? 2 : 0) | (phase == LiftPhase.Riding && s.To < s.From ? 4 : 0);
            if (lamps == m_ShownLamps) return;
            m_ShownLamps = lamps;
            if (HereLamp != null) HereLamp.enabled = (lamps & 1) != 0;
            if (UpLamp != null) UpLamp.enabled = (lamps & 2) != 0;
            if (DownLamp != null) DownLamp.enabled = (lamps & 4) != 0;
        }
    }
}
