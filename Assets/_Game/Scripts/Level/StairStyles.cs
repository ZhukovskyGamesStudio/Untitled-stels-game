using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>How a staircase is built (its look only: steps, landing and railings keep the same colliders).</summary>
    public enum StairStyle { Concrete, Wood, Steel, Glass }

    /// <summary>
    /// Which staircase look fits a model family (pure, unit tested): each family has a few that suit it, and the house
    /// seed picks one of them per staircase — every peer agrees, and the same house always looks the same.
    /// </summary>
    public static class StairStyles
    {
        public const int Count = 4;

        static readonly StairStyle[] Any = { StairStyle.Concrete, StairStyle.Wood, StairStyle.Steel, StairStyle.Glass };

        /// <summary>The looks that suit a family, the most typical first.</summary>
        public static StairStyle[] For(ModelFamily family) => family switch
        {
            ModelFamily.Lowpoly => new[] { StairStyle.Concrete, StairStyle.Wood },
            ModelFamily.Toy => new[] { StairStyle.Wood, StairStyle.Glass },
            ModelFamily.Lofi => new[] { StairStyle.Concrete, StairStyle.Steel },
            ModelFamily.Clean => new[] { StairStyle.Glass, StairStyle.Steel },
            ModelFamily.Realistic => new[] { StairStyle.Wood, StairStyle.Concrete, StairStyle.Glass },
            ModelFamily.Derelict => new[] { StairStyle.Steel, StairStyle.Concrete },
            _ => Any,
        };

        public static StairStyle Pick(ModelFamily family, int seed)
        {
            var options = For(family);
            uint h = (uint)seed * 2654435761u;
            return options[(int)((h >> 7) % (uint)options.Length)];
        }
    }
}
