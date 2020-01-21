using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BattleTech;
using BattleTech.Data;
using BattleTech.ModSupport.Caches;
using BattleTech.ModSupport.Utils;
using BattleTech.Save;
using BattleTech.UI;
using Harmony;
using HBS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleTech.ModSupport
{
	public static class ModLoader
	{
		public static ModLogger modLogger = null;

		private static DataManager dm;

		private static readonly string[] IGNORE_LIST = { ".DS_STORE", "~", ".nomedia" };
		private static readonly string[] VANILLA_TYPES = Enum.GetNames(typeof(BattleTechResourceType));

		private static readonly string[] MODTEK_TYPES = { "Video", "AdvancedJSONMerge", "GameTip", "SoundBank", "DebugSettings" };

		public static string GameDirectory { get; private set; }
		public static string ModsDirectory { get; private set; }
		public static string StreamingAssetsDirectory { get; private set; }
		public static string HarmonyPath { get; private set; }
		public static string HarmonyVersion { get; private set; }
		public static string BattletechUserDirectory { get; set; }

		//Names
		private const string MYGAMES_DIRECTORY_NAME = "My Games";
		private const string BATTLETECH_DIRECTORY_NAME = "BattleTech";
		private const string MODS_DIRECTORY_NAME = "mods";
		private const string HBS_DIRECTORY_NAME = "HBS";
		private const string HARMONEY_DLL_NAME = "0Harmony.dll";
		private const string NEWTON_DLL_NAME = "Newtonsoft.Json.dll";
		private const string LOAD_ORDER_FILE_NAME = "load_order.json";
		private const string SYSTEM_LOAD_ORDER_FILE_NAME = "system_load_order.json";
		private const string MOD_JSON_NAME = "mod.json";
		private const string MOD_STATUS_FILE_NAME = "mod_status.json";
		private const string SYSTEM_MOD_JSON_NAME = "systemMod.json";

		//"Hopefully" Temp
		private const string CACHE_DIRECTORY_NAME = "Cache";
		private const string MERGE_CACHE_FILE_NAME = "merge_cache.json";
		private const string TYPE_CACHE_FILE_NAME = "type_cache.json";
		private const string DB_CACHE_FILE_NAME = "database_cache.json";
		private const string MDD_FILE_NAME = "MetadataDatabase.db";
		private const string DATABASE_DIRECTORY_NAME = "Database";
		private const string DATA_DIRECTORY_NAME = "data";
		private const string HARMONY_SUMMARY_FILE_NAME = "harmony_summary.log";

		public const string ModLoggerFileName = "modloader.log";
		public const string ModLoggerPreviousFileName = "modloader_previous.log";


		public static string CacheDirectory { get; private set; }
		public static string MergeCachePath { get; private set; }
		public static string TypeCachePath { get; private set; }
		public static string ModStatusFilePath { get; private set; }
		public static string DBCachePath { get; private set; }
		public static string MDDBPath { get; private set; }
		public static string ModMDDBPath { get; private set; }
		public static string DatabaseDirectory { get; private set; }
		public static string DataDirectory { get; private set; }
		private static string NewtonPath { get; set; }

		//Paths
		private static string MyGamesDirectory { get; set; }
		private static string HBSDirectory { get; set; }
		private static string ManifestDirectory { get; set; }
		private static string LoadOrderPath { get; set; }
		private static string SystemModLoadOrderPath { get; set; }
		private static string HarmonySummaryPath { get; set; }

		public static string ModLoggerPath { get; set; }
		public static string ModLoggerPreviousPath { get; set; }

		//Streaming Assets/data/mods/HBS
		private static string HBSModsSourceDirectory { get; set; }

        private static string[] ModFilePaths { get; set; }
        private static string[] SystemModFilePaths { get; set; }

        private static List<string> modEntryList = new List<string>();
        private static List<VersionManifestEntry> tempModEntryList = new List<VersionManifestEntry>();

        //All Mod Definitions
        /// <summary>
        /// Complete list of both System and Game Mod Definitions.
        /// To be used where you would want to have a complete collection of all existing mods.
        /// </summary>
        public static Dictionary<string, BaseModDef> ModDefs
        {
            get
            {
                var modDefs = GenerateBaseModDefs();
                return modDefs;
            }
        }

        //System Mod Definitions
        /// <summary>
        /// Mod Definitions that are loaded without a manifest/assets associated with them.
        /// These mods are loaded first and can mod the mod loading process.
        /// </summary>
        public static Dictionary<string, SystemModDef> SystemModDefs = new Dictionary<string, SystemModDef>();
        
        //Game Mod Definitions
        /// <summary>
        /// Mod Definitions that are loaded with a manifest/assets
        /// These mods are loaded after the initial loop of SystemMods have been loaded and initialized 
        /// </summary>
        public static Dictionary<string, GameModDef> GameModDefs = new Dictionary<string, GameModDef>();

        public static List<string> ActiveModDefs
        {
            get
            {
                List<string> activeDefs = new List<string>();
                foreach (BaseModDef def in ModDefs.Values)
                {
                    if (def.Enabled)
                    {
                        activeDefs.Add(def.Name);
                    }
                }

                return activeDefs;
            }
        }

        public static List<string> SaveAffectingModDefs
        {
            get
            {
                List<string> saveAffectingDefs = new List<string>();

                for (int i = 0; i < ActiveModDefs.Count; i++)
                {
                    if (ModDefs[ActiveModDefs[i]].IsSaveAffecting)
                    {
                        saveAffectingDefs.Add(ActiveModDefs[i]);
                    }
                }

                return saveAffectingDefs;
            }
        }

        private static List<string> ModLoadOrder;
        private static List<string> SystemModLoadOrder;
        public static HashSet<string> FailedToLoadMods { get; } = new HashSet<string>();
        public static List<BaseModDef> FailedToLoadModDefs { get; } = new List<BaseModDef>();
        private static Dictionary<string, Assembly> TryResolveAssemblies = new Dictionary<string, Assembly>();

        // internal temp structures
        private static Stopwatch stopwatch = new Stopwatch();
        private static Dictionary<string, JObject> cachedJObjects = new Dictionary<string, JObject>();

        private static Dictionary<string, Dictionary<string, List<MergeEntry>>> merges =
            new Dictionary<string, Dictionary<string, List<MergeEntry>>>();

        //
        internal static string DebugSettingsPath { get; } =
            Path.Combine(Path.Combine("data", "debug"), "settings.json");

        //Maybe Temp?
        public static VersionManifest CachedVersionManifest;
        private static List<ModEntry> AddBTRLEntries = new List<ModEntry>();
        private static List<VersionManifestEntry> RemoveBTRLEntries = new List<VersionManifestEntry>();

        public static Dictionary<string, Dictionary<string, VersionManifestEntry>> CustomResources =
            new Dictionary<string, Dictionary<string, VersionManifestEntry>>();

        public static Dictionary<string, string> ModAssetBundlePaths { get; } = new Dictionary<string, string>();

        public static bool AreModsEnabled => PlayerPrefs.GetInt("ModsEnabled", 0) == 1;

        private static bool modsFinishedLoading = false;

        public static Dictionary<string, bool> loadedModStatus = new Dictionary<string, bool>();
        //private static Dictionary<string, bool> previouslyLoadedModStatus = new Dictionary<string, bool>();
        
        public static bool FinishedLoading
        {
            get { return modsFinishedLoading; }
        }

        private static Action OnModLoadComplete;

        public static void Init(Action callback)
        {
			// Tell logging to make sure and log things while we're loging mods.
			HBS.Logging.Logger.ForceEnableLogging = true;

            OnModLoadComplete += callback;
            dm = UnityGameInstance.BattleTechGame.DataManager;

            ManifestDirectory = Path.GetDirectoryName(VersionManifestUtilities.MANIFEST_FILEPATH);
            GameDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));


#if UNITY_EDITOR
            StreamingAssetsDirectory = Path.GetFullPath(Path.Combine(ManifestDirectory, ".."));
#else
            StreamingAssetsDirectory = Path.GetFullPath(Path.Combine(ManifestDirectory, "../../StreamingAssets"));
#endif

            DataDirectory = Path.GetFullPath(Path.Combine(StreamingAssetsDirectory, DATA_DIRECTORY_NAME));

            HBSModsSourceDirectory = Path.GetFullPath(Path.Combine(Path.Combine(DataDirectory, MODS_DIRECTORY_NAME), HBS_DIRECTORY_NAME));
            GameDirectory = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(ManifestDirectory, ".."), ".."), ".."));

            MDDBPath = Path.Combine(Path.Combine(Path.Combine(ManifestDirectory, "../../StreamingAssets"), "MDD"), MDD_FILE_NAME);

            //Specific folder will need to be OS agnostic
            MyGamesDirectory = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), MYGAMES_DIRECTORY_NAME));
            BattletechUserDirectory = Path.Combine(MyGamesDirectory, BATTLETECH_DIRECTORY_NAME);

            ModsDirectory = Path.Combine(Path.Combine(BattletechUserDirectory, MODS_DIRECTORY_NAME));
            HBSDirectory = Path.Combine(ModsDirectory, HBS_DIRECTORY_NAME);
            HarmonyPath = Path.Combine(HBSDirectory, HARMONEY_DLL_NAME);
            LoadOrderPath = Path.Combine(ModsDirectory, LOAD_ORDER_FILE_NAME);
            SystemModLoadOrderPath = Path.Combine(ModsDirectory, SYSTEM_LOAD_ORDER_FILE_NAME);
            NewtonPath = Path.Combine(HBSDirectory, NEWTON_DLL_NAME);

            //
            CacheDirectory = Path.Combine(HBSDirectory, CACHE_DIRECTORY_NAME);
            MergeCachePath = Path.Combine(CacheDirectory, MERGE_CACHE_FILE_NAME);
            TypeCachePath = Path.Combine(CacheDirectory, TYPE_CACHE_FILE_NAME);
            ModStatusFilePath = Path.Combine(CacheDirectory, MOD_STATUS_FILE_NAME);

            HarmonySummaryPath = Path.Combine(HBSDirectory, HARMONY_SUMMARY_FILE_NAME);
            DatabaseDirectory = Path.Combine(HBSDirectory, DATABASE_DIRECTORY_NAME);
            DBCachePath = Path.Combine(DatabaseDirectory, DB_CACHE_FILE_NAME);
            ModMDDBPath = Path.Combine(DatabaseDirectory, MDD_FILE_NAME);


			ModLoggerPreviousPath = Path.Combine(ModsDirectory, ModLoggerPreviousFileName);
			ModLoggerPath = Path.Combine(ModsDirectory, ModLoggerFileName);

			//LogAllPaths();

			//Check for HBS Mods Folder
			if (!Directory.Exists(HBSDirectory) || !File.Exists(HarmonyPath))
            {
                //Create HBS Mods Directory
                //Copy from streaming assets the mods folder including harmony
                Directory.CreateDirectory(HBSDirectory);

                //Create Source File Directories
                var sourceDirectories =
                    Directory.GetDirectories(HBSModsSourceDirectory, "*", SearchOption.AllDirectories);
                foreach (string sourceDirectory in sourceDirectories)
                {
                    Directory.CreateDirectory(sourceDirectory.Replace(HBSModsSourceDirectory, HBSDirectory));
                }

                //Grab Source Files and Copy to User Mods folder
#if UNITY_EDITOR
                var sourceFiles = Directory.GetFiles(HBSModsSourceDirectory, "*", SearchOption.AllDirectories)
                    .Where(name => !name.EndsWith(".meta"));
#else
                var sourceFiles = Directory.GetFiles(HBSModsSourceDirectory, "*", SearchOption.AllDirectories);
#endif
                foreach (string sourcePath in sourceFiles)
                {
                    File.Copy(sourcePath, sourcePath.Replace(HBSModsSourceDirectory, HBSDirectory), true);
                }
            }

            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }

            if (!Directory.Exists(DatabaseDirectory))
            {
                Directory.CreateDirectory(DatabaseDirectory);
            }

			if (File.Exists(ModLoggerPath))
			{
				File.Copy(ModLoggerPath, ModLoggerPreviousPath, overwrite: true);
			}

			modLogger = new ModLogger(ModLoggerPath, DebugBridge.ModLoggerLogLevel);

            // Load Harmony
            FileVersionInfo harmonyInfo = FileVersionInfo.GetVersionInfo(HarmonyPath);
            HarmonyVersion = harmonyInfo.FileVersion;

            AssemblyUtil.LoadDLL(NewtonPath);

            // setup assembly resolver
            TryResolveAssemblies.Add("0Harmony", Assembly.GetAssembly(typeof(HarmonyInstance)));
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var resolvingName = new AssemblyName(args.Name);
                return !TryResolveAssemblies.TryGetValue(resolvingName.Name, out var assembly) ? null : assembly;
            };

            try
            {
                HarmonyInstance.Create("ModLoader").PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                modLogger.LogException("Error: PATCHING FAILED!", e);
                return;
            }

            //Collect all ModJSON files
            if (Directory.Exists(ModsDirectory))
            {
                SystemModFilePaths = Directory.GetFiles(ModsDirectory, SYSTEM_MOD_JSON_NAME, SearchOption.AllDirectories);
                foreach (var systemModFilePath in SystemModFilePaths)
                {
                    modLogger.Log("SystemModPath : " + systemModFilePath);
                }
                
                ModFilePaths = Directory.GetFiles(ModsDirectory, MOD_JSON_NAME, SearchOption.AllDirectories);
                foreach (string modFilePath in ModFilePaths)
                {
                    modLogger.Log("ModPath : " + modFilePath);
                }

				modLogger.Flush();
				InitResources();
            }
        }

        private static void LogAllPaths()
        {
            modLogger.LogError("Not really errors, I just want to see all the paths involved.");
            modLogger.LogError($"CacheDirectory[{CacheDirectory}]");
            modLogger.LogError($"MergeCachePath[{MergeCachePath}]");
            modLogger.LogError($"TypeCachePath[{TypeCachePath}]");
            modLogger.LogError($"DBCachePath[{DBCachePath}]");
            modLogger.LogError($"MDDBPath[{MDDBPath}]");
            modLogger.LogError($"ModMDDBPath[{ModMDDBPath}]");
            modLogger.LogError($"DatabaseDirectory[{DatabaseDirectory}]");
            modLogger.LogError($"DataDirectory[{DataDirectory}]");
            modLogger.LogError($"NewtonPath[{NewtonPath}]");
            modLogger.LogError($"MyGamesDirectory[{MyGamesDirectory}]");
            modLogger.LogError($"BattletechUserDirectory[{BattletechUserDirectory}]");
            modLogger.LogError($"HBSDirectory[{HBSDirectory}]");
            modLogger.LogError($"ManifestDirectory[{ManifestDirectory}]");
            modLogger.LogError($"LoadOrderPath[{LoadOrderPath}]");
            modLogger.LogError($"HarmonySummaryPath[{HarmonySummaryPath}]");
            modLogger.LogError($"HBSModsSourceDirectory[{HBSModsSourceDirectory}]");
			modLogger.Flush();
		}

		private static Dictionary<string, BaseModDef> GenerateBaseModDefs()
        {
            Dictionary<string, BaseModDef> modDefs = new Dictionary<string, BaseModDef>();
            
            if (SystemModDefs != null)
            {
                foreach (var kvp in SystemModDefs)
                {
                    modDefs.Add(kvp.Key, kvp.Value);
                }                    
            }

            if (GameModDefs != null)
            {
                foreach (var kvp in GameModDefs)
                {
                    modDefs.Add(kvp.Key, kvp.Value);
                }
            }

            return modDefs;
        }

        private static void InitResources()
        {
            InitSystemModResources();
        }

        private static void InitSystemModResources()
        {
            MountSystemModAddendum();
            RequestSystemModResources();
        }

        private static void InitModResources()
        {
            MountGameModAddendum();
            RequestGameModResources();
        }

        private static void MountSystemModAddendum()
        {
            VersionManifestAddendum modAddendum = new VersionManifestAddendum("SystemMods");

            foreach (string systemModFilePath in SystemModFilePaths)
            {
                modAddendum.Add(VersionManifestEntry.CreateAddendumEntry(
                    Path.GetFileName(Path.GetDirectoryName(systemModFilePath)), systemModFilePath,
                    BattleTechResourceType.SystemModDef.ToString(), DateTime.Now, "1"));
            }

            dm.ResourceLocator.ApplyAddendum(modAddendum);
        }
        
        private static void MountGameModAddendum()
        {
            VersionManifestAddendum modAddendum = new VersionManifestAddendum("Mods");

            foreach (string modFilePath in ModFilePaths)
            {
                modAddendum.Add(VersionManifestEntry.CreateAddendumEntry(
                    Path.GetFileName(Path.GetDirectoryName(modFilePath)), modFilePath,
                    BattleTechResourceType.GameModDef.ToString(), DateTime.Now, "1"));
            }

            dm.ResourceLocator.ApplyAddendum(modAddendum);
        }

        private static void RequestSystemModResources()
        {
            //DM Load Request for SystemMods
            LoadRequest loadRequest = dm.CreateLoadRequest(OnSystemModLoadComplete);
            VersionManifestEntry[] entryList = dm.ResourceLocator.AllEntriesOfResource(BattleTechResourceType.SystemModDef);
            if (modEntryList.Count == 0 || modEntryList.Count != entryList.Length)
            {
                for (int i = 0; i < entryList.Length; i++)
                {
                    VersionManifestEntry entry = entryList[i];
                    if (entry.IsTemplate)
                    {
                        continue;
                    }

                    if (modEntryList.Contains(entry.Id))
                    {
                        continue;
                    }

                    loadRequest.AddBlindLoadRequest(BattleTechResourceType.SystemModDef, entry.Id);
                    tempModEntryList.Add(entry);
                }
            }

            loadRequest.ProcessRequests();
        }
        
        private static void RequestGameModResources()
        {
            //DM Load Request for Mods
            LoadRequest loadRequest = dm.CreateLoadRequest(OnLoadComplete);
            VersionManifestEntry[] entryList = dm.ResourceLocator.AllEntriesOfResource(BattleTechResourceType.GameModDef);
            if (modEntryList.Count == 0 || modEntryList.Count != entryList.Length)
            {
                for (int i = 0; i < entryList.Length; i++)
                {
                    VersionManifestEntry entry = entryList[i];
                    if (entry.IsTemplate)
                    {
                        continue;
                    }

                    if (modEntryList.Contains(entry.Id))
                    {
                        continue;
                    }

                    loadRequest.AddBlindLoadRequest(BattleTechResourceType.GameModDef, entry.Id);
                    tempModEntryList.Add(entry);
                }
            }

            loadRequest.ProcessRequests();
        }

        private static void OnSystemModLoadComplete(LoadRequest loadRequest)
        {
            PopulateSystemModDefs();
        }
        
        private static void OnLoadComplete(LoadRequest loadRequest)
        {
            PopulateGameModDefs();
        }

        private static void PopulateSystemModDefs()
        {
            foreach (VersionManifestEntry entry in tempModEntryList)
            {
                if (dm.SystemModDefs.Exists(entry.Id))
                {
                    SystemModDef systemModDef = dm.SystemModDefs.Get(entry.Id);
                    if (SystemModDefs.ContainsKey(systemModDef.Name))
                    {
                        continue;
                    }

                    systemModDef.Directory = Path.GetDirectoryName(entry.FilePath);
                    SystemModDefs.Add(systemModDef.Name, systemModDef);
                }
            }
            
            tempModEntryList.Clear();
            
            if (AreModsEnabled)
            {
                LoadingCurtain.ExecuteWhenVisible(LoadSystemMods);

                LoadingCurtain.ShowUntil(() => FinishedLoading, false, GameTipManager.GameTipType.Any, 1.0f);
            }
            else
            {
				// Tell logging it can go back to normal.
				HBS.Logging.Logger.ForceEnableLogging = false;

                OnModLoadComplete.Invoke();
                OnModLoadComplete = null;
            }
        }
        
        private static void PopulateGameModDefs()
        {
            foreach (VersionManifestEntry entry in tempModEntryList)
            {
                if (dm.GameModDefs.Exists(entry.Id))
                {
                    GameModDef moddef = dm.GameModDefs.Get(entry.Id);
                    if (GameModDefs.ContainsKey(moddef.Name))
                    {
                        continue;
                    }

                    moddef.Directory = Path.GetDirectoryName(entry.FilePath);
                    GameModDefs.Add(moddef.Name, moddef);
                }
            }

            tempModEntryList.Clear();
            
            LoadMods();
        }

        private static void LoadSystemMods()
        {
            CustomResources.Add("DebugSettings", new Dictionary<string, VersionManifestEntry>());
            CustomResources["DebugSettings"]["settings"] = new VersionManifestEntry("settings", Path.Combine(StreamingAssetsDirectory, DebugSettingsPath), "DebugSettings", DateTime.Now, "1");
            
            ClearModCacheIfNecessary(SystemModDefs);
            
            InitSystemModsLoop();
            SystemModFinishLoop();

            UpdatePreviouslyLoadedMods(SystemModDefs);
            
            //Start the actual game mod loading process
            //Things should be moddable from here forward
            InitModResources();
        }
        
        private static void LoadMods()
        {
            CachedVersionManifest = VersionManifestUtilities.LoadDefaultManifest();

            CustomResources.Add("Video", new Dictionary<string, VersionManifestEntry>());
            CustomResources.Add("SoundBank", new Dictionary<string, VersionManifestEntry>());
            
            CustomResources.Add("GameTip", new Dictionary<string, VersionManifestEntry>());
            CustomResources["GameTip"]["general"] = new VersionManifestEntry("general", Path.Combine(StreamingAssetsDirectory, Path.Combine("GameTips", "general.txt")), "GameTip", DateTime.Now, "1");
            CustomResources["GameTip"]["combat"] = new VersionManifestEntry("combat", Path.Combine(StreamingAssetsDirectory, Path.Combine("GameTips", "combat.txt")), "GameTip", DateTime.Now, "1");
            CustomResources["GameTip"]["lore"] = new VersionManifestEntry("lore", Path.Combine(StreamingAssetsDirectory, Path.Combine("GameTips", "lore.txt")), "GameTip", DateTime.Now, "1");
            CustomResources["GameTip"]["sim"] = new VersionManifestEntry("sim", Path.Combine(StreamingAssetsDirectory, Path.Combine("GameTips", "sim.txt")), "GameTip", DateTime.Now, "1");

            ClearModCacheIfNecessary(GameModDefs);

            InitGameModsLoop();
            HandleModManifestLoop();
            MergeFilesLoop();
            AddToDBLoop();
            FinishLoop();

            UpdatePreviouslyLoadedMods(GameModDefs);

            modsFinishedLoading = true;

			// Tell logging it can go back to normal.
			HBS.Logging.Logger.ForceEnableLogging = false;

            OnModLoadComplete.Invoke();
            OnModLoadComplete = null;
        }

        public static bool modCacheCleared = false;

        private static void ClearModCacheIfNecessary<T>(Dictionary<string, T> modDefs) where T : BaseModDef
        {
			if (DebugBridge.AlwaysClearModCache)
			{
				ClearModCache();
				return;
			}

            bool clearModCache = false;
            if (modDefs != null)
            {
                LocalUserSettings playerSettings = ActiveOrDefaultSettings.LocalSettings;
                loadedModStatus = GetOrCreateModStatusFromFile(ModStatusFilePath);

                List<string> loadedMods = loadedModStatus.Keys.ToList();
                List<string> previouslyLoadedMods = playerSettings.previouslyLoadedMods.Keys.ToList();

                // See if any mods exist in one list but not the other.
                List<string> loadedNotPreviouslyLoaded = loadedMods.Except(previouslyLoadedMods).ToList();
                List<string> previouslyLoadedNotLoaded = previouslyLoadedMods.Except(loadedMods).ToList();
                if (loadedNotPreviouslyLoaded.Count != 0 || previouslyLoadedNotLoaded.Count != 0)
                {
                    modLogger.LogWarning($"Previously loaded mod have changed. Clearing the ModCache/mddb to start from scratch.");
                    clearModCache = true;
                }
                // Otherwise the lists are the same.
                else
                {
                    // See if any mod was enabled or disabled.
                    for (int i = 0; i < loadedMods.Count; ++i)
                    {
                        string modName = loadedMods[i];
                        if (loadedModStatus[modName] != playerSettings.previouslyLoadedMods[modName])
                        {
                            modLogger.LogWarning($"Previously loaded mod have changed. Clearing the ModCache/mddb to start from scratch.");
                            clearModCache = true;
                        }
                    }
                }

                // See if a mod was deleted.
                bool modWasDeleted = false;
                List<string> cachedOrder = LoadOrder.FromFile(LoadOrderPath, removeVersionNumberEntry:false);

				bool versionChanged = false;

				// IF we don't have a cached order, clear the mod cache.
				if (cachedOrder.Count == 0)
					versionChanged = true;
				else
				{
					// If the first item doesn't match the ReleaseVersion, clear the mod cache.
					if (cachedOrder[0] != VersionInfo.GetReleaseVersionForModLoading())
					{
						versionChanged = true;
					}

					// Remove the version entry.
					cachedOrder.RemoveAt(0);
				}

				if (versionChanged)
				{
					clearModCache = true;
					modLogger.LogWarning($"The version of the game has changed since previously loading mods. Clearing the ModCache/mddb to reapply mods from scratch.");
				}
				else
				{
					// Loop through the mods that existed during the last run.
					foreach (string modName in cachedOrder)
					{
						if (!modDefs.ContainsKey(modName))
						{
							modLogger.LogWarning($"Previously loaded mod[{modName}] was no longer found in the list of mods. Clearing the ModCache/mddb to reapply mods from scratch.");
							modWasDeleted = true;
						}
					}

					if (modWasDeleted)
					{
						clearModCache = true;
					}
				}
            }

            if (clearModCache)
                ClearModCache();
        }

        private static void InitSystemModsLoop()
        {
            if (SystemModDefs != null)
            {
                DBCache.EnsureModMDDB(MDDBPath, ModMDDBPath);
                
                MergeModDependencies(SystemModDefs);
                List<string> cachedOrder = LoadOrder.FromFile(SystemModLoadOrderPath, removeVersionNumberEntry:true);
                SystemModLoadOrder = GenerateLoadOrder(SystemModDefs, cachedOrder);
                
                foreach (var modName in SystemModLoadOrder)
                {
                    var modDef = SystemModDefs[modName];

                    //Check to see if Mod Dependencies failed to load
                    if (modDef.DependsOn.Intersect(FailedToLoadMods).Any())
                    {
                        FailToLoadModDef(modDef);
                        continue;
                    }

                    try
                    {
                        //If we've loaded this before, check if it is enabled
                        if (loadedModStatus.ContainsKey(modDef.Name))
                        {
                            if (loadedModStatus[modDef.Name])
                            {
                                if (!LoadSystemMod(modDef))
                                {
                                    FailToLoadModDef(modDef);
                                }
                            }
                        }
                        // Else this is a new mod we haven't seen before
                        else
                        {
                            // Try to load it
                            if (!LoadSystemMod(modDef))
                            {
                                FailToLoadModDef(modDef, logWarning: false);
                            }
                            else
                            {
                                loadedModStatus.Add(modDef.Name, modDef.Enabled);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        modLogger.LogException($"Error: Tried to load mod: {modDef.Name}, but something went wrong. Make sure all of your JSON is correct!", e);


						FailToLoadModDef(modDef);
                        if (modDef.IgnoreLoadFailure)
                        {
                            continue;
                        }
                    }
                }
            }
        }
        
        private static void InitGameModsLoop()
        {
            if (GameModDefs != null)
            {
                DBCache.EnsureModMDDB(MDDBPath, ModMDDBPath);

                MergeModDependencies(GameModDefs);
                List<string> cachedOrder = LoadOrder.FromFile(LoadOrderPath, removeVersionNumberEntry:true);
                ModLoadOrder = GenerateLoadOrder(GameModDefs, cachedOrder);

                foreach (var modName in ModLoadOrder)
                {
                    var modDef = GameModDefs[modName];

                    //Check to see if Mod Dependencies failed to load
                    if (modDef.DependsOn.Intersect(FailedToLoadMods).Any())
                    {
                        FailToLoadModDef(modDef);
                        continue;
                    }

                    try
                    {
                        //If we've loaded this before, check if it is enabled
                        if (loadedModStatus.ContainsKey(modDef.Name))
                        {
                            if (loadedModStatus[modDef.Name])
                            {
                                if (!LoadGameMod(modDef))
                                {
                                    FailToLoadModDef(modDef);
                                }
								modLogger.Flush();
							}
						}
                        // Else this is a new mod we haven't seen before
                        else
                        {
                            // Try to load it
                            if (!LoadGameMod(modDef))
                            {
                                FailToLoadModDef(modDef, logWarning: false);
                            }
                            else
                            {
                                loadedModStatus.Add(modDef.Name, modDef.Enabled);
                            }
							modLogger.Flush();
						}
					}
                    catch (Exception e)
                    {
                        modLogger.LogException($"Error: Tried to load mod: {modDef.Name}, but something went wrong. Make sure all of your JSON is correct!", e);

                        FailToLoadModDef(modDef);
                        if (modDef.IgnoreLoadFailure)
                        {
                            continue;
                        }
                    }
                }
            }
        }

        private static void ClearModCache()
        {
            DBCache.RecreateModMDDB(MDDBPath, ModMDDBPath);

            DeleteFileIfExists(LoadOrderPath);
            DeleteFileIfExists(MergeCachePath);
            DeleteFileIfExists(TypeCachePath);
            DeleteFileIfExists(DBCachePath);

            modCacheCleared = true;
        }

        public static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void FailToLoadModDef<T>(T modDef, bool logWarning = true) where T : BaseModDef
        {
            if (GameModDefs.ContainsKey(modDef.Name))
            {
                GameModDefs.Remove(modDef.Name);    
            }
            else if (SystemModDefs.ContainsKey(modDef.Name))
            {
                SystemModDefs.Remove(modDef.Name);
            }

            FailedToLoadModDefs.Add(modDef);
            modDef.FailedToLoad = true;

            if (!modDef.IgnoreLoadFailure)
            {
                if (logWarning)
                    modLogger.LogWarning($"Warning: Skipping load of {modDef.Name} because one of its dependencies failed to load.");
                FailedToLoadMods.Add(modDef.Name);
				modLogger.Flush();
			}
		}

        /// <summary>
        /// This function walks the dependency chain of all the mods this mod depends on and adds them as dependencies to 
        /// this mod. It has cycle detection and logs an error and unloads the mod when detected. If a mod depends on a mod
        /// that is no longer in the system, it fails that dependency as well.
        /// </summary>
        public static void MergeModDependencies<T>(Dictionary<string, T> baseModDefs) where T : BaseModDef
        {
            bool madeChanges;
            List<string> modsToAdd = new List<string>();

            // TODO/Eck - May need to do something similar with OptionalDependencies.

            // Keep looping through the mods, checking dependencies until we are no longer making changes.
            do
            {
                // Loop through the mods
                madeChanges = false;
                foreach (KeyValuePair<string, T> modDefKeyValue in baseModDefs)
                {
                    var modDef = modDefKeyValue.Value;

                    // If we contain a dependency to ourself, break the dependency chain.
                    if (modDef.DependsOn.Contains(modDef.Name))
                        continue;

                    // Loop through the mods we depend on.
                    modsToAdd.Clear();
                    foreach (string dependedOnModDefName in modDef.DependsOn)
                    {
                        //if (modDef.DependsOn.Contains(dependedOnModDefName))
                        //{
                        // Make sure the mod we depend on exist in the list.
                        if (baseModDefs.ContainsKey(dependedOnModDefName))
                        {
                            // Loop through the dependedOnModDef's DependsOn list
                            BaseModDef dependedOnModDef = baseModDefs[dependedOnModDefName];
                            foreach (string dependedOnDependedOnModDefName in dependedOnModDef.DependsOn)
                            {
                                // If we don't have a direct dependency to a grand-child, add it to our dependency list.
                                if (!modDef.DependsOn.Contains(dependedOnDependedOnModDefName))
                                {
                                    madeChanges = true;
                                    modsToAdd.Add(dependedOnDependedOnModDefName);

                                    // If we have a dependency to ourself, log an error (but still add it to our dependency list so we don't load it later)
                                    if (modDef.Name == dependedOnDependedOnModDefName)
                                    {
                                        modLogger.LogError($"\tError:ModDef[{modDef.Name}] has a dependency to itself through its dependency chain and will fail to load.");
                                    }
                                }
                            }
                        }
                        // If the mod doesn't exist, log an error.
                        else
                        {
                            modLogger.LogError($"\tError:ModDef[{modDef.Name}] depends on [{dependedOnModDefName}], but the depended on mod was not in the ModDefs list.");
                        }
                    }

                    // If we have any mods to add, add them to our list
                    if (modsToAdd.Count > 0)
                    {
                        modDef.DependsOn.AddRange(modsToAdd);
                    }

					modLogger.Flush();
				}
			}
            while (madeChanges == true);
        }

        private static bool LoadSystemMod(SystemModDef systemModDef)
        {
            modLogger.Log($"{systemModDef.Name} {systemModDef.Version}");
            
            //read in custom resource types
            foreach (var customResourceType in systemModDef.CustomResourceTypes)
            {
                if (VANILLA_TYPES.Contains(customResourceType) || MODTEK_TYPES.Contains(customResourceType))
                {
                    modLogger.LogWarning($"\tWarning: {systemModDef.Name} has a custom resource type that has the same name as a vanilla/modtek resource type. Ignoring this type.");
                    continue;
                }

                if (!CustomResources.ContainsKey(customResourceType))
                    CustomResources.Add(customResourceType, new Dictionary<string, VersionManifestEntry>());
            }
            
            // load the mod assembly
            if (systemModDef.DLL != null && !LoadAssemblyAndCallInit(systemModDef))
            {
                return false;
            }

            return true;
        }

        private static bool LoadGameMod(GameModDef gameModDef)
        {
            modLogger.Log($"{gameModDef.Name} {gameModDef.Version}");

            //read in custom resource types
            foreach (var customResourceType in gameModDef.CustomResourceTypes)
            {
                if (VANILLA_TYPES.Contains(customResourceType) || MODTEK_TYPES.Contains(customResourceType))
                {
                    modLogger.LogWarning($"\tWarning: {gameModDef.Name} has a custom resource type that has the same name as a vanilla/modtek resource type. Ignoring this type.");
                    continue;
                }

                if (!CustomResources.ContainsKey(customResourceType))
                    CustomResources.Add(customResourceType, new Dictionary<string, VersionManifestEntry>());
            }

            // expand the manifest (parses all JSON as well)
            var expandedManifest = ExpandManifest(gameModDef);
            if (expandedManifest == null)
            {
                return false;
            }

            // load the mod assembly
            if (gameModDef.DLL != null && !LoadAssemblyAndCallInit(gameModDef))
            {
                return false;
            }

            // replace the manifest with our expanded manifest since we successfully got through loading the other stuff
            if (expandedManifest.Count > 0)
            {
                modLogger.Log($"\t{expandedManifest.Count} manifest entries");
            }

            gameModDef.Manifest = expandedManifest;

            return true;
        }

        private static List<ModEntry> ExpandManifest(GameModDef gameModDef)
        {
            // note: if a JSON has errors, this mod will not load, since InferIDFromFile will throw from parsing the JSON
            var expandedManifest = new List<ModEntry>();

            if (gameModDef.LoadImplicitManifest && gameModDef.Manifest.All(x =>
                    Path.GetFullPath(Path.Combine(gameModDef.Directory, x.Path)) !=
                    Path.GetFullPath(Path.Combine(gameModDef.Directory, "StreamingAssets"))))
                gameModDef.Manifest.Add(new ModEntry("StreamingAssets", true));

            foreach (var modEntry in gameModDef.Manifest)
            {
                // handle prefabs; they have potential internal path to assetbundle
                if (modEntry.Type == "Prefab" && !string.IsNullOrEmpty(modEntry.AssetBundleName))
                {
                    if (!expandedManifest.Any(x => x.Type == "AssetBundle" && x.Id == modEntry.AssetBundleName))
                    {
                        modLogger.LogError($"\tError: {gameModDef.Name} has a Prefab '{modEntry.Id}' that's referencing an AssetBundle '{modEntry.AssetBundleName}' that hasn't been loaded. Put the assetbundle first in the manifest!");
                        return null;
                    }

                    modEntry.Id = Path.GetFileNameWithoutExtension(modEntry.Path);

                    if (!FileIsOnDenyList(modEntry.Path))
                        expandedManifest.Add(modEntry);

                    continue;
                }

                if (string.IsNullOrEmpty(modEntry.Path) && string.IsNullOrEmpty(modEntry.Type) &&
                    modEntry.Path != "StreamingAssets")
                {
                    modLogger.LogError($"\tError: {gameModDef.Name} has a manifest entry that is missing its path or type! Aborting load.");
                    return null;
                }

                if (!string.IsNullOrEmpty(modEntry.Type)
                    && !VANILLA_TYPES.Contains(modEntry.Type)
                    && !MODTEK_TYPES.Contains(modEntry.Type)
                    && !CustomResources.ContainsKey(modEntry.Type))
                {
                    modLogger.LogError($"\tError: {gameModDef.Name} has a manifest entry that has a type '{modEntry.Type}' that doesn't match an existing type and isn't declared in CustomResourceTypes");
                    return null;
                }

                var entryPath = Path.GetFullPath(Path.Combine(gameModDef.Directory, modEntry.Path));
                if (Directory.Exists(entryPath))
                {
                    // path is a directory, add all the files there
                    var files = Directory.GetFiles(entryPath, "*", SearchOption.AllDirectories)
                        .Where(filePath => !FileIsOnDenyList(filePath));
                    foreach (var filePath in files)
                    {
                        var path = Path.GetFullPath(filePath);
                        try
                        {
                            var childModEntry = new ModEntry(modEntry, path, InferIDFromFile(filePath));
                            expandedManifest.Add(childModEntry);
                        }
                        catch (Exception e)
                        {
                            modLogger.LogException($"\tError: Canceling {gameModDef.Name} load!\n\tCaught exception reading file at {GetRelativePath(path, GetRootPath(path))}", e);

							return null;
                        }
                    }
                }
                else if (File.Exists(entryPath) && !FileIsOnDenyList(entryPath))
                {
                    // path is a file, add the single entry
                    try
                    {
                        modEntry.Id = modEntry.Id ?? InferIDFromFile(entryPath);
                        modEntry.Path = entryPath;
                        expandedManifest.Add(modEntry);
                    }
                    catch (Exception e)
                    {
                        modLogger.LogException($"\tError: Canceling {gameModDef.Name} load!\n\tCaught exception reading file at {GetRelativePath(entryPath, GetRootPath(entryPath))}", e);

						return null;
                    }
                }
                else if (modEntry.Path != "StreamingAssets")
                {
                    // path is not StreamingAssets and it's missing
                    modLogger.LogWarning($"\tWarning: Manifest specifies file/directory of {modEntry.Type} at path {modEntry.Path}, but it's not there. Continuing to load.");
                }
            }

			modLogger.Flush();

			return expandedManifest;
        }

        private static bool LoadAssemblyAndCallInit(BaseModDef modDef)
        {
            var dllPath = Path.Combine(modDef.Directory, modDef.DLL);
            string typeName = null;
            var methodName = "Init";

            if (!File.Exists(dllPath))
            {
                modLogger.LogError($"\tError: DLL specified ({dllPath}), but it's missing! Aborting load.");
                return false;
            }

            if (modDef.DLLEntryPoint != null)
            {
                var pos = modDef.DLLEntryPoint.LastIndexOf('.');
                if (pos == -1)
                {
                    methodName = modDef.DLLEntryPoint;
                }
                else
                {
                    typeName = modDef.DLLEntryPoint.Substring(0, pos);
                    methodName = modDef.DLLEntryPoint.Substring(pos + 1);
                }
            }

            var assembly = AssemblyUtil.LoadDLL(dllPath);
            if (assembly == null)
            {
                modLogger.LogError($"\tError: Failed to load mod assembly at path {dllPath}.");
                return false;
            }

            var methods = AssemblyUtil.FindMethods(assembly, methodName, typeName);
            if (methods == null || methods.Length == 0)
            {
                modLogger.LogError($"\t\tError: Could not find any methods in assembly with name '{methodName}' and with type '{typeName ?? "not specified"}'");
                return false;
            }

            foreach (var method in methods)
            {
                var directory = modDef.Directory;
                if (modDef.Settings == null)
                {
                    modDef.Settings = new JObject();
                }
                var settings = modDef.Settings.ToString(Formatting.None);

                var parameterDictionary = new Dictionary<string, object>
                {
                    {"modDir", directory},
                    {"modDirectory", directory},
                    {"directory", directory},
                    {"modSettings", settings},
                    {"settings", settings},
                    {"settingsJson", settings},
                    {"settingsJSON", settings},
                    {"JSON", settings},
                    {"json", settings},
                };

                try
                {
                    if (AssemblyUtil.InvokeMethodByParameterNames(method, parameterDictionary))
                        continue;

                    if (AssemblyUtil.InvokeMethodByParameterTypes(method, new object[] {directory, settings}))
                        continue;
                }
                catch (Exception e)
                {
                    modLogger.LogException($"\tError: While invoking '{method.DeclaringType?.Name}.{method.Name}', an exception occured", e);

					return false;
                }

                modLogger.LogError($"\tError: Could not invoke method with name '{method.DeclaringType?.Name}.{method.Name}'");
				modLogger.Flush();

				return false;
            }

            modDef.Assembly = assembly;

            if (!modDef.EnableAssemblyVersionCheck)
            {
                TryResolveAssemblies.Add(assembly.GetName().Name, assembly);
            }

			modLogger.Flush();

			return true;
        }

        //Goes through and preps the entries present in the Mods Manifest
        private static void HandleModManifestLoop()
        {
            // there are no mods loaded, just return
            if (ModLoadOrder == null || ModLoadOrder.Count == 0)
                return;

            modLogger.Log("\nAdding Mod Content...");
            var typeCache = new TypeCache(TypeCachePath);
            typeCache.UpdateToIDBased();
            modLogger.Log("");

            var manifestMods = ModLoadOrder.Where(name => GameModDefs.ContainsKey(name) && GameModDefs[name].HasModData).ToList();
            foreach (var modName in manifestMods)
            {
                var gameModDef = GameModDefs[modName];

                modLogger.Log($"{modName}:");


                foreach (SqlEntry sqlEntry in gameModDef.SqlEntries)
                {
                    LoadSql(sqlEntry, gameModDef.Directory);
                }

                // Moving dynamic enumerations to the top of the list so other things loaded can count on
                // these entries existing in the database.
                foreach (DataAddendumEntry dataAddendumEntry in gameModDef.DataAddendumEntries)
                {
                    LoadDataAddendum(dataAddendumEntry, gameModDef.Directory);
                }

                if (gameModDef.SqlEntries.Count > 0 || gameModDef.DataAddendumEntries.Count > 0)
                {
                    MetadataDatabase.Instance.WriteInMemoryDBToDisk();
                }

                foreach (var modEntry in gameModDef.Manifest)
                {
                    //Folders may appear as "Typed" with no IDs
                    if (string.IsNullOrEmpty(modEntry.Id) || modEntry.Path.EndsWith(".meta"))
                    {
                        continue;
                    }

                    // type being null means we have to figure out the type from the path (StreamingAssets)
                    if (modEntry.Type == null)
                    {
                        var relativePath = GetRelativePath(modEntry.Path, Path.Combine(gameModDef.Directory, "StreamingAssets"));

                        if (relativePath == DebugSettingsPath)
                            modEntry.Type = "DebugSettings";
                    }

                    // type *still* being null means that this is an "non-special" case, i.e. it's in the manifest
                    if (modEntry.Type == null)
                    {
                        var relativePath = GetRelativePath(modEntry.Path, Path.Combine(gameModDef.Directory, "StreamingAssets"));
                        var fakeStreamingAssetsPath = Path.GetFullPath(Path.Combine(StreamingAssetsDirectory, relativePath));
                        if (!File.Exists(fakeStreamingAssetsPath))
                        {
                            modLogger.LogWarning($"\tWarning: Could not find a file at {fakeStreamingAssetsPath} for {modName} {modEntry.Id}. NOT LOADING THIS FILE");
                            continue;
                        }

                        var types = typeCache.GetTypes(modEntry.Id, CachedVersionManifest);
                        if (types == null)
                        {
                            modLogger.LogWarning($"\tWarning: Could not find an existing VersionManifest entry for {modEntry.Id}. Is this supposed to be a new entry? Don't put new entries in StreamingAssets!");
                            continue;
                        }

                        // this is getting merged later and then added to the BTRL entries then
                        // StreamingAssets don't get default appendText
                        if (Path.GetExtension(modEntry.Path)?.ToLowerInvariant() == ".json" && modEntry.ShouldMergeJSON)
                        {
                            // this assumes that vanilla .json can only have a single type
                            // typeCache will always contain this path
                            modEntry.Type = typeCache.GetTypes(modEntry.Id)[0];
                            AddMerge(modEntry.Type, modEntry.Id, modEntry.Path, modEntry.AddToDB);
                            modLogger.Log($"\tMerge: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" ({modEntry.Type})");
                            continue;
                        }

                        foreach (var type in types)
                        {
                            var subModEntry = new ModEntry(modEntry, modEntry.Path, modEntry.Id);
                            subModEntry.Type = type;
                            AddModEntry(subModEntry);
                            RemoveMerge(type, modEntry.Id);
                        }

                        continue;
                    }

                    // special handling for types
                    switch (modEntry.Type)
                    {
                        case "AdvancedJSONMerge":
                        {
                            var advancedJSONMerge = AdvancedJSONMerge.FromFile(modEntry.Path);

                            if (!string.IsNullOrEmpty(advancedJSONMerge.TargetID) &&
                                advancedJSONMerge.TargetIDs == null)
                                advancedJSONMerge.TargetIDs = new List<string> {advancedJSONMerge.TargetID};

                            if (advancedJSONMerge.TargetIDs == null || advancedJSONMerge.TargetIDs.Count == 0)
                            {
                                modLogger.LogError($"\tError: AdvancedJSONMerge: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" didn't target any IDs. Skipping this merge.");
                                continue;
                            }

                            foreach (var id in advancedJSONMerge.TargetIDs)
                            {
                                var type = advancedJSONMerge.TargetType;
                                if (string.IsNullOrEmpty(type))
                                {
                                    var types = typeCache.GetTypes(id, CachedVersionManifest);
                                    if (types == null || types.Count == 0)
                                    {
                                        modLogger.LogError($"\tError: AdvancedJSONMerge: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" could not resolve type for ID: {id}. Skipping this merge");
                                        continue;
                                    }

                                    // assume that only a single type
                                    type = types[0];
                                }

                                var entry = FindEntry(type, id);
                                if (entry == null)
                                {
                                    modLogger.LogError($"\tError: AdvancedJSONMerge: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" could not find entry {id} ({type}). Skipping this merge");
                                    continue;
                                }

                                AddMerge(type, id, modEntry.Path, modEntry.AddToDB);
                                modLogger.Log($"\tAdvancedJSONMerge: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" targeting '{id}' ({type})");
                            }

                            continue;
                        }
                    }

                    // non-StreamingAssets json merges
                    if (Path.GetExtension(modEntry.Path)?.ToLowerInvariant() == ".json" && modEntry.ShouldMergeJSON
                        || (Path.GetExtension(modEntry.Path)?.ToLowerInvariant() == ".txt" ||
                            Path.GetExtension(modEntry.Path)?.ToLowerInvariant() == ".csv") &&
                        modEntry.ShouldAppendText)
                    {
                        // have to find the original path for the manifest entry that we're merging onto
                        var matchingEntry = FindEntry(modEntry.Type, modEntry.Id);

                        if (matchingEntry == null)
                        {
                            modLogger.LogWarning($"\tWarning: Could not find an existing VersionManifest entry for {modEntry.Id}!");
                            continue;
                        }

                        // this assumes that .json can only have a single type
                        typeCache.TryAddType(modEntry.Id, modEntry.Type);
                        modLogger.Log($"\tMerge: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" ({modEntry.Type})");
                        AddMerge(modEntry.Type, modEntry.Id, modEntry.Path, modEntry.AddToDB);
                        continue;
                    }

                    typeCache.TryAddType(modEntry.Id, modEntry.Type);
                    AddModEntry(modEntry);
                    RemoveMerge(modEntry.Type, modEntry.Id);
                }

                foreach (var removeID in GameModDefs[modName].RemoveManifestEntries)
                {
                    if (!RemoveEntry(removeID, typeCache))
                    {
                        modLogger.LogWarning($"\tWarning: Could not find manifest entries for {removeID} to remove them. Skipping.");
                    }
                }
            }

            typeCache.ToFile(TypeCachePath);
        }

        // This class takes a Data Addendum and uses reflection to call the StaticLoadDataAddendum class. It was originally
        // designed to allow modders to easily append factions into the game and MDDB. But I implemented it for every 
        // DynamicEnumeration class. It could also be extended to other classes.
        public static void LoadDataAddendum(DataAddendumEntry dataAddendumEntry, string modDefDirectory)
        {
            try
            {
                // Get the specified type
                Type type = Type.GetType(dataAddendumEntry.name);
                if (type == null)
                {
                    modLogger.LogError($"\tError: Could not find DataAddendum class named {dataAddendumEntry.name}");
                    return;
                }

                // Get the static load method.
                PropertyInfo instanceProperty = type.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty);
                if (instanceProperty == null)
                {
                    modLogger.LogError($"\tError: Could not find static method [Instance] on class named [{dataAddendumEntry.name}]");
                    return;
                }

                // See if it implements the IDataAddendum interface
                IDataAddendum dataAddendum = instanceProperty.GetValue(null) as IDataAddendum;
                if (dataAddendum == null)
                {
                    modLogger.LogError($"\tError: Class does not implement interface [IDataAddendum] on class named [{dataAddendumEntry.name}]");
                    return;
                }

                // Have the class load it's data.
                string path = Path.Combine(modDefDirectory, dataAddendumEntry.path);
                string jsonData = File.ReadAllText(path);
                dataAddendum.LoadDataAddendum(jsonData, modLogger);

                modLogger.Log($"\tLog: DataAddendum successfully loaded name[{dataAddendumEntry.name}] path[{dataAddendumEntry.path}]");
            }
            catch (Exception ex)
            {
                modLogger.LogException($"\tException: Exception caught while processing DataAddendum [{dataAddendumEntry.name}]", ex);

				return;
            }
        }

        // This class takes a Sql Entry, loads the referenced file, and then executes that sql directly against the database.
        public static void LoadSql(SqlEntry sqlEntry, string modDefDirectory)
        {
            try
            {
                // Have the class load it's data.
                string path = Path.Combine(modDefDirectory, sqlEntry.path);
                string sql = File.ReadAllText(path);
                MetadataDatabase.Instance.Execute(sql);
                modLogger.Log($"\tLog: SqlEntry successfully loaded name[{sqlEntry.name}] path[{sqlEntry.path}]");
            }
            catch (Exception ex)
            {
                modLogger.LogException($"\tException: Exception caught while processing DataAddendum [{sqlEntry.name}]", ex);

				return;
            }
        }

        private static void MergeFilesLoop()
        {
            // there are no mods loaded, just return
            if (ModLoadOrder == null || ModLoadOrder.Count == 0)
                return;

            // perform merges into cache
            modLogger.Log("\nDoing merges...");

            var mergeCache = MergeCache.FromFile(MergeCachePath);
            mergeCache.UpdateToRelativePaths();

            foreach (var type in merges.Keys)
            {
                foreach (var id in merges[type].Keys)
                {
                    var existingEntry = FindEntry(type, id);
                    if (existingEntry == null)
                    {
                        modLogger.LogWarning($"\tWarning: Have merges for {id} but cannot find an original file! Skipping.");
                        continue;
                    }

                    var originalPath = Path.GetFullPath(existingEntry.FilePath);
                    List<MergeEntry> mergeEntries = merges[type][id];

                    MergeCache.CacheEntry cacheEntry = mergeCache.GetOrCreateCachedEntry(originalPath, mergeEntries);

                    // something went wrong (the parent json prob had errors)
                    if (cacheEntry.CacheAbsolutePath == null)
                        continue;

                    var modEntry = new ModEntry(cacheEntry.CacheAbsolutePath)
                    {
                        ShouldAppendText = false,
                        ShouldMergeJSON = false,
                        Type = existingEntry.Type,
                        Id = id,
                        AddToDB = cacheEntry.AddToDb,
                    };

                    AddModEntry(modEntry);
                }
            }

            mergeCache.ToFile(MergeCachePath);
        }

        private static void AddToDBLoop()
        {
            // there are no mods loaded, just return
            if (ModLoadOrder == null || ModLoadOrder.Count == 0)
                return;
            //yield break;

            modLogger.Log("\nSyncing Database...");
            //yield return new ProgressReport(1, "Syncing Database", "", true);

            var dbCache = new DBCache(DBCachePath, MDDBPath, ModMDDBPath, modCacheCleared);
            dbCache.UpdateToRelativePaths();

            // since DB instance is read at type init, before we patch the file location
            // need re-init the mddb to read from the proper modded location
            var mddbTraverse = Traverse.Create(typeof(MetadataDatabase));
            mddbTraverse.Field("instance").SetValue(null);
            mddbTraverse.Method("InitInstance").GetValue();

            // check if files removed from DB cache
            var shouldWriteDB = false;
            var shouldRebuildDB = false;
            var replacementEntries = new List<VersionManifestEntry>();
            var removeEntries = new List<string>();
            foreach (var path in dbCache.Entries.Keys)
            {
                var absolutePath = ResolvePath(path, BattletechUserDirectory);

                // check if the file in the db cache is still used
                if (AddBTRLEntries.Exists(x => x.Path == absolutePath))
                    continue;

                modLogger.Log($"\tNeed to remove DB entry from file in path: {path}");

                // file is missing, check if another entry exists with same filename in manifest or in BTRL entries
                var fileName = Path.GetFileName(path);
                var existingEntry = AddBTRLEntries.FindLast(x => Path.GetFileName(x.Path) == fileName)
                                        ?.GetVersionManifestEntry()
                                    ?? CachedVersionManifest.Find(x => Path.GetFileName(x.FilePath) == fileName);

                // TODO: DOES NOT HANDLE CASE WHERE REMOVING VANILLA CONTENT IN DB

                if (existingEntry == null)
                {
                    modLogger.Log("\t\tHave to rebuild DB, no existing entry in VersionManifest matches removed entry");
                    shouldRebuildDB = true;
                    break;
                }

                replacementEntries.Add(existingEntry);
                removeEntries.Add(path);
            }

            // add removed entries replacements to db
            if (!shouldRebuildDB)
            {
                // remove old entries
                foreach (var removeEntry in removeEntries)
                    dbCache.Entries.Remove(removeEntry);

                foreach (var replacementEntry in replacementEntries)
                {
                    if (AddModEntryToDB(MetadataDatabase.Instance, dbCache, Path.GetFullPath(replacementEntry.FilePath),
                        replacementEntry.Type))
                    {
                        modLogger.Log($"\t\tReplaced DB entry with an existing entry in path: {GetRelativePath(replacementEntry.FilePath, ModLoader.GetRootPath(replacementEntry.FilePath))}");
                        shouldWriteDB = true;
                    }
                }
            }

            // if an entry has been removed and we cannot find a replacement, have to rebuild the mod db
            if (shouldRebuildDB)
                dbCache = new DBCache(null, MDDBPath, ModMDDBPath, modCacheCleared);

            // add needed files to db
            var addCount = 0;
            foreach (var modEntry in AddBTRLEntries)
            {
                if (modEntry.AddToDB &&
                    AddModEntryToDB(MetadataDatabase.Instance, dbCache, modEntry.Path, modEntry.Type))
                {
                    //yield return new ProgressReport(addCount / ((float)AddBTRLEntries.Count), "Populating Database", modEntry.Id);
                    modLogger.Log($"\tAdded/Updated {modEntry.Id} ({modEntry.Type})");
                    shouldWriteDB = true;
                }

                addCount++;
            }

            dbCache.ToFile(DBCachePath);

            if (shouldWriteDB || shouldRebuildDB)
            {
                modLogger.Log("Writing DB");
                MetadataDatabase.Instance.WriteInMemoryDBToDisk();
            }
        }

        private static void SystemModFinishLoop()
        {
            modLogger.Log("\nFinishing Up System Mods");
            
            if (CustomResources["DebugSettings"]["settings"].FilePath != Path.Combine(StreamingAssetsDirectory, DebugSettingsPath))
            {
                DebugBridge.LoadSettings(CustomResources["DebugSettings"]["settings"].FilePath);
            }
            
            if (ModLoadOrder != null && ModLoadOrder.Count > 0)
            {
                CallFinishedLoadMethods(SystemModDefs);
                PrintHarmonySummary(HarmonySummaryPath);
                LoadOrder.ToFile(SystemModLoadOrder, SystemModLoadOrderPath);
            }
        }
        
        private static void FinishLoop()
        {
            // "Loop"
            //yield return new ProgressReport(1, "Finishing Up", "", true);
            modLogger.Log("\nFinishing Up");

            if (CustomResources["DebugSettings"]["settings"].FilePath != Path.Combine(StreamingAssetsDirectory, DebugSettingsPath))
            {
                DebugBridge.LoadSettings(CustomResources["DebugSettings"]["settings"].FilePath);
            }

            //
            VersionManifestAddendum modAddendum = new VersionManifestAddendum("ModEntries");
			Dictionary<string, VersionManifestAddendum> dictAddendums = new Dictionary<string, VersionManifestAddendum>();
			VersionManifestAddendum addendum;
			foreach (ModEntry entry in AddBTRLEntries)
            {
				// If we're supposed to add this to a specifc addendum, do so.
				if (!string.IsNullOrEmpty(entry.AddToAddendum))
				{
					// If we haven't found the addendum yet,
					if (!dictAddendums.ContainsKey(entry.AddToAddendum))
					{
						// Try and look it up.
						addendum = dm.ResourceLocator.GetAddendumByName(entry.AddToAddendum);
						// If it exists, keep up with it in the dictionary
						if (addendum != null)
							dictAddendums[entry.AddToAddendum] = addendum;
						// If it doesn't exist log an error and add it to the ModEntries addendum.
						else
						{
							modLogger.LogError($"\tModEntry ID [{entry.Id}] tried to add itself to Addendum[{entry.AddToAddendum}] but it doesn't exist. Adding it to ModEntries instead");
							modAddendum.Add(entry.GetVersionManifestEntry());
							continue;
						}
					}

					dictAddendums[entry.AddToAddendum].Add(entry.GetVersionManifestEntry());
				}
				// Otherwise, add it to the ModEntries enum
				else
					modAddendum.Add(entry.GetVersionManifestEntry());
            }

            dm.ResourceLocator.ApplyAddendum(modAddendum);

            foreach (VersionManifestEntry manifestEntry in RemoveBTRLEntries)
            {
                dm.ResourceLocator.RemoveEntry(manifestEntry);
            }

            if (ModLoadOrder != null && ModLoadOrder.Count > 0)
            {
                CallFinishedLoadMethods(GameModDefs);
                PrintHarmonySummary(HarmonySummaryPath);
				LoadOrder.ToFile(ModLoadOrder, LoadOrderPath);
			}

            //Config?.ToFile(ConfigPath);

            Finish();
        }

        private static void UpdatePreviouslyLoadedMods<T>(Dictionary<string, T> modDefs) where T : BaseModDef
        {
            LocalUserSettings playerSettings = ActiveOrDefaultSettings.LocalSettings;
            loadedModStatus = GetOrCreateModStatusFromFile(ModStatusFilePath);

            UpdateModStatus(modDefs);
            
            playerSettings.previouslyLoadedMods = new Dictionary<string, bool>(loadedModStatus);
            SaveModStatusToFile();
            ActiveOrDefaultSettings.SaveUserSettings();
        }

        private static void CallFinishedLoadMethods<T>(Dictionary<string, T> modDefs) where T : BaseModDef
        {
            var hasPrinted = false;
            var assemblyMods = ModLoadOrder.Where(name => modDefs.ContainsKey(name) && modDefs[name].Assembly != null).ToList();
            foreach (var assemblyMod in assemblyMods)
            {
                var modDef = modDefs[assemblyMod];
                var methods = AssemblyUtil.FindMethods(modDef.Assembly, "FinishedLoading");

                if (methods == null || methods.Length == 0)
                {
                    continue;
                }

                if (!hasPrinted)
                {
                    modLogger.Log("\nCalling FinishedLoading:");
                    hasPrinted = true;
                }

                var paramsDictionary = new Dictionary<string, object>
                {
                    {"loadOrder", new List<string>(ModLoadOrder)},
                };

                if (modDef.CustomResourceTypes.Count > 0)
                {
                    var customResources = new Dictionary<string, Dictionary<string, VersionManifestEntry>>();
                    foreach (var resourceType in modDef.CustomResourceTypes)
                    {
                        customResources.Add(resourceType,
                            new Dictionary<string, VersionManifestEntry>(CustomResources[resourceType]));
                    }

                    paramsDictionary.Add("customResources", customResources);
                }

                foreach (var method in methods)
                {
                    if (!AssemblyUtil.InvokeMethodByParameterNames(method, paramsDictionary))
                    {
                        modLogger.LogError($"\tError: {modDef.Name}: Failed to invoke '{method.DeclaringType?.Name}.{method.Name}', parameter mismatch");
                    }
                }
            }
        }

        private static void Finish()
        {
            // clear temp objects
            cachedJObjects = null;
            merges = null;
			modLogger.Flush();
        }

        private static List<string> GenerateLoadOrder<T>(Dictionary<string, T> modDefs, List<string> cachedOrder) where T : BaseModDef
        {
            List<string> modLoadOrder = LoadOrder.CreateLoadOrder(modDefs, out var notLoaded, cachedOrder);
            foreach (var modName in notLoaded)
            {
                var modDef = modDefs[modName];

                FailToLoadModDef(modDef);
            }

            return modLoadOrder;
        }

		#region AddRemoveContent

		private static void AddModEntry(ModEntry modEntry)
		{
			if (modEntry.Path == null)
				return;

			// since we're adding a new entry here, we want to remove anything that would remove it again after the fact
			if (RemoveBTRLEntries.RemoveAll(entry => entry.Id == modEntry.Id && entry.Type == modEntry.Type) > 0)
				modLogger.Log($"\t\t{modEntry.Id} ({modEntry.Type}) -- this entry replaced an entry that was slated to be removed. Removed the removal.");

			if (CustomResources.ContainsKey(modEntry.Type))
			{
				modLogger.Log($"\tAdd/Replace (CustomResource): \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" ({modEntry.Type})");
				CustomResources[modEntry.Type][modEntry.Id] = modEntry.GetVersionManifestEntry();
				return;
			}

			VersionManifestAddendum addendum = null;
			if (!string.IsNullOrEmpty(modEntry.AddToAddendum))
			{
				addendum = CachedVersionManifest.GetAddendumByName(modEntry.AddToAddendum);

				if (addendum == null)
				{
					modLogger.LogWarning($"\tWarning: Cannot add {modEntry.Id} to {modEntry.AddToAddendum} because addendum doesn't exist in the manifest.");
					return;
				}
			}

			// special handling for particular types
			switch (modEntry.Type)
			{
				case "AssetBundle":
					ModAssetBundlePaths[modEntry.Id] = modEntry.Path;
					break;
			}

			// add to addendum instead of adding to manifest
			if (addendum != null)
				modLogger.Log($"\tAdd/Replace: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" ({modEntry.Type}) [{addendum.Name}]");
			else
				modLogger.Log($"\tAdd/Replace: \"{GetRelativePath(modEntry.Path, ModsDirectory)}\" ({modEntry.Type})");
			
			// entries in AddBTRLEntries will be added to game through patch in Patches\BattleTechResourceLocator
			AddBTRLEntries.Add(modEntry);
		}

        private static bool AddModEntryToDB(MetadataDatabase db, DBCache dbCache, string absolutePath, string typeStr)
        {
            if (Path.GetExtension(absolutePath)?.ToLowerInvariant() != ".json")
                return false;

            var type = (BattleTechResourceType) Enum.Parse(typeof(BattleTechResourceType), typeStr);
            var relativePath = GetRelativePath(absolutePath, GetRootPath(absolutePath));

            switch (type) // switch is to avoid poisoning the output_log.txt with known types that don't use MDD
            {
                case BattleTechResourceType.TurretDef:
                case BattleTechResourceType.UpgradeDef:
                case BattleTechResourceType.VehicleDef:
                case BattleTechResourceType.ContractOverride:
                case BattleTechResourceType.SimGameEventDef:
                case BattleTechResourceType.LanceDef:
                case BattleTechResourceType.MechDef:
                case BattleTechResourceType.PilotDef:
                case BattleTechResourceType.WeaponDef:
                    var writeTime = File.GetLastWriteTimeUtc(absolutePath);
                    if (!dbCache.Entries.ContainsKey(relativePath) || dbCache.Entries[relativePath] != writeTime)
                    {
                        try
                        {
                            VersionManifestHotReload.InstantiateResourceAndUpdateMDDB(type, absolutePath, db);

                            // don't write game files to the dbCache, since they're assumed to be default in the db
                            if (!absolutePath.Contains(StreamingAssetsDirectory))
                                dbCache.Entries[relativePath] = writeTime;

                            return true;
                        }
                        catch (Exception e)
                        {
                            modLogger.LogException($"\tError: Add to DB failed for {Path.GetFileName(absolutePath)}, exception caught:", e);

							return false;
                        }
                    }

                    break;
            }

            return false;
        }


        private static void AddMerge(string type, string id, string path, bool addToDb)
        {
            if (!merges.ContainsKey(type))
                merges[type] = new Dictionary<string, List<MergeEntry>>();

            if (!merges[type].ContainsKey(id))
                merges[type][id] = new List<MergeEntry>();

            MergeEntry mergeEntry = merges[type][id].Find(me => me.Path == path);

            if (mergeEntry != null)
            {
                mergeEntry.AddToDb = mergeEntry.AddToDb || addToDb;
                return;
            }

            merges[type][id].Add(new MergeEntry(type, id, path, addToDb));
        }

        private static bool RemoveEntry(string id, TypeCache typeCache)
        {
            var removedEntry = false;

            var containingCustomTypes = CustomResources.Where(pair => pair.Value.ContainsKey(id)).ToList();
            foreach (var pair in containingCustomTypes)
            {
                modLogger.Log($"\tRemove: \"{pair.Value[id].Id}\" ({pair.Value[id].Type}) - Custom Resource");
                pair.Value.Remove(id);
                removedEntry = true;
            }

            var modEntries = AddBTRLEntries.FindAll(entry => entry.Id == id);
            foreach (var modEntry in modEntries)
            {
                modLogger.Log($"\tRemove: \"{modEntry.Id}\" ({modEntry.Type}) - Mod Entry");
                AddBTRLEntries.Remove(modEntry);
                removedEntry = true;
            }

            var vanillaEntries = CachedVersionManifest.FindAll(entry => entry.Id == id);
            foreach (var vanillaEntry in vanillaEntries)
            {
                modLogger.Log($"\tRemove: \"{vanillaEntry.Id}\" ({vanillaEntry.Type}) - Vanilla Entry");
                RemoveBTRLEntries.Add(vanillaEntry);
                removedEntry = true;
            }

            var types = typeCache.GetTypes(id, CachedVersionManifest);
            foreach (var type in types)
            {
                if (!merges.ContainsKey(type) || !merges[type].ContainsKey(id))
                    continue;

                modLogger.Log($"\t\tAlso removing JSON merges for {id} ({type})");
                merges[type].Remove(id);
            }

            return removedEntry;
        }

        private static void RemoveMerge(string type, string id)
        {
            if (!merges.ContainsKey(type) || !merges[type].ContainsKey(id))
                return;

            merges[type].Remove(id);
            modLogger.LogWarning($"\t\tHad merges for {id} but had to toss, since original file is being replaced");
        }

        #endregion

        private static bool FileIsOnDenyList(string filePath)
        {
            return IGNORE_LIST.Any(x => filePath.EndsWith(x, StringComparison.InvariantCultureIgnoreCase));
        }

        private static string InferIDFromFile(string path)
        {
            // if not json, return the file name without the extension, as this is what HBS uses
            var ext = Path.GetExtension(path);
            if (ext == null || ext.ToLowerInvariant() != ".json" || !File.Exists(path))
                return Path.GetFileNameWithoutExtension(path);

            // read the json and get ID out of it if able to
            return InferIDFromJObject(ParseGameJSONFile(path)) ?? Path.GetFileNameWithoutExtension(path);
        }

        private static string InferIDFromJObject(JObject jObj)
        {
            if (jObj == null)
                return null;

            // go through the different kinds of id storage in JSONs
            string[] jPaths = {"Description.Id", "id", "Id", "ID", "identifier", "Identifier"};
            return jPaths.Select(jPath => (string) jObj.SelectToken(jPath)).FirstOrDefault(id => id != null);
        }

        private static VersionManifestEntry FindEntry(string type, string id)
        {
            if (CustomResources.ContainsKey(type) && CustomResources[type].ContainsKey(id))
                return CustomResources[type][id];

            var modEntry = AddBTRLEntries.FindLast(x => x.Type == type && x.Id == id)?.GetVersionManifestEntry();
            if (modEntry != null)
                return modEntry;

            // if we're slating to remove an entry, then we don't want to return it here from the manifest
            return !RemoveBTRLEntries.Exists(entry => entry.Type == type && entry.Id == id)
                ? CachedVersionManifest.Find(entry => entry.Type == type && entry.Id == id)
                : null;
        }

        public static JObject ParseGameJSONFile(string path)
        {
            if (cachedJObjects.ContainsKey(path))
                return cachedJObjects[path];

            // because StripHBSCommentsFromJSON is private, use Harmony to call the method
            var commentsStripped = Traverse.Create(typeof(JSONSerializationUtility))
                .Method("StripHBSCommentsFromJSON", File.ReadAllText(path)).GetValue<string>();

            if (commentsStripped == null)
                throw new Exception("StripHBSCommentsFromJSON returned null.");

            // add missing commas, this only fixes if there is a newline
            var rgx = new Regex(@"(\]|\}|""|[A-Za-z0-9])\s*\n\s*(\[|\{|"")", RegexOptions.Singleline);
            var commasAdded = rgx.Replace(commentsStripped, "$1,\n$2");

            cachedJObjects[path] = JObject.Parse(commasAdded);
            return cachedJObjects[path];
        }

        public static string ResolvePath(string path, string rootPathToUse)
        {
            if (!Path.IsPathRooted(path))
                path = Path.Combine(rootPathToUse, path);

            return Path.GetFullPath(path);
        }

        public static string GetRelativePath(string path, string rootPath)
        {
            if (!Path.IsPathRooted(path))
                return path;

            rootPath = Path.GetFullPath(rootPath);
            if (rootPath.Last() != Path.DirectorySeparatorChar)
                rootPath += Path.DirectorySeparatorChar;

            var pathUri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            var rootUri = new Uri(rootPath, UriKind.Absolute);

            if (pathUri.Scheme != rootUri.Scheme)
                return path;

            var relativeUri = rootUri.MakeRelativeUri(pathUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (pathUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return relativePath;
        }

        // Used with "GetRelativePath()" to determine the proper root path of a mod entry 
        public static string GetRootPath(string path)
        {
            var rootDir = Path.GetPathRoot(path);

            if (rootDir == Path.GetPathRoot(GameDirectory))
            {
                return GameDirectory;
            }

            if (rootDir == Path.GetPathRoot(BattletechUserDirectory))
            {
                return BattletechUserDirectory;
            }

            return null;
        }

        private static void PrintHarmonySummary(string path)
        {
            var harmony = HarmonyInstance.Create("ModLoader");

            var patchedMethods = harmony.GetPatchedMethods().ToArray();
            if (patchedMethods.Length == 0)
                return;

            using (var writer = File.CreateText(path))
            {
                writer.WriteLine($"Harmony Patched Methods (after startup) -- {DateTime.Now}\n");

                foreach (var method in patchedMethods)
                {
                    var info = harmony.GetPatchInfo(method);

                    if (info == null || method.ReflectedType == null)
                        continue;

                    writer.WriteLine($"{method.ReflectedType.FullName}.{method.Name}:");

                    // prefixes
                    if (info.Prefixes.Count != 0)
                        writer.WriteLine("\tPrefixes:");
                    foreach (var patch in info.Prefixes)
                        writer.WriteLine($"\t\t{patch.owner}");

                    // transpilers
                    if (info.Transpilers.Count != 0)
                        writer.WriteLine("\tTranspilers:");
                    foreach (var patch in info.Transpilers)
                        writer.WriteLine($"\t\t{patch.owner}");

                    // postfixes
                    if (info.Postfixes.Count != 0)
                        writer.WriteLine("\tPostfixes:");
                    foreach (var patch in info.Postfixes)
                        writer.WriteLine($"\t\t{patch.owner}");

                    writer.WriteLine("");
                }
            }
        }
        
        private static Dictionary<string, bool> GetOrCreateModStatusFromFile(string path)
        {
            Dictionary<string, bool> modStatus;

            if (File.Exists(path))
            {
                try
                {
                    modStatus = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(path));
                    modLogger.Log("Loaded mod status.");
                    return modStatus;
                }
                catch (Exception e)
                {
                    modLogger.LogException("Loading mod status failed -- will rebuild it.", e);
				}
			}

            // create a new one if it doesn't exist or couldn't be added'
            modLogger.Log("Building new Mod Status.");
            modStatus = new Dictionary<string, bool>();
            return modStatus;
        }

        public static void SaveModStatusToFile()
        {
            File.WriteAllText(ModStatusFilePath, JsonConvert.SerializeObject(loadedModStatus, Formatting.Indented));
        }

        public static void UpdateModStatus<T>(Dictionary<string, T> modDefs) where T : BaseModDef
        {
            List<string> missingMods = new List<string>();

            if (!AreModsEnabled)
            {
                List<string> modKeys = new List<string>(loadedModStatus.Keys);

                foreach (string loadedModKey in modKeys)
                {
                    loadedModStatus[loadedModKey] = false;
                }

                return;
            }

            if (modDefs != null)
            {
                //Should be mods that loaded properly
                foreach (string key in modDefs.Keys)
                {
                    if (!loadedModStatus.ContainsKey(key))
                    {
                        loadedModStatus.Add(key, true);
                    }
                    else
                    {
                        loadedModStatus[key] = loadedModStatus[key];
                    }
                }

                foreach (string missingMod in loadedModStatus.Keys.Except(modDefs.Keys))
                {
                    missingMods.Add(missingMod);
                }
            }

            if (FailedToLoadMods != null && FailedToLoadMods.Count > 0)
            {
                //Should be mods that failed to load or missing dependecies
                foreach (string failedToLoadMod in FailedToLoadMods)
                {
                    if (!loadedModStatus.ContainsKey(failedToLoadMod))
                    {
                        loadedModStatus.Add(failedToLoadMod, false);
                    }
                    else
                    {
                        loadedModStatus[failedToLoadMod] = false;
                    }

                    if (missingMods.Contains(failedToLoadMod))
                    {
                        missingMods.Remove(failedToLoadMod);
                    }
                }
            }

            foreach (string missingMod in missingMods)
            {
                loadedModStatus.Remove(missingMod);
            }
        }
    }
}