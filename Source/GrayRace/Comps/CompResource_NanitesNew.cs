using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Needs;
using SD.GrayRace.UISet.Gizmos;
using UnityEngine;
using Verse;

namespace SD.GrayRace.Comps
{
    // 纳米机械资源实现
    public class CompResource_NanitesNew: ThingComp
    {
        private float _curNanites;

        private Gizmo_NaniteResourcesNew _gizmo;
        private Need_GrayRaceEnergy _energyNeed;

        public Need_GrayRaceEnergy EnergyNeed
        {
            get
            {
                _energyNeed ??= Pawn.needs.TryGetNeed<Need_GrayRaceEnergy>();

                return _energyNeed;
            }
        }
        public Pawn Pawn => parent as Pawn;
        public CompProperties_Nanites Props => (CompProperties_Nanites)props;

        public float Max
        {
            get
            {
                return Pawn.GetStatValue(GrayRaceDefOf.GRStat_NaniteMax);
            }
        }
        public float CurrentNanites
        {
            get => _curNanites;
            set => _curNanites = value;
        }

        public float CurrentNanitesPercent => Max > 0 ? _curNanites / Max : 0f;

        public bool CanRegenNanites
        {
            get
            {
                if (Pawn.Dead || _curNanites >= Max) return false;
                return EnergyNeed is { CurLevel: > 0f };
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            _gizmo ??= new Gizmo_NaniteResourcesNew(this);

            _curNanites = Mathf.Clamp(_curNanites, 0f, Max);
            EnsureRegenHediff();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _curNanites, "curNanites");
        }

        public override void CompTick()
        {
            base.CompTick();

            if (Pawn.IsHashIntervalTick(60))
            {
                TickRegen();
            }
        }

        private void TickRegen()
        {
            if (!CanRegenNanites) return;

            float regenAmount = Pawn.GetStatValue(GrayRaceDefOf.GRStat_NaniteRegenRate);

            _curNanites = Mathf.Min(_curNanites + regenAmount, Max);
        }

        private void EnsureRegenHediff()
        {
            if (!Pawn.health.hediffSet.HasHediff(GrayRaceDefOf.NanitesRegeneration))
            {
                Pawn.health.AddHediff(GrayRaceDefOf.NanitesRegeneration);
            }
        }

        public bool TrySpendNanites(float amount, string reason = "")
        {
            if (EnergyNeed != null && EnergyNeed.CurLevel <= 0f)
            {
                if (Pawn.IsColonistPlayerControlled)
                {
                    Messages.Message(reason, Pawn, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            if (_curNanites >= amount)
            {
                _curNanites -= amount;
                return true;
            }

            return false;
        }

        public void OffsetNanites(float amount)
        {
            _curNanites = Mathf.Clamp(_curNanites + amount, 0f, Max);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Pawn.IsColonistPlayerControlled && Pawn.IsGrayRace())
            {
                _gizmo ??= new Gizmo_NaniteResourcesNew(this);
                yield return _gizmo;
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: -10 Nanites", action = () => OffsetNanites(-10f)
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Fill Nanites", action = () => OffsetNanites(Max)
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: +10 Nanites", action = () => OffsetNanites(10f)
                };
            }
        }
    }
}
