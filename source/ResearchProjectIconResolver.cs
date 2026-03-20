using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Research_Icons
{
    internal static class ResearchProjectIconResolver
    {
        internal const float DefaultProjectWidth = 172f;
        internal const float DefaultIconSize = 24f;
        internal const float DefaultIconPadding = 4f;

        private static readonly ResearchProjectExtension DefaultExtension = new ResearchProjectExtension();
        private static readonly Dictionary<ResearchProjectDef, ResearchProjectExtension> ProjectIconExtensionCache = new Dictionary<ResearchProjectDef, ResearchProjectExtension>();
        private static readonly Dictionary<ResearchProjectDef, CachedProjectIcon> ProjectIconCache = new Dictionary<ResearchProjectDef, CachedProjectIcon>();
        private static readonly Dictionary<string, Texture2D> ManualTextureCache = new Dictionary<string, Texture2D>();

        internal static float GetProjectWidth(ResearchProjectDef project)
        {
            return GetProjectIconExtension(project).projectWidth;
        }

        internal static float GetIconSize(ResearchProjectDef project)
        {
            return GetProjectIconExtension(project).iconSize;
        }

        internal static float GetIconPadding(ResearchProjectDef project)
        {
            return GetProjectIconExtension(project).iconPadding;
        }

        internal static bool TryGetProjectIcon(ResearchProjectDef project, out Def iconDef, out Texture2D texture)
        {
            return TryGetProjectIcon(project, out iconDef, out texture, out _, out _);
        }

        internal static bool TryGetProjectIcon(ResearchProjectDef project, out Def iconDef, out Texture2D texture, out ThingDef stuffDef, out Color? color)
        {
            iconDef = null;
            texture = null;
            stuffDef = null;
            color = null;

            if (!ProjectIconCache.TryGetValue(project, out CachedProjectIcon cachedProjectIcon))
            {
                cachedProjectIcon = ResolveProjectIcon(project);
                ProjectIconCache.Add(project, cachedProjectIcon);
            }

            if (cachedProjectIcon == null)
            {
                return false;
            }

            iconDef = cachedProjectIcon.IconDef;
            texture = cachedProjectIcon.Texture;
            stuffDef = cachedProjectIcon.StuffDef;
            color = cachedProjectIcon.Color;
            return iconDef != null || texture != null;
        }

        private static ResearchProjectExtension GetProjectIconExtension(ResearchProjectDef project)
        {
            if (!ProjectIconExtensionCache.TryGetValue(project, out ResearchProjectExtension extension))
            {
                extension = project.GetModExtension<ResearchProjectExtension>() ?? DefaultExtension;
                ProjectIconExtensionCache.Add(project, extension);
            }

            return extension;
        }

        private static CachedProjectIcon ResolveProjectIcon(ResearchProjectDef project)
        {
            ResearchProjectExtension extension = GetProjectIconExtension(project);
            switch (extension.iconSelectionMethod)
            {
                case ResearchProjectIconSelectionMethod.PreferredDef:
                    CachedProjectIcon preferredIcon = ResolvePreferredDefIcon(project, extension.preferredDefName);
                    return preferredIcon ?? ResolveAutomaticIcon(project);
                case ResearchProjectIconSelectionMethod.Manual:
                    return ResolveManualIcon(extension.manualIconPath);
                default:
                    return ResolveAutomaticIcon(project);
            }
        }

        private static CachedProjectIcon ResolvePreferredDefIcon(ResearchProjectDef project, string preferredDefName)
        {
            if (preferredDefName.NullOrEmpty())
            {
                return null;
            }

            List<Def> unlockedDefs = project.UnlockedDefs;
            for (int i = 0; i < unlockedDefs.Count; i++)
            {
                Def unlockedDef = unlockedDefs[i];
                if (string.Equals(unlockedDef.defName, preferredDefName, StringComparison.OrdinalIgnoreCase)
                    && CanUseUnlockedDefIcon(unlockedDef))
                {
                    return CreateCachedProjectIcon(project, unlockedDef);
                }
            }

            return null;
        }

        private static CachedProjectIcon ResolveAutomaticIcon(ResearchProjectDef project)
        {
            List<Def> unlockedDefs = project.UnlockedDefs;
            for (int i = 0; i < unlockedDefs.Count; i++)
            {
                Def unlockedDef = unlockedDefs[i];
                if (CanUseUnlockedDefIcon(unlockedDef))
                {
                    return CreateCachedProjectIcon(project, unlockedDef);
                }
            }

            return null;
        }

        private static CachedProjectIcon ResolveManualIcon(string iconPath)
        {
            if (iconPath.NullOrEmpty())
            {
                return null;
            }

            if (!ManualTextureCache.TryGetValue(iconPath, out Texture2D texture))
            {
                texture = ContentFinder<Texture2D>.Get(iconPath, false);
                ManualTextureCache.Add(iconPath, texture);
            }

            if (texture == null || texture == BaseContent.BadTex)
            {
                return null;
            }

            return CreateCachedProjectIcon(null, null, texture);
        }

        private static bool CanUseUnlockedDefIcon(Def unlockedDef)
        {
            return unlockedDef is BuildableDef || unlockedDef is RecipeDef || unlockedDef is TerrainDef || unlockedDef is PsychicRitualDef;
        }

        private static CachedProjectIcon CreateCachedProjectIcon(ResearchProjectDef project, Def iconDef = null, Texture2D texture = null)
        {
            ResearchProjectExtension extension = project != null ? GetProjectIconExtension(project) : DefaultExtension;
            return new CachedProjectIcon
            {
                IconDef = iconDef,
                Texture = texture,
                StuffDef = extension.iconStuffDef,
                Color = extension.iconColorDef?.color
            };
        }

        private sealed class CachedProjectIcon
        {
            public Def IconDef;
            public Texture2D Texture;
            public ThingDef StuffDef;
            public Color? Color;
        }
    }
}
