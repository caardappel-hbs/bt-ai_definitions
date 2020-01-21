using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;
using Newtonsoft.Json;

namespace BattleTech.ModSupport.Caches
{
    internal class TypeCache
    {
        public static readonly HBS.Logging.ILog logger =
            HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.MODLOADER, HBS.Logging.LogLevel.Log);
        
        private readonly Dictionary<string, List<string>> entries;

        public TypeCache(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    entries = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText(path));
                    logger.Log("Loaded type cache.");
                    return;
                }
                catch (Exception e)
                {
                    logger.LogException("Loading type cache failed -- will rebuild it.", e);
                }
            }

            logger.Log("Building new Type Cache.");
            entries = new Dictionary<string, List<string>>();
        }

        public void UpdateToIDBased()
        {
            var toRemove = new List<string>();
            var toAdd = new Dictionary<string, List<string>>();

            foreach (var path in entries.Keys)
            {
                var id = Path.GetFileNameWithoutExtension(path);

                if (id == null || id == path || toAdd.ContainsKey(id) || entries.ContainsKey(id))
                    continue;

                toAdd[id] = entries[path];
                toRemove.Add(path);
            }

            foreach (var addKVP in toAdd)
                entries.Add(addKVP.Key, addKVP.Value);

            foreach (var path in toRemove)
                entries.Remove(path);
        }

        //Is it possible for this to ever return more than 1 type for a entry in the manifest?
        //    Based on the output file the answer is yes. Prefab+Assetbundle | Texture2D+Sprite are examples
        //Can a manifest entry even have more than one type?
        //    Multiple entries with the same ID could have different types in their entry
        public List<string> GetTypes(string id, VersionManifest manifest = null)
        {
            if (entries.ContainsKey(id))
                return entries[id];

            if (manifest != null)
            {
                // get the types from the manifest
                var matchingEntries = manifest.FindAll(x => x.Id == id);
                if (matchingEntries == null || matchingEntries.Count == 0)
                    return null;

                var types = new List<string>();

                foreach (var existingEntry in matchingEntries)
                    types.Add(existingEntry.Type);

                entries[id] = types;
                return entries[id];
            }

            return null;
        }

        public void TryAddType(string id, string type)
        {
            var types = GetTypes(id);
            if (types != null && types.Contains(type))
                return;

            if (types != null && !types.Contains(type))
            {
                types.Add(type);
                return;
            }

            // add the new entry
            entries[id] = new List<string> { type };
        }

        public void ToFile(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(entries, Formatting.Indented));
        }
    }
}
