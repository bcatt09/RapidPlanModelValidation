using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidPlanModelValidation
{
	public sealed class Logger
	{
		private static readonly Logger _logger = new Logger();
		private string Message;

		private Logger()
		{
			Message = "";
		}

		private static Logger GetLogger()
		{
			return _logger;
		}

		public static void LogWarning(string title, string message, string patient = "", string plan = "")
		{
			LogMessage($"WARNING - {title}\n{(patient != "" ? $"Patient: {patient}\n" : "")}{(plan != "" ? $"Plan: {plan}\n" : "")}{message}");
		}

		public static void LogError(string title, string message, string patient = "", string plan = "")
		{
			LogMessage($"ERROR - {title}\n{(patient != "" ? $"Patient: {patient}\n" : "")}{(plan != "" ? $"Plan: {plan}\n" : "")}{message}");
		}

		/// <summary>
		/// Add a message to the log file
		/// </summary>
		/// <param name="message">Message to add</param>
		public static void LogMessage(string message)
		{
			message = message.Replace("\n", $"{Environment.NewLine}    ");
			GetLogger().Message += $"{message}{Environment.NewLine}";
		}

		/// <summary>
		/// Get the complete log
		/// </summary>
		/// <returns></returns>
		public static string GetLog()
		{
			return GetLogger().Message;
		}

		/// <summary>
		/// Clear the log file
		/// </summary>
		public static void ClearLog()
		{
			GetLogger().Message = "";
		}
	}
}
