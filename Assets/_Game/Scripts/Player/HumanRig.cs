using UnityEngine;

namespace Stealth.Player
{
    /// <summary>What the body is busy with besides walking (replicated by the owner; the rigged human plays a take for it).</summary>
    public enum BodyPose : byte
    {
        None,
        /// <summary>On a ladder: the Climb take, played by the climbing speed (backwards on the way down).</summary>
        Ladder,
        /// <summary>At a terminal's keyboard, a safe's dial, a vault console: typing.</summary>
        Terminal,
        /// <summary>Hands on a lock, a grille, a robot, a teammate: holding E with the progress growing.</summary>
        Work,
    }

    /// <summary>
    /// A rigged human shown in place of the player model (Tools/Blender/human.py; a humanoid Avatar on the common skeleton,
    /// <see cref="HumanSkeleton"/>). Purely visual: <see cref="PlayerController"/> feeds it the speed, crouch, downed state,
    /// what the body is busy with, the look pitch and whether the hand holds something every frame; it drives the
    /// Animator's layers (<see cref="HumanLayers"/>: the legs, the upper body, the head, the right hand's grip), carries the
    /// held thing in the right hand by IK (<see cref="OnIK"/>), and after the animation puts the feet on the floor, the held
    /// thing in the palm and turns the head where the player looks. Its parts are named like the player model's
    /// (Body…, Accent…, Eye…) so <see cref="PlayerLooks.Paint"/> colours them per player.
    /// </summary>
    public class HumanRig : MonoBehaviour
    {
        public Animator Animator;
        [Tooltip("The head bone: tilted by the look pitch after the animation")]
        public Transform HeadBone;
        [Tooltip("How much of the look pitch the head follows")]
        [Range(0, 1)] public float HeadFollow = 0.7f;
        [Tooltip("The hips (the skeleton's root bone): moved up or down after the animation so the lower foot stands on the floor")]
        public Transform Hips;
        public Transform FootL, FootR;
        [Tooltip("The toes' bones (their origin is the ball of the foot): a foot standing on its ball is planted by it, not by its raised ankle")]
        public Transform ToeL, ToeR;
        [Tooltip("How high the ankle (the foot bone's origin) is over the sole when standing, metres in the rig's space (the builder measures it in the Idle take)")]
        public float AnkleHeight = 0.08f;
        [Tooltip("How high the ball of the foot (the toe bone's origin) is over the sole when standing, metres in the rig's space (the builder measures it in the Idle take)")]
        public float BallHeight = 0.02f;
        [Tooltip("The right hand and its finger bone: the held thing lies in the palm between them")]
        public Transform HandR, FingersR;
        [Tooltip("The chest's bone: the hand carries a thing before it, wherever the take puts it (standing, crouched, bowed)")]
        public Transform Chest;
        [Tooltip("Where the chest's bone is when standing, in the body's space (metres for the model's own size; the builder measures it in the Idle take)")]
        public Vector3 ChestStanding = new(0f, 1.25f, 0f);

        /// <summary>The held thing's anchor this rig carries in its right hand (another player's; null: nothing to place).</summary>
        [System.NonSerialized] public Transform Held;
        /// <summary>
        /// The owner's own view: the eyes this body hangs from. Every frame, after the animation, the body is moved so that
        /// its head is at them (a take leans and bobs the head away from the fixed camera: looking down, the player sees
        /// his own chest, arms and legs under him, never the back of his own head). Null: where the animation has it.
        /// </summary>
        [System.NonSerialized] public Transform Eyes;
        /// <summary>The owner's own view: the right arm is the view's own hand model just now — this body's is folded away.</summary>
        [System.NonSerialized] public bool HideRightArm;
        Transform m_ArmR;

        /// <summary>
        /// The car's steering wheel this body drives with, in the world (<see cref="HoldWheel"/>, every frame; <see cref="LetGoOfWheel"/>):
        /// where each hand holds its rim now and where it would with the wheel straight, the hub and the steering column
        /// (from the wheel's face to the driver). Both hands go there by IK and turn with the wheel.
        /// </summary>
        struct WheelHands { public Vector3 Left, Right, Left0, Right0, Hub, Column; }
        WheelHands m_WheelAt;
        bool m_Driving;
        float m_Wheel;
        /// <summary>How far the body was moved under the eyes last frame (<see cref="FirstPerson"/>): the hands' targets allow for it.</summary>
        Vector3 m_EyeShift;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int DownId = Animator.StringToHash("Down");
        static readonly int SitId = Animator.StringToHash("Sit");
        static readonly int PoseId = Animator.StringToHash("Pose");
        static readonly int ClimbId = Animator.StringToHash("ClimbSpeed");
        static readonly int CupId = Animator.StringToHash("Cup");
        static readonly int StrideId = Animator.StringToHash("Stride");

