using System.Text.Json;
using System.Text.Json.Nodes;
using Lumina;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Uld;
using Lumina.Excel.Sheets;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--write"))
    throw new ArgumentException("SquadronLayoutCheck <game/sqpack> <fixture.json> [--write]");
using var game = new GameData(args[0]);
object Node(UldRoot.NodeData n)
{
    var textId = n.NodeType == 3 ? (uint?)n.Node.GetType().GetField("TextId")!.GetValue(n.Node)! : null;
    return new { Id = n.NodeId, Parent = n.ParentId, Type = n.NodeType, n.X, n.Y, n.W, n.H,
        TextRow = textId, Text = textId.HasValue ? game.Excel.GetSheet<Addon>().GetRow(textId.Value).Text.ExtractText() : null };
}
string[] names = ["GcArmyExpeditionSelect", "GcArmyMemberList", "GcArmyMemberProfile", "GcArmyTrainingList",
    "GcArmyExpeditionResult", "GcArmyCaptureSelect", "GcArmyChangeClass", "GcArmyChangeExpeditionTrait", "GcArmyChangeTactic"];
var layouts = names.Select(name =>
{
    var source = $"ui/uld/{name.ToLowerInvariant()}.uld";
    var layout = game.GetFile<UldFile>(source) ?? throw new Exception($"Missing {source}");
    return new { Name = name, Source = source, Nodes = layout.WidgetData.Nodes.Select(Node),
        Components = layout.Components.Select(c => new { c.Id, c.Type, Nodes = c.Nodes.Select(Node) }) };
}).ToArray();
var json = JsonSerializer.Serialize(layouts);
if (args.Length == 3) File.WriteAllText(args[1], json + Environment.NewLine);
else if (!JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(File.ReadAllText(args[1]))))
    throw new Exception("Installed squadron layouts differ from the test fixture. Review before release.");
Console.WriteLine($"Verified {layouts.Length} squadron layouts from installed game data. Live speech still requires FFXIV.");
