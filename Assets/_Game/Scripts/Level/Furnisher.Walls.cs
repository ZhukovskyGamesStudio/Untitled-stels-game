using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The furnisher: the groups along the walls (see <see cref="Furnisher"/>).</summary>
    public sealed partial class Furnisher
    {
        // ------------------------------------------------------------------ walls

        void Walls(PlanRoom room)
        {
            var list = FacesOf(room); // longest walls first: the big pieces get the long walls
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in list)
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                float t = R(0.15f, 0.9f);
                // a corridor is a walkway (1.7 m stays free in front of its things), a room keeps an aisle
                float walk = room.Type == RoomType.Corridor ? 1.4f : Aisle + 0.1f;
                while (t < w.Length - 0.3f)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    // what does not fit here (too deep for the room, tall under a window, one too many) gives way to
                    // another group of the room's set before the spot is given up
                    float placed = 0f;
                    for (int attempt = 0; attempt < 3 && placed <= 0f; attempt++)
                    {
                        var batch = new Batch();
                        float width = WallGroup(room, batch, face, u, n);
                        if (width <= 0f || t + width > w.Length - 0.1f) continue;
                        // the group spans [t, t + width] along the wall: shift its pieces to the group's middle
                        var shift = u * (width / 2f);
                        for (int i = 0; i < batch.Pieces.Count; i++)
                        {
                            var p = batch.Pieces[i];
                            p.Pos += shift;
                            batch.Pieces[i] = p;
                        }
                        for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                        for (int i = 0; i < batch.Drops.Count; i++) { var d = batch.Drops[i]; d.Spot += shift; batch.Drops[i] = d; }
                        if (Deep(room, face + shift, n, batch, walk) && TryCommit(room, batch, front: n)) placed = width;
                    }
                    if (placed > 0f) t += placed + (room.Type == RoomType.Corridor ? R(4f, 10f) : R(0.15f, 1.2f));
                    else t += 0.5f;
                }
            }
        }

        /// <summary>
        /// A walkway stays in front of a group along a wall: <paramref name="walk"/> of the room's floor, and no other
        /// furniture within <see cref="Facing"/> more — the walkability check wants more room between two pieces than
        /// between a piece and a bare wall (<see cref="Walk"/>: the navmesh erodes more around furniture).
        /// </summary>
        bool Deep(PlanRoom room, Vector3 face, Vector3 n, Batch b, float walk = Aisle + 0.1f)
        {
            // the whole footprint, pushed walk further into the room, must still be the room's floor
            var u = Vector3.Cross(Vector3.up, n);
            foreach (var p in b.Pieces)
            {
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                foreach (var c in new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax) })
                    for (float d = 0f; d <= walk + Facing + 0.01f; d += 0.25f)
                    {
                        var q = c + n * d;
                        if (d <= walk + 0.01f && !InRoom(room.Floor, room.Id, q.x, q.z)) return false;
                        if (d > 0.2f && m_Taken[room.Floor].Contains(new Vector2(q.x, q.z))) return false;
                    }
            }
            return true;
        }

        /// <summary>One group along a wall, laid out from the wall's face (u along it from 0 to the returned width, n into the room).</summary>
        float WallGroup(PlanRoom room, Batch b, Vector3 face, Vector3 u, Vector3 n)
        {
            var f = new Frame(face, u, n);
            float Single(PropKind k, float w)
            {
                b.Add(k, f.P(0, BackOf(k)), f.Face);
                return w;
            }
            // a thing on the wall (a clock, a board, a cabinet) — nothing on the floor to walk into
            float Hung(PropKind k, float w)
            {
                b.Add(k, f.P(0, BackOf(k)), f.Face, solid: false);
                return w;
            }
            // lying along the wall (its side to it): beds, a rowing machine
            float Along(PropKind k, float du = 0f)
            {
                var r = FootprintOf(k);
                b.Add(k, f.P(du, r.width / 2f + WallGap), f.Face + 90f);
                return r.height + 0.05f;
            }
            float Clock(float w)
            {
                if (Chance(0.3)) b.Add(PropKind.WallClock, f.P(0, BackOf(PropKind.WallClock)), f.Face, solid: false); // over something low
                return w;
            }
            int roll = m_Rng.Next(100);
            switch (room.Type)
            {
                case RoomType.Bedroom:
                    if (roll < 40) { Single(PropKind.Wardrobe, 1.25f); return 1.25f; }
                    if (roll < 65) { Single(PropKind.Dresser, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.Dresser), 0.86f)); return 1.05f; }
                    if (roll < 78) return Single(PropKind.Armchair, 0.9f);
                    if (roll < 90) return Single(PropKind.FloorLamp, 0.5f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Living:
                    if (roll < 28) { Single(PropKind.TvStand, 1.45f); return 1.45f; }
                    if (roll < 50) { Single(PropKind.Bookshelf, 1.25f); return 1.25f; }
                    if (roll < 64) return Single(PropKind.Armchair, 0.9f);
                    if (roll < 76) { Single(PropKind.Dresser, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.Dresser), 0.86f)); return 1.05f; }
                    if (roll < 88) return Single(PropKind.FloorLamp, 0.5f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.FlatHall:
                    if (roll < 45) return Single(PropKind.Wardrobe, 1.25f);
                    if (roll < 70) return Single(PropKind.UmbrellaStand, 0.35f);
                    return Single(PropKind.CoatRack, 0.9f);
                case RoomType.Bathroom:
                    if (roll < 60) return Single(PropKind.WashingMachine, 0.65f);
                    return Single(PropKind.SanitaryBin, 0.35f);
                case RoomType.Corridor when m_L.PlanKind == HouseKind.Apartments:
                    // the landing: a notice board, the fire hose cabinet (the lift is a real one now: PlanGenerator.LiftDoorways)
                    if (roll < 50) return Hung(PropKind.NoticeBoard, 1.25f);
                    if (roll < 80) return Hung(PropKind.FireCabinet, 0.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Lobby when m_L.PlanKind == HouseKind.Apartments:
                    // the entrance hall: the mailboxes, the notice board, a bench and a plant
                    if (roll < 45) return Hung(PropKind.Mailboxes, 1.25f);
                    if (roll < 65) return Hung(PropKind.NoticeBoard, 1.25f);
                    if (roll < 85) return Single(PropKind.CorridorBench, 1.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Corridor when m_L.PlanKind == HouseKind.Hospital:
                    // a ward corridor: trolleys and chairs along the walls, far apart (the beds pass in the middle)
                    if (roll < 22) return Along(PropKind.Gurney);
                    if (roll < 40) return Single(PropKind.WaitingSeats, 2.5f);
                    if (roll < 55) return Single(PropKind.Wheelchair, 0.75f);
                    if (roll < 68) return Single(PropKind.LinenCart, 0.75f);
                    if (roll < 80) return Single(PropKind.MedicationCart, 1.05f);
                    if (roll < 90) return Hung(PropKind.HandSanitizer, 0.3f);
                    return Hung(PropKind.FireCabinet, 0.45f);
                case RoomType.ChargingStation:
                    if (roll < 28) return Single(PropKind.BatteryStack, 0.95f);
                    if (roll < 46) return Single(PropKind.ScrapShrine, 1.5f);
                    if (roll < 62) return Hung(PropKind.ConduitGrowth, 1.3f);
                    if (roll < 78) { b.Add(PropKind.CableMass, f.P(0, 0.65f), f.Face, solid: false); return 2.1f; }
                    if (roll < 88) return Single(PropKind.ChargingDock, 1.2f); // a spare dock
                    return Row(f, b, PropKind.BatteryStack, 2, 0.95f);
                case RoomType.Corridor:
                    // corridors are walkways: only slim things, far apart (Walls keeps 1.7 m free in front of them)
                    if (roll < 28) return Hung(PropKind.FireCabinet, 0.45f);
                    if (roll < 52) return Hung(PropKind.NoticeBoard, 1.25f);
                    if (roll < 70) return Single(PropKind.BinPair, 0.75f);
                    if (roll < 84) return Single(PropKind.UmbrellaStand, 0.35f);
                    return Single(PropKind.CorridorBench, 1.45f);
                case RoomType.Hall:
                    if (roll < 26) return Single(PropKind.Plant, 0.75f); // stair halls stay clear (no vending machines: they look like ATMs)
                    if (roll < 34) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 40) return Single(PropKind.CoatRack, 0.9f);
                    if (roll < 46) return Single(PropKind.Armchair, 0.9f);
                    if (roll < 58) { Single(PropKind.DisplayCase, 1.05f); return Clock(1.05f); }
                    if (roll < 72) { Single(PropKind.HallBench, 1.85f); if (Chance(0.4)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.HallBench), 0.455f)); return 1.85f; }
                    if (roll < 82) return Single(PropKind.DirectoryBoard, 0.85f);
                    if (roll < 92) return Single(PropKind.PlanterLarge, 1.05f);
                    { b.Add(PropKind.Sculpture, f.P(0, 0.45f), f.Face); return 0.7f; }
                case RoomType.Lobby when m_L.PlanKind == HouseKind.Hospital:
                    // the admissions' waiting area: rows of seats, water, a wheelchair or two by the door
                    if (roll < 40) return Single(PropKind.WaitingSeats, 2.5f);
                    if (roll < 52) return Single(PropKind.Wheelchair, 0.75f);
                    if (roll < 62) return Single(PropKind.WaterCooler, 0.5f);
                    if (roll < 72) return Single(PropKind.VendingMachine, 1.0f);
                    if (roll < 82) return Hung(PropKind.HandSanitizer, 0.3f);
                    if (roll < 92) return Single(PropKind.Plant, 0.75f);
                    return Single(PropKind.MagazineRack, 0.6f);
                case RoomType.Lobby when m_L.PlanKind == HouseKind.Garage:
                    // the waiting area: chairs along the wall, the tyre display, coffee and water, a rack of brochures
                    if (roll < 30) return Row(f, b, PropKind.GuestChair, 3, 0.65f);
                    if (roll < 48) return Single(PropKind.TireRack, 2.1f);
                    if (roll < 58) return Single(PropKind.VendingMachine, 1.0f);
                    if (roll < 66) return Single(PropKind.WaterCooler, 0.5f);
                    if (roll < 76) { b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face); b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face); b.Item(f.P(0, 1.35f, 0.42f)); return 2.25f; }
                    if (roll < 84) return Single(PropKind.MagazineRack, 0.6f);
                    if (roll < 92) return Single(PropKind.Plant, 0.75f);
                    return Single(PropKind.TireStack, 0.85f);
                case RoomType.Lobby:
                    if (roll < 18) { b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face); b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face); b.Item(f.P(0, 1.35f, 0.42f)); return 2.25f; }
                    if (roll < 30) { b.Add(PropKind.Lockers, f.P(-0.525f, BackOf(PropKind.Lockers)), f.Face); b.Add(PropKind.Lockers, f.P(0.525f, BackOf(PropKind.Lockers)), f.Face); return 2.1f; }
                    if (roll < 40) return Single(PropKind.Plant, 0.75f);                  // no vending machines
                    if (roll < 48) return Single(PropKind.Lockers, 1.05f);                 // lockers to open instead of a plant
                    if (roll < 54) return Single(PropKind.CoatRack, 0.9f);
                    if (roll < 60) return Single(PropKind.FloorLamp, 0.45f);
                    if (roll < 70) return Single(PropKind.LogoTotem, 0.85f);
                    if (roll < 80) return Row(f, b, PropKind.InfoKiosk, 2, 0.7f);
                    if (roll < 90) return Single(PropKind.PlanterLarge, 1.05f);
                    return Single(PropKind.DirectoryBoard, 0.85f);
                case RoomType.Office:
                    if (roll < 20)
                    {   // a work desk against the wall
                        b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(R(-0.1f, 0.1f), 1.2f), f.Back + R(-15f, 15f));
                        if (Chance(0.6)) b.Add(PropKind.DeskLamp, f.P(0.55f, 0.3f, DeskTop), f.Face, solid: false);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f));
                        if (!Chance(0.5)) return 1.65f;
                        b.Add(PropKind.FilePedestal, f.P(1.05f, BackOf(PropKind.FilePedestal)), f.Face); // its drawers beside it
                        return 2.55f;
                    }
                    if (roll < 32)
                    {   // the manager's desk: bookcase behind, the desk facing the room, a guest chair
                        b.Add(PropKind.Bookshelf, f.P(0, BackOf(PropKind.Bookshelf)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 0.85f), f.Face);
                        b.Add(PropKind.Desk, f.P(0, 1.6f), f.Back);
                        b.Add(PropKind.GuestChair, f.P(0, 2.4f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 1.6f, DeskTop + 0.005f));
                        return 1.65f;
                    }
                    if (roll < 42)
                    {   // the boss's suite: credenza at the wall, the big desk in front of it facing the door, two guest chairs
                        b.Add(PropKind.Credenza, f.P(0, BackOf(PropKind.Credenza)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 0.9f), f.Face);
                        b.Add(PropKind.ExecutiveDesk, f.P(0, 1.72f), f.Face);
                        b.Add(PropKind.GuestChair, f.P(-0.45f, 2.55f), f.Back);
                        b.Add(PropKind.GuestChair, f.P(0.45f, 2.55f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 1.7f, 0.765f));
                        b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.Credenza), 0.755f));
                        return Clock(1.85f);
                    }
                    if (roll < 50) { Single(PropKind.Safe, 0.85f); b.Item(f.P(0, BackOf(PropKind.Safe), 1.02f)); return 0.85f; }
                    if (roll < 58) { b.Add(PropKind.Bookshelf, f.P(-0.51f, BackOf(PropKind.Bookshelf)), f.Face); b.Add(PropKind.Bookshelf, f.P(0.51f, BackOf(PropKind.Bookshelf)), f.Face); return 2.05f; }
                    if (roll < 66) { Row(f, b, PropKind.FilingCabinet, 2, 0.5f); b.Item(f.P(R(-0.2f, 0.2f), BackOf(PropKind.FilingCabinet), 1.305f)); return Clock(1.05f); }
                    if (roll < 73) { Single(PropKind.Credenza, 1.65f); b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.Credenza), 0.755f)); return Clock(1.65f); }
                    if (roll < 80) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(0.95f); }
                    if (roll < 86 && !m_Printer) { m_Printer = true; return Single(PropKind.Printer, 0.65f); }
                    if (roll < 93) return Row(f, b, PropKind.GuestChair, 2, 0.65f);
                    return Single(PropKind.Lockers, 1.05f);
                case RoomType.OpenSpace:
                    if (roll < 8 && !m_Printer) { m_Printer = true; return Single(PropKind.Printer, 0.65f); } // one per room, rarely
                    if (roll < 30) { b.Add(PropKind.Cabinet, f.P(-0.475f, BackOf(PropKind.Cabinet)), f.Face); b.Add(PropKind.Cabinet, f.P(0.475f, BackOf(PropKind.Cabinet)), f.Face); b.Item(f.P(0.4f, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(1.9f); }
                    if (roll < 40) { Single(PropKind.StandingDesk, 1.45f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.StandingDesk), 1.105f)); return 1.45f; }
                    if (roll < 48) return Row(f, b, PropKind.FilePedestal, 2, 0.45f);
                    if (roll < 56) return Single(PropKind.MobileWhiteboard, 1.45f);
                    if (roll < 64) return Single(PropKind.SofaPod, 1.85f);
                    if (roll < 72) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 82) { b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face); b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back); b.Item(f.P(0.3f, 0.5f, DeskTop + 0.005f)); return 1.65f; }
                    if (roll < 92) return Single(PropKind.PlanterDivider, 1.65f);
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Meeting:
                    if (roll < 20) { Single(PropKind.TvStand, 1.45f); return 1.45f; }
                    if (roll < 34) { b.Add(PropKind.Whiteboard, f.P(0, 0.14f), f.Face); return 1.95f; }
                    if (roll < 50)
                    {   // the projector screen over the AV cabinet
                        b.Add(PropKind.AvCredenza, f.P(0, BackOf(PropKind.AvCredenza)), f.Face);
                        b.Add(PropKind.ProjectorScreen, f.P(0, BackOf(PropKind.ProjectorScreen)), f.Face, solid: false);
                        b.Item(f.P(R(-0.6f, 0.1f), BackOf(PropKind.AvCredenza), 0.755f));
                        return 2.05f;
                    }
                    if (roll < 60) return Single(PropKind.Flipchart, 0.75f);
                    if (roll < 70) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(0.95f); }
                    if (roll < 78) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 90) return Row(f, b, PropKind.MeetingChair, 2, 0.62f);
                    { Single(PropKind.Credenza, 1.65f); b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.Credenza), 0.755f)); return Clock(1.65f); }
                case RoomType.Kitchen:
                    if (!m_Fridge) return Single(PropKind.Fridge, 0.75f); // every kitchen has its fridge
                    if (!m_Table)
                    {   // and its table: by the wall, a chair on each side of it
                        var table = f.P(0, 0.5f + WallGap);
                        b.Add(PropKind.RoundTable, table, 0);
                        foreach (var d in new[] { f.U, -f.U }) b.Add(PropKind.Chair, table + d * 0.95f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        b.Item(table + Vector3.up * 0.76f);
                        return 2.6f;
                    }
                    if (roll < 26)
                    {   // the kitchen run: sink, cooker (or the dishwasher), worktop with the microwave and the coffee machine, cupboards over it
                        b.Add(PropKind.KitchenSink, f.P(-1.52f, BackOf(PropKind.KitchenSink)), f.Face);
                        var mid = Chance(0.5) ? PropKind.Stove : PropKind.Dishwasher;
                        b.Add(mid, f.P(-0.62f, BackOf(mid)), f.Face);
                        b.Add(PropKind.KitchenCounter, f.P(0.92f, BackOf(PropKind.KitchenCounter)), f.Face);
                        // the counter has its microwave at its local −X: the coffee machine on the other end
                        var cx = Quaternion.Euler(0, f.Face, 0) * Vector3.right;
                        b.Add(PropKind.CoffeeMachine, f.P(0.92f, BackOf(PropKind.KitchenCounter) + 0.05f, 0.93f) + cx * 0.75f, f.Face, solid: false);
                        b.Add(PropKind.WallCabinet, f.P(-1.52f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Add(PropKind.WallCabinet, f.P(0.92f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Item(f.P(0.9f, BackOf(PropKind.KitchenCounter), 0.93f));
                        return 4.3f;
                    }
                    if (roll < 40)
                    {   // a shorter run: the sink and a worktop
                        b.Add(PropKind.KitchenSink, f.P(-1.22f, BackOf(PropKind.KitchenSink)), f.Face);
                        b.Add(PropKind.KitchenCounter, f.P(0.62f, BackOf(PropKind.KitchenCounter)), f.Face);
                        b.Add(PropKind.WallCabinet, f.P(-1.22f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Item(f.P(0.3f, BackOf(PropKind.KitchenCounter), 0.93f));
                        return 3.7f;
                    }
                    // the fridge is there already (one a kitchen): drawers, a pantry, the water, the recycling, the bin
                    if (roll < 50) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 58) { Single(PropKind.PantryCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PantryCabinet), 0.915f)); return 0.95f; }
                    if (roll < 66) return Single(PropKind.RecyclingBins, 1.25f);
                    if (roll < 72) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 84)
                    {   // a table pushed against the wall, two chairs at it
                        b.Add(PropKind.DiningTable, f.P(0, 0.45f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(-0.42f, 1.25f), f.Back);
                        b.Add(PropKind.Chair, f.P(0.42f, 1.25f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.4f, 0.755f));
                        return Clock(1.65f);
                    }
                    if (roll < 92) return Single(PropKind.TrashBin, 0.45f);
                    { Single(PropKind.PantryCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PantryCabinet), 1.315f)); return 0.95f; }
                case RoomType.Server:
                    if (roll < 45)
                    {
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.ServerRack, f.P(i * 0.72f, BackOf(PropKind.ServerRack)), f.Face);
                        return 2.2f;
                    }
                    if (roll < 56) return Row(f, b, PropKind.NetworkCabinet, 2, 0.62f);
                    if (roll < 66) return Single(PropKind.CracUnit, 1.05f);
                    if (roll < 75) return Single(PropKind.TapeLibrary, 1.25f);
                    if (roll < 84) { Row(f, b, PropKind.UpsUnit, 2, 0.55f); b.Item(f.P(R(-0.2f, 0.2f), BackOf(PropKind.UpsUnit), 0.985f)); return 1.15f; }
                    if (roll < 92) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    { Single(PropKind.CrashCart, 0.65f); b.Item(f.P(0, BackOf(PropKind.CrashCart), 0.815f)); return 0.65f; }
                case RoomType.Workshop:
                    // the benches and machines along the walls behind the bays: a real shop's equipment list
                    if (roll < 22) { Single(PropKind.ToolChest, 1.2f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.ToolChest), 1.06f)); return 1.2f; }
                    if (roll < 34) return Single(PropKind.TireRack, 2.1f);
                    if (roll < 42) return Single(PropKind.TireChanger, 1.2f);
                    if (roll < 50) return Single(PropKind.WheelBalancer, 1.2f);
                    if (roll < 56) return Single(PropKind.AirCompressor, 1.3f);
                    if (roll < 64) { Single(PropKind.OilDrums, 1.25f); return 1.25f; }
                    if (roll < 70) return Single(PropKind.WeldingCart, 0.7f);
                    if (roll < 76) return Single(PropKind.EngineHoist, 1.0f);
                    if (roll < 86) { Single(PropKind.WorkTable, 1.7f); b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.WorkTable), 0.92f)); return 1.7f; }
                    if (roll < 93) return Hung(PropKind.HoseReel, 0.6f);
                    { Single(PropKind.MetalShelving, 1.85f); if (Chance(0.7)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.MetalShelving), StorageDecks[m_Rng.Next(3)])); return 1.85f; }
                case RoomType.Ward:
                    if (roll < 25) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 45) return Single(PropKind.GuestChair, 0.7f);
                    if (roll < 60) return Single(PropKind.VitalsMonitor, 0.7f);
                    if (roll < 72) return Hung(PropKind.HandSanitizer, 0.3f);
                    if (roll < 86) return Single(PropKind.Wheelchair, 0.75f);
                    return Single(PropKind.IvStand, 0.5f);
                case RoomType.NurseStation:
                    if (roll < 30) { Single(PropKind.MedicationCart, 1.05f); b.Item(f.P(0, BackOf(PropKind.MedicationCart), 1.06f)); return 1.05f; }
                    if (roll < 50) return Single(PropKind.LinenCart, 0.75f);
                    if (roll < 70) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 85) return Single(PropKind.VitalsMonitor, 0.7f);
                    return Hung(PropKind.HandSanitizer, 0.3f);
                case RoomType.Operating:
                    if (roll < 30) return Single(PropKind.ScrubSink, 1.6f);
                    if (roll < 55) { Single(PropKind.MedicationCart, 1.05f); b.Item(f.P(0, BackOf(PropKind.MedicationCart), 1.06f)); return 1.05f; }
                    if (roll < 75) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 88) return Single(PropKind.LinenCart, 0.75f);
                    return Hung(PropKind.HandSanitizer, 0.3f);
                case RoomType.Imaging:
                    if (roll < 35) { Single(PropKind.Desk, 1.45f); b.Item(f.P(R(-0.4f, 0.4f), 0.4f + WallGap, DeskTop + 0.005f)); return 1.45f; }
                    if (roll < 60) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 80) return Single(PropKind.Wheelchair, 0.75f);
                    return Hung(PropKind.HandSanitizer, 0.3f);
                case RoomType.PaintBooth:
                    if (roll < 50) { Single(PropKind.PaintRack, 1.45f); b.Item(f.P(-0.3f, BackOf(PropKind.PaintRack), 0.95f)); return 1.45f; }
                    if (roll < 70) return Single(PropKind.ToolChest, 1.2f);
                    if (roll < 85) return Hung(PropKind.HoseReel, 0.6f);
                    return Single(PropKind.TireStack, 0.85f);
                case RoomType.Storage:
                    if (m_L.PlanKind == HouseKind.Garage && roll < 30) return Single(PropKind.TireRack, 2.1f); // the parts store keeps tyres too
                    if (roll < 86)
                    {
                        // empty pallet racks all round (no boxes on them: the decks are for loot): now and then a thing
                        // on an upper deck from the start, the bottom deck for the workers — three places along it
                        b.Add(PropKind.StorageRack, f.P(0, BackOf(PropKind.StorageRack)), f.Face);
                        if (Chance(0.5)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.StorageRack), ShelfLevels[1 + m_Rng.Next(2)]));
                        foreach (float along in new[] { -0.62f, 0f, 0.62f })
                            b.Drops.Add(new StorageDrop { Spot = f.P(along, BackOf(PropKind.StorageRack), Enemies.CargoMotion.ShelfTop + 0.02f), Facing = -f.N, Shelf = true });
                        return 2.05f;
                    }
                    // the rest of the walls: boxes, drums, crates
                    if (roll < 91) { b.Add(PropKind.Boxes, f.P(-0.2f, 0.45f), f.Face + m_Rng.Next(4) * 90f); return 1.2f; }
                    if (roll < 95) return Single(PropKind.DrumPair, 0.95f);
                    if (roll < 98) { Single(PropKind.CrateStack, 1.25f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.CrateStack), 1.385f)); return 1.25f; }
                    return Single(PropKind.RollingCage, 0.95f);
                case RoomType.Restroom:
                    if (roll < 38)
                    {
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.ToiletStall, f.P(i * 1.06f, BackOf(PropKind.ToiletStall)), f.Face);
                        b.Item(f.P(0, 2.1f));
                        return 3.2f;
                    }
                    if (roll < 52)
                    {   // basins with mirrors, the hand dryer beside them
                        b.Add(PropKind.MirrorSink, f.P(-0.62f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.MirrorSink, f.P(0.18f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.HandDryer, f.P(0.82f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                        if (Chance(0.4)) b.Item(f.P(-0.62f, BackOf(PropKind.MirrorSink), 0.86f));
                        return 2.0f;
                    }
                    if (roll < 64)
                    {   // the double vanity, the towels beside it
                        b.Add(PropKind.DoubleSink, f.P(-0.15f, BackOf(PropKind.DoubleSink)), f.Face);
                        b.Add(PropKind.TowelDispenser, f.P(0.82f, BackOf(PropKind.TowelDispenser)), f.Face, solid: false);
                        if (Chance(0.4)) b.Item(f.P(-0.15f, BackOf(PropKind.DoubleSink) + 0.05f, 0.855f));
                        return 2.0f;
                    }
                    if (roll < 74) return Row(f, b, PropKind.Urinal, 3, 0.7f);
                    if (roll < 82) return Single(PropKind.JanitorCart, 0.95f);
                    if (roll < 88) return Hung(PropKind.ChangingTable, 0.95f);
                    if (roll < 94) return Single(PropKind.SanitaryBin, 0.35f);
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Lounge:
                    if (roll < 16)
                    {
                        b.Add(PropKind.TvStand, f.P(0, BackOf(PropKind.TvStand)), f.Face);
                        b.Add(PropKind.Armchair, f.P(-0.6f, 2.0f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0.6f, 2.0f), f.Back);
                        return 1.45f;
                    }
                    if (roll < 30)
                    {
                        b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                        b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face);
                        b.Item(f.P(0, BackOf(PropKind.Sofa), 0.46f));
                        b.Item(f.P(0, 1.35f, 0.42f));
                        return Clock(2.25f);
                    }
                    if (roll < 40)
                    {   // the sectional against the wall, a lamp table at its end
                        b.Add(PropKind.SectionalSofa, f.P(-0.3f, BackOf(PropKind.SectionalSofa)), f.Face);
                        b.Add(PropKind.SideTable, f.P(1.2f, BackOf(PropKind.SideTable)), f.Face);
                        b.Item(f.P(1.2f, BackOf(PropKind.SideTable) + 0.1f, 0.555f));
                        return 3.0f;
                    }
                    if (roll < 47) return Single(PropKind.ArcadeMachine, 0.8f);
                    if (roll < 53) return Single(PropKind.Plant, 0.75f);                  // no vending machines
                    if (roll < 58) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 65) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 76) { b.Add(PropKind.EggChair, f.P(-0.3f, BackOf(PropKind.EggChair)), f.Face); b.Add(PropKind.SideTable, f.P(0.45f, BackOf(PropKind.SideTable)), f.Face); return 1.45f; }
                    if (roll < 84) { Single(PropKind.MagazineRack, 0.65f); b.Item(f.P(0, BackOf(PropKind.MagazineRack), 0.41f)); return 0.65f; }
                    if (roll < 92) return Single(PropKind.FloorLamp, 0.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Gym:
                    if (roll < 18) return Row(f, b, PropKind.Treadmill, 2, 0.95f);
                    if (roll < 30) return Row(f, b, PropKind.ExerciseBike, 2, 0.65f);
                    if (roll < 40) { Single(PropKind.DumbbellRack, 1.5f); return 1.5f; }
                    if (roll < 47) { b.Add(PropKind.PunchingBag, f.P(0, 0.75f), f.Face); return 0.8f; }
                    if (roll < 52) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 58) return Single(PropKind.Bench, 1.7f);
                    if (roll < 67) return Single(PropKind.KettlebellRack, 1.05f);
                    if (roll < 75) return Single(PropKind.MatRack, 0.85f);
                    if (roll < 85) return Single(PropKind.CableMachine, 1.25f);
                    if (roll < 94) return Along(PropKind.RowingMachine);
                    return Single(PropKind.PowerCage, 1.45f);
                case RoomType.Library:
                    if (roll < 36)
                    {
                        Row(f, b, PropKind.TallBookcase, 2, 1.02f);
                        if (Chance(0.6)) b.Item(f.P(R(-0.8f, 0.8f), BackOf(PropKind.TallBookcase), BookcaseLevels[m_Rng.Next(3)]));
                        return 2.05f;
                    }
                    if (roll < 48)
                    {   // a reading corner: the wingback under a floor lamp
                        b.Add(PropKind.WingbackChair, f.P(-0.3f, BackOf(PropKind.WingbackChair) + 0.1f), f.Face);
                        b.Add(PropKind.FloorLamp, f.P(0.45f, 0.3f), f.Face);
                        return 1.4f;
                    }
                    if (roll < 54) { b.Add(PropKind.Globe, f.P(0, 0.4f), f.Face + R(-40f, 40f)); return 0.7f; }
                    if (roll < 60) return Row(f, b, PropKind.Bookshelf, 2, 1.02f);
                    if (roll < 70) { Single(PropKind.CardCatalog, 0.85f); b.Item(f.P(0, BackOf(PropKind.CardCatalog), 1.305f)); return 0.85f; }
                    if (roll < 82)
                    {   // a study carrel with its chair
                        b.Add(PropKind.StudyCarrel, f.P(0, BackOf(PropKind.StudyCarrel)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 1.12f), f.Back);
                        b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.StudyCarrel) + 0.1f, 0.765f));
                        return 1.05f;
                    }
                    if (roll < 92) return Single(PropKind.BookCart, 0.8f);
                    { b.Add(PropKind.Lectern, f.P(0, 0.45f), f.Face); return 0.55f; }
                case RoomType.CopyRoom:
                    if (roll < 22) { Single(PropKind.Copier, 1.0f); b.Item(f.P(0.2f, BackOf(PropKind.Copier), 1.05f)); return 1.0f; }
                    if (roll < 36) { Single(PropKind.MailShelf, 1.25f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.MailShelf), 0.87f)); return 1.25f; }
                    if (roll < 44) return Single(PropKind.PaperStack, 0.85f);
                    if (roll < 50) return Single(PropKind.Shredder, 0.5f);
                    if (roll < 56) return Single(PropKind.Printer, 0.65f);
                    if (roll < 62) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(0.95f); }
                    if (roll < 72) return Single(PropKind.Plotter, 1.55f);
                    if (roll < 82) { Single(PropKind.SupplyShelf, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.SupplyShelf), 0.915f)); return 1.05f; }
                    if (roll < 91) return Single(PropKind.PaperBin, 0.65f);
                    return Single(PropKind.MailCart, 0.95f);
                case RoomType.ControlRoom:
                    if (roll < 30)
                    {   // another robots' control console and its operator (LevelGenerator.PlaceTerminals; spare ones are desks)
                        b.Add(PropKind.Terminal, f.P(0, BackOf(PropKind.Terminal)), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(0, BackOf(PropKind.Terminal) + 0.85f), f.Back);
                        return 1.7f;
                    }
                    if (roll < 50)
                    {   // the video wall, the operators' console in front of it facing it
                        b.Add(PropKind.MonitorWall, f.P(0, BackOf(PropKind.MonitorWall)), f.Face);
                        b.Add(PropKind.ConsoleDesk, f.P(0, 1.55f), f.Face); // its screens face the operators, who face the wall
                        b.Add(PropKind.OperatorChair, f.P(-0.6f, 2.35f), f.Back);
                        b.Add(PropKind.OperatorChair, f.P(0.6f, 2.35f), f.Back);
                        b.Item(f.P(R(-0.9f, 0.9f), 1.45f, 0.77f));
                        return 2.45f;
                    }
                    if (roll < 44) return Row(f, b, PropKind.ServerRack, 2, 0.72f);
                    if (roll < 52) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 56) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 66) return Row(f, b, PropKind.RadioRack, 2, 0.65f);
                    if (roll < 78)
                    {   // the supervisor at the wall: the desk, its chair in front
                        b.Add(PropKind.SupervisorDesk, f.P(0, BackOf(PropKind.SupervisorDesk)), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(0, 1.3f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.SupervisorDesk) + 0.2f, 1.105f));
                        return 1.85f;
                    }
                    if (roll < 90) return Hung(PropKind.MapBoard, 2.05f);
                    return Hung(PropKind.AlarmPanel, 0.85f);
                case RoomType.Lab:
                    if (roll < 26)
                    {
                        b.Add(PropKind.LabBench, f.P(0, BackOf(PropKind.LabBench)), f.Face);
                        b.Add(PropKind.Chair, f.P(R(-0.3f, 0.3f), 1.2f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.LabBench) + 0.1f, 0.91f));
                        return 1.85f;
                    }
                    if (roll < 36) { Single(PropKind.PartsCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PartsCabinet), 1.41f)); return 0.95f; }
                    if (roll < 42) { b.Add(PropKind.RobotArm, f.P(0, BackOf(PropKind.RobotArm)), f.Face); return 0.7f; }
                    if (roll < 48) { Single(PropKind.Shelf, 2.05f); if (Chance(0.6)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.Shelf), ShelfLevels[m_Rng.Next(3)])); return 2.05f; }
                    if (roll < 52) return Single(PropKind.TrashBin, 0.45f);
                    if (roll < 63) { Single(PropKind.FumeHood, 1.55f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.FumeHood) + 0.05f, 0.935f)); return 1.55f; }
                    if (roll < 73) return Single(PropKind.ComponentShelf, 1.25f);
                    if (roll < 82) return Single(PropKind.Printer3d, 0.78f);
                    if (roll < 90) { Single(PropKind.ScopeCart, 0.65f); b.Item(f.P(0.1f, BackOf(PropKind.ScopeCart), 0.955f)); return 0.65f; }
                    {
                        b.Add(PropKind.RobotTable, f.P(0, BackOf(PropKind.RobotTable)), f.Face);
                        b.Add(PropKind.Chair, f.P(0.35f, 1.3f), f.Back);
                        b.Item(f.P(0.35f, BackOf(PropKind.RobotTable), 0.905f));
                        return 1.85f;
                    }
                case RoomType.NapRoom:
                    if (roll < 26) { Row(f, b, PropKind.SleepPod, 2, 1.15f); b.Item(f.P(R(-0.6f, 0.6f), 2.35f)); return 2.3f; }
                    if (roll < 36) { b.Add(PropKind.Hammock, f.P(0, 0.55f), f.Face); return 2.65f; }
                    if (roll < 42) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 46) return Single(PropKind.FloorLamp, 0.45f);
                    if (roll < 60)
                    {   // the bunk bed lengthwise along the wall, the night table at its head
                        float w = Along(PropKind.BunkBed, 0.3f);
                        b.Add(PropKind.NightTable, f.P(0.3f - w / 2f - 0.3f, BackOf(PropKind.NightTable)), f.Face);
                        b.Item(f.P(0.3f, 0.5f, 0.505f));
                        return w + 0.6f;
                    }
                    if (roll < 74)
                    {
                        b.Add(PropKind.Daybed, f.P(-0.3f, BackOf(PropKind.Daybed)), f.Face);
                        b.Add(PropKind.NightTable, f.P(1.0f, BackOf(PropKind.NightTable)), f.Face);
                        b.Item(f.P(1.0f, BackOf(PropKind.NightTable), 0.405f));
                        return 2.6f;
                    }
                    if (roll < 84) { b.Add(PropKind.FloorCushions, f.P(0, 0.7f), f.Face); return 1.2f; }
                    if (roll < 92) return Single(PropKind.NightTable, 0.55f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Studio:
                    if (roll < 20)
                    {
                        b.Add(PropKind.MixingDesk, f.P(0, BackOf(PropKind.MixingDesk)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.MixingDesk) + 0.25f, 0.76f));
                        return 1.65f;
                    }
                    if (roll < 34) { b.Add(PropKind.AcousticPanel, f.P(-0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); b.Add(PropKind.AcousticPanel, f.P(0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); return 2.45f; }
                    if (roll < 42) { b.Add(PropKind.DrumKit, f.P(0, 0.75f), f.Face); return 1.65f; }
                    if (roll < 48) { b.Add(PropKind.MicStand, f.P(0, 0.5f), f.Face); return 0.55f; }
                    if (roll < 56) { Single(PropKind.StudioCouch, 2.05f); b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.StudioCouch), 0.465f)); return 2.05f; }
                    if (roll < 66) { b.Add(PropKind.SynthStand, f.P(0, BackOf(PropKind.SynthStand)), f.Face); b.Add(PropKind.BarStool, f.P(0, 0.95f), f.Back); return 1.35f; }
                    if (roll < 76) { b.Add(PropKind.AmpCabinet, f.P(-0.25f, BackOf(PropKind.AmpCabinet)), f.Face); b.Add(PropKind.GuitarStand, f.P(0.4f, BackOf(PropKind.GuitarStand) + 0.05f), f.Face); b.Item(f.P(-0.25f, BackOf(PropKind.AmpCabinet), 0.675f)); return 1.2f; }
                    if (roll < 84) return Row(f, b, PropKind.GuitarStand, 2, 0.5f);
                    if (roll < 93) return Single(PropKind.VocalBooth, 1.25f);
                    return Single(PropKind.AmpCabinet, 0.75f);
                case RoomType.LockerRoom:
                    if (roll < 26) return Row(f, b, PropKind.Lockers, 3, 1.05f);
                    if (roll < 38) return Row(f, b, PropKind.ShowerStall, 2, 0.98f);
                    if (roll < 47)
                    {
                        b.Add(PropKind.MirrorSink, f.P(-0.25f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.HandDryer, f.P(0.45f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                        return 1.2f;
                    }
                    if (roll < 56) { b.Add(PropKind.Bench, f.P(0, 0.35f), f.Face); if (Chance(0.4)) b.Item(f.P(R(-0.5f, 0.5f), 0.35f, 0.46f)); return 1.65f; }
                    if (roll < 60) return Single(PropKind.TrashBin, 0.45f);
                    if (roll < 70) { Single(PropKind.TowelShelf, 0.95f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.TowelShelf), 0.865f)); return 0.95f; }
                    if (roll < 80) { Single(PropKind.ShoeRack, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.ShoeRack), 0.605f)); return 1.05f; }
                    if (roll < 88) return Single(PropKind.WeighingScale, 0.45f);
                    return Single(PropKind.LaundryCart, 0.85f);
                case RoomType.Infirmary:
                    if (roll < 22) { Row(f, b, PropKind.HospitalBed, 2, 1.15f); b.Item(f.P(R(-0.5f, 0.5f), 1.2f, 0.66f)); return 2.3f; }
                    if (roll < 34) { Single(PropKind.MedicineCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.MedicineCabinet), 1.65f)); return 0.95f; }
                    if (roll < 41) { b.Add(PropKind.PrivacyScreen, f.P(0, 0.35f), f.Face); return 1.65f; }
                    if (roll < 47) { Single(PropKind.MirrorSink, 0.75f); return 0.75f; }
                    if (roll < 52) return Single(PropKind.Cabinet, 0.95f);
                    if (roll < 64)
                    {   // the doctor's desk at the wall, the doctor's chair in front of it
                        b.Add(PropKind.DoctorDesk, f.P(0, BackOf(PropKind.DoctorDesk)), f.Face);
                        b.Add(PropKind.Chair, f.P(-0.3f, 1.1f), f.Back);
                        b.Item(f.P(0.35f, BackOf(PropKind.DoctorDesk) + 0.1f, 0.765f));
                        return Clock(1.45f);
                    }
                    if (roll < 74) { float w = Along(PropKind.ExamTable); b.Item(f.P(0, 0.35f + WallGap, 0.725f)); return w; }
                    if (roll < 80) return Hung(PropKind.DefibCabinet, 0.45f);
                    if (roll < 90) { b.Add(PropKind.Wheelchair, f.P(0, BackOf(PropKind.Wheelchair)), f.Face); return 0.7f; }
                    return Single(PropKind.IvStand, 0.5f);
                case RoomType.GameRoom:
                    if (roll < 12) { b.Add(PropKind.DartBoard, f.P(0, BackOf(PropKind.DartBoard)), f.Face, solid: false); return 0.65f; }
                    if (roll < 24) return Row(f, b, PropKind.ArcadeMachine, 2, 0.8f);
                    if (roll < 36)
                    {   // the bar with stools in front of it
                        b.Add(PropKind.BarCounter, f.P(0, BackOf(PropKind.BarCounter)), f.Face);
                        b.Add(PropKind.BarStool, f.P(-0.55f, 0.95f), f.Back);
                        b.Add(PropKind.BarStool, f.P(0.55f, 0.95f), f.Back);
                        b.Item(f.P(R(-0.7f, 0.7f), BackOf(PropKind.BarCounter), 1.06f));
                        return 2.05f;
                    }
                    if (roll < 46)
                    {
                        b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                        b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face);
                        b.Item(f.P(0, 1.35f, 0.42f));
                        return 2.25f;
                    }
                    if (roll < 52) return Single(PropKind.VendingMachine, 0.95f);
                    if (roll < 56) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 66) return Single(PropKind.Jukebox, 0.85f);
                    if (roll < 78) return Row(f, b, PropKind.Pinball, 2, 0.8f);
                    if (roll < 90)
                    {   // the console corner: the TV on its stand, two bean bags in front of it
                        b.Add(PropKind.GamingCorner, f.P(0, BackOf(PropKind.GamingCorner)), f.Face);
                        // turned by 15° a 0.9 m bean bag takes 1.1 m: 1.16 m apart, or the two sink into each other
                        b.Add(PropKind.BeanBag, f.P(-0.58f, 1.45f), f.Back + 15f);
                        b.Add(PropKind.BeanBag, f.P(0.58f, 1.45f), f.Back - 15f);
                        b.Item(f.P(0.65f, BackOf(PropKind.GamingCorner), 0.38f));
                        return 1.65f;
                    }
                    return Single(PropKind.Pinball, 0.8f);
            }
            return 0f;
        }

        /// <summary><paramref name="n"/> pieces side by side along a wall, <paramref name="step"/> apart, backs to it; returns the width.</summary>
        float Row(Frame f, Batch b, PropKind k, int n, float step)
        {
            for (int i = 0; i < n; i++) b.Add(k, f.P((i - (n - 1) / 2f) * step, BackOf(k)), f.Face);
            return n * step + 0.05f;
        }

        /// <summary>
        /// A room the rules above left almost bare (small, doors on every wall, windows): its own wall groups once more,
        /// at every spot of every wall, more tries at each, until it has a few pieces for its size.
        /// </summary>
        void Sparse(PlanRoom room)
        {
            if (room.Type is RoomType.Corridor or RoomType.Hall) return;
            int want = Mathf.Clamp(Mathf.RoundToInt(room.Area / 5f), 3, 8);
            int Have() { int n = 0; foreach (var kv in m_InRoom) n += kv.Value; return n; }
            if (Have() >= want) return;
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in FacesOf(room))
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 0.15f; t < w.Length - 0.3f;)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    float placed = 0f;
                    for (int attempt = 0; attempt < 4 && placed <= 0f; attempt++)
                    {
                        var batch = new Batch();
                        float width = WallGroup(room, batch, face, u, n);
                        if (width <= 0f || t + width > w.Length - 0.1f) continue;
                        var shift = u * (width / 2f);
                        for (int i = 0; i < batch.Pieces.Count; i++) { var p = batch.Pieces[i]; p.Pos += shift; batch.Pieces[i] = p; }
                        for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                        if (Deep(room, face + shift, n, batch, Aisle) && TryCommit(room, batch, front: n)) placed = width;
                    }
                    if (placed > 0f)
                    {
                        if (Have() >= want) return;
                        t += placed + 0.15f;
                    }
                    else t += 0.25f;
                }
            }
        }
    }
}
