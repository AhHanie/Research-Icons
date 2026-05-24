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
    internal static class ResearchProjectIconUtility
    {
        internal const float OriginalProjectWidth = 140f;
        private const float ProjectHeight = 50f;
        private const float KnowledgeCategoryStripWidth = 14f;

        internal static void ClampProjectLabelRect(ref Rect rect)
        {
            rect.xMax = rect.xMin + OriginalProjectWidth;
        }

        internal static float GetProjectWidth(ResearchProjectDef project)
        {
            return ResearchProjectIconResolver.GetProjectWidth(project);
        }

        internal static void DrawProjectIcons(MainTabWindow_Research window)
        {
            DrawProjectIcons(window.VisibleResearchProjects, window.CurTab);
        }

        internal static void DrawProjectIcons(List<ResearchProjectDef> visibleResearchProjects, ResearchTabDef curTab)
        {
            if (visibleResearchProjects == null || curTab == null)
            {
                return;
            }

            for (int i = 0; i < visibleResearchProjects.Count; i++)
            {
                ResearchProjectDef project = visibleResearchProjects[i];
                if (project.tab != curTab || project.IsHidden)
                {
                    continue;
                }

                if (!ResearchProjectIconResolver.TryGetProjectIcon(project, out Def iconDef, out Texture2D iconTexture, out ThingDef stuffDef, out Color? color))
                {
                    continue;
                }

                float knowledgeCategoryOffset = 0f;
                if (ModsConfig.AnomalyActive && project.knowledgeCategory != null)
                {
                    knowledgeCategoryOffset = KnowledgeCategoryStripWidth;
                }

                Rect projectRect = new Rect(project.ResearchViewX * 190f, project.ResearchViewY * 100f, GetProjectWidth(project), ProjectHeight);
                projectRect.xMax += knowledgeCategoryOffset;

                float iconSize = ResearchProjectIconResolver.GetIconSize(project);
                float iconPadding = ResearchProjectIconResolver.GetIconPadding(project);
                Rect iconRect = new Rect(projectRect.xMax - iconSize - iconPadding, projectRect.y + iconPadding, iconSize, iconSize);

                if (iconDef != null)
                {
                    Widgets.DefIcon(iconRect, iconDef, stuffDef, color: color);
                }
                else if (iconTexture != null)
                {
                    Widgets.DrawTextureFitted(iconRect, iconTexture, 1f);
                }
            }
        }

        internal static bool IsFloatConstant(CodeInstruction instruction, float value)
        {
            return instruction.opcode == OpCodes.Ldc_R4
                && instruction.operand is float operand
                && Math.Abs(operand - value) < 0.001f;
        }

        internal static bool CallsMethod(CodeInstruction instruction, MethodInfo method)
        {
            return method != null
                && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo calledMethod
                && calledMethod == method;
        }

        internal static bool CallsConstructor(CodeInstruction instruction, ConstructorInfo constructor)
        {
            return constructor != null
                && (instruction.opcode == OpCodes.Newobj || instruction.opcode == OpCodes.Call)
                && instruction.operand is ConstructorInfo calledConstructor
                && calledConstructor == constructor;
        }

        internal static bool CallsMethodNamed(CodeInstruction instruction, string methodName)
        {
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo calledMethod
                && calledMethod.Name == methodName;
        }

        internal static CodeInstruction CloneInstruction(CodeInstruction instruction)
        {
            return new CodeInstruction(instruction.opcode, instruction.operand);
        }

        internal static void LogTranspilerResult(string methodName, int expectedReplacements, int actualReplacements)
        {
            if (actualReplacements == expectedReplacements)
            {
                Logger.Message($"{methodName} transpiler succeeded with {actualReplacements} width replacement(s).");
            }
            else
            {
                Logger.Warning($"{methodName} transpiler expected {expectedReplacements} width replacement(s) but found {actualReplacements}.");
            }
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "ViewSize")]
    public static class MainTabWindow_Research_ViewSize
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>(instructions);
            MethodInfo posXMethod = AccessTools.Method(typeof(MainTabWindow_Research), "PosX");
            MethodInfo getProjectWidthMethod = AccessTools.Method(typeof(ResearchProjectIconUtility), nameof(ResearchProjectIconUtility.GetProjectWidth));
            int replacements = 0;

            for (int i = 0; i < patchedInstructions.Count; i++)
            {
                if (ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i], ResearchProjectIconUtility.OriginalProjectWidth)
                    && i >= 2
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 1], posXMethod))
                {
                    patchedInstructions[i] = ResearchProjectIconUtility.CloneInstruction(patchedInstructions[i - 2]);
                    patchedInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Call, getProjectWidthMethod));
                    replacements++;
                    break;
                }
            }

            ResearchProjectIconUtility.LogTranspilerResult("MainTabWindow_Research.ViewSize", 1, replacements);
            return patchedInstructions;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "ListProjects")]
    public static class MainTabWindow_Research_ListProjects
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>(instructions);
            MethodInfo posXMethod = AccessTools.Method(typeof(MainTabWindow_Research), "PosX");
            MethodInfo posYMethod = AccessTools.Method(typeof(MainTabWindow_Research), "PosY");
            ConstructorInfo rectConstructor = AccessTools.Constructor(typeof(Rect), new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            MethodInfo getProjectWidthMethod = AccessTools.Method(typeof(ResearchProjectIconUtility), nameof(ResearchProjectIconUtility.GetProjectWidth));
            MethodInfo clampProjectLabelRectMethod = AccessTools.Method(typeof(ResearchProjectIconUtility), nameof(ResearchProjectIconUtility.ClampProjectLabelRect));
            bool lineWidthReplaced = false;
            bool rectWidthReplaced = false;
            bool labelClampInserted = false;
            int widthReplacements = 0;

            if (getProjectWidthMethod == null || rectConstructor == null || clampProjectLabelRectMethod == null)
            {
                Logger.Warning("MainTabWindow_Research.ListProjects transpiler setup failed due to missing reflected methods.");
                return patchedInstructions;
            }

            for (int i = 0; i < patchedInstructions.Count; i++)
            {
                if (!lineWidthReplaced
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i], ResearchProjectIconUtility.OriginalProjectWidth)
                    && i >= 2
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 1], posXMethod))
                {
                    patchedInstructions[i] = ResearchProjectIconUtility.CloneInstruction(patchedInstructions[i - 2]);
                    patchedInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Call, getProjectWidthMethod));
                    lineWidthReplaced = true;
                    widthReplacements++;
                    i++;
                    continue;
                }

                if (!rectWidthReplaced
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i], ResearchProjectIconUtility.OriginalProjectWidth)
                    && i >= 5
                    && i + 2 < patchedInstructions.Count
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 5], posXMethod)
                    && ResearchProjectIconUtility.CallsMethod(patchedInstructions[i - 1], posYMethod)
                    && ResearchProjectIconUtility.IsFloatConstant(patchedInstructions[i + 1], 50f)
                    && ResearchProjectIconUtility.CallsConstructor(patchedInstructions[i + 2], rectConstructor))
                {
                    patchedInstructions[i] = ResearchProjectIconUtility.CloneInstruction(patchedInstructions[i - 3]);
                    patchedInstructions.Insert(i + 1, ResearchProjectIconUtility.CloneInstruction(patchedInstructions[i - 2]));
                    patchedInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Call, getProjectWidthMethod));
                    rectWidthReplaced = true;
                    widthReplacements++;
                    i += 2;
                    continue;
                }

                if (!labelClampInserted
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

            ResearchProjectIconUtility.LogTranspilerResult("MainTabWindow_Research.ListProjects", 2, widthReplacements);
            if (labelClampInserted)
            {
                Logger.Message("MainTabWindow_Research.ListProjects label clamp inserted successfully.");
            }
            else
            {
                Logger.Warning("MainTabWindow_Research.ListProjects label clamp insertion failed.");
            }

            return patchedInstructions;
        }

        public static void Postfix(MainTabWindow_Research __instance)
        {
            ResearchProjectIconUtility.DrawProjectIcons(__instance);
        }
    }
}
