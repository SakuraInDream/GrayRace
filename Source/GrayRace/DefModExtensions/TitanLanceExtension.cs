using System.Collections.Generic;
using SD.GrayRace.Verbs;
using UnityEngine;
using Verse;

namespace SD.GrayRace.DefModExtensions;

public class TitanLanceExtension : DefModExtension
{
    public float minPulseDamage;
    public float maxPulseDamage;
    public float armorPenetration;
    public SimpleCurve damageRampCurve;
    public SimpleCurve distanceDamageFactorCurve;

    private ThingDef parentWeaponDef;

    public override void ResolveReferences(Def parentDef)
    {
        base.ResolveReferences(parentDef);
        parentWeaponDef = parentDef as ThingDef;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        if (minPulseDamage <= 0f || maxPulseDamage <= 0f)
        {
            yield return "Titan lance pulse damage values must be greater than zero.";
        }
        else if (maxPulseDamage < minPulseDamage)
        {
            yield return "Titan lance maxPulseDamage must be greater than or equal to minPulseDamage.";
        }

        if (!Mathf.Approximately(armorPenetration, 1f))
        {
            yield return "Titan lance armorPenetration must be exactly 1.";
        }

        foreach (string error in ValidateDamageRampCurve())
        {
            yield return error;
        }

        VerbProperties verbProps = FindTitanLanceVerbProperties();
        if (verbProps == null)
        {
            yield return "Titan lance extension must be attached to a weapon with Verb_GrayTitanLance.";
            yield break;
        }

        if (verbProps.burstShotCount != 24 || verbProps.ticksBetweenBurstShots != 15
            || verbProps.burstShotCount * verbProps.ticksBetweenBurstShots != 360)
        {
            yield return "Titan lance must define 24 pulses at 15 ticks per pulse for a 360-tick beam.";
        }

        if (verbProps.beamDamageDef == null || verbProps.beamDamageDef.defName != "GR_Laser")
        {
            yield return "Titan lance beamDamageDef must reference GR_Laser.";
        }

        if (verbProps.beamMoteDef == null
            || verbProps.beamMoteDef.defName != "Mote_GR_TitanLance"
            || verbProps.beamMoteDef.thingClass == null
            || !typeof(MoteDualAttached).IsAssignableFrom(verbProps.beamMoteDef.thingClass))
        {
            yield return "Titan lance beamMoteDef must reference Mote_GR_TitanLance using MoteDualAttached.";
        }

        foreach (string error in ValidateDistanceCurve(verbProps))
        {
            yield return error;
        }
    }

    private VerbProperties FindTitanLanceVerbProperties()
    {
        List<VerbProperties> verbs = parentWeaponDef?.Verbs;
        if (verbs == null)
        {
            return null;
        }

        for (int i = 0; i < verbs.Count; i++)
        {
            VerbProperties current = verbs[i];
            if (current?.verbClass == typeof(Verb_GrayTitanLance))
            {
                return current;
            }
        }

        return null;
    }

    private IEnumerable<string> ValidateDamageRampCurve()
    {
        if (damageRampCurve == null || damageRampCurve.PointsCount < 2)
        {
            yield return "Titan lance damageRampCurve must contain at least two points.";
            yield break;
        }

        List<CurvePoint> points = damageRampCurve.Points;
        if (!Mathf.Approximately(points[0].x, 0f) || !Mathf.Approximately(points[0].y, 0f)
            || !Mathf.Approximately(points[points.Count - 1].x, 1f)
            || !Mathf.Approximately(points[points.Count - 1].y, 1f))
        {
            yield return "Titan lance damageRampCurve must cover (0, 0) through (1, 1).";
        }

        for (int i = 0; i < points.Count; i++)
        {
            CurvePoint point = points[i];
            if (point.y < 0f || point.y > 1f)
            {
                yield return "Titan lance damageRampCurve factors must stay between 0 and 1.";
                yield break;
            }

            if (i > 0 && (point.x <= points[i - 1].x || point.y < points[i - 1].y))
            {
                yield return "Titan lance damageRampCurve points must increase in X without decreasing in Y.";
                yield break;
            }
        }
    }

    private IEnumerable<string> ValidateDistanceCurve(VerbProperties verbProps)
    {
        if (distanceDamageFactorCurve == null || distanceDamageFactorCurve.PointsCount < 2)
        {
            yield return "Titan lance distanceDamageFactorCurve must contain at least two points.";
            yield break;
        }

        List<CurvePoint> points = distanceDamageFactorCurve.Points;
        if (points[0].x > verbProps.minRange || points[points.Count - 1].x < verbProps.range)
        {
            yield return "Titan lance distanceDamageFactorCurve must cover the complete configured range.";
        }

        for (int i = 0; i < points.Count; i++)
        {
            CurvePoint point = points[i];
            if (point.y <= 0f || point.y > 1f)
            {
                yield return "Titan lance distance damage factors must be greater than 0 and no greater than 1.";
                yield break;
            }

            if (i > 0 && point.x <= points[i - 1].x)
            {
                yield return "Titan lance distanceDamageFactorCurve points must strictly increase in X.";
                yield break;
            }
        }
    }
}
