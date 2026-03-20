using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Research_Icons
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content)
            : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "ResearchIcons.LoadingLabel", doAsynchronously: true, null);
        }

        private static void Init()
        {
            new Harmony("sk.researchicons").PatchAll();
        }
    }
}
