using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BattleTech.ModSupport;
using HBS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public abstract class BaseModDef : IJsonTemplated
{
    [JsonIgnore]
    public string Directory { get; set; }

    [JsonProperty(Required = Required.Always)]
    public string Name { get; set; }

    // informational
    public string Description { get; set; }
    public string Author { get; set; }
    public string Website { get; set; }
    public string Contact { get; set; }

    // versioning
    public string Version { get; set; }
    public DateTime? PackagedOn { get; set; }
    public string BattleTechVersionMin { get; set; }
    public string BattleTechVersionMax { get; set; }
    public string BattleTechVersion { get; set; }

    // this will abort loading by ModLoader if set to false
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    [DefaultValue(false)]
    public bool FailedToLoad { get; set; } = false;

    // load order and requirements
    public List<string> DependsOn { get; set; } = new List<string>();
    public List<string> ConflictsWith { get; set; } = new List<string>();
    public List<string> OptionallyDependsOn { get; set; } = new List<string>();

    [DefaultValue(false)]
    public bool IgnoreLoadFailure { get; set; }

    public bool IsSaveAffecting = true;

    // adding and running code
    [JsonIgnore]
    public Assembly Assembly { get; set; }
    public string DLL { get; set; }
    public string DLLEntryPoint { get; set; }

    [DefaultValue(false)]
    public bool EnableAssemblyVersionCheck { get; set; } = false;
    
    // custom resources types that will be passed into FinishedLoading method
    public List<string> CustomResourceTypes { get; set; } = new List<string>();
    
    // a settings file to be nice to our users and have a known place for settings
    // these will be different depending on the mod obviously
    public JObject Settings { get; set; } = new JObject();
   
    /// <summary>
    /// Checks if all dependencies are present in param loaded
    /// </summary>
    public bool AreDependenciesResolved(IEnumerable<string> loaded)
    {
        bool areDependenciesResolved = DependsOn.Count == 0 || DependsOn.Intersect(loaded).Count() == DependsOn.Count;
        return areDependenciesResolved;
    }

	/// <summary>
	/// Checks if any optional dependencies still need to be loaded
	/// </summary>
	public bool AreOptionalDependenciesResolved(List<string> notLoaded)
	{
		foreach (string optionalDependencyModName in OptionallyDependsOn)
		{
			// If the notLoadedList contains a mod we optionally depend on, we can't load yet.
			if (notLoaded.Contains(optionalDependencyModName))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Checks against provided list of mods to see if any of them conflict
	/// </summary>
	public bool HasConflicts(IEnumerable<string> otherMods)
    {
        return ConflictsWith.Intersect(otherMods).Any();
    }
    
    public abstract string ToJSON();
    public abstract void FromJSON(string json);
    public abstract string GenerateJSONTemplate();
}

public class GameModDef : BaseModDef
{
    // changing implicit loading behavior
    [DefaultValue(true)]
    public bool LoadImplicitManifest { get; set; } = true;

    // manifest, for including any kind of things to add to the game's manifest
    public List<ModEntry> Manifest { get; set; } = new List<ModEntry>();

    // remove these entries by ID from the game
    public List<string> RemoveManifestEntries { get; set; } = new List<string>();

	// A list of data to add into the game. Currently used for Factions and other dynamic enums.
	public List<DataAddendumEntry> DataAddendumEntries { get; set; } = new List<DataAddendumEntry>();

	// A list of data that points to sql files to run.
	public List<SqlEntry> SqlEntries { get; set; } = new List<SqlEntry>();

	// Returns true if any work needs to be done when this moddef is loaded.
	public bool HasModData
	{
		get
		{
			return Manifest.Count > 0 || RemoveManifestEntries.Count > 0 || DataAddendumEntries.Count > 0 || SqlEntries.Count > 0;
		}
	}

    #region IJson
    public override string ToJSON()
    {
        return JSONSerializationUtility.ToJSON(this);
    }

    public override void FromJSON(string json)
    {
        JsonConvert.PopulateObject(json,this);
    }

    public override string GenerateJSONTemplate()
    {
        return JSONSerializationUtility.ToJSON(new GameModDef());
    }
    #endregion
}
