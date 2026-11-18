using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The furnisher: the free corners (see <see cref="Furnisher"/>).</summary>
    public sealed partial class Furnisher
    {
        // ------------------------------------------------------------------ corners

        /// <summary>
        /// A piece the room must have and the walls had no run for (a kitchen's fridge): against any bit of wall, its back to
        /// it, wherever it fits.
        /// </summary>
        void AgainstAnyWall(PlanRoom room, PropKind kind, bool walkway = false)
        {
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            for (float x = b.xMin + 0.25f; x < b.xMax; x += 0.25f)
                for (float z = b.yMin + 0.25f; z < b.yMax; z += 0.25f)
                {
                    if (!InRoom(room.Floor, room.Id, x, z)) continue;
                    foreach (var d in dirs)
                    {
                        // the wall right behind it (a model's back sits on BackOf from the wall's face)
                        var behind = new Vector3(x, 0f, z) + d * (BackOf(kind) + 0.15f);
                        if (InRoom(room.Floor, room.Id, behind.x, behind.z)) continue;
                        var batch = new Batch();
                        batch.Add(kind, new Vector3(x, y, z), Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        if (TryCommit(room, batch, 0.05f, front: walkway ? -d : null)) return;
                    }
                }
        }

        /// <summary>Plants, lamps, bins, stands and the room's own odds and ends in the free inner corners of a room.</summary>
        void Corners(PlanRoom room)
        {
            if (room.Type == RoomType.Kitchen && !m_Fridge) AgainstAnyWall(room, PropKind.Fridge);
            if (room.Type is RoomType.Corridor or RoomType.Server) return;
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            int placed = 0, max = Mathf.Clamp(Mathf.RoundToInt(room.Area / 16f), 1, 7);
            for (float x = b.xMin + 0.4f; x < b.xMax && placed < max; x += 0.5f)
                for (float z = b.yMin + 0.4f; z < b.yMax && placed < max; z += 0.5f)
                {
                    if (!InRoom(room.Floor, room.Id, x, z)) continue;
                    // a corner: room cells on two sides, a wall within 0.6 m on two perpendicular sides
                    bool wx = !InRoom(room.Floor, room.Id, x - 0.6f, z) || !InRoom(room.Floor, room.Id, x + 0.6f, z);
                    bool wz = !InRoom(room.Floor, room.Id, x, z - 0.6f) || !InRoom(room.Floor, room.Id, x, z + 0.6f);
                    if (!wx || !wz || !Chance(0.45)) continue;
                    PropKind Pick(params PropKind[] kinds) => kinds[m_Rng.Next(kinds.Length)];
                    var kind = room.Type switch
                    {
                        RoomType.Lounge => Pick(PropKind.FloorLamp, PropKind.Plant, PropKind.PlanterLarge),
                        RoomType.Lobby => Pick(PropKind.FloorLamp, PropKind.PlanterLarge, PropKind.UmbrellaStand),
                        RoomType.Hall => Pick(PropKind.PlanterLarge, PropKind.UmbrellaStand, PropKind.Plant),
                        RoomType.Office => Pick(PropKind.Plant, PropKind.CoatRack, PropKind.FilingCabinet),
                        RoomType.OpenSpace => Pick(PropKind.Plant, PropKind.TrashBin, PropKind.FilePedestal),
                        RoomType.Meeting => Pick(PropKind.Plant, PropKind.Flipchart),
                        RoomType.Kitchen => Pick(PropKind.TrashBin, PropKind.Plant),
                        RoomType.Restroom => Pick(PropKind.SanitaryBin, PropKind.TrashBin),
                        RoomType.Storage => Pick(PropKind.Boxes, PropKind.HandTruck, PropKind.DrumPair),
                        // the themed rooms' own odds and ends
                        RoomType.Gym => Pick(PropKind.PunchingBag, PropKind.WaterCooler),
                        RoomType.Library => Pick(PropKind.Globe, PropKind.FloorLamp, PropKind.Lectern),
                        RoomType.CopyRoom => Pick(PropKind.Shredder, PropKind.PaperStack, PropKind.PaperBin),
                        RoomType.ControlRoom => Pick(PropKind.Plant, PropKind.WaterCooler),
                        RoomType.Lab => Pick(PropKind.RobotArm, PropKind.TrashBin, PropKind.ScopeCart),
                        RoomType.NapRoom => Pick(PropKind.BeanBag, PropKind.FloorLamp, PropKind.NightTable),
                        RoomType.Studio => Pick(PropKind.MicStand, PropKind.FloorLamp, PropKind.GuitarStand, PropKind.AmpCabinet),
                        RoomType.Infirmary => Pick(PropKind.PrivacyScreen, PropKind.Plant, PropKind.IvStand, PropKind.Wheelchair),
                        RoomType.GameRoom => Pick(PropKind.BarStool, PropKind.BeanBag),
                        RoomType.LockerRoom => Pick(PropKind.TrashBin, PropKind.LaundryCart, PropKind.WeighingScale),
                        _ => Chance(0.7) ? PropKind.Plant : PropKind.TrashBin,
                    };
                    var p = new Vector3(x, y, z);
                    var batch = new Batch();
                    batch.Add(kind, p, m_Rng.Next(4) * 90f);
                    if (TryCommit(room, batch, 0.05f)) placed++;
                }
        }
    }
}
