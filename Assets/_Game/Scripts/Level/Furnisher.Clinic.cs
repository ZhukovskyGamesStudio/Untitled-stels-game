namespace Stealth.Level
{
    /// <summary>
    /// What a hospital's office is: a doctor's consulting room (the desk, a chair for the patient, the couch behind its
    /// screen), the records office (the clerk's desk, files, the copier) or the staff's on-call room (a bed, lockers,
    /// armchairs).
    /// </summary>
    public enum Practice { Consulting, Records, OnCall }

    /// <summary>
    /// The hospital's own rooms under the office's room types. A hospital shares the types (an office, a lab) with the
    /// other houses, but not their furniture: its lab is a clinical one (benches with a microscope and a centrifuge, the
    /// sample fridge, a steel sink), its offices are the doctors' and the staff's (<see cref="Practice"/>). The groups
    /// themselves stand in the furnisher's tables, as cases <c>when Clinic</c> before the office's. No office desk with its
    /// PC stands in a hospital: a room's computer is the workstation on wheels (<see cref="PropKind.MedWorkstation"/>),
    /// one a room, as the doctor's desk is.
    /// </summary>
    public sealed partial class Furnisher
    {
        /// <summary>The house is a hospital: its labs and offices take the clinic's furniture.</summary>
        bool Clinic => m_L.PlanKind == HouseKind.Hospital;

        /// <summary>What the office being furnished is (a hospital's; <see cref="PracticeOf"/>).</summary>
        Practice m_Practice;

        /// <summary>The pieces a consulting room and an on-call room are known by (instead of the office's desks).</summary>
        static readonly PropKind[] ConsultingSignatures = { PropKind.DoctorDesk }, OnCallSignatures = { PropKind.Daybed },
            RecordsSignatures = { PropKind.MedWorkstation, PropKind.FilingCabinet };

        /// <summary>
        /// The pieces that make this room what it is (pure; null: its type has none): <see cref="Signatures"/> by its type,
        /// in a hospital's office by its practice.
        /// </summary>
        public static PropKind[] SignaturesOf(LevelLayout l, PlanRoom room)
        {
            if (!Signatures.TryGetValue(room.Type, out var kinds)) return null;
            if (l.PlanKind != HouseKind.Hospital || room.Type != RoomType.Office) return kinds;
            return PracticeOf(l.Seed, room.Id) switch { Practice.Consulting => ConsultingSignatures, Practice.OnCall => OnCallSignatures, _ => RecordsSignatures };
        }

        /// <summary>
        /// What a hospital's office is (pure): by the house's seed and the room, a stream of its own — six in ten
        /// consulting rooms, two the records, two on call, so a floor of offices is not one room repeated.
        /// </summary>
        public static Practice PracticeOf(int seed, int room)
        {
            int roll = new System.Random(LevelRandom.Mix(seed, Salt.Practice, room)).Next(10);
            return roll < 6 ? Practice.Consulting : roll < 8 ? Practice.Records : Practice.OnCall;
        }
    }
}
