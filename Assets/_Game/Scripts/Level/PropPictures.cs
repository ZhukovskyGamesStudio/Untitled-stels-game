using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a piece's own picture is drawn on. In the order of <see cref="LevelArt.Pictures"/>: append only.</summary>
    public enum PictureKind { Boards, Mirror, Projector }

    /// <summary>
    /// One picture of a sheet of them (a texture cut into <see cref="Cols"/> × <see cref="Rows"/> cells, cell 0 top left) for
    /// the flat faces of a mesh (<see cref="ScreenFaces"/>); what is no such face lies on the middle of <see cref="Rest"/>.
    /// </summary>
    public struct PictureSheet
    {
        public int Cols, Rows, Cell, Rest;
        /// <summary>A face smaller than this either way gets no picture, metres.</summary>
        public float MinSide;
        /// <summary>Faces lying flat count too (a panel's display).</summary>
        public bool Lying;
    }

    /// <summary>
    /// What is written, hung or left on a piece that differs from one to the next (pure; from the house's kind and the
    /// piece's own stream of its seed — every peer sees the same): a whiteboard's writing by whose house it is (formulas in
    /// the station, sales in the shop, diagrams and sticky notes with the programmers; many wiped), a flipchart's page, a
    /// mirror clean, dirty or cracked, a projector's screen whole or torn. Pictures: Tools/Art/generate_boards.py, in this order.
    /// </summary>
    public static class PropPictures
    {
        public struct Pick
        {
            public PictureKind Kind;
            /// <summary>The part that carries it (how its name begins); null: whatever is mirror glass.</summary>
            public string Part;
            public PictureSheet Faces;
        }

        // boards.png, 4 x 4
        public const int Blank = 0, Washed = 1, Formulas = 3, Chart = 5, Table = 6, Diagram = 7, Code = 8, Notes = 9, Jokes = 10, Rota = 11,
            FlipList = 12, FlipChart = 13, FlipHome = 14, FlipBlank = 15;

        static readonly int[] Wiped = { Blank, Washed, Washed + 1 };

        /// <summary>What people in such a house write on a board.</summary>
        public static int[] Writings(HouseKind house) => house switch
        {
            HouseKind.Abandoned => new[] { Formulas, Formulas + 1 },
            HouseKind.Hospital => new[] { Rota, Formulas + 1 },
            HouseKind.Business or HouseKind.Warehouse or HouseKind.Garage => new[] { Chart, Table },
            HouseKind.Apartments => new[] { Notes, Table },
            _ => new[] { Diagram, Code, Notes, Jokes },
        };

        /// <summary>Share of boards wiped or never written on, of mirrors not clean, of projector screens damaged, percent.</summary>
        public const int WipedShare = 32;

        public static bool TryPick(PropKind kind, HouseKind house, int seed, out Pick pick)
        {
            pick = default;
            uint roll = (uint)seed % 100u, more = (uint)seed / 100u;
            bool left = house == HouseKind.Abandoned; // years without care
            switch (kind)
            {
                case PropKind.Whiteboard:
                case PropKind.MobileWhiteboard:
                {
                    var written = Writings(house);
                    int cell = roll < WipedShare ? Wiped[more % (uint)Wiped.Length] : written[more % (uint)written.Length];
                    pick = new Pick { Kind = PictureKind.Boards, Part = kind == PropKind.Whiteboard ? "Body" : "Trim", Faces = Sheet(4, 4, cell, Blank) };
                    return true;
                }
                case PropKind.Flipchart:
                {
                    int cell = roll < WipedShare ? FlipBlank : house == HouseKind.Apartments ? FlipHome : more % 2u == 0u ? FlipList : FlipChart;
                    pick = new Pick { Kind = PictureKind.Boards, Part = "Trim", Faces = Sheet(4, 4, cell, FlipBlank) };
                    return true;
                }
                case PropKind.WashBasin:
                case PropKind.MirrorSink:
                case PropKind.DoubleSink:
                {
                    int clean = left ? 10 : 45, dirty = left ? 60 : 82;
                    int cell = roll < clean ? 0 : roll < dirty ? 1 : 2;
                    pick = new Pick { Kind = PictureKind.Mirror, Part = null, Faces = Sheet(3, 1, cell, cell) };
                    return true;
                }
                case PropKind.ProjectorScreen:
                {
                    int cell = roll < (left ? 10 : 45) ? 0 : 1 + (int)(more % 4u);
                    pick = new Pick { Kind = PictureKind.Projector, Part = "Trim", Faces = Sheet(3, 2, cell, 5) };
                    return true;
                }
            }
            return false;
        }

        static PictureSheet Sheet(int cols, int rows, int cell, int rest) => new() { Cols = cols, Rows = rows, Cell = cell, Rest = rest, MinSide = ScreenFaces.MinSide };
    }
}