        /// <summary>The held thing is a compact one: it lies in the open palm (a long one goes through the fist).</summary>
        [System.NonSerialized] public bool Cupped;

        float m_Pitch, m_Ground, m_Hold;

        /// <summary>The driver's hands on the wheel this frame (world points; see <see cref="HumanMath.WheelWrist"/>).</summary>
        public void HoldWheel(Vector3 left, Vector3 right, Vector3 left0, Vector3 right0, Vector3 hub, Vector3 column)
        {
            m_WheelAt = new WheelHands { Left = left, Right = right, Left0 = left0, Right0 = right0, Hub = hub, Column = column.normalized };
            m_Driving = true;
        }

        /// <summary>Not driving: the hands go back to what the take does with them.</summary>
        public void LetGoOfWheel() => m_Driving = false;
        bool m_Planted, m_Holding;
        static readonly RaycastHit[] s_Floor = new RaycastHit[6];

        /// <summary>The state of the player this frame. <paramref name="climb"/>: metres a second up the ladder (negative: down).</summary>
        public void Pose(float speed, bool crouching, bool downed, float pitch, bool seated = false, BodyPose pose = BodyPose.None, float climb = 0f,
                         bool holding = false)
        {
            pose = HumanMath.Shown(pose, downed, seated);
            m_Planted = HumanMath.Planted(downed, seated, pose);
            m_Holding = HumanMath.Carries(holding, downed, seated, pose);
            m_Hold = Mathf.MoveTowards(m_Hold, m_Holding ? 1f : 0f, Time.deltaTime * 4f);
            m_Wheel = Mathf.MoveTowards(m_Wheel, m_Driving && seated && !downed ? 1f : 0f, Time.deltaTime * HumanMath.WheelReachRate);
            m_Pitch = HumanMath.HeadPitch(pitch, HumanMath.HeadShare(HeadFollow, pose), downed);
            if (Animator == null || !Animator.isActiveAndEnabled) return;
            // in a seat the body rides with the car: its speed is the car's, not a walk
            float walk = HumanMath.WalkSpeed(speed, seated || pose != BodyPose.None);
            Animator.SetFloat(SpeedId, walk, 0.1f, Time.deltaTime);
            // faster than the take steps: the steps quicken, the feet do not slide
            Animator.SetFloat(StrideId, HumanMath.StrideRate(walk, crouching), 0.1f, Time.deltaTime);
            Animator.SetBool(CrouchId, crouching && !downed && !seated && pose == BodyPose.None);
            Animator.SetBool(DownId, downed);
            Animator.SetBool(SitId, seated && !downed);
            Animator.SetInteger(PoseId, (int)pose);
            Animator.SetFloat(ClimbId, HumanMath.ClimbRate(climb), 0.08f, Time.deltaTime);
            // the right fingers close round the wheel's rim as they do round a handle (the fist, not the cupped palm)
            if (Animator.layerCount > HumanLayers.RightHand) Animator.SetLayerWeight(HumanLayers.RightHand, Mathf.Max(m_Hold, m_Wheel));
            Animator.SetFloat(CupId, Cupped && m_Wheel <= 0f ? 1f : 0f, 0.08f, Time.deltaTime);
        }

        /// <summary>Skips the blend of the hold (a still picture: Stealth → Human Pose Pictures).</summary>
        public void Settle()
        {
            m_Hold = m_Holding ? 1f : 0f;
            m_Wheel = m_Driving ? 1f : 0f;
            if (Animator != null && Animator.layerCount > HumanLayers.RightHand) Animator.SetLayerWeight(HumanLayers.RightHand, Mathf.Max(m_Hold, m_Wheel));
            if (Animator != null) Animator.SetFloat(CupId, Cupped && m_Wheel <= 0f ? 1f : 0f);
        }

