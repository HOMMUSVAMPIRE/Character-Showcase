using UnityEngine;

namespace MeridianMotion.Characters
{
    // Paw placement: running along a wall (the body rolled to it, the paws onto its face).
    public partial class LegPlacementIK
    {
        /// <summary>
        /// Running along a wall: the body rolled toward the wall (about its legs' tops, along the way it faces) as far as
        /// its legs need to reach the wall (Max Wall Roll at most), eased in and out over Wall Roll Blend Time; and each
        /// paw kept out of the wall, put on its face where the animation has it through it, and drawn onto it as it's
        /// planted (low, in the animation), so the character runs on the wall, paws to it.
        /// </summary>
        private void SolveWallRun(CharacterAnimationState state, float floor, float scale, float deltaTime)
        {
            bool running = state.wallRunning && state.wallNormal != Vector3.zero;
            if (running)
            {
                wallNormal = state.wallNormal;
                wallPoint = state.wallPoint;
            }
            wallWeight = wallRollBlendTime > 0f ? Mathf.MoveTowards(wallWeight, running ? 1f : 0f, deltaTime / wallRollBlendTime) : running ? 1f : 0f;
            if (wallWeight <= 0f || wallNormal == Vector3.zero) { return; }

            // Where the legs' tops are, and how long the legs are as animated (to their paws).
            Vector3 pivot = Vector3.zero;
            float legLength = 0f;
            int count = 0;
            for (int i = 0; i < feet.Length; i++)
            {
                Leg leg = legs[i];
                if (!IsSolvable(leg)) { continue; }
                pivot += leg.upper.position;
                legLength += Vector3.Distance(leg.upper.position, leg.foot.TransformPoint(leg.contact));
                count++;
            }
            if (count == 0) { return; }
            pivot /= count;
            legLength /= count;

            // Rolled so the legs, hanging from their tops, reach the wall: the roll whose sine is the wall's distance
            // over the legs' length. Toward the wall: the body's down turned to point at it.
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
            if (forward == Vector3.zero) { return; }
            float distance = Vector3.Dot(pivot - wallPoint, wallNormal);
            float roll = Mathf.Min(Mathf.Asin(Mathf.Clamp01(distance / Mathf.Max(legLength, 1e-3f))) * Mathf.Rad2Deg, maxWallRoll) * wallWeight;
            if (Vector3.Dot(Quaternion.AngleAxis(roll, forward) * Vector3.down, wallNormal) > 0f) { roll = -roll; }
            Quaternion rolled = Quaternion.AngleAxis(roll, forward);

            // Each paw's lift as animated (before the roll), for how planted it is.
            float plantedLift = plantedHeight * scale, liftedLift = Mathf.Max(liftedHeight * scale, plantedLift + 1e-3f);
            for (int i = 0; i < feet.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.foot == null) { continue; }
                feet[i].lift = leg.foot.TransformPoint(leg.contact).y - floor;
            }
            rootBone.SetPositionAndRotation(pivot + rolled * (rootBone.position - pivot), rolled * rootBone.rotation);

            for (int i = 0; i < feet.Length; i++)
            {
                Leg leg = legs[i];
                if (!IsSolvable(leg)) { continue; }
                Vector3 contact = leg.foot.TransformPoint(leg.contact);
                // Out from the wall's face (less the paw's thickness beyond its contact, which would go through it, the
                // paw rolled to face the wall); negative, through it.
                float off = Vector3.Dot(contact - wallPoint, wallNormal) - leg.wallClearance * scale;
                float planted = 1f - Mathf.InverseLerp(plantedLift, liftedLift, feet[i].lift);
                float onto = off < 0f ? off : off * planted * wallWeight;
                if (Mathf.Abs(onto) < 1e-4f) { continue; }
                Vector3 target = contact - wallNormal * onto;
                Quaternion footRotation = leg.foot.rotation;
                Vector3 footTarget = target - footRotation * Vector3.Scale(leg.contact, leg.foot.lossyScale);
                Vector3 bend = leg.bendDirection == Vector3.zero ? Vector3.zero : rolled * transform.TransformDirection(leg.bendDirection);
                SolveTwoBone(leg.upper, leg.lower, leg.foot, footTarget, bend);
                leg.foot.rotation = footRotation;
            }
        }
    }
}
