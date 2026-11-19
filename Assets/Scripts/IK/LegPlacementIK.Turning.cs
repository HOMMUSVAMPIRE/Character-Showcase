using UnityEngine;

namespace MeridianMotion.Characters
{
    // Paw placement: bending the body into a turn.
    public partial class LegPlacementIK
    {
        /// <summary>
        /// Bends the body into the way the model is turning (it turns after the character): each of Turn Bend Bones
        /// turned about the model's up by its share of the bend, eased, as far as Max Turn Bend at Full Bend Turn Speed.
        /// Before the legs are placed, so they're placed from the bent body.
        /// </summary>
        private void BendIntoTurn(float deltaTime)
        {
            float yaw = transform.eulerAngles.y;
            float rate = hasLastYaw && deltaTime > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / deltaTime : 0f;
            lastYaw = yaw;
            hasLastYaw = true;
            float goal = Mathf.Clamp(rate / fullBendTurnSpeed, -1f, 1f) * maxTurnBend;
            turnBend = turnBendSmoothTime > 0f
                ? Mathf.SmoothDamp(turnBend, goal, ref turnBendVelocity, turnBendSmoothTime, Mathf.Infinity, deltaTime)
                : goal;
            if (Mathf.Abs(turnBend) < 1e-3f) { return; }
            Vector3 up = transform.up;
            foreach (TurnBendBone bend in turnBendBones)
            {
                if (bend?.bone == null || bend.share == 0f) { continue; }
                bend.bone.rotation = Quaternion.AngleAxis(turnBend * bend.share, up) * bend.bone.rotation;
            }
        }
    }
}
