using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.UISet.Gizmos;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.Comps;

[StaticConstructorOnStartup]
public class CompGrayMechShield : ThingComp
{
    protected float energy;
    protected int ticksToReset = -1;
    protected int lastKeepDisplayTick = -9999;

    private Vector3 impactAngleVect;
    private int lastAbsorbDamageTick = -9999;

    private const int JitterDurationTicks = 8;
    private const int KeepDisplayingTicks = 1000;
    private static readonly Material BubbleMat = MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent);

    public CompPropertiesGrayMechShield Props => (CompPropertiesGrayMechShield)props;
    public float Energy => energy;
    public float EnergyMax => parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax);
    public float EnergyRechargeRate => parent.GetStatValue(StatDefOf.EnergyShieldRechargeRate);
    public bool HasShieldCapacity => EnergyMax > 0.001f;

    private float EnergyGainPerTick => EnergyRechargeRate / 60f;

    private Pawn PawnOwner => parent as Pawn;

    public ShieldState ShieldState
    {
        get
        {
            Pawn pawnOwner = PawnOwner;
            if (pawnOwner == null || !HasShieldCapacity)
            {
                return ShieldState.Disabled;
            }

            if (pawnOwner.IsCharging() || pawnOwner.IsSelfShutdown())
            {
                return ShieldState.Disabled;
            }

            CompCanBeDormant dormant = parent.GetComp<CompCanBeDormant>();
            if (dormant != null && !dormant.Awake)
            {
                return ShieldState.Disabled;
            }

            return ticksToReset <= 0 ? ShieldState.Active : ShieldState.Resetting;
        }
    }

    protected bool ShouldDisplay
    {
        get
        {
            Pawn pawnOwner = PawnOwner;
            if (pawnOwner == null || !pawnOwner.Spawned || pawnOwner.Dead || pawnOwner.Downed || !HasShieldCapacity || energy <= 0.001f)
            {
                return false;
            }

            if (pawnOwner.InAggroMentalState || pawnOwner.Drafted)
            {
                return true;
            }

            if (pawnOwner.Faction != null && pawnOwner.Faction.HostileTo(Faction.OfPlayer) && !pawnOwner.IsPrisoner)
            {
                return true;
            }

            if (Find.TickManager.TicksGame < lastKeepDisplayTick + KeepDisplayingTicks)
            {
                return true;
            }

            return ModsConfig.BiotechActive && pawnOwner.IsColonyMech && Find.Selector.SingleSelectedThing == pawnOwner;
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref energy, "energy", 0f);
        Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
        Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick", -9999);
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (!respawningAfterLoad && HasShieldCapacity)
        {
            energy = EnergyMax;
            ticksToReset = -1;
        }
        else if (!HasShieldCapacity)
        {
            energy = 0f;
            ticksToReset = -1;
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo item in base.CompGetGizmosExtra())
        {
            yield return item;
        }

        Pawn pawnOwner = PawnOwner;
        if (pawnOwner != null && HasShieldCapacity && Find.Selector.SingleSelectedThing == pawnOwner)
        {
            yield return new Gizmo_GrayMechShieldStatus
            {
                shield = this
            };
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        if (PawnOwner == null || !HasShieldCapacity)
        {
            energy = 0f;
            ticksToReset = -1;
            return;
        }

        if (ShieldState == ShieldState.Resetting)
        {
            ticksToReset--;
            if (ticksToReset <= 0)
            {
                Reset();
            }

            return;
        }

        if (ShieldState == ShieldState.Active)
        {
            energy += EnergyGainPerTick;
            if (energy > EnergyMax)
            {
                energy = EnergyMax;
            }
        }
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = false;
        if (ShieldState != ShieldState.Active || PawnOwner == null || !HasShieldCapacity || energy <= 0.001f)
        {
            return;
        }

        if (dinfo.Def == DamageDefOf.EMP)
        {
            energy = 0f;
            Break();
            return;
        }

        if (!dinfo.Def.ignoreShields && (dinfo.Def.isRanged || dinfo.Def.isExplosive))
        {
            energy -= dinfo.Amount * Props.energyLossPerDamage;
            if (energy < 0f)
            {
                Break();
            }
            else
            {
                AbsorbedDamage(dinfo);
            }

            absorbed = true;
        }
    }

    public override void PostDraw()
    {
        base.PostDraw();
        DrawShieldBubble();
    }

    private void DrawShieldBubble()
    {
        if (!ShouldDisplay || ShieldState != ShieldState.Active)
        {
            return;
        }

        float drawScale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, Mathf.Clamp01(energy / Mathf.Max(EnergyMax, 0.001f)));
        Vector3 drawPos = PawnOwner.Drawer.DrawPos;
        drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        int ticksSinceAbsorb = Find.TickManager.TicksGame - lastAbsorbDamageTick;
        if (ticksSinceAbsorb < JitterDurationTicks)
        {
            float jitter = (float)(JitterDurationTicks - ticksSinceAbsorb) / JitterDurationTicks * 0.05f;
            drawPos += impactAngleVect * jitter;
            drawScale -= jitter;
        }

        Matrix4x4 matrix = default;
        matrix.SetTRS(drawPos, Quaternion.AngleAxis(Rand.Range(0, 360), Vector3.up), new Vector3(drawScale, 1f, drawScale));
        Graphics.DrawMesh(MeshPool.plane10, matrix, BubbleMat, 0);
    }

    private void KeepDisplaying()
    {
        lastKeepDisplayTick = Find.TickManager.TicksGame;
    }

    private void AbsorbedDamage(DamageInfo dinfo)
    {
        SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
        impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
        Vector3 loc = PawnOwner.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
        float flashScale = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
        FleckMaker.Static(loc, PawnOwner.Map, FleckDefOf.ExplosionFlash, flashScale);
        int dustCount = (int)flashScale;
        for (int i = 0; i < dustCount; i++)
        {
            FleckMaker.ThrowDustPuff(loc, PawnOwner.Map, Rand.Range(0.8f, 1.2f));
        }

        lastAbsorbDamageTick = Find.TickManager.TicksGame;
        KeepDisplaying();
    }

    private void Break()
    {
        if (parent.Spawned)
        {
            float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, Mathf.Clamp01(energy / Mathf.Max(EnergyMax, 0.001f)));
            EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, scale);
            FleckMaker.Static(PawnOwner.TrueCenter(), PawnOwner.Map, FleckDefOf.ExplosionFlash, 12f);
            for (int i = 0; i < 6; i++)
            {
                FleckMaker.ThrowDustPuff(PawnOwner.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), PawnOwner.Map, Rand.Range(0.8f, 1.2f));
            }
        }

        energy = 0f;
        ticksToReset = Props.startingTicksToReset;
    }

    private void Reset()
    {
        if (PawnOwner != null && PawnOwner.Spawned)
        {
            SoundDefOf.EnergyShield_Reset.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
            FleckMaker.ThrowLightningGlow(PawnOwner.TrueCenter(), PawnOwner.Map, 3f);
        }

        ticksToReset = -1;
        energy = Mathf.Min(Props.energyOnReset, EnergyMax);
    }
}
