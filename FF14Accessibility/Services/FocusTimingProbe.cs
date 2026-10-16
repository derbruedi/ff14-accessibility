using System;
using System.Diagnostics;

namespace FF14Accessibility.Services;

internal static class FocusTimingProbe
{
	internal enum Phase
	{
		FokusGesamt,
		Agent,
		NameAusBlatt,
		Basics,
		Gear,
		EigeneKlassen,
		GearsetMarke,
		TascheGesamt,
		TascheBeutel,
		RahmenGesamt,
		RahmenAbstand,
		WarteBild,
		WarteMs,
		WarteEndeAgent,
		WarteEndeErsatzHinterher,
		WarteEndeErsatzTaub,
		WarteEndeWechsel,
		WarteStartOffen,
		Count
	}

	internal enum WaitEnd
	{
		Agent,
		ErsatzHinterher,
		ErsatzTaub,
		Wechsel
	}

	private static readonly double MsPerTick = 1000.0 / (double)Stopwatch.Frequency;

	private static readonly long[] _counts = new long[18];

	private static readonly double[] _totalMs = new double[18];

	private static readonly double[] _maxMs = new double[18];

	private static long _lastFrameTicks;

	private const int FrameWindow = 120;

	private const int FrameWindowMinimum = 25;

	private static readonly double[] _frameWindow = new double[120];

	private static int _frameWindowCount;

	private static int _frameWindowNext;

	public static long Start()
	{
		return Stopwatch.GetTimestamp();
	}

	public static void Add(Phase phase, long startTicks)
	{
		Record(phase, (double)(Stopwatch.GetTimestamp() - startTicks) * MsPerTick);
	}

	public static void EndWait(long startTicks, int frames, bool startedOpen, WaitEnd end)
	{
		if (startTicks != 0L)
		{
			Record(Phase.WarteMs, (double)(Stopwatch.GetTimestamp() - startTicks) * MsPerTick);
		}
		if (frames > 0)
		{
			Record(Phase.WarteBild, frames);
		}
		Count(end switch
		{
			WaitEnd.Agent => Phase.WarteEndeAgent,
			WaitEnd.ErsatzHinterher => Phase.WarteEndeErsatzHinterher,
			WaitEnd.ErsatzTaub => Phase.WarteEndeErsatzTaub,
			_ => Phase.WarteEndeWechsel,
		});
		if (startedOpen)
		{
			Count(Phase.WarteStartOffen);
		}
	}

	private static void Count(Phase phase)
	{
		_counts[(int)phase]++;
	}

	private static void Record(Phase phase, double ms)
	{
		_counts[(int)phase]++;
		_totalMs[(int)phase] += ms;
		if (ms > _maxMs[(int)phase])
		{
			_maxMs[(int)phase] = ms;
		}
	}

	public static void MarkFrame()
	{
		long timestamp = Stopwatch.GetTimestamp();
		if (_lastFrameTicks != 0L)
		{
			double num = (double)(timestamp - _lastFrameTicks) * MsPerTick;
			Record(Phase.RahmenAbstand, num);
			NoteFrame(num);
		}
		_lastFrameTicks = timestamp;
	}

	private static void NoteFrame(double gapMs)
	{
		_frameWindow[_frameWindowNext] = gapMs;
		_frameWindowNext = (_frameWindowNext + 1) % 120;
		if (_frameWindowCount < 120)
		{
			_frameWindowCount++;
		}
	}

	public static bool CurrentFrameRate(out int fps, out int meanMs, out int maxMs)
	{
		fps = 0;
		meanMs = 0;
		maxMs = 0;
		if (_frameWindowCount < 25)
		{
			return false;
		}
		double num = 0.0;
		double num2 = 0.0;
		for (int i = 0; i < _frameWindowCount; i++)
		{
			double num3 = _frameWindow[i];
			num += num3;
			if (num3 > num2)
			{
				num2 = num3;
			}
		}
		double num4 = num / (double)_frameWindowCount;
		if (num4 <= 0.0)
		{
			return false;
		}
		fps = (int)Math.Round(1000.0 / num4);
		meanMs = (int)Math.Round(num4);
		maxMs = (int)Math.Round(num2);
		return true;
	}

	public static void Reset()
	{
		for (int i = 0; i < 18; i++)
		{
			_counts[i] = 0L;
			_totalMs[i] = 0.0;
			_maxMs[i] = 0.0;
		}
	}
}
