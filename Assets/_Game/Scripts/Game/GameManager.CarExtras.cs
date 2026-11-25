using Stealth.Items;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The car's unique upgrades that do something (server-decided, replicated by the network twin): the teleport — the red
    /// button on the glovebox brings everyone to the car, then recharges on fuel put into the shredder over what the drive
    /// needs (<see cref="TeleportServer"/>, <see cref="ChargeTeleport"/>); the radio — cassettes lie in the houses, one put in
    /// plays near the car and on the drive (<see cref="InsertCassetteServer"/>, Vehicles.CarRadio).
    /// </summary>
    public partial class GameManager
    {
        // ------------------------------------------------------------------ the teleport

        /// <summary>Fuel put into the shredder over what the drive needs since the button was last pressed (full: ready).</summary>
        public int TeleportCharge { get; private set; }

        static int TeleportNeed => Mathf.Max(1, UpgradeSettings.TeleportRecharge);

        /// <summary>The button can be pressed: fitted and charged.</summary>
        public bool TeleportReady => Upgrades.Level(CarUpgrade.Teleport) > 0 && TeleportCharge >= TeleportNeed;

        /// <summary>How charged it is, 0..100 (the button's prompt).</summary>
        public int TeleportChargePercent => Mathf.Clamp(TeleportCharge * 100 / TeleportNeed, 0, 100);

        /// <summary>Peers: the server's charge, as a share 0..100.</summary>
        public void ApplyTeleportCharge(byte percent)
        {
            if (!m_IsServer) TeleportCharge = percent * TeleportNeed / 100; // (the server keeps its exact count)
        }

        /// <summary>
        /// How much of a deposit charges the teleport (pure): only the fuel over what the drive needs — the part of
        /// <paramref name="added"/> that takes the tank from <paramref name="before"/> above <paramref name="need"/>.
        /// </summary>
        public static int OverTheNeed(int before, int added, int need) => Mathf.Max(0, before + added - Mathf.Max(before, need));

        /// <summary>Server: fuel just went into the tank — what is over the drive's need charges the button.</summary>
        void ChargeTeleport(int before, int added)
        {
            if (Upgrades.Level(CarUpgrade.Teleport) <= 0) return;
            TeleportCharge = Mathf.Min(TeleportNeed, TeleportCharge + OverTheNeed(before, added, FuelNeeded));
        }

        /// <summary>Server: the teleport was just fitted (or a new run) — it comes charged.</summary>
        void FillTeleport() => TeleportCharge = Upgrades.Level(CarUpgrade.Teleport) > 0 ? TeleportNeed : 0;

        /// <summary>
        /// Server: the driver or the passenger pressed the red button — everyone who is up gets into a free seat of the car
        /// (beside it when the seats are taken), whoever is down is laid beside it; the button is spent until recharged.
        /// </summary>
        public void TeleportServer(PlayerController presser)
        {
            var car = Car.Active;
            if (car == null || presser == null || !TeleportReady || DriveUntil > 0) return;
            int seat = car.SeatOf(presser.ClientId);
            if (seat is not (0 or 1)) return;
            int n = 0;
            foreach (var p in PlayerController.All)
            {
                if (p == null || car.SeatOf(p.ClientId) >= 0) continue;
                int free = -1;
                for (int i = 0; i < Car.SeatCount && free < 0 && !p.Downed; i++)
                    if (car.OccupantOf(i) == Car.Nobody) free = i;
                if (free >= 0) car.SitServer(p, free);
                else p.PlaceServer(car.ExitPoint(n % Car.SeatCount) + car.transform.forward * (0.6f * (n / Car.SeatCount)), car.transform.eulerAngles.y);
                n++;
            }
            TeleportCharge = 0;
            Banner(Loc.Pack("banner.teleport", presser.Nick));
            SoundServer(Audio.GameSound.Stun, car.transform.position + Vector3.up);
        }

        // ------------------------------------------------------------------ the radio

        /// <summary>The cassette in the car's radio (its song, 0..ItemCatalog.CassetteSongs − 1), −1: none.</summary>
        public int Cassette { get; private set; } = -1;

        /// <summary>Peers: the server's cassette.</summary>
        public void ApplyCassette(sbyte song)
        {
            if (!m_IsServer) Cassette = song;
        }

        /// <summary>
        /// Server: a player put a cassette from their backpack into the radio (the radio fitted, at the car): it plays; the
        /// one that was in comes out into their backpack (into the car's bed if it is full).
        /// </summary>
        public void InsertCassetteServer(PlayerController player, int index)
        {
            if (player == null || Upgrades.Level(CarUpgrade.Radio) <= 0 || !AtCar(player.transform.position)) return;
            var inv = player.GetComponent<PlayerInventory>();
            if (inv == null || !inv.TakeCassetteServer(index, out int song)) return;
            int was = Cassette;
            Cassette = song;
            if (was >= 0 && !inv.GiveServer(ItemCatalog.Cassette, (byte)was))
                DropOnCarServer(ItemCatalog.Cassette, (byte)was, Car.Active.Bed != null ? Car.Active.Bed.position + Car.Active.Bed.forward * 0.5f : Car.Active.transform.position, 0f);
            Banner(Loc.Pack("banner.cassette", player.Nick, "@" + ItemCatalog.CassetteKey(song)));
        }

        /// <summary>Server: a new run — the radio is empty.</summary>
        void ClearRadioServer() => Cassette = -1;
    }
}
