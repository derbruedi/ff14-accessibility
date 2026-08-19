using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace FF14Accessibility.Services;

/// <summary>One line of the game's task list, already turned into speech.</summary>
/// <param name="Text">The line as the game words it ("Abgesuchte Gebiete").</param>
/// <param name="Detail">Counter, percentage or remaining time; empty when the
/// line carries none.</param>
/// <param name="Complete">Whether the game marks this line as done.</param>
public sealed record DirectorTodoLine(string Text, string Detail, bool Complete);

/// <summary>The task list of one running director (levequest, duty, FATE ...).</summary>
/// <param name="Title">What the director calls itself - the leve or duty name.</param>
/// <param name="Objective">Its one-line objective, or empty.</param>
/// <param name="Lines">The task lines, in the game's own order.</param>
public sealed record DirectorTasks(string Title, string Objective, IReadOnlyList<DirectorTodoLine> Lines);

/// <summary>
/// Reads the task list the game shows at the edge of the screen - the lines a
/// sighted player checks to see what is being asked of them right now and how
/// far they have got ("Abgesuchte Gebiete", "Streunender Dodo 0/1").
///
/// A blind player had no access to this at all. It came up while chasing
/// levequest enemies (2026-08-18): the leve's own task list turned out to be the
/// one thing that could say what the leve wanted, and it is worth far more than
/// that one case - the same field carries dungeon and FATE objectives.
///
/// SOURCE (ilspycmd-verified 2026-08-18, values measured the same day):
///   EventFramework.Instance()->DirectorModule.DirectorList
///     -> every active director.
///   Director.Title@760, Director.Objective@864, Director.DirectorTodos@1088
///     (StdVector&lt;DirectorTodo&gt;).
///   DirectorTodo: Enabled@0, Type@4 (TodoType), Text@8 (Utf8String),
///     Complete@112, and a UNION at @120/@124 whose meaning depends on Type -
///     CurrentCount/NeededCount for the fraction kinds, CurrentPercentage for
///     the bar kinds, EndTimestamp@128 for the time kind.
/// All of these sit inside the 1120-byte Director base, so no director subclass
/// has to be identified to read them.
///
/// The union is exactly why the type is honoured instead of reading one field
/// blindly: a percentage read as a count would be a number that means nothing.
/// </summary>
public sealed class DirectorTodoService
{
    private readonly IPluginLog _log;

    public DirectorTodoService(IPluginLog log) => _log = log;

    /// <summary>
    /// The task lists of everything running right now, read fresh. Usually zero
    /// or one entry; several are possible (a leve inside a duty) and all are
    /// returned rather than picked between - guessing which one the player means
    /// would silently hide the other.
    /// </summary>
    public unsafe List<DirectorTasks> GetActiveTasks()
    {
        var result = new List<DirectorTasks>();

        var framework = EventFramework.Instance();
        if (framework == null) return result;

        ref var directors = ref framework->DirectorModule.DirectorList;
        for (var i = 0; i < directors.LongCount; i++)
        {
            var director = directors[i].Value;
            if (director == null) continue;

            var lines = new List<DirectorTodoLine>();
            ref var todos = ref director->DirectorTodos;
            for (var t = 0; t < todos.LongCount; t++)
            {
                ref var todo = ref todos[t];
                if (!todo.Enabled) continue;

                var text = todo.Text.ToString().Trim();
                if (text.Length == 0) continue;

                lines.Add(new DirectorTodoLine(text, DescribeProgress(ref todo), todo.Complete));
            }

            var title = director->Title.ToString().Trim();
            var objective = director->Objective.ToString().Trim();
            if (lines.Count == 0 && title.Length == 0 && objective.Length == 0) continue;

            result.Add(new DirectorTasks(title, objective, lines));
        }

        return result;
    }

    /// <summary>
    /// The progress part of one line, read according to its type. Types the game
    /// documents as carrying nothing extra return an empty string - saying
    /// nothing is right there, and reading the union anyway would speak a number
    /// that belongs to a different field.
    /// </summary>
    private static string DescribeProgress(ref DirectorTodo todo) => todo.Type switch
    {
        TodoType.FractionBar or TodoType.Fraction or TodoType.LargeGrayFraction
            => AccessibilityStrings.TodoFraction(todo.CurrentCount, todo.NeededCount),
        TodoType.Number or TodoType.LargeGrayNumber
            => AccessibilityStrings.TodoCount(todo.CurrentCount),
        TodoType.Bar or TodoType.LargeBar or TodoType.ColorableBar or TodoType.LongBar
            => AccessibilityStrings.TodoPercent(todo.CurrentPercentage),
        TodoType.TimeRemaining or TodoType.LargeTimeRemaining
            => DescribeRemainingTime(todo.EndTimestamp),
        _ => string.Empty,
    };

    /// <summary>Remaining time from the line's end timestamp (unix seconds).
    /// Empty when the timestamp is unset or already past - a countdown that has
    /// run out is not information, it is noise.</summary>
    private static string DescribeRemainingTime(long endTimestamp)
    {
        if (endTimestamp <= 0) return string.Empty;
        var remaining = endTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return remaining <= 0 ? string.Empty : AccessibilityStrings.TodoTimeLeft((int)remaining);
    }
}
