using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Floor pole spinning a laser line at knee height. Crossing it raises an alarm — jump over it.</summary>
    public class LidarScanner : SecurityDevice
    {
        public LineRenderer Line;
        public Transform Head;
        public float BeamHeight = 0.38f;
        public float Range = 6f;
        public float Speed = 110f; // degrees per second

        Vector3 Dir => ScanLine.Direction(Stealth.Game.Session.ServerTimeF, Speed, NetId * 53f);

        protected override void TickLocal()
        {
            var d = Dir;
            if (Head != null) Head.localRotation = Quaternion.LookRotation(-d); // emitter on the model's -Y side → Unity +Z
            if (Line == null) return;
            var start = transform.position + Vector3.up * BeamHeight;
            // The beam stops at the first wall.
            float len = Physics.Raycast(start, d, out var hit, Range, Perception.SightMask, QueryTriggerInteraction.Ignore) ? hit.distance : Range;
            Line.enabled = true;
            Line.SetPosition(0, start);
            Line.SetPosition(1, start + d * len);
        }

        protected override void TickServer()
        {
            var d = Dir;
            var start = transform.position + Vector3.up * BeamHeight;
            float len = Physics.Raycast(start, d, out var hit, Range, Perception.SightMask, QueryTriggerInteraction.Ignore) ? hit.distance : Range;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown || !Perception.SameFloor(p.Position, transform.position)) continue;
                if (ScanLine.Above(p.Position.y, transform.position.y + BeamHeight)) continue; // jumped over
                if (ScanLine.Crosses(transform.position, d, len, false, p.Position)) { Trip(p); return; }
            }
            Decay(0.6f);
        }
    }
}
