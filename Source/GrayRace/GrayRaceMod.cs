using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SD.GrayRace
{
    public class GrayRaceMod : Mod
    {
        public static Harmony HarmonyInstance;

        public GrayRaceMod(ModContentPack content) : base(content)
        {
            // harmony = new Harmony("sd.grayrace.mod");
            // harmony.PatchAll();
        }
    }
}
