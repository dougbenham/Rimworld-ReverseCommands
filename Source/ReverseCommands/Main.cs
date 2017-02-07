﻿using System.Reflection;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;
using Verse.AI;
using Harmony.ILCopying;

namespace ReverseCommands
{
	[StaticConstructorOnStartup]
	static class Main
	{
		static Main()
		{
			var harmony = HarmonyInstance.Create("net.pardeike.reversecommands");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			SpecialAllPawnsSpawnedCountFix(harmony);
		}

		static void SpecialAllPawnsSpawnedCountFix(HarmonyInstance harmony)
		{
			var original = AccessTools.Method(typeof(PawnPathPool), "GetEmptyPawnPath");
			var replacer = new HarmonyProcessor(Priority.Normal, new string[0], new string[0]);
			replacer.AddILProcessor(new MethodReplacer(
				AccessTools.Method(typeof(MapPawns), "get_AllPawnsSpawnedCount"), 
				AccessTools.Method(typeof(Main), "AllPawnsSpawnedCountx2")
			));
			harmony.Patch(original, null, null, replacer);
		}

		static int AllPawnsSpawnedCountx2(MapPawns instance)
		{
			// all we want is some extra allocation
			return instance.AllPawnsSpawnedCount * 5 + 2;
		}
	}

	[HarmonyPatch(typeof(DynamicDrawManager))]
	[HarmonyPatch("DrawDynamicThings")]
	class Patch1
	{
		static void Prefix()
		{
			if (PathInfo.current != null)
			{
				var path = PathInfo.GetPath(PathInfo.current);
				if (path != null) path.DrawPath(null);
			}
			PathInfo.current = null;
		}
	}

	[HarmonyPatch(typeof(MainTabsRoot))]
	[HarmonyPatch("HandleLowPriorityShortcuts")]
	class Patch2
	{
		static bool Prefix()
		{
			if (Event.current.type != EventType.MouseDown) return true;
			Tools.CloseLabelMenu(true);
			if (Event.current.button != 1) return true;
			return Tools.GetPawnActions().Count() == 0;
		}
	}

	[HarmonyPatch(typeof(Selector))]
	[HarmonyPatch("HandleMapClicks")]
	class Patch3
	{
		static bool Prefix(Selector __instance)
		{
			if (Event.current.isKey && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
				Tools.CloseLabelMenu(true);

			if (Event.current.type != EventType.MouseDown) return true;
			if (Event.current.button != 1) return true;

			var labeledPawnActions = Tools.GetPawnActions();
			if (labeledPawnActions.Count() == 0) return true;

			var cell = UI.MouseCell();
			Find.VisibleMap.mapPawns.FreeColonists.Where(Tools.PawnUsable).Do(pawn => PathInfo.AddInfo(pawn, cell));

			var items = labeledPawnActions.Keys.Select(label => {
				var dict = labeledPawnActions[label];
				return Tools.MakeMenuItemForLabel(cell, label, dict);
			}).ToList();

			Tools.labelMenu = new FloatMenuLabels(items);
			Find.WindowStack.Add(Tools.labelMenu);

			Event.current.Use();
			return false;
		}
	}
}