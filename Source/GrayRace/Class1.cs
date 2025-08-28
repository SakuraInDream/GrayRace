using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SD.GrayRace
{
    [StaticConstructorOnStartup]
    public class Message
    {
        static Message()
        {
            Log.Message("Welcome to the Gray Race Mod!");
        }
    }
}
