using Stealth.Level;
using UnityEngine;

namespace Stealth
{
    /// <summary>
    /// The house's state as the scene's components read it (every peer: the server's, replicated): which doors, safes,
    /// drawers, crates, lights, grates… are open (<see cref="OpenKeys"/>), the lift, the clock. The game sets it
    /// (<see cref="World.Set"/>); with no game (tests, a prefab in the editor) nothing is open.
    /// </summary>
    public interface IWorldState
    {
        bool IsOpened(int key);
        /// <summary>The vault's lock panels the team has solved.</summary>
        int VaultSolved { get; }
        /// <summary>Remote hacks a hacking terminal has left.</summary>
        int TerminalCharges(int terminal);
        LiftState Lift { get; }
        /// <summary>The shared clock (server time).</summary>
        double Now { get; }
    }

    /// <summary>The house's state the scene reads (<see cref="IWorldState"/>).</summary>
    public static class World
    {
        static readonly IWorldState s_Nothing = new Nothing();

        public static IWorldState State { get; private set; } = s_Nothing;

        public static void Set(IWorldState state) => State = state ?? s_Nothing;

        /// <summary>The state goes away (the game is unbound): nothing is open again, if it still is that state.</summary>
        public static void Clear(IWorldState state)
        {
            if (State == state) State = s_Nothing;
        }

        public static bool IsOpened(int key) => State.IsOpened(key);

        sealed class Nothing : IWorldState
        {
            public bool IsOpened(int key) => false;
            public int VaultSolved => 0;
            public int TerminalCharges(int terminal) => 0;
            public LiftState Lift => LiftState.ParkedAt(0);
            public double Now => Time.timeAsDouble;
        }
    }
}
