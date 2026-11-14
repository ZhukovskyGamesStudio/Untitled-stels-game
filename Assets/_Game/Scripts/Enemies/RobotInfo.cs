using Stealth.Level;

namespace Stealth.Enemies
{
    /// <summary>
    /// What players learn by looking at a robot: its name and how it senses (shown under the crosshair). The texts are the
    /// table's <c>robot.&lt;kind&gt;.name</c> / <c>.senses</c> (<see cref="Key"/>).
    /// </summary>
    public static class RobotInfo
    {
        /// <summary>The table key prefix of a kind: "robot.guard"; <c>robot.unknown</c> for a kind without texts.</summary>
        public static string Key(RobotKind k) => "robot." + k.ToString().ToLowerInvariant();

        /// <summary>The table key of the kind's name (for texts packed with an "@" argument).</summary>
        public static string NameKey(RobotKind k) => Loc.Has(Key(k) + ".name") ? Key(k) + ".name" : "robot.unknown.name";

        public static string Name(RobotKind k) => Loc.T(NameKey(k));

        public static string Senses(RobotKind k) => Loc.Has(Key(k) + ".senses") ? Loc.T(Key(k) + ".senses") : "";
    }
}
