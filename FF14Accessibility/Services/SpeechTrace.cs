using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

internal static class SpeechTrace
{
	public const string FileName = "FF14_Sprache.txt";

	private const int MaxEntries = 4000;

	private static readonly List<string> _entries = new List<string>();

	private static long _startTicks;

	private static long _lastTicks;

	public static bool Armed { get; private set; }

	public static int Count => _entries.Count;

	public static void Arm()
	{
		_entries.Clear();
		_startTicks = Stopwatch.GetTimestamp();
		_lastTicks = _startTicks;
		Armed = true;
	}

	public static void Disarm()
	{
		Armed = false;
	}

	public static void Note(string kind, string text)
	{
		if (Armed && _entries.Count < 4000)
		{
			long timestamp = Stopwatch.GetTimestamp();
			double value = (double)(timestamp - _startTicks) / (double)Stopwatch.Frequency;
			double value2 = (double)(timestamp - _lastTicks) / (double)Stopwatch.Frequency;
			_lastTicks = timestamp;
			_entries.Add($"+{value,8:0.000}s (+{value2,6:0.000}) {kind,-8} {CallerName()} | {Short(text)}");
		}
	}

	private static string CallerName()
	{
		try
		{
			StackTrace stackTrace = new StackTrace(2, fNeedFileInfo: false);
			for (int i = 0; i < stackTrace.FrameCount; i++)
			{
				MethodBase? methodBase = stackTrace.GetFrame(i)?.GetMethod();
				Type? type = methodBase?.DeclaringType;
				if (!(methodBase == null) && !(type == null) && !(type == typeof(TolkService)) && !(type == typeof(SpeechTrace)))
				{
					return type.Name + "." + methodBase.Name;
				}
			}
		}
		catch (Exception)
		{
		}
		return "?";
	}

	private static string Short(string text)
	{
		string text2 = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
		if (text2.Length > 200)
		{
			return text2.Substring(0, 200) + "…";
		}
		return text2;
	}

	public static int Write(IPluginLog log, string version, bool withHeader)
	{
		List<string> list = new List<string>();
		if (withHeader)
		{
			list.Add($"=== FF14 Accessibility {version} - Sprach-Mitschnitt {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
			list.Add("Frage: welche Ansage kommt zuerst - Name oder Beschreibung - und WER spricht sie?");
			list.Add("Jede Zeile ist eine Ansage, in der Reihenfolge des Absendens.");
			list.Add("Spalte 1: Sekunden seit dem Einschalten. Spalte 2: Abstand zur vorigen Ansage.");
			list.Add("Spalte 3: Sprechart - INT unterbricht (schneidet ab), QUE reiht sich ein,");
			list.Add("         DEB/TEIL-DEB/MUT heisst: GAR NICHT gesprochen (Entprellung/Stumm).");
			list.Add("Spalte 4: die Methode, die die Ansage abgeschickt hat (der Sprecher).");
			list.Add(string.Empty);
		}
		list.AddRange(_entries);
		if (_entries.Count == 0)
		{
			list.Add("(keine Ansage mitgeschnitten)");
		}
		try
		{
			File.WriteAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FF14_Sprache.txt"), list, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return list.Count;
		}
		catch (Exception ex)
		{
			log.Error("[Spur] Datei konnte nicht geschrieben werden: " + ex.Message, Array.Empty<object>());
			return -1;
		}
	}
}
