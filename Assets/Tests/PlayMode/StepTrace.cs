using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Character;
using UnityEngine;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// A record of the character's steps (pass Record as a Run's afterStep): where it was, whether it was on the ground
    /// and how the ground check found it, and what collision did (see StepResolution), with measurements of how unsteady
    /// its contact with the ground was: how often it left the ground and came back, how often its body was pushed out of
    /// the world or lifted back out of the ground, how far it overlapped the world, and how much its height wobbled. Can
    /// be written out (Write) as a table, step by step, to compare a run before and after a change to collision.
    /// </summary>
    public sealed class StepTrace
    {
        /// <summary>One step, as it ended.</summary>
        public struct Step
        {
            public Vector3 position;
            public float yaw;
            public bool grounded;
            public CollisionFlags collisions;
            public StepResolution resolution;
            public bool bodyFits;
            /// <summary>
            /// How far (m) the feet are above the ground straight under them (infinity over nothing within reach): more
            /// than nothing where the body rests on something else (a step's edge, or a box's corner on a slope), with
            /// its feet in the air, for the model to make up for.
            /// </summary>
            public float feetClearance;
        }

        // How far above the feet (m) the ray finding the ground under them starts, and how far below them it reaches.
        private const float ClearanceRayLift = 0.25f, ClearanceRayReach = 2f;
        private readonly RaycastHit[] clearanceHits = new RaycastHit[16];

        // Below this (m), a push, lift or change of height is rounding, not movement.
        private const float Noise = 1e-5f;
        // Above this (m), feet on the ground are in the air, as far as the summary counts: more than the skin width any
        // body keeps from the ground.
        public const float FeetAboveGround = 0.03f;

        private readonly CharacterTestRig rig;
        private readonly List<Step> steps = new();

        public StepTrace(CharacterTestRig rig) => this.rig = rig;

        /// <summary>The steps recorded, in order.</summary>
        public IReadOnlyList<Step> Steps => steps;

        /// <summary>Records the step the rig has just run (a Run's afterStep).</summary>
        public void Record(int _)
        {
            MovementData data = rig.Movement.lastFrameMovementData;
            if (data == null) { return; }
            steps.Add(new Step
            {
                position = rig.Position,
                yaw = rig.Body.rotation.eulerAngles.y,
                grounded = data.initialGrounded,
                collisions = data.collisions,
                resolution = data.resolution,
                bodyFits = rig.BodyFits,
                feetClearance = FeetClearance(),
            });
        }

        private float FeetClearance()
        {
            Vector3 origin = rig.Position + Vector3.up * ClearanceRayLift;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, clearanceHits, ClearanceRayLift + ClearanceRayReach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (clearanceHits[i].transform.IsChildOf(rig.Character.transform)) { continue; }
                nearest = Mathf.Min(nearest, clearanceHits[i].distance - ClearanceRayLift);
            }
            return nearest;
        }

        /// <summary>How many steps the character was on the ground with its feet more than the given height (m) above the ground under them.</summary>
        public int StepsWithFeetAbove(float height) => Count(s => s.grounded && s.feetClearance > height);

        /// <summary>The highest (m) the character's feet were above the ground under them while it was on the ground.</summary>
        public float HighestFeet => Max(s => s.grounded && float.IsFinite(s.feetClearance) ? s.feetClearance : 0f);

        // ---- Measurements ----

        /// <summary>How many times the character left the ground or came back to it, from one step to the next.</summary>
        public int GroundedChanges
        {
            get
            {
                int changes = 0;
                for (int i = 1; i < steps.Count; i++) { if (steps[i].grounded != steps[i - 1].grounded) { changes++; } }
                return changes;
            }
        }

        /// <summary>How many steps the character started off the ground.</summary>
        public int AirborneSteps => Count(s => !s.grounded);

        /// <summary>How many steps the ground check found the character on the ground the given way.</summary>
        public int CountGroundedBy(GroundedBy how) => Count(s => s.resolution.groundedBy == how);

        /// <summary>How many steps the body was pushed out of the world before it moved.</summary>
        public int Depenetrations => Count(s => s.resolution.depenetration.sqrMagnitude > Noise * Noise);

        /// <summary>The furthest (m) the body was pushed out of the world in a step.</summary>
        public float DeepestDepenetration => Max(s => s.resolution.depenetration.magnitude);

        /// <summary>How many steps the ground snap lifted the body back up out of the ground.</summary>
        public int SnapLifts => Count(s => s.resolution.snapped && s.resolution.groundSnap < -Noise);

        /// <summary>The furthest (m) the ground snap lifted the body back up out of the ground in a step.</summary>
        public float HighestSnapLift => Max(s => s.resolution.snapped ? -s.resolution.groundSnap : 0f);

        /// <summary>How many steps the body ended overlapping the world (more than the skin width).</summary>
        public int Overlaps => Count(s => !s.bodyFits);

        /// <summary>How many steps collision stepped the body up.</summary>
        public int StepUps => Count(s => s.resolution.steppedUp > Noise);

        /// <summary>How many times a wall started or stopped blocking the character, from one step to the next.</summary>
        public int SideContactChanges
        {
            get
            {
                int changes = 0;
                for (int i = 1; i < steps.Count; i++)
                {
                    if (((steps[i].collisions ^ steps[i - 1].collisions) & CollisionFlags.Sides) != 0) { changes++; }
                }
                return changes;
            }
        }

        /// <summary>The most (m) the character's height changed in a step.</summary>
        public float LargestRise => MaxOver(1, i => Mathf.Abs(steps[i].position.y - steps[i - 1].position.y));

        /// <summary>
        /// The most (m) the character's height changed by in a step from how much it changed by the step before: how
        /// sharply it was jolted up or down. 0 on level ground and along an even slope, however fast it goes.
        /// </summary>
        public float LargestJolt => MaxOver(2, Jolt);

        /// <summary>
        /// How many times the character's height turned from going up to going down or back again: on level ground, or
        /// along a slope, each is a wobble.
        /// </summary>
        public int HeightReversals
        {
            get
            {
                int reversals = 0;
                float lastSign = 0f;
                for (int i = 1; i < steps.Count; i++)
                {
                    float rise = steps[i].position.y - steps[i - 1].position.y;
                    if (Mathf.Abs(rise) <= Noise) { continue; }
                    float sign = Mathf.Sign(rise);
                    if (lastSign != 0f && sign != lastSign) { reversals++; }
                    lastSign = sign;
                }
                return reversals;
            }
        }

        /// <summary>How many times the character's turning changed direction (left to right or back): turning in a tight space, chatter.</summary>
        public int TurnReversals
        {
            get
            {
                int reversals = 0;
                float lastSign = 0f;
                for (int i = 1; i < steps.Count; i++)
                {
                    float turn = Mathf.DeltaAngle(steps[i - 1].yaw, steps[i].yaw);
                    if (Mathf.Abs(turn) <= 0.01f) { continue; }
                    float sign = Mathf.Sign(turn);
                    if (lastSign != 0f && sign != lastSign) { reversals++; }
                    lastSign = sign;
                }
                return reversals;
            }
        }

        private float Jolt(int i) => Mathf.Abs(steps[i].position.y - 2f * steps[i - 1].position.y + steps[i - 2].position.y);

        private int Count(System.Func<Step, bool> which)
        {
            int count = 0;
            foreach (Step s in steps) { if (which(s)) { count++; } }
            return count;
        }

        private float Max(System.Func<Step, float> value)
        {
            float max = 0f;
            foreach (Step s in steps) { max = Mathf.Max(max, value(s)); }
            return max;
        }

        private float MaxOver(int from, System.Func<int, float> value)
        {
            float max = 0f;
            for (int i = from; i < steps.Count; i++) { max = Mathf.Max(max, value(i)); }
            return max;
        }

        /// <summary>The measurements, on one line, for a test's message or log.</summary>
        public string Summary()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return string.Format(c,
                "{0} steps: grounded changes {1}, airborne {2}, grounded by probe {3} / step edge {4} / body rest {5}; " +
                "depenetrations {6} (deepest {7:0.#####} m), snap lifts {8} (highest {9:0.#####} m), overlaps {10}, step-ups {11}, " +
                "wall contact changes {12}; largest rise {13:0.#####} m, largest jolt {14:0.#####} m, height reversals {15}, turn reversals {16}; " +
                "feet above the ground {17} steps (highest {18:0.#####} m)",
                steps.Count, GroundedChanges, AirborneSteps, CountGroundedBy(GroundedBy.Probe), CountGroundedBy(GroundedBy.ProbeOnStepEdge),
                CountGroundedBy(GroundedBy.BodyRest), Depenetrations, DeepestDepenetration, SnapLifts, HighestSnapLift, Overlaps, StepUps,
                SideContactChanges, LargestRise, LargestJolt, HeightReversals, TurnReversals, StepsWithFeetAbove(FeetAboveGround), HighestFeet);
        }

        /// <summary>
        /// Writes the steps out as a table (Logs/StepTraces/[name].csv, under the project: Unity clears Temp as it quits),
        /// with the summary first, and returns where.
        /// </summary>
        public string Write(string name)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "StepTraces"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, name + ".csv");
            CultureInfo c = CultureInfo.InvariantCulture;
            var text = new StringBuilder();
            text.Append("# ").AppendLine(Summary());
            text.AppendLine("step,x,y,z,yaw,grounded,groundedBy,collisions,depenetration,steppedUp,snapped,groundSnap,bodyFits,feetClearance");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                text.AppendLine(string.Join(",", i.ToString(c), F(s.position.x), F(s.position.y), F(s.position.z), F(s.yaw),
                    s.grounded ? "1" : "0", s.resolution.groundedBy.ToString(), ((int)s.collisions).ToString(c),
                    F(s.resolution.depenetration.magnitude), F(s.resolution.steppedUp), s.resolution.snapped ? "1" : "0",
                    F(s.resolution.groundSnap), s.bodyFits ? "1" : "0", float.IsFinite(s.feetClearance) ? F(s.feetClearance) : ""));
            }
            File.WriteAllText(file, text.ToString());
            return file;

            string F(float value) => value.ToString("0.######", c);
        }
    }
}
