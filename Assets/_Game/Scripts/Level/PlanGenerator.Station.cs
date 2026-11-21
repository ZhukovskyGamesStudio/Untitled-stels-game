using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The abandoned research station's plan (<see cref="BuildingSettings.Station"/>), on the hospital's slab: a spine
    /// corridor down its length with rooms on both sides, the entrance checkpoint at the front in the middle and the stair
    /// core behind it. Ground floor: the labs and the control room at the front, the server hall, the stores, the
    /// changing room and the canteen at the back. Upper floors: more labs and offices at the front, the staff quarters
    /// (bedrooms, a lounge, the toilets, a kitchen) at the back; the top floor's front is the computing floor (servers
    /// round the control room).
    /// </summary>
    public static partial class PlanGenerator
    {
        /// <summary>
        /// One floor of the station: the bands of rooms cut into the floor's program, from the building's ends towards
        /// the core (the corridor is claimed already).
        /// </summary>
        static void ZoneStationBands(Ctx c, int f, HospitalLayout h, int[,] owner, System.Action<RectInt, RoomType> room,
            bool storage, bool top, System.Random rng)
        {
            List<RoomType> Front(bool left)
            {
                if (f == 0)
                    return left ? Fill(new[] { RoomType.Lab, RoomType.Lab, RoomType.Office, RoomType.Infirmary }, RoomType.Lab, RoomType.Office)
                                : Fill(new[] { RoomType.ControlRoom, RoomType.Lab, RoomType.Office }, RoomType.Lab, RoomType.Office);
                if (top)
                    return left ? Fill(new[] { RoomType.Server, RoomType.Server, RoomType.Office }, RoomType.Server, RoomType.Office)
                                : Fill(new[] { RoomType.Server, RoomType.ControlRoom, RoomType.Server }, RoomType.Server, RoomType.Office);
                return left ? Fill(new[] { RoomType.Lab, RoomType.Office, RoomType.Lab }, RoomType.Lab, RoomType.Office)
                            : Fill(new[] { RoomType.Lab, RoomType.Library, RoomType.Office }, RoomType.Lab, RoomType.Office);
            }
            List<RoomType> Back(bool left)
            {
                if (f == 0)
                    return left ? Fill(new[] { RoomType.Server, RoomType.Server, RoomType.Restroom }, RoomType.Storage, RoomType.Office)
                                : Fill(storage ? new[] { RoomType.Storage, RoomType.LockerRoom, RoomType.Kitchen } : new[] { RoomType.LockerRoom, RoomType.Kitchen, RoomType.Storage },
                                       RoomType.Storage, RoomType.Office);
                return left ? Fill(new[] { RoomType.Bedroom, RoomType.Bedroom, RoomType.Restroom }, RoomType.Bedroom, RoomType.Bedroom)
                            : Fill(new[] { RoomType.Lounge, RoomType.Kitchen, RoomType.Bedroom }, RoomType.Bedroom, RoomType.Bedroom);
            }

            foreach (var band in new[] { h.FrontBand, h.BackBand })
            {
                bool front = band.yMin == h.FrontBand.yMin;
                foreach (var run in BandRuns(band, owner))
                {
                    bool left = run.center.x < h.Length / 2f;
                    // the named rooms at the building's ends, the fillers towards the core
                    CutStrip(run, front ? Front(left) : Back(left), rng, room, StationWidthOf, fromEnd: !left, minRest: 4 * M);
                }
            }

            static List<RoomType> Fill(RoomType[] first, RoomType a, RoomType b)
            {
                var list = new List<RoomType>(first);
                for (int k = 0; k < 18; k++) list.Add(k % 2 == 0 ? a : b);
                return list;
            }
        }

        /// <summary>How wide a station room is along its band, metres.</summary>
        static int StationWidthOf(RoomType t, System.Random rng) => t switch
        {
            RoomType.Lab => 6 + rng.Next(2),
            RoomType.Server => 5 + rng.Next(2),
            RoomType.ControlRoom => 6,
            RoomType.Kitchen => 6, // the canteen: the kitchen run along one wall, tables in front of it
            RoomType.Lounge => 6,
            RoomType.Library => 6,
            RoomType.LockerRoom => 5,
            RoomType.Storage => 5,
            RoomType.Bedroom => 4,
            RoomType.Restroom => 4,
            RoomType.Infirmary => 4,
            _ => 4 + rng.Next(2),
        };
    }
}
