using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HBS.Logging;

namespace BattleTech.ModSupport
{
	/// <summary>
	/// This interface is used by the ModLoader to load extra data into the game/MDDB.
	/// Unfortunately, implementers of this class also need to implement a static "Instance" 
	/// property.
	/// </summary>
	public interface IDataAddendum
	{
		void LoadDataAddendum(string fileData, ModLogger logger);
	}

	[Serializable]
	public class DataAddendumEntry
	{
		public string name;
		public string path;
	}
}
