namespace FF14Accessibility.Services;

/// <summary>A read-only cursor over visible text. Identity is separate from text,
/// so changing a quantity does not move the reader to a different row.</summary>
internal sealed class TableReader
{
    internal sealed record Cell(string Key, string Text, float X, float Y, float Height, string Group = "", string Label = "");
    internal sealed record Row(string Section, IReadOnlyList<Cell> Cells);
    internal IReadOnlyList<Row> Rows { get; private set; } = [];
    internal int RowIndex { get; private set; }
    internal int ColumnIndex { get; private set; }
    private string? spokenSection;

    internal static IReadOnlyList<Row> Arrange(string section, IEnumerable<Cell> cells)
    {
        var result = new List<Row>();
        var unique = cells.Where(c => !string.IsNullOrWhiteSpace(c.Text)
                && float.IsFinite(c.X) && float.IsFinite(c.Y))
            .DistinctBy(c => c.Key).OrderBy(c => c.Y).ThenBy(c => c.X);
        var line = new List<Cell>();
        float top = 0, tolerance = 0;
        foreach (var cell in unique)
        {
            // Compare against the first cell, not the previous one: chained
            // offsets must never merge successive rows. Tolerance scales with text.
            if (line.Count == 0 || Math.Abs(cell.Y - top) <= Math.Min(tolerance, Math.Clamp(cell.Height * .35f, 2, 10)))
            {
                if (line.Count == 0) { top = cell.Y; tolerance = Math.Clamp(cell.Height * .35f, 2, 10); }
                line.Add(cell);
            }
            else
            {
                AddLine(section, line, result);
                line = [cell]; top = cell.Y; tolerance = Math.Clamp(cell.Height * .35f, 2, 10);
            }
        }
        if (line.Count > 0) AddLine(section, line, result);
        return result;
    }

    private static void AddLine(string section, List<Cell> line, List<Row> result)
    {
        // A visual line can contain independent controls or several label/value
        // pairs. Neither the screen Y nor the previous text proves ownership.
        var fields = new List<Cell>();
        foreach (var cell in line.OrderBy(c => c.X))
        {
            if (fields.Count > 0 && (cell.Group != fields[0].Group || !IsNumber(cell.Text)))
            {
                result.Add(new Row(section, fields.ToArray()));
                fields.Clear();
            }
            fields.Add(cell);
        }
        if (fields.Count > 0) result.Add(new Row(section, fields.ToArray()));
    }

    internal bool Refresh(IReadOnlyList<Row> rows)
    {
        var key = Rows.Count == 0 ? null : Rows[RowIndex].Cells[ColumnIndex].Key;
        Rows = rows.Where(r => r.Cells.Count > 0).ToArray();
        RowIndex = Math.Clamp(RowIndex, 0, Math.Max(0, Rows.Count - 1));
        ColumnIndex = 0;
        for (var i = 0; key != null && i < Rows.Count; i++)
            for (var j = 0; j < Rows[i].Cells.Count; j++)
                if (Rows[i].Cells[j].Key == key) { RowIndex = i; ColumnIndex = j; return true; }
        return Rows.Count > 0;
    }

    internal void MoveRow(int delta)
    {
        RowIndex = (int)Math.Clamp((long)RowIndex + delta, 0, Math.Max(0, Rows.Count - 1));
        ColumnIndex = 0;
    }
    internal void MoveColumn(int delta)
    {
        if (Rows.Count > 0) ColumnIndex = Math.Clamp(ColumnIndex + delta, 0, Rows[RowIndex].Cells.Count - 1);
    }
    internal void MoveSection(int delta)
    {
        if (Rows.Count == 0 || delta == 0) return;
        var section = Rows[RowIndex].Section;
        for (var i = RowIndex + delta; i >= 0 && i < Rows.Count; i += delta)
            if (Rows[i].Section != section)
            {
                while (i > 0 && Rows[i - 1].Section == Rows[i].Section) i--;
                RowIndex = i; ColumnIndex = 0; return;
            }
    }
    internal string SpeakRow()
    {
        if (Rows.Count == 0) return AccessibilityStrings.TableEmpty;
        var row = Rows[RowIndex];
        var parts = row.Cells.Select((cell, i) => i == 0 ? cell.Text
            : (LabelFor(row, i) == row.Cells[i - 1].Text ? ": " : "; ") + cell.Text);
        var section = SectionToSpeak();
        if (row.Cells.Count == 1 && row.Cells[0].Text == section) section = "";
        return AccessibilityStrings.TableRow(section, RowIndex + 1, Rows.Count, string.Concat(parts));
    }

    internal string SpeakCell()
    {
        if (Rows.Count == 0) return AccessibilityStrings.TableEmpty;
        var row = Rows[RowIndex];
        var section = SectionToSpeak();
        return (section.Length == 0 ? "" : section + ". ")
            + AccessibilityStrings.TableCell(ColumnIndex + 1, row.Cells.Count,
                LabelFor(row, ColumnIndex), row.Cells[ColumnIndex].Text);
    }

    private string SectionToSpeak()
    {
        var section = Rows[RowIndex].Section;
        if (section == spokenSection) return "";
        spokenSection = section;
        return section;
    }

    private static string LabelFor(Row row, int index)
    {
        var cell = row.Cells[index];
        if (cell.Label.Length > 0) return cell.Label;
        if (!IsNumber(cell.Text)) return "";
        return row.Cells.Take(index).LastOrDefault(c => c.Group == cell.Group && !IsNumber(c.Text))?.Text ?? "";
    }

    private static bool IsNumber(string text) => text.Any(char.IsDigit)
        && text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || ",.%/+-−".Contains(c));
}
