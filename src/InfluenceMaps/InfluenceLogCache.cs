using System.Collections.Generic;
using UnityEngine;
using BattleTech;

public class AILogCache
{
	private struct LogData
	{
		public string filename;
		public string data;

		public LogData(string filename, string logData)
		{
			this.filename = filename;
			this.data = logData;
		}
	}

	const int HISTORY_COUNT = 50;

	List<LogData> logHistory;
	CombatGameState combat;

	public AILogCache(CombatGameState combat)
	{
		this.combat = combat;
		logHistory = new List<LogData>();
	}

    public bool ImmediatelyWrite { get; set; }

	public void AddLogData(string filename, string logData)
	{
		LogData newData = new LogData(filename, logData);
		logHistory.Add(newData);
		while (logHistory.Count > HISTORY_COUNT)
		{
			logHistory.RemoveAt(0);
		}

        if (DebugBridge.AILogCacheWriteImmediate)
        {
            WriteAllToDisk();
            logHistory.Clear();
        }
    }

	public void WriteAllToDisk()
	{
		for (int i = 0; i < logHistory.Count; ++i)
		{
			LogData data = logHistory[i];

			// write output to the file
			using (System.IO.StreamWriter file = new System.IO.StreamWriter(data.filename))
			{
				file.Write(data.data);
			}
			Debug.Log("Wrote Influence Map Data to " + data.filename);
		}
	}

	public string MakeFilename(string prefix)
	{
		// grab the first unit we can
		if (this.combat.AllActors.Count == 0)
		{
			Debug.LogError("AILogCache cannot get BV Context");
			return null;
		}
		AbstractActor anyUnit = this.combat.AllActors[0];
		string dirName = anyUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_InfluenceMapCalculationLogDirectory).StringVal;
		dirName = System.IO.Path.Combine(Application.persistentDataPath, dirName);
		try
		{
			System.IO.Directory.CreateDirectory(dirName);
		}
		catch (System.IO.IOException ioException)
		{
			UnityEngine.Debug.LogError(string.Format("Failed to create directory {0} : exception {1}", dirName, ioException));
			return null;
		}

		System.DateTime timeNow = System.DateTime.Now;

		string filename = string.Format("{0}_{1:D4}_{2:D2}_{3:D2}_{4:D2}.{5:D2}.{6:D2}_r{7:D2}_p{8:D2}.txt",
			prefix,
			timeNow.Year, timeNow.Month, timeNow.Day,
			timeNow.Hour, timeNow.Minute, timeNow.Second,
            this.combat.TurnDirector.CurrentRound,
            this.combat.TurnDirector.CurrentPhase);

		return System.IO.Path.Combine(dirName, filename);
	}
}
