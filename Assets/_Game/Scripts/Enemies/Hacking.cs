using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// IT people can hack the machines — for a while. Rules (pure, unit tested):
    /// devices are hacked up close; mobile robots only from behind and only while they are not chasing you.
    /// </summary>
    public static class Hacking
    {
        public const float Reach = 1.9f;
        public const float HoldTime = 2.5f;
        public const float Duration = 20f;

        public static bool CanHackRobot(Vector3 robotPos, Vector3 robotForward, Vector3 player, bool chasing)
        {
            if (chasing) return false;
            var to = player - robotPos;
            to.y = 0;
            if (to.magnitude > Reach) return false;
            var f = new Vector3(robotForward.x, 0, robotForward.z);
            return Vector3.Angle(f, to) > 110f; // standing behind it
        }

        public static bool CanHackDevice(Vector3 devicePos, Vector3 player)
        {
            var d = player - devicePos;
            float vertical = Mathf.Abs(d.y);
            d.y = 0;
            return d.magnitude <= Reach && vertical < 3.2f; // wall / ceiling devices are reachable from below
        }
    }
}
