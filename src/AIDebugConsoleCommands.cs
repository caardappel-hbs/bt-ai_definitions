using UnityEngine;

using HBS.Logging;
using HBS.Scripting.Attributes;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace BattleTech
{
	[ScriptBinding("AI")]
	public static class AIDebugConsoleCommands
	{
        static private readonly ILog logger = HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.AI);

		static CombatGameState Combat
		{
			get
			{
				return UnityGameInstance.Instance.Game.Combat;
			}
		}

		static AbstractActor SelectedAIUnit;

		[ScriptBinding]
		public static void Next()
		{
			logger.Log("Selecting next");

			cycleAISelection();

			logger.Log(string.Format("Selected unit: {0}", safeName(SelectedAIUnit)));
		}

		private static void cycleAISelection()
		{
			List<AbstractActor> allUnits = Combat.AllActors;
			List<AbstractActor> aiUnits = new List<AbstractActor>();
			for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
			{
				AbstractActor unit = allUnits[unitIndex];
				AITeam aiTeam = unit.team as AITeam;

				if (aiTeam != null && aiTeam.ThinksOnThisMachine)
				{
					aiUnits.Add(unit);
				}
			}

			if (aiUnits.Count == 0)
			{
				SelectedAIUnit = null;
				return;
			}

			if (SelectedAIUnit == null)
			{
				SelectedAIUnit = aiUnits[0];
				return;
			}

			int index = aiUnits.FindIndex(x => x == SelectedAIUnit);

			if (index == -1)
			{
				SelectedAIUnit = aiUnits[0];
				return;
			}

			index++;

			if (index == aiUnits.Count)
			{
				SelectedAIUnit = null;
				return;
			}

			SelectedAIUnit = aiUnits[index];

			return;
		}

		static string safeName(AbstractActor unit)
		{
			if (unit == null)
			{
				return "[NONE]";
			}
			return string.Format("{0} ({1})", unit.DisplayName, unit.GUID);
		}

		[ScriptBinding]
		public static void BVs()
		{
			logger.Log(string.Format("Behavior Variables for {0}", safeName(SelectedAIUnit)));

			if (SelectedAIUnit == null)
			{
				return;
			}

			logBVsForScope("unit", SelectedAIUnit.BehaviorTree.unitBehaviorVariables);
			logBVsForScope("lance", SelectedAIUnit.lance.BehaviorVariables);
			logBVsForScope("team", SelectedAIUnit.team.BehaviorVariables);
			// TODO: log weight class, faction, and global BVS
		}

		static void logBVsForScope(string scopeName, BehaviorVariableScope scope)
		{
			logger.Log(string.Format(" ** Behavior variables on the {0} **", scopeName));
			List<BehaviorVariableName> variables = scope.VariableNames;
			for (int bvIndex = 0; bvIndex < variables.Count; ++bvIndex)
			{
				BehaviorVariableValue bvv = scope.GetVariable(variables[bvIndex]);
				logger.Log(bvToString(variables[bvIndex], bvv));
			}
		}

		static string bvToString(BehaviorVariableName name, BehaviorVariableValue bvv)
		{
			string nameString = name.ToString();
			switch (bvv.type)
			{
			case BehaviorVariableValue.BehaviorVariableType.Bool:
				return string.Format("{0}: {1}", nameString, bvv.BoolVal);
			case BehaviorVariableValue.BehaviorVariableType.String:
				return string.Format("{0}: {1}", nameString, bvv.StringVal);
			case BehaviorVariableValue.BehaviorVariableType.Int:
				return string.Format("{0}: {1}", nameString, bvv.IntVal);
			case BehaviorVariableValue.BehaviorVariableType.Float:
				return string.Format("{0}: {1}", nameString, bvv.FloatVal);
			default:
				Debug.LogAssertion("unknown behavior variable type" + bvv.type);
				return ("???");
			}
		}


		[ScriptBinding]
		public static void LastOrder()
		{
			logger.Log(string.Format("Last Order for selected AI unit: {0}", safeName(SelectedAIUnit)));

			if (SelectedAIUnit != null)
			{
				string lastOrder = SelectedAIUnit.BehaviorTree.lastOrderDebugString;
				logger.Log((lastOrder != null) ? lastOrder : "[No order]");
			}
		}

		[ScriptBinding]
		public static void ResetBV()
		{
			logger.Log(string.Format("Resetting behavior variables for AI unit: {0}", safeName(SelectedAIUnit)));

			if (SelectedAIUnit != null)
			{
				SelectedAIUnit.ResetBehaviorVariables();
			}
		}

		[ScriptBinding]
		public static void Vis()
		{
			logger.Log(string.Format("visibility for AI unit: {0}", safeName(SelectedAIUnit)));

			if (SelectedAIUnit == null)
			{
				return;
			}

			List<AbstractActor> detectedUnits = SelectedAIUnit.VisibilityCache.GetDetectedEnemyUnits();
			List<AbstractActor> visibleUnits = SelectedAIUnit.VisibilityCache.GetVisibleEnemyUnits();

			logger.Log(" * Visible Units *");
			for (int unitIndex = 0; unitIndex < visibleUnits.Count; ++unitIndex)
			{
				logger.Log(safeName(visibleUnits[unitIndex]));
			}
			logger.Log(" * Detected Units *");
			for (int unitIndex = 0; unitIndex < detectedUnits.Count; ++unitIndex)
			{
				AbstractActor unit = detectedUnits[unitIndex];
				if (!visibleUnits.Contains(unit)) 
				{
					logger.Log(safeName(unit));
				}
			}
		}

		[ScriptBinding]
		public static void Memberships()
		{
			logger.Log(string.Format("Memberships for AI unit: {0}", safeName(SelectedAIUnit)));
			logger.Log(string.Format("Lance: {0}", SelectedAIUnit.lance.GUID));
			logger.Log(string.Format("Team: {0}", SelectedAIUnit.team.GUID));
		}
	}
}
