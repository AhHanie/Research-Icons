using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Research_Icons
{
    public enum ResearchProjectIconSelectionMethod
    {
        Automatic,
        PreferredDef,
        Manual
    }

    public class ResearchProjectExtension : DefModExtension
    {
        public ResearchProjectIconSelectionMethod iconSelectionMethod = ResearchProjectIconSelectionMethod.Automatic;
        public string preferredDefName;
        public string manualIconPath;
        public ColorDef iconColorDef;
        public ThingDef iconStuffDef;
        public float projectWidth = ResearchProjectIconResolver.DefaultProjectWidth;
        public float iconSize = ResearchProjectIconResolver.DefaultIconSize;
        public float iconPadding = ResearchProjectIconResolver.DefaultIconPadding;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (iconSelectionMethod == ResearchProjectIconSelectionMethod.PreferredDef && preferredDefName.NullOrEmpty())
            {
                yield return $"{nameof(ResearchProjectExtension)} with PreferredDef selection must define {nameof(preferredDefName)}.";
            }

            if (iconSelectionMethod == ResearchProjectIconSelectionMethod.Manual)
            {
                if (manualIconPath.NullOrEmpty())
                {
                    yield return $"{nameof(ResearchProjectExtension)} with Manual selection must define {nameof(manualIconPath)}.";
                }
            }

            if (iconStuffDef != null && !iconStuffDef.IsStuff)
            {
                yield return $"{nameof(ResearchProjectExtension)} {nameof(iconStuffDef)} must reference a StuffDef.";
            }
        }
    }
}
