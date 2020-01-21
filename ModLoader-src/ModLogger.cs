using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HBS.Logging;
using UnityEngine;

namespace BattleTech.ModSupport
{
	public class ModLogger : IDisposable
	{
		private static ILog hbsModLogger = null;
		public bool autoFlushOnException = true;
		protected StreamWriter streamWriter = null;
		protected LogLevel minimumLogLevel;

		public ModLogger(string path, LogLevel minimumLogLevel)
		{
			this.minimumLogLevel = minimumLogLevel;
			streamWriter = new StreamWriter(path);
			streamWriter.AutoFlush = false;
			hbsModLogger = HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.MODLOADER, minimumLogLevel);
		}

		public void Dispose()
		{
			Flush();
			streamWriter.Close();
			streamWriter.Dispose();
			streamWriter = null;
		}

		public void LogDebug(string message)
		{
			LogItem(LogLevel.Debug, "DEBUG", message);
			hbsModLogger.LogDebug(message);
		}

		public void Log(string message)
		{
			LogItem(LogLevel.Log, "LOG", message);
			hbsModLogger.Log(message);
		}

		public void LogWarning(string message)
		{
			LogItem(LogLevel.Warning, "WARNING", message);
			hbsModLogger.LogWarning(message);
		}

		public void LogError(string message)
		{
			LogItem(LogLevel.Error, "ERROR", message);
			hbsModLogger.LogError(message);
		}

		public void LogException(string message, Exception exception)
		{
			string messageAndException = $"{message}\n{ exception.ToString()}";
			LogItem(LogLevel.Error, "EXCEPTION", messageAndException);
			hbsModLogger.LogError(message, exception);

			if (autoFlushOnException && ShouldLog(LogLevel.Error))
			{
				Flush();
			}
		}

		public void Flush()
		{
			streamWriter.Flush();
		}

		protected void LogItem(LogLevel itemLevel, string heading, string message)
		{
			if (ShouldLog(itemLevel))
			{
				WriteLine(heading, message);
			}
		}

		protected void WriteLine(string heading, string message)
		{
			streamWriter.WriteLine($"{heading}: {message}");
		}

		protected bool ShouldLog(LogLevel logLevel)
		{
			if (logLevel >= minimumLogLevel)
				return true;
			else
				return false;
		}
	}
}
