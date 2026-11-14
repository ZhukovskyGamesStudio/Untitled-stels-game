using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>How a staircase is built (its look only: steps, landing and railings keep the same colliders).</summary>
    public enum StairStyle { Concrete, Wood, Steel, Glass }

    /// <summary>What a material slot of a staircase look is: kept as built, or dressed in the style's wall finish, floor finish or trim.</summary>
    public enum StairPart { Keep, Body, Tread, Trim }

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

        /// <summary>The look a style asks for, or (Auto) the one its family and the seed pick.</summary>
        public static StairStyle Pick(StairChoice choice, ModelFamily family, int seed) =>
            choice != StairChoice.Auto ? (StairStyle)((int)choice - 1) : Pick(family, seed);

        /// <summary>
        /// What each material slot (submesh) of a look is made of, so the style can dress it in its own architecture:
        /// the solid body in the wall finish, the treads in the floor finish, balusters and rails in the trim. The
        /// staircase (<paramref name="railing"/> false) and the gallery railing bay have their own slot layouts
        /// (<c>AssetBuilder.Stairs</c>).
        /// </summary>
        public static StairPart[] Parts(StairStyle look, bool railing) => railing
            ? look switch
            {
                StairStyle.Concrete => new[] { StairPart.Body, StairPart.Trim },
                StairStyle.Wood => new[] { StairPart.Trim, StairPart.Keep },
                _ => new[] { StairPart.Keep, StairPart.Keep },
            }
            : look switch
            {
                StairStyle.Concrete => new[] { StairPart.Body, StairPart.Tread, StairPart.Trim },
                StairStyle.Wood => new[] { StairPart.Body, StairPart.Tread, StairPart.Keep, StairPart.Trim },
                StairStyle.Steel => new[] { StairPart.Keep, StairPart.Keep, StairPart.Keep },
                _ => new[] { StairPart.Tread, StairPart.Keep, StairPart.Keep },
            };
    }
}
