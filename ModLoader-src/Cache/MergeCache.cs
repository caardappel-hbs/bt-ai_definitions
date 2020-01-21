using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using BattleTech.ModSupport.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace BattleTech.ModSupport.Caches
{
    internal class MergeCache
    {
        public static readonly HBS.Logging.ILog logger =
            HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.MODLOADER, HBS.Logging.LogLevel.Log);
        
        [UsedImplicitly]
        public Dictionary<string, CacheEntry> CachedEntries { get; set; } = new Dictionary<string, CacheEntry>();

        public CacheEntry GetOrCreateCachedEntry(string absolutePath, List<MergeEntry> mergeEntries)
        {
            absolutePath = Path.GetFullPath(absolutePath);
            var relativePath = ModLoader.GetRelativePath(absolutePath, ModLoader.GetRootPath(absolutePath));
            

            logger.Log("");

			// TOOD - Do we need to change this?
            if (!CachedEntries.ContainsKey(relativePath) || !CachedEntries[relativePath].MatchesPaths(absolutePath, mergeEntries.Select(me => me.Path).ToList()))
            {
                var cachedAbsolutePath = Path.GetFullPath(Path.Combine(ModLoader.CacheDirectory, relativePath));
                var cachedEntry = new CacheEntry(cachedAbsolutePath, absolutePath, mergeEntries);

                if (cachedEntry.HasErrors)
                    return null;

                CachedEntries[relativePath] = cachedEntry;

                logger.Log($"Merge performed: {Path.GetFileName(absolutePath)}");
            }
            else
            {
                logger.Log($"Cached merge: {Path.GetFileName(absolutePath)} ({File.GetLastWriteTime(CachedEntries[relativePath].CacheAbsolutePath):G})");
            }

            logger.Log($"\t{relativePath}");

			foreach (MergeEntry mergeEntry in mergeEntries)
			{
				string contributingPath = mergeEntry.Path;
				logger.Log($"\t{ModLoader.GetRelativePath(contributingPath, ModLoader.GetRootPath(contributingPath))}");
			}

            logger.Log("");

            CachedEntries[relativePath].CacheHit = true;
            return CachedEntries[relativePath];
        }

        public bool HasCachedEntry(string originalPath, List<string> mergePaths)
        {
            var relativePath = ModLoader.GetRelativePath(originalPath, ModLoader.GetRootPath(originalPath));
            return CachedEntries.ContainsKey(relativePath) && CachedEntries[relativePath].MatchesPaths(originalPath, mergePaths);
        }

        public void ToFile(string path)
        {
            // remove all of the cache that we didn't use
            var unusedMergePaths = new List<string>();
            foreach (var cachedEntryKVP in CachedEntries)
            {
                if (!cachedEntryKVP.Value.CacheHit)
                    unusedMergePaths.Add(cachedEntryKVP.Key);
            }

            if (unusedMergePaths.Count > 0)
                logger.Log("");

            foreach (var unusedMergePath in unusedMergePaths)
            {
                var cacheAbsolutePath = CachedEntries[unusedMergePath].CacheAbsolutePath;
                CachedEntries.Remove(unusedMergePath);

                if (File.Exists(cacheAbsolutePath))
                    File.Delete(cacheAbsolutePath);

                logger.Log($"Old Merge Deleted: {cacheAbsolutePath}");

                var directory = Path.GetDirectoryName(cacheAbsolutePath);
                while (Directory.Exists(directory) && Directory.GetDirectories(directory).Length == 0 && Directory.GetFiles(directory).Length == 0 && Path.GetFullPath(directory) != ModLoader.CacheDirectory)
                {
                    Directory.Delete(directory);
                    logger.Log($"Old Merge folder deleted: {directory}");
                    directory = Path.GetFullPath(Path.Combine(directory, ".."));
                }
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public void UpdateToRelativePaths()
        {
            var toRemove = new List<string>();
            var toAdd = new Dictionary<string, CacheEntry>();

            foreach (var path in CachedEntries.Keys)
            {
                if (Path.IsPathRooted(path))
                {
                    var relativePath = ModLoader.GetRelativePath(path, ModLoader.GetRootPath(path));

                    toAdd[relativePath] = CachedEntries[path];
                    toRemove.Add(path);

                    toAdd[relativePath].CachePath = ModLoader.GetRelativePath(toAdd[relativePath].CachePath, ModLoader.GetRootPath(toAdd[relativePath].CachePath));
                    foreach (var merge in toAdd[relativePath].Merges)
                        merge.RelativePath = ModLoader.GetRelativePath(merge.RelativePath, ModLoader.GetRootPath(merge.RelativePath));
                }
            }

            foreach (var addKVP in toAdd)
            {
                if (!CachedEntries.ContainsKey(addKVP.Key))
                {
                    CachedEntries.Add(addKVP.Key, addKVP.Value);    
                }
            }
                

            foreach (var path in toRemove)
                CachedEntries.Remove(path);
        }

        public static MergeCache FromFile(string path)
        {
            MergeCache mergeCache;

            if (File.Exists(path))
            {
                try
                {
                    mergeCache = JsonConvert.DeserializeObject<MergeCache>(File.ReadAllText(path));
                    logger.Log("Loaded merge cache.");
                    return mergeCache;
                }
                catch (Exception e)
                {
                    logger.LogException("Loading merge cache failed -- will rebuild it.", e);
                }
            }

            // create a new one if it doesn't exist or couldn't be added'
            logger.Log("Building new Merge Cache.");
            mergeCache = new MergeCache();
            return mergeCache;
        }

        internal class CacheEntry
        {
            [JsonIgnore] private string cacheAbsolutePath;
            [JsonIgnore] internal bool CacheHit; // default is false
            [JsonIgnore] internal string ContainingDirectory;
            [JsonIgnore] internal bool HasErrors; // default is false
            public string CachePath { get; set; }
            public DateTime OriginalTime { get; set; }
            public List<MergeEntryTimeTuple> Merges { get; set; } = new List<MergeEntryTimeTuple>();

            [JsonIgnore]
            internal string CacheAbsolutePath
            {
                get
                {
                    if (string.IsNullOrEmpty(cacheAbsolutePath))
                        cacheAbsolutePath = ModLoader.ResolvePath(CachePath, ModLoader.BattletechUserDirectory);

                    return cacheAbsolutePath;
                }
            }

			public bool AddToDb
			{
				get
				{
					foreach (var merge in this.Merges)
					{
						if (merge.MergeEntry.AddToDb)
							return true;
					}

					return false;
				}
			}

			[JsonConstructor]
            public CacheEntry()
            {
            }

            public CacheEntry(string absolutePath, string originalAbsolutePath, List<MergeEntry> mergeEntries)
            {
                cacheAbsolutePath = absolutePath;
                CachePath = ModLoader.GetRelativePath(absolutePath, ModLoader.GetRootPath(absolutePath));
                ContainingDirectory = Path.GetDirectoryName(absolutePath);
                OriginalTime = File.GetLastWriteTimeUtc(originalAbsolutePath);

                if (string.IsNullOrEmpty(ContainingDirectory))
                {
                    HasErrors = true;
                    return;
                }

				foreach (MergeEntry mergeEntry in mergeEntries)
				{
					string mergePath = mergeEntry.Path;
					Merges.Add(new MergeEntryTimeTuple(ModLoader.GetRelativePath(mergePath, ModLoader.GetRootPath(mergePath)), mergeEntry, File.GetLastWriteTimeUtc(mergePath)));
				}

                Directory.CreateDirectory(ContainingDirectory);

                // do json merge if json
                if (Path.GetExtension(absolutePath)?.ToLowerInvariant() == ".json")
                {
                    // get the parent JSON
                    JObject parentJObj;
                    try
                    {
                        parentJObj = ModLoader.ParseGameJSONFile(originalAbsolutePath);
                    }
                    catch (Exception e)
                    {
                        logger.LogException($"\tParent JSON at path {originalAbsolutePath} has errors preventing any merges!", e);
                        HasErrors = true;
                        return;
                    }

                    using (var writer = File.CreateText(absolutePath))
                    {
                        // merge all of the merges
                        foreach (var mergeEntry in mergeEntries)
                        {
							string mergePath = mergeEntry.Path;
                            try
                            {
                                // since all json files are opened and parsed before this point, they won't have errors
                                JSONMerger.MergeIntoTarget(parentJObj, ModLoader.ParseGameJSONFile(mergePath));
                            }
                            catch (Exception e)
                            {
                                logger.LogException($"\tMod JSON merge at path {ModLoader.GetRelativePath(mergePath, ModLoader.GetRootPath(mergePath))} has errors preventing merge!", e);
                            }
                        }

                        // write the merged onto file to disk
                        var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
                        parentJObj.WriteTo(jsonWriter);
                        jsonWriter.Close();
                    }

                    return;
                }

                // do file append if not json
                using (var writer = File.CreateText(absolutePath))
                {
                    writer.Write(File.ReadAllText(originalAbsolutePath));

					foreach (var mergeEntry in mergeEntries)
					{
						string mergePath = mergeEntry.Path;
						writer.Write(File.ReadAllText(mergePath));
					}
                }
            }

            internal bool MatchesPaths(string originalPath, List<string> mergePaths)
            {
                // must have an existing cached json
                if (!File.Exists(CacheAbsolutePath))
                    return false;

                // must have the same original file
                if (File.GetLastWriteTimeUtc(originalPath) != OriginalTime)
                    return false;

                // must match number of merges
                if (mergePaths.Count != Merges.Count)
                    return false;

                // if all paths match with write times, we match
                for (var index = 0; index < mergePaths.Count; index++)
                {
                    var mergeAbsolutePath = mergePaths[index];
                    var mergeTime = File.GetLastWriteTimeUtc(mergeAbsolutePath);
                    var cachedMergeAbsolutePath = ModLoader.ResolvePath(Merges[index].RelativePath, ModLoader.BattletechUserDirectory);
                    var cachedMergeTime = Merges[index].Time;

                    if (mergeAbsolutePath != cachedMergeAbsolutePath || mergeTime != cachedMergeTime)
                        return false;
                }

                return true;
            }

            internal class MergeEntryTimeTuple
            {
				public string RelativePath { get; set; }
                public MergeEntry MergeEntry { get; set; }
                public DateTime Time { get; set; }

                public MergeEntryTimeTuple(string relativePath, MergeEntry mergeEntry, DateTime time)
                {
					RelativePath = relativePath;
                    MergeEntry = mergeEntry;
                    Time = time;
                }
            }
        }
    }
}
