using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The hospital's plan (<see cref="BuildingSettings.Hospital"/>), laid out like a real inpatient building: a slab with a
    /// wide corridor down its length and rooms on both sides (the double-loaded nursing unit), the stair core behind the
    /// entrance. Ground floor: the admissions — the lobby with the waiting area at the front, examination and treatment
    /// rooms, X-ray and CT rooms, the lab, a store, the staff's toilets and locker room. Upper floors: patient rooms on both
    /// sides of the corridor with the nurse station open to it in the middle, the staff room and the toilets by the stairs;
    /// the top floor ends in the operating block (two theatres).
    /// </summary>
    public static partial class PlanGenerator
    {
        sealed class HospitalLayout
        {
            public int Length;               // cells
            public RectInt Corridor;         // cells, the same on every floor
            public RectInt FrontBand, BackBand;
        }

        /// <summary>Depths across the slab, metres: the front rooms, the corridor (beds pass each other), the back rooms.</summary>
        const int HospFront = 7, HospCorridor = 3, HospBack = 6;
        /// <summary>A patient room's width along the corridor, metres (two beds side by side, the door beside them).</summary>
        public const int WardWidth = 4;

        static List<RectInt> HospitalMass(Ctx c, System.Random rng, float area, out string kind)
        {
            int depth = HospFront + HospCorridor + HospBack;
            int length = Mathf.Clamp(Mathf.RoundToInt(area / depth / WardWidth) * WardWidth + WardWidth * rng.Next(2), 32, 72);
            c.Hospital = new HospitalLayout
            {
                Length = length * M,
                Corridor = new RectInt(0, HospFront * M, length * M, HospCorridor * M),
                FrontBand = new RectInt(0, 0, length * M, HospFront * M),
                BackBand = new RectInt(0, (HospFront + HospCorridor) * M, length * M, HospBack * M),
            };
            kind = "hospital";
            return new List<RectInt> { new(0, 0, length, depth) };
        }

        /// <summary>The entrance hall at the front in the middle; the stair core right behind it across the corridor.</summary>
        static void ReserveHospital(Ctx c)
        {
            var h = c.Hospital;
            int mid = h.Length / 2 / M * M;
            c.Lobby = new RectInt(mid - 6 * M, 0, 12 * M, HospFront * M);
            int side = 6 * M;
            for (int f = 0; f < c.Floors - 1; f++)
            {
                // the halls of neighbouring floors side by side (a core), each stair in its back corner on the outside
                bool right = f % 2 == 1;
                var hall = new RectInt(right ? mid : mid - side, h.BackBand.yMin, side, h.BackBand.height);
                c.Halls.Add((f, hall, new Vector2Int(right ? 1 : -1, 1)));
            }
        }

        /// <summary>
        /// One floor of the hospital: the corridor first, then the two bands of rooms cut into the floor's program from the
        /// ends inwards, each band side by side of the lobby and the stair core.
        /// </summary>
        static void ZoneHospital(Ctx c, int f, List<Region> regions, int[,] owner, System.Func<RectInt, Region, int> claim)
        {
            var h = c.Hospital;
            int nx = c.Plan.NX, nz = c.Plan.NZ;
            var rng = c.Rng(0x6057, f);
            void Room(RectInt r, RoomType t) => claim(r, new Region { Type = t, Shape = RoomShape.Rect, Typed = true });
            claim(h.Corridor, new Region { Type = RoomType.Corridor, Shape = RoomShape.Corridor, Fixed = true });

            bool storage = false;
            if (f == 0 && c.Program != null) foreach (var p in c.Program.Floors[0]) storage |= p.Type == RoomType.Storage && p.Required;
            bool top = f == c.Floors - 1 && f > 0;
            // what each band holds, end to end (left of the core, then right of it); the rest of a band is patient rooms upstairs
            List<RoomType> Front(bool left, RectInt run)
            {
                if (f == 0)
                    return left ? new List<RoomType> { RoomType.Infirmary, RoomType.Infirmary, RoomType.Imaging, RoomType.Infirmary, RoomType.Office }
                                : new List<RoomType> { RoomType.Lab, RoomType.Infirmary, RoomType.Office, RoomType.Infirmary, RoomType.Infirmary };
                // upstairs: patient rooms, the nurse station in the middle of the floor (across from the stairs)
                int before = Mathf.Max(0, (run.width / M / 2 - 3) / WardWidth);
                var list = new List<RoomType>();
                for (int k = 0; k < before; k++) list.Add(RoomType.Ward);
                list.Add(RoomType.NurseStation);
                for (int k = 0; k < 18; k++) list.Add(RoomType.Ward);
                return list;
            }
            List<RoomType> Back(bool left) => f == 0
                ? (left ? new List<RoomType> { RoomType.Imaging, RoomType.Imaging, RoomType.Restroom, RoomType.Office }
                        : storage ? new List<RoomType> { RoomType.Storage, RoomType.LockerRoom, RoomType.Kitchen, RoomType.Office }
                                  : new List<RoomType> { RoomType.LockerRoom, RoomType.Kitchen, RoomType.Lab, RoomType.Office })
                : top && !left ? new List<RoomType> { RoomType.Operating, RoomType.Operating, RoomType.Infirmary }
                : left ? Wards(new List<RoomType> { RoomType.Restroom, RoomType.Kitchen }) : Wards(new List<RoomType> { RoomType.Restroom });

            foreach (var band in new[] { h.FrontBand, h.BackBand })
            {
                bool front = band.yMin == h.FrontBand.yMin;
                // the band's free runs along x (the lobby and the stair halls split it)
                var runs = new List<RectInt>();
                int start = -1;
                for (int x = 0; x <= band.xMax; x++)
                {
                    bool free = x < band.xMax;
                    if (free)
                        for (int z = band.yMin; z < band.yMax && free; z++) free = owner[x, z] == -1;
                    if (free && start < 0) start = x;
                    if (!free && start >= 0) { runs.Add(new RectInt(start, band.yMin, x - start, band.height)); start = -1; }
                }
                for (int i = 0; i < runs.Count; i++)
                {
                    bool left = runs[i].center.x < h.Length / 2f;
                    var types = front ? Front(left, runs[i]) : Back(left);
                    // cut from the far end of the building towards the core (the special rooms at the ends)
                    // which end the list starts from: the ground floor's special rooms and the theatres at the building's ends,
                    // upstairs the toilets and the staff room by the stairs, the front band from one end (the station falls mid-way)
                    bool fromHighEnd = f == 0 ? !left : front ? false : top && !left ? true : left;
                    CutStrip(runs[i], types, rng, (r, t) => Room(r, t), WidthOf, fromEnd: fromHighEnd, minRest: WardWidth * M);
                }
            }

            Cleanup(regions, owner, nx, nz);
            for (int settle = 0; settle < 4; settle++)
            {
                Slim(regions, owner, nx, nz);
                SplitDisconnected(regions, owner, nx, nz);
                Cleanup(regions, owner, nx, nz);
            }
            AssignTypes(c, f, regions);
            Commit(c, f, regions, owner);

            static List<RoomType> Wards(List<RoomType> first, RoomType? middle = null)
            {
                var list = new List<RoomType>(first);
                for (int k = 0; k < 18; k++) list.Add(k == 2 && middle is RoomType m ? m : RoomType.Ward);
                return list;
            }
        }

        /// <summary>How wide a hospital room is along its band, metres.</summary>
        static int WidthOf(RoomType t, System.Random rng) => t switch
        {
            RoomType.Ward => WardWidth,
            RoomType.NurseStation => 5,
            RoomType.Operating => 7,
            RoomType.Imaging => 6 + rng.Next(2),
            RoomType.Infirmary => 4,
            RoomType.Restroom => 4,
            _ => 4 + rng.Next(2),
        };
    }
}
