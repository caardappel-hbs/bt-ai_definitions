using System;
using System.Collections.Generic;
using UnityEngine;
using BattleTech;

public class AIManager
{
	List<AITeam> aiTeams;
	// TODO commenting out warnings:: GameInstance gameInstance;

	public AIManager(GameInstance gameInstance)
	{
		aiTeams = new List<AITeam>();

		VerifyNoBehaviorVariableDups();
	}

	public void Update()
	{
	}

    public void Reset()
    {
        aiTeams.Clear();
    }

	public void AddAITeam(AITeam team)
	{
		Debug.Assert(!aiTeams.Contains(team));
		aiTeams.Add(team);
	}

	void VerifyNoBehaviorVariableDups()
	{
		Dictionary<int, BehaviorVariableName> behaviorVariableTable = new Dictionary<int, BehaviorVariableName>();

		string[] bvNames = Enum.GetNames(typeof(BehaviorVariableName));

		for (int i = 0; i < bvNames.Length; ++i)
		{
			string bvName = bvNames[i];

			BehaviorVariableName bvTag;
			try
			{
				bvTag = (BehaviorVariableName) Enum.Parse(typeof(BehaviorVariableName), bvName);
			}
			catch (ArgumentException)
			{
				Debug.LogError(string.Format("Parsing {0} failed", bvName));
				throw;
			}

			int bvValue = (int)bvTag;
			if (behaviorVariableTable.ContainsKey(bvValue))
			{
				Debug.LogError(string.Format("ERROR: behavior variables are not unique: ({0}) {1} {2}", bvValue, bvName, behaviorVariableTable[bvValue]));
			}
			else
			{
				behaviorVariableTable[bvValue] = bvTag;
			}
		}
	}
}

