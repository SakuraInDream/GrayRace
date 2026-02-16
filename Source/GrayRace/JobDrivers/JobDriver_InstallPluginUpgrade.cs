using System.Collections.Generic;
using RimWorld;
using SD.GrayRace.Defs;
using SD.GrayRace.Modules;
using Verse;
using Verse.AI;

namespace SD.GrayRace.JobDrivers;

public class JobDriver_InstallPluginUpgrade : JobDriver
{
    private const int GatherTicksPerStack = 45;
    private const TargetIndex MaterialTargetIndex = TargetIndex.C;
    private const TargetIndex InstallStationTargetIndex = TargetIndex.B;
    private const TargetIndex PawnTargetIndex = TargetIndex.A;

    private readonly List<UpgradeModule.MaterialPickupTask> _materialPlan = new List<UpgradeModule.MaterialPickupTask>();
    private GRUpgradeDef _upgradeDef;
    private BodyPartRecord _targetPart;
    private Thing _installStation;
    private bool _operationPrepared;

    private UpgradeModule UpgradeComp => pawn.GetManager()?.upgradeModule;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!TryPrepareOperation(out _))
        {
            UpgradeComp?.ClearPendingPluginInstall();
            return false;
        }

        foreach (UpgradeModule.MaterialPickupTask task in _materialPlan)
        {
            if (task?.thing == null) continue;
            if (!pawn.Reserve(task.thing, job, 1, task.count, null, errorOnFailed))
            {
                UpgradeComp?.ClearPendingPluginInstall();
                return false;
            }
        }

        if (_installStation != null && !pawn.Reserve(_installStation, job, 1, -1, null, errorOnFailed))
        {
            UpgradeComp?.ClearPendingPluginInstall();
            return false;
        }

        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(condition =>
        {
            _operationPrepared = false;
            if (condition != JobCondition.Succeeded)
            {
                UpgradeComp?.ClearPendingPluginInstall();
            }
        });

        if (!TryPrepareOperation(out string reason))
        {
            yield return FailWithMessageToil(reason);
            yield break;
        }

        this.FailOn(() => UpgradeComp == null || !UpgradeComp.HasPendingPluginInstall);

        foreach (UpgradeModule.MaterialPickupTask task in _materialPlan)
        {
            if (task?.thing == null) continue;

            yield return SetTargetToil(MaterialTargetIndex, task.thing);
            yield return Toils_Goto.GotoThing(MaterialTargetIndex, PathEndMode.Touch);
            yield return Toils_General.Wait(GatherTicksPerStack)
                .FailOnDestroyedNullOrForbidden(MaterialTargetIndex)
                .FailOnCannotTouch(MaterialTargetIndex, PathEndMode.Touch)
                .WithProgressBarToilDelay(MaterialTargetIndex);
        }

        if (_installStation != null)
        {
            yield return SetTargetToil(InstallStationTargetIndex, _installStation);
            yield return Toils_Goto.GotoThing(InstallStationTargetIndex, PathEndMode.InteractionCell);
        }

        int installTicks = UpgradeComp.GetInstallDurationTicks(_upgradeDef);
        Toil waitInstall = Toils_General.Wait(installTicks);
        if (_installStation != null)
        {
            waitInstall.WithProgressBarToilDelay(InstallStationTargetIndex);
        }
        else
        {
            // Job target A is the pawn itself. Use it as the progress anchor for in-place installs.
            waitInstall.WithProgressBarToilDelay(PawnTargetIndex);
        }
        yield return waitInstall;

        yield return FinalizeInstallToil();
    }

    private bool TryPrepareOperation(out string reason)
    {
        if (_operationPrepared)
        {
            reason = string.Empty;
            return true;
        }

        reason = string.Empty;
        _materialPlan.Clear();
        _upgradeDef = null;
        _targetPart = null;
        _installStation = null;

        if (UpgradeComp == null)
        {
            reason = "升级模块不可用";
            return false;
        }

        if (!UpgradeComp.TryGetPendingPluginInstall(out _upgradeDef, out _targetPart))
        {
            reason = "没有待安装插件任务";
            return false;
        }

        if (!UpgradeComp.CanApplyUpgrade(_upgradeDef, _targetPart, out reason, checkMaterials: true, checkPendingInstall: false))
        {
            return false;
        }

        if (!UpgradeComp.TryBuildMaterialCollectionPlan(_upgradeDef, out List<UpgradeModule.MaterialPickupTask> plan, out reason))
        {
            return false;
        }

        _materialPlan.AddRange(plan);
        _installStation = UpgradeComp.TryFindInstallationBuilding(_upgradeDef);
        _operationPrepared = true;
        return true;
    }

    private Toil FinalizeInstallToil()
    {
        return new Toil
        {
            initAction = () =>
            {
                if (UpgradeComp == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!UpgradeComp.TryFinalizePendingPluginInstall(_materialPlan, out string reason))
                {
                    if (!reason.NullOrEmpty())
                    {
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput);
                    }

                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Messages.Message($"已启用: {_upgradeDef.LabelCap}", pawn, MessageTypeDefOf.PositiveEvent);
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
    }

    private Toil SetTargetToil(TargetIndex index, Thing thing)
    {
        return new Toil
        {
            initAction = () =>
            {
                job.SetTarget(index, thing);
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
    }

    private Toil FailWithMessageToil(string reason)
    {
        return new Toil
        {
            initAction = () =>
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput);
                }

                UpgradeComp?.ClearPendingPluginInstall();
                EndJobWith(JobCondition.Incompletable);
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
    }
}