        /// <summary>
        /// The IK pass of a layer (<see cref="HumanIK"/> relays it): the upper body's carries what the hand holds before the
        /// chest (the right hand to <see cref="HumanSkeleton.Carry"/>, the elbow out to the side).
        /// </summary>
        public void OnIK(int layer)
        {
            if (Animator == null || !Animator.isHuman) return;
            var body = transform;
            float size = Animator.transform.lossyScale.y; // (a person's height is the model's scale)
            // before the chest where the take has it now (the bones still hold the last frame's pose here: near enough)
            var chest = Chest != null && size > 1e-4f ? body.InverseTransformPoint(Chest.position) / size : ChestStanding;
            Vector3 At(Vector3 standing) => body.position + body.rotation * (HumanSkeleton.CarryAt(standing, chest, ChestStanding) * size);
            if (layer == HumanLayers.UpperBody && m_Wheel > 0f)
            {
                OnTheWheel(size);
                return;
            }
            if (layer == HumanLayers.UpperBody)
            {
                Animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                Animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, m_Hold);
                Animator.SetIKRotationWeight(AvatarIKGoal.RightHand, m_Hold);
                Animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, m_Hold);
                if (m_Hold <= 0f) return;
                Animator.SetIKPosition(AvatarIKGoal.RightHand, At(HumanSkeleton.Carry));
                Animator.SetIKRotation(AvatarIKGoal.RightHand, body.rotation * HumanSkeleton.CarryHand);
                Animator.SetIKHintPosition(AvatarIKHint.RightElbow, At(HumanSkeleton.CarryElbow));
            }
            // (the head's look is not an IK look-at: on this Avatar it bowed every head — the head is turned after the
            // animation instead, LateUpdate)
        }

        /// <summary>
        /// Both hands on the wheel's rim (the IK pass of the upper body): the wrists where the fingers close round the rim
        /// (<see cref="HumanMath.WheelWrist"/>), turned with the wheel from the way the Sit take holds them, the elbows low and
        /// out. The owner's body is moved under the eyes after the animation: the targets allow for last frame's shift.
        /// </summary>
        void OnTheWheel(float size)
        {
            var w = m_WheelAt;
            var shift = Eyes != null ? m_EyeShift : Vector3.zero;
            var body = transform;
            var chest = Chest != null ? Chest.position : body.position + body.up * (ChestStanding.y * 0.75f * size);
            Hand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, w.Left, w.Left0, -1f, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleDistal);
            Hand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, w.Right, w.Right0, 1f, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleDistal);

            void Hand(AvatarIKGoal goal, AvatarIKHint hint, Vector3 at, Vector3 at0, float side, HumanBodyBones wristBone, HumanBodyBones knuckleBone, HumanBodyBones tipBone)
            {
                // the wrist where the fist's hollow goes round the rim: the hollow from the hand's own bones (last frame's
                // pose: the hand turns slowly), or the rule of thumb before the bones are there
                var wrist = HumanMath.WheelWrist(at, w.Hub, w.Column, size);
                var hand = Animator.GetBoneTransform(wristBone);
                var knuckle = Animator.GetBoneTransform(knuckleBone);
                var tip = Animator.GetBoneTransform(tipBone);
                if (hand != null && knuckle != null && tip != null)
                    wrist = at - (HumanMath.FistHollow(hand.position, knuckle.position, tip.position) - hand.position);
                // (the take's hand as it holds a wheel, turned as far as the rim turned under it)
                var turn = Quaternion.FromToRotation(at0 - w.Hub, at - w.Hub);
                var rotation = turn * Animator.GetIKRotation(goal);
                var elbow = HumanSkeleton.WheelElbow;
                Animator.SetIKPositionWeight(goal, m_Wheel);
                Animator.SetIKRotationWeight(goal, m_Wheel);
                Animator.SetIKHintPositionWeight(hint, m_Wheel);
                Animator.SetIKPosition(goal, wrist - shift);
                Animator.SetIKRotation(goal, rotation);
                Animator.SetIKHintPosition(hint, chest - shift + (body.right * (side * elbow.x) + body.up * elbow.y + body.forward * elbow.z) * size);
            }
        }

        void LateUpdate()
        {
            PlantFeet();
            FirstPerson();
            PlaceHeld();
            // the head follows the look after the animation (over whatever the Head layer's take does)
            if (HeadBone == null || Mathf.Approximately(m_Pitch, 0f)) return;
            HeadBone.rotation = Quaternion.AngleAxis(m_Pitch, transform.right) * HeadBone.rotation;
        }

        /// <summary>After the animation and the feet: the owner's own body under his eyes (<see cref="Eyes"/>, <see cref="HideRightArm"/>).</summary>
        void FirstPerson()
        {
            if (m_ArmR == null && Animator != null && Animator.isHuman) m_ArmR = Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (m_ArmR != null)
            {
                var scale = Eyes != null && HideRightArm ? Vector3.one * 1e-4f : Vector3.one;
                if (m_ArmR.localScale != scale) m_ArmR.localScale = scale;
            }
            if (Eyes == null || HeadBone == null || Hips == null) return;
            m_EyeShift = HumanMath.EyeShift(Eyes.position, HeadBone.position, transform.forward, transform.lossyScale.y);
            Hips.position += m_EyeShift;
        }

        /// <summary>
        /// After the animation and the feet: another player's held thing in this body's right hand (in its closed fingers),
        /// facing the body's way tipped with the look — not floating at the head.
        /// </summary>
        void PlaceHeld()
        {
            if (Held == null || HandR == null || FingersR == null || !m_Holding) return;
            Held.SetPositionAndRotation(HeldSpot(),
                Quaternion.AngleAxis(m_Pitch * 0.6f, transform.right) * transform.rotation);
        }

        /// <summary>Where the held thing's grip is, in the world: in the closed fingers of the right hand (<see cref="HumanMath.Grip"/>).</summary>
        public Vector3 HeldSpot() => HandR == null || FingersR == null ? transform.position + Vector3.up
            : HumanMath.Grip(HandR.position, FingersR.position, transform.up, Animator != null ? Animator.transform.lossyScale.y : 1f, Cupped);

        /// <summary>
        /// After the animation: the lower sole onto the floor under the body. The player's capsule floats a skin's width over
        /// the floor (and a body set down on a spot a few centimetres more), so the take alone left the feet in the air.
        /// A sole is where its foot stands — on its ankle's height when flat, on its ball when the heel is up
        /// (<see cref="HumanMath.Sole"/>); the body comes up at once and goes down slowly, so a run keeps its flight
        /// (<see cref="HumanMath.PlantFollow"/>).
        /// </summary>
        void PlantFeet()
        {
            if (Hips == null || FootL == null || FootR == null) return;
            float want = 0f;
            if (m_Planted && Floor(out float floor))
            {
                float size = transform.lossyScale.y;
                float sole = Mathf.Min(SoleOf(FootL, ToeL, size), SoleOf(FootR, ToeR, size));
                want = HumanMath.PlantShift(sole, floor, 0f);
            }
            m_Ground = HumanMath.PlantFollow(m_Ground, want, Time.deltaTime);
            if (Mathf.Abs(m_Ground) > 1e-4f) Hips.position += Vector3.up * m_Ground;
        }

        float SoleOf(Transform foot, Transform toe, float size) =>
            HumanMath.Sole(foot.position.y, toe != null ? toe.position.y : float.PositiveInfinity, AnkleHeight * size, BallHeight * size);

        /// <summary>The floor under the body (not the player's own colliders), within a step below its feet.</summary>
        bool Floor(out float y)
        {
            y = 0f;
            var from = transform.position + Vector3.up * 0.6f;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, s_Floor, 1.0f, ~(1 << Level.LevelMetrics.CeilingLayer), QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            var owner = transform.parent != null ? transform.parent.root : transform;
            for (int i = 0; i < n; i++)
            {
                if (s_Floor[i].collider.transform.IsChildOf(owner)) continue;
                if (s_Floor[i].point.y > best) best = s_Floor[i].point.y;
            }
            if (float.IsNegativeInfinity(best)) return false;
            y = best;
            return true;
        }
    }

    /// <summary>Rules of the rigged human (pure, unit tested).</summary>
    public static class HumanMath
    {
        /// <summary>The Climb take is one step of each hand: this many metres of ladder, at this many metres a second.</summary>
        public const float ClimbCycle = 0.6f, ClimbTakeSpeed = 0.5f;

        /// <summary>How far the head tilts for a look pitch (degrees, + looks down): a share of it, clamped; none when lying.</summary>
        public static float HeadPitch(float lookPitch, float follow, bool downed) =>
            downed ? 0f : Mathf.Clamp(lookPitch * follow, -45f, 45f);

        /// <summary>The speed the walk blend gets: none in a seat (riding in the car is not walking) or busy with the hands.</summary>
        public static float WalkSpeed(float speed, bool seated) => seated ? 0f : speed;

        /// <summary>What the body is busy with: a ladder first, then a terminal, then the hands at work.</summary>
        public static BodyPose PoseOf(bool climbing, bool atTerminal, bool working) =>
            climbing ? BodyPose.Ladder : atTerminal ? BodyPose.Terminal : working ? BodyPose.Work : BodyPose.None;

        /// <summary>The pose the rig shows: lying or seated, the body is busy with nothing else.</summary>
        public static BodyPose Shown(BodyPose pose, bool downed, bool seated) => downed || seated ? BodyPose.None : pose;

        /// <summary>
        /// How fast the Climb take plays for a climbing speed (metres a second up, negative down): its own speed at
        /// <see cref="ClimbTakeSpeed"/>, backwards on the way down, still when the player hangs on the ladder.
        /// </summary>
        public static float ClimbRate(float metresPerSecond) =>
            Mathf.Abs(metresPerSecond) < 0.03f ? 0f : Mathf.Clamp(metresPerSecond / ClimbTakeSpeed, -3f, 3f);

        /// <summary>
        /// How much of the look pitch the head follows in a pose: on a ladder the eyes go where the player looks; at a
        /// terminal and at work the take moves the head (the player's camera is on the terminal's screen, not in the head).
        /// </summary>
        public static float HeadShare(float follow, BodyPose pose) => pose is BodyPose.Terminal or BodyPose.Work ? 0f : follow;

        /// <summary>
        /// The hand carries what it holds before the chest: holding something and free — not lying, not seated, not on a
        /// ladder, at a terminal or at work (those takes have the hands busy).
        /// </summary>
        public static bool Carries(bool holding, bool downed, bool seated, BodyPose pose) => holding && !downed && !seated && pose == BodyPose.None;

        /// <summary>
        /// Where a held thing's grip is in the carrying hand (palm up, the fingers folded over it): by the knuckles, a
        /// little over the palm — a long thing goes through the folded fingers there, a compact one rests on them.
        /// <paramref name="size"/>: the person's scale.
        /// </summary>
        /// <summary>Where the eyes are from the head bone (the skull's base): up and ahead, metres of a person of scale 1.</summary>
        public const float EyesUp = 0.1f, EyesAhead = 0.05f;
        /// <summary>The furthest a body is moved to bring its head to its owner's eyes, metres (a pose that leaves the camera far behind is left alone past it).</summary>
        public const float EyeReach = 0.8f;

        /// <summary>How to move a body so that its eyes are at <paramref name="eyes"/> (pure): its head bone is at <paramref name="head"/>, it faces <paramref name="forward"/>.</summary>
        public static Vector3 EyeShift(Vector3 eyes, Vector3 head, Vector3 forward, float size) =>
            Vector3.ClampMagnitude(eyes - (head + Vector3.up * (EyesUp * size) + forward * (EyesAhead * size)), EyeReach);

        /// <summary>
        /// A part of the body its owner must not see from inside his own head (pure, by the renderer's name, "part.slot"):
        /// the eyeballs and what is worn on the head — hair, beard, a hat, glasses. They still cast their shadow. The skin
        /// of the head itself is seen from inside: its faces look away.
        /// </summary>
        public static bool OnTheHead(string renderer)
        {
            if (string.IsNullOrEmpty(renderer)) return false;
            if (renderer.StartsWith("Sclera", System.StringComparison.Ordinal)) return true;
            int dot = renderer.LastIndexOf('.');
            string slot = dot >= 0 ? renderer.Substring(dot + 1) : "";
            return slot is "hair" or "beard" or "head" or "eyes";
        }

        public static Vector3 Grip(Vector3 hand, Vector3 fingers, Vector3 up, float size, bool cupped = false) =>
            Vector3.Lerp(hand, fingers, cupped ? CupAlong : GripAlong) + up * ((cupped ? CupOverPalm : GripOverPalm) * size);

        /// <summary>
        /// The fist grips by the knuckles, at the height of the hand. A cupped palm carries its thing over its middle: the
        /// place of the thing is counted from the top of a fist (PlayerController.HeldLocal), and the skin of an open palm
        /// lies that much lower.
        /// </summary>
        public const float GripAlong = 0.9f, GripOverPalm = 0f, CupAlong = 0.62f, CupOverPalm = -0.022f;

        /// <summary>
        /// Where the wrist goes for the fingers to close round the wheel's rim at <paramref name="rim"/>: the fist lies on the
        /// rim with the hand reaching forward over it (towards the wheel's face, down the column), so the wrist is
        /// <see cref="WheelWristBack"/> back towards the driver and <see cref="WheelWristOut"/> out from the hub — the rim
        /// in the palm, not under the wrist. Metres for the model's own size (<paramref name="size"/>: a person's height).
        /// </summary>
        public static Vector3 WheelWrist(Vector3 rim, Vector3 hub, Vector3 column, float size)
        {
            column = column.normalized;
            var outward = Vector3.ProjectOnPlane(rim - hub, column);
            outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector3.zero;
            return rim + (column * WheelWristBack + outward * WheelWristOut) * size;
        }

        /// <summary>
        /// The hollow of a closed fist (what a rim or a bar goes through): halfway between the knuckles' line (most of the
        /// way from the wrist to the middle finger's first joint) and that finger's last joint, curled in under it.
        /// </summary>
        public static Vector3 FistHollow(Vector3 wrist, Vector3 knuckle, Vector3 fingerTip) =>
            (Vector3.Lerp(wrist, knuckle, 0.9f) + fingerTip) * 0.5f;

        /// <summary>The wrist from the rim it holds: along the column towards the driver and out from the hub, metres.</summary>
        public const float WheelWristBack = 0.07f, WheelWristOut = 0.02f;

        /// <summary>How fast the hands go to the wheel when the driver sits down (and back when getting out), per second.</summary>
        public const float WheelReachRate = 3f;

        /// <summary>Standing on its feet: not lying, not seated, not on a ladder — the feet are put on the floor.</summary>
        public static bool Planted(bool downed, bool seated, BodyPose pose) => !downed && !seated && pose != BodyPose.Ladder;

        /// <summary>
        /// How far to move the body so the lower ankle stands <paramref name="ankleHeight"/> over the floor (− down), clamped
        /// to a few centimetres: it only takes up the capsule's float, it does not drag a falling body to the ground.
        /// </summary>
        public static float PlantShift(float lowerAnkle, float floor, float ankleHeight) =>
            Mathf.Clamp(floor + ankleHeight - lowerAnkle, -0.15f, 0.08f);

        /// <summary>
        /// The speeds the walking takes step at, metres a second (Tools/Blender/humanlib/anim.py: a planted foot goes back
        /// under the body this fast): where the blend of the legs plays each take, so the feet do not slide.
        /// </summary>
        public const float WalkTakeSpeed = 1.5f, RunTakeSpeed = 4.2f, CrouchTakeSpeed = 0.95f;

        /// <summary>
        /// How fast the walking takes play for a speed: their own rate up to the fastest take's speed (the blend slows the
        /// steps below it), quicker steps above it — a sprint, a crouched hurry — up to twice.
        /// </summary>
        public static float StrideRate(float speed, bool crouching) =>
            Mathf.Clamp(Mathf.Abs(speed) / (crouching ? CrouchTakeSpeed : RunTakeSpeed), 1f, 2f);

        /// <summary>
        /// The height of the floor a foot stands on by its bones: under the ankle when the foot is flat, under the ball when
        /// the heel is up (the lower of the two; a rig without toe bones passes +infinity for the ball).
        /// </summary>
        public static float Sole(float ankle, float ball, float ankleHeight, float ballHeight) =>
            Mathf.Min(ankle - ankleHeight, ball - ballHeight);

        /// <summary>
        /// The body's shift follows the wanted one: up at once (a foot never stays under the floor), down slowly — both feet
        /// in the air for a moment (a run's flight, a step off a kerb) do not pull the body after them.
        /// </summary>
        public static float PlantFollow(float now, float want, float dt) =>
            Mathf.Lerp(now, want, 1f - Mathf.Exp(-(want > now ? PlantUpRate : PlantDownRate) * Mathf.Max(0f, dt)));

        /// <summary>How fast the planted body comes up out of the floor and goes down to it, per second.</summary>
        public const float PlantUpRate = 30f, PlantDownRate = 4f;

        /// <summary>The progress of a hold is growing: the hands are at work (it is kept when E is let go, so its being there is not work).</summary>
        public static bool Growing(float now, float before) => now > before + 1e-5f;
    }
}
