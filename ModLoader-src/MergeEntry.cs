using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleTech.ModSupport
{
	public class MergeEntry
	{
		public string Type;
		public string ID;
		public string Path;
		public bool AddToDb;

		public MergeEntry()
		{
		}

		public MergeEntry(string type, string id, string path, bool addToDb)
		{
			Type = type;
			ID = id;
			Path = path;
			AddToDb = addToDb;
		}
	}
}
