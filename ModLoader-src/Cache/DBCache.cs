using System;
using System.Collections.Generic;
using System.IO;
using BattleTech.Data;
using Newtonsoft.Json;

namespace BattleTech.ModSupport.Caches
{
    internal class DBCache
    {
        public static readonly HBS.Logging.ILog logger =
            HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.MODLOADER, HBS.Logging.LogLevel.Log);
        
        public Dictionary<string, DateTime> Entries { get; }

        public DBCache(string path, string mddbPath, string modMDDBPath, bool modCacheCleared)
        {
            // If the modCache was cleared, we can't load up the Entries, we have to recalculate everything.
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && File.Exists(modMDDBPath) && !modCacheCleared)
            {
                try
                {
                    Entries = JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(File.ReadAllText(path));
                    if (Entries != null)
                    {
                        logger.Log("Loaded db cache.");
                        return;                        
                    }
                }
                catch (Exception e)
                {
                    logger.LogException("Loading db cache failed -- will rebuild it.", e);
                }
            }

            // If the modcache was not cleared we used to be clearing it here. But we can't do that anymore.
			if (!modCacheCleared)
			{
				// Since we have other things that could have changed the database, we are no longer allowed to recreate 
				// the database here, so throw an exception.
				//RecreateModMDDB(mddbPath, modMDDBPath);
				throw new InvalidOperationException("LET ECK KNOW ABOUT THIS - DBCache wants to recreate the ModMDDB, but some changes may have already happened. Since I'm checking to see if we should reset the cache at the begining of the process, I don't think this can happen. If it does, please let Eck know and which combination of mods it happened with.");
			}

			// create a new one if it doesn't exist or couldn't be added
			logger.Log("Copying over DB and building new DB Cache.");
            Entries = new Dictionary<string, DateTime>();
        }

		public static void RecreateModMDDB(string mddbPath, string modMDDBPath)
		{
			// delete mod db if it exists the cache does not
			if (File.Exists(modMDDBPath))
				File.Delete(modMDDBPath);

			EnsureModMDDB(mddbPath, modMDDBPath);
		}

		public static void EnsureModMDDB(string mddbPath, string modMDDBPath)
		{
			// delete mod db if it exists the cache does not
			if (!File.Exists(modMDDBPath))
			{
				File.Copy(mddbPath, modMDDBPath);
				MetadataDatabase.ReloadFromDisk();
			}
		}

		public void UpdateToRelativePaths()
        {
            var toRemove = new List<string>();
            var toAdd = new Dictionary<string, DateTime>();

            foreach (var path in Entries.Keys)
            {
                if (!Path.IsPathRooted(path))
                    continue;

                var relativePath = ModLoader.GetRelativePath(path, ModLoader.GetRootPath(path));
                toAdd[relativePath] = Entries[path];
                toRemove.Add(path);
            }

            foreach (var addKVP in toAdd)
            {
                if (!Entries.ContainsKey(addKVP.Key))
                {
                    Entries.Add(addKVP.Key, addKVP.Value);
                }
            }
                

            foreach (var path in toRemove)
                Entries.Remove(path);
        }

        public void ToFile(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(Entries, Formatting.Indented));
        }
    }
}
