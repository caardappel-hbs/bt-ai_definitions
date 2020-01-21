using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace BattleTech.ModSupport.Utils
{
    internal static class LoadOrder
    {
        public static readonly HBS.Logging.ILog modLogger =
            HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.MODLOADER, HBS.Logging.LogLevel.Log);
        
        public static List<string> CreateLoadOrder<T>(Dictionary<string, T> modDefs, out List<string> notloaded, List<string> cachedOrder) where T : BaseModDef
        {
            var modDefsCopy = new Dictionary<string, T>(modDefs);
            var loadOrder = new List<string>();

            // remove all mods that have a conflict
            var tryToLoad = modDefs.Keys.ToList();
            var hasConflicts = new List<string>();
            foreach (var modDef in modDefs.Values)
            {
                if (!modDef.HasConflicts(tryToLoad))
                    continue;

                modDefsCopy.Remove(modDef.Name);
                hasConflicts.Add(modDef.Name);
            }

            // load the order specified in the file
            foreach (var modName in cachedOrder)
            {
                // If we don't have this mod, or its dependencies weren't resolved, or it's optional dependencies aren't resolved
                if (!modDefsCopy.ContainsKey(modName) || !modDefsCopy[modName].AreDependenciesResolved(loadOrder) || !modDefsCopy[modName].AreOptionalDependenciesResolved(tryToLoad))
                    continue;

                tryToLoad.Remove(modName);
                modDefsCopy.Remove(modName);
                loadOrder.Add(modName);
            }

			// everything that is left in the copy hasn't been loaded before
			notloaded = new List<string>();
            notloaded.AddRange(modDefsCopy.Keys.OrderByDescending(x => x).ToList());

			// there is nothing left to load
			if (modDefsCopy.Count == 0)
            {
                notloaded.AddRange(hasConflicts);
                return loadOrder;
            }

            ProcessLoadOrder(modDefs, notloaded, loadOrder, checkOptionalDependencies: true);
            ProcessLoadOrder(modDefs, notloaded, loadOrder, checkOptionalDependencies: false);

            notloaded.AddRange(hasConflicts);

			return loadOrder;
        }

		private static void ProcessLoadOrder<T>(Dictionary<string, T> modDefs, List<string> notloaded, List<string> loadOrder, bool checkOptionalDependencies) where T : BaseModDef
		{
			// this is the remainder that haven't been loaded before
			int removedThisPass;
			do
			{
				removedThisPass = 0;

				for (var i = notloaded.Count - 1; i >= 0; i--)
				{
					var modDef = modDefs[notloaded[i]];

					if (!modDef.AreDependenciesResolved(loadOrder))
						continue;

					if (checkOptionalDependencies && !modDef.AreOptionalDependenciesResolved(notloaded))
						continue;

					notloaded.RemoveAt(i);
					loadOrder.Add(modDef.Name);
					removedThisPass++;
				}
			} while (removedThisPass > 0 && notloaded.Count > 0);
		}

		public static void ToFile(List<string> order, string path)
        {
            if (order == null)
                return;

			List<string> orderWithVersionNumberFirst = new List<string>(order);
			orderWithVersionNumberFirst.Insert(0, VersionInfo.GetReleaseVersionForModLoading());

			File.WriteAllText(path, JsonConvert.SerializeObject(orderWithVersionNumberFirst, Formatting.Indented));
        }

        public static List<string> FromFile(string path, bool removeVersionNumberEntry)
        {
            List<string> order;

            if (File.Exists(path))
            {
                try
                {
                    order = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
                    modLogger.Log("Loaded cached load order.");

					if (removeVersionNumberEntry && order.Count > 0)
						order.RemoveAt(0);

                    return order;
                }
                catch (Exception e)
                {
                    modLogger.LogException("Loading cached load order failed, rebuilding it.", e);
                }
            }

            // create a new one if it doesn't exist or couldn't be added
            modLogger.Log("Building new load order!");
            order = new List<string>();
            return order;
        }
    }
}