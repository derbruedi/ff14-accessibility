using System.Text.Json;
using System.Text.Json.Nodes;
using Lumina;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Uld;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--write"))
    throw new ArgumentException("TableLayoutCheck <game/sqpack> <fixture.json> [--write]");
using var game = new GameData(args[0]);
const string source = "ui/uld/characterstatus.uld";
var layout = game.GetFile<UldFile>(source) ?? throw new Exception("CharacterStatus layout missing.");
int[] Node(UldRoot.NodeData n) => [(int)n.NodeId, n.ParentId, n.NodeType, n.X, n.Y, n.W, n.H];
var snapshot = new
{
    Source = source,
    Columns = new[] { "id", "parent", "type", "x", "y", "width", "height" },
    Nodes = layout.WidgetData.Nodes.Select(Node),
    Components = layout.Components.Select(c => new { c.Id, Nodes = c.Nodes.Select(Node) }),
};
var json = JsonSerializer.Serialize(snapshot);
if (args.Length == 3) File.WriteAllText(args[1], json + Environment.NewLine);
else if (!JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(File.ReadAllText(args[1]))))
    throw new Exception("Installed game layout differs from the native test fixture. Review CharacterStatusTable before release.");
Console.WriteLine($"Verified {layout.WidgetData.Nodes.Length} window nodes and {layout.Components.Length} component layouts from {source}. This is not an in-game speech test.");
