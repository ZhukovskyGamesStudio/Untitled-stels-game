using System.Collections.Generic;
using Stealth.Configs;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What a location does differently, in one place (pure): the office (every character of it), the hospital, the auto
    /// repair shop, the apartment block. The generator, the doors, the looks and the house's features ask the rules of
    /// the plan the house was built by (<see cref="Of"/>: <see cref="LevelLayout.PlanKind"/> — a hospital in the rooms
    /// test is laid out as an office, and dressed and doored as one) instead of each asking "is it the shop?". What only
    /// the furniture tables know (a shop's lobby has a tyre display) stays with them, keyed by the room's type.
    /// </summary>
    public abstract class HouseRules
    {
        public static readonly HouseRules Office = new OfficeRules(), Hospital = new HospitalRules(), Garage = new GarageRules(), Apartments = new ApartmentRules(),
            Abandoned = new AbandonedRules();

        /// <summary>The rules of a kind of house (an office of any character: the office's).</summary>
        public static HouseRules For(HouseKind kind) => kind switch
        {
            HouseKind.Hospital => Hospital,
            HouseKind.Garage => Garage,
            HouseKind.Apartments => Apartments,
            HouseKind.Abandoned => Abandoned,
            _ => Office,
        };

        public static HouseRules For(Location location) => location switch
        {
            Location.Hospital => Hospital,
            Location.Garage => Garage,
            Location.Apartments => Apartments,
            Location.Abandoned => Abandoned,
            _ => Office,
        };

        /// <summary>The rules of the plan this house was laid out by.</summary>
        public static HouseRules Of(LevelLayout l) => For(l.PlanKind);

        public abstract Location Location { get; }

        /// <summary>Lays the house out by a plan of its own (<see cref="PlanGenerator"/>'s location plans), not the office generator's rooms and zones.</summary>
        public bool OwnPlan => Location != Location.Office;

        /// <summary>
        /// The building settings of its own plan, on <paramref name="s"/> (a copy of <paramref name="b"/>; the rooms test's
        /// house of every type, <see cref="BuildingSettings.EveryRoomType"/>, keeps the office's plan).
        /// </summary>
        public virtual void Shape(BuildingSettings s, BuildingSettings b) { }

        /// <summary>Rule enforcement and zoning may retype its rooms (the apartment block's every room is its flat's).</summary>
        public virtual bool RetypesRooms => true;

        /// <summary>After the plan's doorways are listed: the location's own doorways (the paint booth's, widened for a car).</summary>
        public virtual void AfterDoorways(LevelLayout l) { }

        /// <summary>The exit hatch's room where the location has one of its own (the shop: its workshop), or null: the far wet room.</summary>
        public virtual PlanRoom ExitRoom(LevelLayout l, HashSet<Vector2Int> ducts) => null;

        /// <summary>The chance a doorway has a door (one that does not always have one, <see cref="DoorRules.Always"/>).</summary>
        public virtual float DoorChance(LevelLayout l, Doorway d, float chance) => chance;

        /// <summary>
        /// The kind of an inner door between rooms <paramref name="a"/> and <paramref name="b"/> (the vault's, the storage
        /// room's, the booth's and the plant room's are decided before: <see cref="DoorRules.KindOf"/>).
        /// </summary>
        public abstract DoorKind InnerDoor(LevelLayout l, Doorway d, PlanRoom a, PlanRoom b);

        /// <summary>The street door's kind, or null: the glass leaves.</summary>
        public virtual DoorKind? StreetDoor => null;

        /// <summary>The style's geometry dressed for this house (the style's own where the location has no look of its own).</summary>
        public virtual StyleGeometry Look(StyleGeometry g, StyleKit kit, LevelLayout l) => g;

        /// <summary>The location's unique feature as a piece of furniture (<see cref="HouseFeatures.PointOfInterest"/>), or none.</summary>
        public abstract PropKind? PointOfInterest { get; }

        /// <summary>The robots may take this room for their charging room (<see cref="ChargingRooms"/>).</summary>
        public virtual bool ChargingRoomOn(PlanRoom r) => true;

        /// <summary>The house hides the map to the shelter (<see cref="HouseFeatures.PlanShelterMap"/>).</summary>
        public virtual bool HasShelterMap => false;

        /// <summary>
        /// Soft warm light (<see cref="LightMood"/>): the lamps and the air of a place people work and live in. Not where
        /// the light is clinical or dead (the hospital, the abandoned station).
        /// </summary>
        public virtual bool WarmLight => true;

        /// <summary>The location's own plan settings shared by all of them: a single block, no atriums, rotundas or cut corners.</summary>
        protected static void SingleBlock(BuildingSettings s, int[] floors)
        {
            s.FloorsPerHouse = floors;
            s.LinkedChance = 0f;
            s.AtriumChance = 0f;
            s.RotundaChance = 0f;
            s.CornerCutChance = 0f;
        }

        protected static bool Either(PlanRoom a, PlanRoom b, System.Func<RoomType, bool> f) => f(a.Type) || f(b.Type);
    }

    sealed class OfficeRules : HouseRules
    {
        public override Location Location => Location.Office;
        public override PropKind? PointOfInterest => PropKind.Programmer;

        public override DoorKind InnerDoor(LevelLayout l, Doorway d, PlanRoom a, PlanRoom b)
        {
            // glass sliding doors only where the tech is; steel to storage; double doors to big rooms
            if (Either(a, b, t => t is RoomType.Server or RoomType.ControlRoom or RoomType.Lab)) return DoorKind.Slide;
            if (l.Kind == HouseKind.DataCenter && DoorRules.Circulation(a.Type) && DoorRules.Circulation(b.Type)) return DoorKind.Slide;
            if (Either(a, b, t => t is RoomType.Storage)) return DoorKind.Steel;
            if (Either(a, b, t => t is RoomType.Restroom or RoomType.LockerRoom)) return DoorKind.Wood;
            var room = DoorRules.Circulation(a.Type) ? b : a;
            if (room.Type is RoomType.Meeting or RoomType.Library or RoomType.Lounge or RoomType.Gym or RoomType.OpenSpace or RoomType.GameRoom
                or RoomType.Kitchen && DoorRules.Big(room)) return DoorKind.WoodDouble;
            if (DoorRules.Circulation(a.Type) && DoorRules.Circulation(b.Type)) return DoorKind.WoodDouble; // a lobby onto its corridor
            return DoorKind.Wood;
        }
    }

    sealed class HospitalRules : HouseRules
    {
        public override Location Location => Location.Hospital;
        public override bool WarmLight => false; // cold and even: a hospital
        public override PropKind? PointOfInterest => PropKind.MedTable;

        public override void Shape(BuildingSettings s, BuildingSettings b)
        {
            // a slab: the corridor down its length, rooms on both sides; two or three floors (the theatres on top)
            if (b.EveryRoomType) { s.CorridorChance = 1f; return; }
            s.Hospital = true;
            SingleBlock(s, new[] { 2, 2, 3, 3, 3 });
        }

        public override float DoorChance(LevelLayout l, Doorway d, float chance) => Mathf.Max(chance, 0.6f); // wards, theatres and the corridor's swing doors

        public override DoorKind InnerDoor(LevelLayout l, Doorway d, PlanRoom a, PlanRoom b)
        {
            if (Either(a, b, t => t is RoomType.Operating)) return DoorKind.ClinicSwing;
            if (DoorRules.Circulation(a.Type) && DoorRules.Circulation(b.Type)) return DoorKind.ClinicSwing; // the corridor's dividing doors
            if (Either(a, b, t => t is RoomType.Storage)) return DoorKind.Steel;
            return DoorKind.Clinic;
        }

        public override StyleGeometry Look(StyleGeometry g, StyleKit kit, LevelLayout l) => ClinicLook.For(g, kit, l);
    }

    sealed class GarageRules : HouseRules
    {
        public override Location Location => Location.Garage;
        public override PropKind? PointOfInterest => PropKind.Workbench;
        public override DoorKind? StreetDoor => DoorKind.Steel;

        public override void Shape(BuildingSettings s, BuildingSettings b)
        {
            // a box of a building: the hall two storeys high, the block beside it two floors, square rooms
            if (b.EveryRoomType) return;
            s.Garage = true;
            SingleBlock(s, new[] { 2 });
            s.DarkChance = b.DarkChance + 0.08f;
        }

        public override void AfterDoorways(LevelLayout l) => LevelGenerator.WidenBoothDoorway(l);

        public override PlanRoom ExitRoom(LevelLayout l, HashSet<Vector2Int> ducts)
        {
            // the oil separator's chamber in the workshop's floor
            foreach (var r in l.Rooms)
                if (r.Type == RoomType.Workshop && r.Floor == 0 && VentPlan.CanReach(l, r, ducts)) return r;
            return null;
        }

        public override DoorKind InnerDoor(LevelLayout l, Doorway d, PlanRoom a, PlanRoom b) =>
            Either(a, b, t => t is RoomType.Workshop or RoomType.PaintBooth or RoomType.Storage) ? DoorKind.Steel : DoorKind.Wood;

        public override StyleGeometry Look(StyleGeometry g, StyleKit kit, LevelLayout l) => ShopLook.For(g, kit, l);
    }

    sealed class ApartmentRules : HouseRules
    {
        public override Location Location => Location.Apartments;
        public override PropKind? PointOfInterest => null; // its find is the shelter map
        public override DoorKind? StreetDoor => DoorKind.Entrance;
        public override bool RetypesRooms => false;
        public override bool HasShelterMap => true;

        public override void Shape(BuildingSettings s, BuildingSettings b)
        {
            // one entrance, one stairwell, a few flats a floor: more storeys than any other house
            if (b.EveryRoomType) return;
            s.Apartments = true;
            SingleBlock(s, new[] { 4, 4, 5, 5, 6 });
        }

        public override float DoorChance(LevelLayout l, Doorway d, float chance) =>
            d.RoomA >= 0 && d.RoomB >= 0 && (l.Plan.Rooms[d.RoomA].Flat >= 0 || l.Plan.Rooms[d.RoomB].Flat >= 0) ? Mathf.Max(chance, 0.75f) : chance;

        public override DoorKind InnerDoor(LevelLayout l, Doorway d, PlanRoom a, PlanRoom b)
        {
            if (DoorRules.IsFlatEntry(l, d)) return DoorKind.Entry;
            if (Either(a, b, t => t is RoomType.Living)) return DoorKind.WoodDouble;
            if (a.Flat < 0 && b.Flat < 0) return DoorKind.Steel; // the ground floor's storeroom, the concierge
            return DoorKind.Wood;
        }

        public override StyleGeometry Look(StyleGeometry g, StyleKit kit, LevelLayout l) => HomeLook.For(g, kit, l);

        // the block's floors above the ground are one typical floor: the robots take a room down below
        public override bool ChargingRoomOn(PlanRoom r) => r.Floor == 0;
    }

    sealed class AbandonedRules : HouseRules
    {
        public override Location Location => Location.Abandoned;
        public override bool WarmLight => false; // what still burns here burns cold
        public override PropKind? PointOfInterest => null; // its find is the station itself: dark, rotten, the robots at home
        public override DoorKind? StreetDoor => DoorKind.Steel;
        public override bool RetypesRooms => false; // the station's program is laid out by its plan

        public override void Shape(BuildingSettings s, BuildingSettings b)
        {
            // a research station: a slab with a spine corridor, labs and servers along it, the quarters upstairs; the power
            // mostly out (more dark rooms)
            if (b.EveryRoomType) { s.CorridorChance = 1f; return; }
            s.Station = true;
            SingleBlock(s, new[] { 2, 2, 3, 3, 3 });
            s.DarkChance = Mathf.Min(0.9f, b.DarkChance + 0.25f);
        }

        public override float DoorChance(LevelLayout l, Doorway d, float chance) => Mathf.Max(chance, 0.55f); // bulkheads along the spine

        public override DoorKind InnerDoor(LevelLayout l, Doorway d, PlanRoom a, PlanRoom b)
        {
            // glass where the science was, steel everywhere else
            if (Either(a, b, t => t is RoomType.Lab or RoomType.Server or RoomType.ControlRoom)) return DoorKind.Slide;
            if (Either(a, b, t => t is RoomType.Restroom or RoomType.Bedroom)) return DoorKind.Wood;
            return DoorKind.Steel;
        }

        public override StyleGeometry Look(StyleGeometry g, StyleKit kit, LevelLayout l) => DerelictLook.For(g, kit, l);
    }
}
