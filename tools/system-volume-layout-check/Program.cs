using System.Text.Json;
using System.Text.Json.Nodes;
using Lumina;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;

if(args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--write"))
    throw new ArgumentException("SystemVolumeLayoutCheck <game/sqpack> <fixture.json> [--write]");
using var game = new GameData(args[0]);
const string source = "ui/uld/configsystem.uld";
var uld = game.GetFile<UldFile>(source) ?? throw new Exception("ConfigSystem layout missing.");
uint[] ids = [111,112,113,115,116,117,119,120,121,123,124,125,127,128,129,131,132,133,135,136,137,139,141,142,143,144,145,146,147,148,149,150];
var snapshot = new { Source=source, Nodes=uld.WidgetData.Nodes.Where(n=>ids.Contains(n.NodeId)).Select(n=> {
    uint? textRow=n.NodeType==3 ? (uint)n.Node.GetType().GetField("TextId")!.GetValue(n.Node)! : null;
    return new { Id=n.NodeId, Parent=n.ParentId, Type=n.NodeType, n.X, n.Y, n.W, n.H,
        TextRow=textRow, Text=textRow.HasValue ? game.Excel.GetSheet<Addon>().GetRow(textRow.Value).Text.ExtractText() : null };
}).ToArray() };
if(snapshot.Nodes.Length != ids.Length) throw new Exception("Missing sound controls.");
var json=JsonSerializer.Serialize(snapshot);
if(args.Length==3) File.WriteAllText(args[1],json+Environment.NewLine);
else if(!JsonNode.DeepEquals(JsonNode.Parse(json),JsonNode.Parse(File.ReadAllText(args[1]))))
    throw new Exception("Installed ConfigSystem layout differs from the sound-control test fixture. Review before release.");
Console.WriteLine("Verified all 10 volume sliders and their right-hand labels against installed ConfigSystem ULD and Addon sheet. Native input and live speech still require FFXIV.");
