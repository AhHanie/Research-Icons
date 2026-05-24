using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Research_Icons.Patches
{
    internal static class BetterResearchTabsAccess
    {
        internal const string TargetTypeName = "TowersBetterResearchTabs.MainTabWindow_Research";

        internal static readonly Type TargetType = AccessTools.TypeByName(TargetTypeName);
        internal static readonly MethodInfo ViewSizeMethod = TargetType == null
            ? null
            : AccessTools.Method(TargetType, "ViewSize", new[] { typeof(ResearchTabDef) });
        internal static readonly MethodInfo ListProjectsMethod = TargetType == null
            ? null
            : AccessTools.Method(TargetType, "ListProjects", new[] { typeof(Rect), typeof(bool).MakeByRefType() });

        private static readonly PropertyInfo VisibleResearchProjectsProperty = TargetType == null
            ? null
            : AccessTools.Property(TargetType, "VisibleResearchProjects");
        private static readonly FieldInfo CurTabField = TargetType == null
            ? null
            : AccessTools.Field(TargetType, "curTabInt");
        private static readonly PropertyInfo CurTabProperty = TargetType == null
            ? null
            : AccessTools.Property(TargetType, "CurTab");

        internal static List<ResearchProjectDef> GetVisibleResearchProjects(object instance)
        {
            return VisibleResearchProjectsProperty?.GetValue(instance, null) as List<ResearchProjectDef>;
        }

        internal static ResearchTabDef GetCurTab(object instance)
        {
            ResearchTabDef curTab = CurTabField?.GetValue(instance) as ResearchTabDef;
            if (curTab != null)
            {
                return curTab;
            }

            return CurTabProperty?.GetValue(instance, null) as ResearchTabDef;
        }
    }

    internal static class BetterResearchTabsTranspilerUtility
    {
        internal static bool LoadsResearchProjectFromDisplayClass(CodeInstruction loadDisplayClassInstruction, CodeInstruction loadProjectInstruction)
        {
            return loadDisplayClassInstruction != null
                && loadProjectInstruction != null
                && loadProjectInstruction.opcode == OpCodes.Ldfld
                && loadProjectInstruction.operand is FieldInfo field
                && field.FieldType == typeof(ResearchProjectDef);
        }

        internal static CodeInstruction CloneWithLabelsFrom(CodeInstruction instruction, CodeInstruction labelsSource)
        {
            CodeInstruction clone = ResearchProjectIconUtility.CloneInstruction(instruction);
            clone.labels.AddRange(labelsSource.labels);
            labelsSource.labels.Clear();
            return clone;
        }
    }

    [HarmonyPatch]
    public static class BetterResearchTabs_MainTabWindow_Research_ViewSize
    {
        public static bool Prepare()
        {
            return BetterResearchTabsAccess.ViewSizeMethod != null;
        }

        public static MethodBase TargetMethod()
        {
            return BetterResearchTabsAccess.ViewSizeMethod;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>(instructions);
            MethodInfo getResearchViewXMethod = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.ResearchViewX));
            MethodInfo mathfMaxMethod = AccessTools.Method(typeof(Mathf), nameof(Mathf.Max), new[] { typeof(float), typeof(float) });
            MethodInfo getProjectWidthMethod = AccessTools.Method(typeof(ResearchProjectIconUtility), nameof(ResearchProjectIconUtility.GetProjectWidth));
            int replacements = 0;

            if (getResearchViewXMethod == null || mathfMaxMethod == null || getProjectWidthMethod == null)
            {
                Logger.Warning($"{BetterResearchTabsAccess.TargetTypeName}.ViewSize transpiler setup failed due to missing reflected methods.");
                return patchedInstructions;
            }

            for (int i = 0; i < patchedInstructions.Count; i++)
            {
                if (ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i], ResearchProjectIconUtility.OriginalProjectWidth)
                    && i >= 4
                    && i + 2 < patchedInstructions.Count
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 3], getResearchViewXMethod)
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i - 2], 190f)
                    && patchedInstructions[i - 1].opcode == OpCodes.Mul
                    && patchedInstructions[i + 1].opcode == OpCodes.Add
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i + 2], mathfMaxMethod))
                {
                    patchedInstructions[i] = BetterResearchTabsTranspilerUtility.CloneWithLabelsFrom(patchedInstructions[i - 4], patchedInstructions[i]);
                    patchedInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Call, getProjectWidthMethod));
                    replacements++;
                    break;
                }
            }

            ResearchProjectIconUtility.LogTranspilerResult($"{BetterResearchTabsAccess.TargetTypeName}.ViewSize", 1, replacements);
            return patchedInstructions;
        }
    }

    [HarmonyPatch]
    public static class BetterResearchTabs_MainTabWindow_Research_ListProjects
    {
        public static bool Prepare()
        {
            return BetterResearchTabsAccess.ListProjectsMethod != null;
        }

        public static MethodBase TargetMethod()
        {
            return BetterResearchTabsAccess.ListProjectsMethod;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>(instructions);
            MethodInfo getResearchViewXMethod = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.ResearchViewX));
            MethodInfo getResearchViewYMethod = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.ResearchViewY));
            ConstructorInfo rectConstructor = AccessTools.Constructor(typeof(Rect), new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            MethodInfo getProjectWidthMethod = AccessTools.Method(typeof(ResearchProjectIconUtility), nameof(ResearchProjectIconUtility.GetProjectWidth));
            MethodInfo clampProjectLabelRectMethod = AccessTools.Method(typeof(ResearchProjectIconUtility), nameof(ResearchProjectIconUtility.ClampProjectLabelRect));
            bool lineWidthReplaced = false;
            bool rectWidthReplaced = false;
            bool labelClampInserted = false;
            int widthReplacements = 0;

            if (getResearchViewXMethod == null || getResearchViewYMethod == null || rectConstructor == null || getProjectWidthMethod == null || clampProjectLabelRectMethod == null)
            {
                Logger.Warning($"{BetterResearchTabsAccess.TargetTypeName}.ListProjects transpiler setup failed due to missing reflected methods.");
                return patchedInstructions;
            }

            for (int i = 0; i < patchedInstructions.Count; i++)
            {
                if (!lineWidthReplaced
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i], ResearchProjectIconUtility.OriginalProjectWidth)
                    && i >= 4
                    && i + 2 < patchedInstructions.Count
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 3], getResearchViewXMethod)
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i - 2], 190f)
                    && patchedInstructions[i - 1].opcode == OpCodes.Mul
                    && patchedInstructions[i + 1].opcode == OpCodes.Add)
                {
                    patchedInstructions[i] = BetterResearchTabsTranspilerUtility.CloneWithLabelsFrom(patchedInstructions[i - 4], patchedInstructions[i]);
                    patchedInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Call, getProjectWidthMethod));
                    lineWidthReplaced = true;
                    widthReplacements++;
                    i++;
                    continue;
                }

                if (!rectWidthReplaced
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i], ResearchProjectIconUtility.OriginalProjectWidth)
                    && i >= 8
                    && i + 2 < patchedInstructions.Count
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 8], getResearchViewXMethod)
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 3], getResearchViewYMethod)
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i - 7], 190f)
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i - 2], 100f)
                    && patchedInstructions[i - 6].opcode == OpCodes.Mul
                    && patchedInstructions[i - 1].opcode == OpCodes.Mul
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i + 1], 50f)
                    && ResearchProjectIconUtility.CallsConstructor(patchedInstructions[i + 2], rectConstructor)
                    && BetterResearchTabsTranspilerUtility.LoadsResearchProjectFromDisplayClass(patchedInstructions[i - 5], patchedInstructions[i - 4]))
                {
                    patchedInstructions[i] = BetterResearchTabsTranspilerUtility.CloneWithLabelsFrom(patchedInstructions[i - 5], patchedInstructions[i]);
                    patchedInstructions.Insert(i + 1, ResearchProjectIconUtility.CloneInstruction(patchedInstructions[i - 4]));
                    patchedInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Call, getProjectWidthMethod));
                    rectWidthReplaced = true;
                    widthReplacements++;
                    i += 2;
                    continue;
                }

                if (!labelClampInserted
                    && rectWidthReplaced
                    && ResearchProjectIconUtility.CallsMethodNamed(patchedInstructions[i], nameof(Widgets.LabelCacheHeight))
                    && i >= 4)
                {
                    patchedInstructions.InsertRange(i - 4, new[]
                    {
                        ResearchProjectIconUtility.CloneInstruction(patchedInstructions[i - 4]),
                        new CodeInstruction(OpCodes.Call, clampProjectLabelRectMethod)
                    });
                    labelClampInserted = true;
                }
            }

            ResearchProjectIconUtility.LogTranspilerResult($"{BetterResearchTabsAccess.TargetTypeName}.ListProjects", 2, widthReplacements);
            if (labelClampInserted)
            {
                Logger.Message($"{BetterResearchTabsAccess.TargetTypeName}.ListProjects label clamp inserted successfully.");
            }
            else
            {
                Logger.Warning($"{BetterResearchTabsAccess.TargetTypeName}.ListProjects label clamp insertion failed.");
            }

            return patchedInstructions;
        }

        public static void Postfix(object __instance)
        {
            List<ResearchProjectDef> visibleResearchProjects = BetterResearchTabsAccess.GetVisibleResearchProjects(__instance);
            ResearchTabDef curTab = BetterResearchTabsAccess.GetCurTab(__instance);

            if (visibleResearchProjects == null || curTab == null)
            {
                Logger.Warning($"{BetterResearchTabsAccess.TargetTypeName}.ListProjects postfix could not read visible projects or current tab.");
                return;
            }

            ResearchProjectIconUtility.DrawProjectIcons(visibleResearchProjects, curTab);
        }
    }
}
