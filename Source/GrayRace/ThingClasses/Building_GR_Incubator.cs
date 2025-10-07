using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using SD.GrayRace.Attributes;
using SD.GrayRace.DefModExtensions;
using SD.GrayRace.Utilities;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SD.GrayRace.ThingClasses
{
    public enum IncubatorState
    {
        [Localized("空闲")]
        Idle,
        [Localized("准备材料")]
        Preparing,
        [Localized("培育中")]
        Incubating,
        [Localized("培育完成")]
        Finished
    }
    // 消耗纳米机械和电力进行培育
    // 配合个 ITab() 显示当前培育状态：比如剩余时间、所需材料、纳米机械数量、放入的人格显示
    [StaticConstructorOnStartup]
    public class Building_GRIncubator: Building_Enterable, IStoreSettingsParent, IThingHolderWithDrawnPawn
    {
        public RecipeDef selectedRecipe;
        public RecipeDef SelectedRecipe => selectedRecipe;

        public RecipeDef foundationRecipe;
        public RecipeDef FoundationRecipe => foundationRecipe;

        private Pawn _baby;

        [Unsaved]
        private CompPowerTrader _power;

        private float _containedNanites;

        public StorageSettings allowedNutritionSettings;

        public IncubatorState State { get; set; } = IncubatorState.Idle;

        // 改，都可以改
        private const float NanitesConsumed = 6f;

        private float NanitesConsumedPerDay
        {
            get
            {
                var consumedPerDay = State == IncubatorState.Incubating ? NanitesConsumed : 0f;

                return consumedPerDay;
            }
        }

        public CompPowerTrader PowerTraderComp => _power ??= this.TryGetComp<CompPowerTrader>();
        public bool PoweredOn => PowerTraderComp.PowerOn;

        public List<RecipeDef> modExtensionRecipes = DefDatabase<RecipeDef>.AllDefsListForReading.Where(t=> t.HasModExtension<DefModExtension_RecipeNewBorn>()).ToList();

        public override AcceptanceReport CanAcceptPawn(Pawn p)
        {
            if (Working)
            {
                return "Occupied".Translate();
            }

            if (!PoweredOn)
            {
                return "NoPower".Translate().CapitalizeFirst();
            }

            if(selectedPawn != null && selectedPawn != p)
            {
                return "WaitingForPawn".Translate(selectedPawn.Named("PAWN"));
            }

            if (selectedPawn != null && !selectedPawn.IsGrayRace())
            {
                return "非灰裔".Translate();
            }

            return p.IsColonist && !p.IsQuestLodger();
        }

        public override void TryAcceptPawn(Pawn p)
        {
            if (selectedPawn != null && CanAcceptPawn(p))
            {
                selectedPawn = p;
                if (innerContainer.TryAddOrTransfer(p))
                {
                    SoundDefOf.GrowthVat_Close.PlayOneShot(SoundInfo.InMap(this));
                    startTick = Find.TickManager.TicksGame;
                    // 这里可能要加点在培育舱里面的逻辑，比如添加个状态，学习加速等

                }
                if (p.DeSpawnOrDeselect())
                {
                    Find.Selector.Select(p, false, false);
                }
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            allowedNutritionSettings = new StorageSettings(this);
            if(def.building.defaultStorageSettings != null)
                allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override bool IsWorking()
        {
            return base.IsWorking();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (State == IncubatorState.Idle)
            {
                yield return new Command_Action
                {
                    defaultLabel = "开始培育准备",
                    defaultDesc = "放入培育准备材料。",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = () =>
                    {
                        State = IncubatorState.Preparing;
                        var recipe = def.recipes.FirstOrDefault();
                        selectedRecipe = recipe;
                    }
                };
            }
            if (State == IncubatorState.Preparing)
            {
                var commandStartIncubation = new Command_Action
                {
                    defaultLabel = "开始培育",
                    defaultDesc = "启动培育程序",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = () =>
                    {
                        startTick = Find.TickManager.TicksGame;
                        State = IncubatorState.Incubating;
                    }
                };
                yield return commandStartIncubation;
                if (selectedRecipe == null)
                {
                    commandStartIncubation.Disable("请先选择培育清单");
                }
                else if (!AllRequiredIngredientsLoaded)
                {
                    commandStartIncubation.Disable("所需材料不足");
                }
                else if (!PoweredOn)
                {
                    commandStartIncubation.Disable("没有电力");
                }

                yield return new Command_Action
                {
                    defaultLabel = "取消准备",
                    defaultDesc = "",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = () =>
                    {
                        State = IncubatorState.Idle;
                        selectedRecipe = null;
                        foundationRecipe = null;
                        EjectContents();
                    }
                };
            }

            if(State == IncubatorState.Preparing && selectedRecipe != null && foundationRecipe == null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "添加人格基底",
                    defaultDesc = "添加人格基底以培育不同类型的灰裔。",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = () =>
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        // Find.WindowStack.Add(new Dialog_SelectForIncubator(this));
                        foreach (var recipe in modExtensionRecipes)
                        {
                            options.Add(new FloatMenuOption(recipe.LabelCap, () =>
                            {
                                foundationRecipe = recipe;
                            }));
                        }

                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                };
            }

            if (State == IncubatorState.Finished)
            {
                yield return new Command_Action
                {
                    defaultLabel = "完成培育",
                    defaultDesc = "完成培育并释放新生儿。",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                    action = Finsh
                };
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var floatMenuOption in base.GetFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder(base.GetInspectString());
            sb.Append($"\n当前状态：{State.ToLocalizedString()}");
            if (selectedPawn != null)
            {
                sb.AppendLine("\n当前加速培育: " + selectedPawn.LabelCap);
            }
            if (State == IncubatorState.Preparing)
            {
                sb.Append("\n等待运送材料：");
                AppendIngredientsList(sb);
            }
            return sb.ToString();
        }

        public override Vector3 PawnDrawOffset => CompBiosculpterPod.FloatingOffset(Find.TickManager.TicksGame);

        public StorageSettings GetStoreSettings()
        {
            return allowedNutritionSettings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public void Notify_SettingsChanged()
        {
        }

        public bool StorageTabVisible => true;

        public float HeldPawnDrawPos_Y => DrawPos.y + 0.03658537f;

        public float HeldPawnBodyAngle => Rotation.AsAngle;

        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(250))
            {
                PowerTraderComp.PowerOutput = State == IncubatorState.Incubating ? -PowerTraderComp.Props.PowerConsumption : -PowerTraderComp.Props.idlePowerDraw;
            }
            if (State == IncubatorState.Incubating)
            {
                _containedNanites -= 1f / 60000f;
                if (_containedNanites <= 0f)
                {
                    TryAbsorbNanites();
                }

                if (startTick > 0 && Find.TickManager.TicksGame - startTick >= 600)
                {
                    Birth();
                    // Finsh();
                }
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
        }

        public IEnumerable<IntVec3> IngredientStackCells => GenAdj.CellsOccupiedBy(this);

        // 是否已经满足所有材料需求
        public bool AllRequiredIngredientsLoaded
        {
            get
            {
                if(selectedRecipe == null) return false;

                bool baseOk = selectedRecipe.ingredients.All(t => GetRequiredCountOf(t.FixedIngredient) - innerContainer.TotalStackCountOfDef(t.FixedIngredient) <= 0);

                if (!baseOk) return false;

                return foundationRecipe == null || foundationRecipe.ingredients.All(t => GetRequiredCountOf(t.FixedIngredient) + GetRequiredCountOf_Foundation(t.FixedIngredient) - innerContainer.TotalStackCountOfDef(t.FixedIngredient) <= 0);
            }
        }

        private void Birth()
        {
            if(State != IncubatorState.Incubating) return;

            PawnKindDef babyKind = GrayRaceDefOf.GR_colonist; // 后面再改成在 Def 里找
            // PawnKindDef babyKind = PawnKindDefOf.Colonist;
            var pReq = new PawnGenerationRequest(
                kind: babyKind,
                faction: Faction.OfPlayer,
                forceGenerateNewPawn: true,
                allowDowned:true,
                canGeneratePawnRelations: false,
                allowGay:false,
                allowFood:false,
                allowAddictions:false,
                developmentalStages: DevelopmentalStage.Child,
                forceNoGear:true);
            _baby = PawnGenerator.GeneratePawn(pReq);
            // baby = (Pawn)PregnancyUtility.ApplyBirthOutcome(null, 100f, Faction.OfPlayer.ideos.PrimaryIdeo.GetPrecept(PreceptDefOf.ChildBirth) as Precept_Ritual, null, null, this, null, null);
            // if (baby != null)
            // {
            //     Log.Message("new Baby!");
            // }
            // else
            // {
            //     Log.Message("No! my Baby!");
            //     return;
            // }

            var ext = foundationRecipe?.GetModExtension<DefModExtension_RecipeNewBorn>();
            if (ext?.newBornBackstory != null)
            {
                _baby.story.Childhood = ext.newBornBackstory;
            }

            State = IncubatorState.Finished;

            selectedRecipe = null;
            foundationRecipe = null;
            startTick = -1;
        }

        private void Finsh()
        {
            if (State != IncubatorState.Finished) return;

            // 可以再弹出一个窗口显示新生儿，然后指定名字等，确定后，再清空舱内
            NameTriple nameTriple = _baby.Name as NameTriple;
            Name name;
            string text = null;
            if (nameTriple != null && nameTriple.First == "Baby".Translate().CapitalizeFirst())
            {
                Rand.PushState();
                Rand.Seed = _baby.thingIDNumber;
                NameStyle nameStyle = NameStyle.Full;
                name = PawnBioAndNameGenerator.GeneratePawnName(_baby, nameStyle);
                Rand.PopState();
                NameTriple nameTriple2 = name as NameTriple;
                text = ((nameTriple2 != null) ? nameTriple2.First: ((NameSingle)name).Name);
            }
            Find.WindowStack.Add(_baby.NamePawnDialog(text));
            State = IncubatorState.Idle;
            SoundDefOf.GrowthVat_Open.PlayOneShot(SoundInfo.InMap(this));
            GenSpawn.Spawn(_baby, InteractionCell, Map);
            innerContainer.ClearAndDestroyContents();
        }

        private void EjectContents()
        {
            if (innerContainer.Count > 0)
            {
                innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
            }
        }
        private void TryAbsorbNanites()
        {
            foreach (var thing in innerContainer)
            {
                if (thing.def != GrayRaceDefOf.GR_Nanites) continue;

                // 1 个纳米机械供给 1
                _containedNanites += 1f;
                thing.SplitOff(1).Destroy();

                break;
            }
        }

        private void AppendIngredientsList(StringBuilder sb)
        {
            if(selectedRecipe == null) return;

            var ingredients = selectedRecipe.ingredients.ConcatIfNotNull(foundationRecipe?.ingredients).GroupBy(t => t.FixedIngredient.defName).Select(t=> t.First()).ToList();

            foreach (var thing in ingredients)
            {
                sb.AppendInNewLine($" - {thing.FixedIngredient.LabelCap} {innerContainer.TotalStackCountOfDef(thing.FixedIngredient)}/{GetRequiredCountOf(thing.FixedIngredient) + GetRequiredCountOf_Foundation(thing.FixedIngredient)}");
            }
        }
        public bool CanAcceptIngredient(Thing thing)
        {
            return GetRequiredCountOf(thing.def) + GetRequiredCountOf_Foundation(thing.def) - innerContainer.TotalStackCountOfDef(thing.def) > 0;
        }
        public int GetRequiredCountOf(ThingDef thingDef)
        {
            if (selectedRecipe == null) return 0;

            foreach (var t in selectedRecipe.ingredients)
            {
                if (t.FixedIngredient != thingDef) continue;

                return (int)t.GetBaseCount();
            }

            return 0;
        }
        public int GetRequiredCountOf_Foundation(ThingDef thingDef)
        {
            if (foundationRecipe == null) return 0;

            foreach (var t in foundationRecipe.ingredients)
            {
                if (t.FixedIngredient != thingDef) continue;

                return (int)t.GetBaseCount();
            }

            return 0;
        }

        public static bool WasLoadingCancelled(Thing thing)
        {
            var incubator = thing as Building_GRIncubator;
            return incubator != null && incubator.State != IncubatorState.Preparing;
        }

    }
}
