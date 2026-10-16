using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using System.Text.RegularExpressions;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var reportPath = args.Length > 1 ? args[1] : Path.Combine(root, "docs/maintenance/russian-speech-source-audit.json");
var missing = new List<object>();
var direct = new List<object>();
var localizations = new HashSet<string>();
var files = Directory.GetFiles(Path.Combine(root, "FF14Accessibility"), "*.cs", SearchOption.AllDirectories)
    .Where(f => !Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).Any(x => x is "obj" or "bin")).Order().ToArray();
foreach (var symbols in new[] { Array.Empty<string>(), new[] { "DEBUG" } })
    foreach (var file in files)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(preprocessorSymbols: symbols));
        foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var expression = call.Expression.ToString();
            var arguments = call.ArgumentList.Arguments;
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var line = tree.GetLineSpan(call.Span).StartLinePosition.Line + 1;
            if (expression is "L" or "AccessibilityStrings.L")
            {
                localizations.Add($"{relative}:{line}:{call.SpanStart}");
                if (arguments.Count < 3) missing.Add(new { file=relative, line, code=call.ToString() });
            }
            if (!expression.EndsWith(".Speak") && !expression.EndsWith(".SpeakInterrupt")) continue;
            // Only text written directly in the speech argument is covered here.
            // Names/labels passed in variables or read from game windows need a separate audit.
            var literals = arguments.SelectMany(a => a.Expression.DescendantNodesAndSelf())
                .Where(n => !n.Ancestors().OfType<InvocationExpressionSyntax>().TakeWhile(n => n != call)
                    .Any(n => n.Expression.ToString().StartsWith("AccessibilityStrings.")))
                .Select(n => n switch
                {
                    LiteralExpressionSyntax s when s.IsKind(SyntaxKind.StringLiteralExpression) => s.Token.ValueText,
                    InterpolatedStringTextSyntax s => s.TextToken.ValueText,
                    _ => ""
                }).Where(s => Regex.IsMatch(s, "[A-Za-zÄÖÜäöüß]{3,}"));
            if (literals.Any()) direct.Add(new { file=relative, line, code=call.ToString() });
        }
    }
var report = new
{
    scope = "L argument presence and direct spoken literals in Release and DEBUG; not all dynamic game text or translation quality",
    source_files = files.Length, localization_calls = localizations.Count,
    missing_russian_arguments = missing.DistinctBy(x => JsonSerializer.Serialize(x)).ToArray(),
    direct_unlocalized_speech = direct.DistinctBy(x => JsonSerializer.Serialize(x)).ToArray()
};
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
Console.WriteLine($"{files.Length} source files; {localizations.Count} language calls; {report.missing_russian_arguments.Length} missing Russian arguments; {report.direct_unlocalized_speech.Length} direct spoken literals.");
if (missing.Count > 0 || direct.Count > 0) Environment.ExitCode=1;
