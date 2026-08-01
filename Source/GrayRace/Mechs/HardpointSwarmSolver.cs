using SD.GrayRace.Defs;
using UnityEngine;

namespace SD.GrayRace.Mechs;

public static class HardpointSwarmSolver
{
    private const int WanderPeriodTicks = 240;
    private const float EngagingMotionMultiplier = 0.2f;
    private const float AnchorAttraction = 0.06f;
    private const float VelocityDamping = 0.82f;
    private const float MaxSpeed = 0.025f;
    private const float TeleportResetDistance = 1.5f;
    private const float TwoPi = Mathf.PI * 2f;

    public static void Initialize(
        MechHardpoint[] hardpoints,
        int count,
        Vector3 pawnDrawPos)
    {
        for (int i = 0; i < count; i++)
        {
            MechHardpoint hp = hardpoints[i];
            Vector3 anchor = ResolveAnchor(pawnDrawPos, hp);
            hp.anchorPosition = anchor;
            hp.swarmPosition = anchor;
            hp.swarmVelocity = Vector3.zero;
        }
    }

    public static void Tick(
        MechHardpoint[] hardpoints,
        int count,
        Vector3 pawnDrawPos,
        GRMechHardpointSwarmSettings settings,
        int ticksGame)
    {
        if (count <= 0)
        {
            return;
        }

        Vector3 nextFirstAnchor = ResolveAnchor(pawnDrawPos, hardpoints[0]);
        if ((nextFirstAnchor - hardpoints[0].anchorPosition).sqrMagnitude
            > TeleportResetDistance * TeleportResetDistance)
        {
            Initialize(hardpoints, count, pawnDrawPos);
            return;
        }

        float wanderRadius = Mathf.Max(0f, settings?.wanderRadius ?? 0.08f);
        float movementLag = Mathf.Clamp01(settings?.movementLag ?? 0.25f);
        float cycle = (ticksGame % WanderPeriodTicks) / (float)WanderPeriodTicks;

        for (int i = 0; i < count; i++)
        {
            MechHardpoint hp = hardpoints[i];
            Vector3 nextAnchor = i == 0 ? nextFirstAnchor : ResolveAnchor(pawnDrawPos, hp);
            Vector3 anchorDelta = nextAnchor - hp.anchorPosition;
            float motionMultiplier = hp.WarmingUp || hp.state == MechHardpoint.State.Firing
                ? EngagingMotionMultiplier
                : 1f;

            hp.swarmPosition += anchorDelta * (1f - movementLag * motionMultiplier);
            hp.anchorPosition = nextAnchor;

            float phase = i / (float)count;
            float angle = (cycle + phase) * TwoPi;
            Vector3 wander = new(
                Mathf.Cos(angle) * wanderRadius * motionMultiplier,
                0f,
                Mathf.Sin(angle) * wanderRadius * motionMultiplier);
            Vector3 desiredPosition = nextAnchor + wander;

            hp.swarmVelocity += (desiredPosition - hp.swarmPosition) * AnchorAttraction;
            hp.swarmVelocity *= VelocityDamping;
            float speedSquared = hp.swarmVelocity.sqrMagnitude;
            if (speedSquared > MaxSpeed * MaxSpeed)
            {
                hp.swarmVelocity *= MaxSpeed / Mathf.Sqrt(speedSquared);
            }

            hp.swarmPosition += hp.swarmVelocity;
        }
    }

    private static Vector3 ResolveAnchor(Vector3 pawnDrawPos, MechHardpoint hp)
    {
        Vector2 anchor = hp?.EffectiveAnchor ?? Vector2.zero;
        return new Vector3(pawnDrawPos.x + anchor.x, 0f, pawnDrawPos.z + anchor.y);
    }
}
