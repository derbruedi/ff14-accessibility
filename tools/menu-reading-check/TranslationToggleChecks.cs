using System.Reflection;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

internal static class TranslationToggleChecks
{
    internal static int Run(GameData game, GameDataReader data, GameDescriptionService service, IPluginLog log)
    {
        var savedMode = Loc.Mode;
        var savedTranslation = Loc.TranslateItemsAndActions;
        var savedClient = data.Language;
        var checks = 0;
        try
        {
            Loc.Mode = LanguageMode.Russian;
            Loc.TranslateItemsAndActions = true;
            data.Language = ClientLanguage.English;
            var potion = game.Excel.GetSheet<Item>().First(r => r.Name.ExtractText() == "Potion").RowId;
            var action = game.Excel.GetSheet<Lumina.Excel.Sheets.Action>().First(r => r.Name.ExtractText() == "Fire").RowId;
            var craft = game.Excel.GetSheet<CraftAction>().First(r => r.Name.ExtractText() == "Basic Synthesis").RowId;
            var trait = game.Excel.GetSheet<Trait>().First(r => r.Name.ExtractText() == "Firestarter" && r.Level == 42).RowId;
            var buddy = game.Excel.GetSheet<BuddyAction>().First(r => r.Name.ExtractText().Length > 0 && r.Description.ExtractText().Length > 0).RowId;
            var pet = game.Excel.GetSheet<PetAction>().First(r => r.Name.ExtractText().Length > 0 && r.Description.ExtractText().Length > 0).RowId;
            var status = game.Excel.GetSheet<Status>().First(r => r.Name.ExtractText() == "Regen").RowId;
            var keyItem = game.Excel.GetSheet<EventItem>().First(r => Regex.IsMatch(service.EventItemName(r.RowId), "[А-Яа-яЁё]")).RowId;
            var aoz = game.Excel.GetSheet<AozAction>().First(r => r.Action.RowId != 0);
            var pomander = game.Excel.GetSheet<DeepDungeonItem>().First(r => r.Name.ExtractText().Length > 0).RowId;
            var inventory = new InventoryService(null!, data, null!, new Configuration(), null!, log, service);
            var notebook = new AozNotebookService(null!, data, log, service);
            var sources = new AozSpellSourceService(data, new PlacesService(data, null!, log), log);
            var samples = new List<(string Label, Func<string> Read, Func<string> Raw)>
            {
                ("ItemName", () => service.ItemName(potion), () => Raw<Item>(potion, r => r.Name)),
                ("InventoryItemName", () => inventory.ResolveItemName(potion), () => Raw<Item>(potion, r => r.Name)),
                ("ItemDescription", () => service.Item(potion), () => Raw<Item>(potion, r => r.Description)),
                ("EventItemName", () => service.EventItemName(keyItem), () => Raw<EventItem>(keyItem, r => r.Name)),
                ("ActionName", () => service.ActionName(action), () => Raw<Lumina.Excel.Sheets.Action>(action, r => r.Name)),
                ("ActionDescription", () => service.Action(action), () => Raw<ActionTransient>(action, r => r.Description)),
                ("CraftActionName", () => service.CraftActionName(craft), () => Raw<CraftAction>(craft, r => r.Name)),
                ("CraftActionDescription", () => service.CraftAction(craft), () => Raw<CraftAction>(craft, r => r.Description)),
                ("TraitName", () => service.TraitName(trait), () => Raw<Trait>(trait, r => r.Name)),
                ("TraitDescription", () => service.Trait(trait), () => Raw<TraitTransient>(trait, r => r.Description)),
                ("BuddyActionName", () => service.BuddyActionName(buddy), () => Raw<BuddyAction>(buddy, r => r.Name)),
                ("BuddyActionDescription", () => service.BuddyAction(buddy), () => Raw<BuddyAction>(buddy, r => r.Description)),
                ("GeneralActionName", () => service.GeneralActionName(30), () => Raw<GeneralAction>(30, r => r.Name)),
                ("GeneralActionDescription", () => service.GeneralAction(30), () => Raw<GeneralAction>(30, r => r.Description)),
                ("PetActionName", () => service.PetActionName(pet), () => Raw<PetAction>(pet, r => r.Name)),
                ("PetActionDescription", () => service.PetAction(pet), () => Raw<PetAction>(pet, r => r.Description)),
                ("StatusName", () => service.StatusName(status), () => Raw<Status>(status, r => r.Name)),
                ("StatusDescription", () => service.Status(status), () => Raw<Status>(status, r => r.Description)),
                ("AozDescription", () => service.AozDescription(aoz.RowId), () => Raw<AozActionTransient>(aoz.RowId, r => r.Description)),
                ("AozStats", () => service.AozStats(aoz.RowId), () => Raw<AozActionTransient>(aoz.RowId, r => r.Stats)),
                ("PomanderName", () => GameText<DeepDungeonItem>(pomander, r => r.Name, "Name"), () => Raw<DeepDungeonItem>(pomander, r => r.Name)),
                ("PomanderTooltip", () => GameText<DeepDungeonItem>(pomander, r => r.Tooltip, "Tooltip"), () => Raw<DeepDungeonItem>(pomander, r => r.Tooltip)),
            };
            foreach (var client in new[] { ClientLanguage.English, ClientLanguage.German, ClientLanguage.French, ClientLanguage.Japanese })
            {
                data.Language = client;
                // A running plugin has one fixed client language. Each client
                // fixture needs fresh services before testing live toggles.
                inventory = new InventoryService(null!, data, null!, new Configuration(), null!, log, service);
                notebook = new AozNotebookService(null!, data, log, service);
                sources = new AozSpellSourceService(data, new PlacesService(data, null!, log), log);
                foreach (var sample in samples)
                {
                    Loc.TranslateItemsAndActions = true;
                    var translated = sample.Read();
                    Require(Regex.IsMatch(translated, "[А-Яа-яЁё]"), $"Missing Russian {sample.Label}/{client}");
                    Loc.TranslateItemsAndActions = false;
                    Require(sample.Read() == sample.Raw(), $"Disabled translation did not return exact game text: {sample.Label}/{client}");
                    Loc.TranslateItemsAndActions = true;
                    Require(sample.Read() == translated, $"Restoring translation failed: {sample.Label}/{client}");
                }
                var menuLabel = data.GetExcelSheet<Trait>().GetRow(trait).Name.ExtractText() + " Ур. 42";
                var resolve = typeof(GameDescriptionService).GetMethod("FromActionMenuLabel", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Loc.TranslateItemsAndActions = true;
                var translatedEntry = resolve.Invoke(service, [menuLabel, "", new HashSet<uint> { trait }])!;
                Loc.TranslateItemsAndActions = false;
                var originalEntry = resolve.Invoke(service, [menuLabel, "", new HashSet<uint> { trait }])!;
                Require((string)originalEntry.GetType().GetProperty("Name")!.GetValue(originalEntry)! == Raw<Trait>(trait, r => r.Name), "Menu cached translated name after off");
                Require((string)originalEntry.GetType().GetProperty("Description")!.GetValue(originalEntry)! == Raw<TraitTransient>(trait, r => r.Description), "Menu cached translated description after off");
                Loc.TranslateItemsAndActions = true;
                Require(translatedEntry.Equals(resolve.Invoke(service, [menuLabel, "", new HashSet<uint> { trait }])), "Menu failed to restore translated entry");

                var translatedSpell = notebook.Lookup(aoz.Action.RowId)!.Value;
                var translatedTarget = sources.GetAll().First(r => r.ActionId == aoz.Action.RowId);
                Loc.TranslateItemsAndActions = false;
                var rawSpell = notebook.Lookup(aoz.Action.RowId)!.Value;
                var rawTarget = sources.GetAll().First(r => r.ActionId == aoz.Action.RowId);
                Require(rawSpell.Name == Raw<Lumina.Excel.Sheets.Action>(aoz.Action.RowId, r => r.Name).Trim(), "Notebook retained Russian name after off");
                Require(rawSpell.Description != translatedSpell.Description, "Notebook retained Russian description after off");
                // RowRef resolves in GameData's default language. Compare with
                // that actual input, not a direct sheet in another language.
                var sourceName = data.GetExcelSheet<AozAction>().GetRow(aoz.RowId).Action.Value.Name.ExtractText().Trim();
                Require(rawTarget.SpellName == sourceName, $"Spell source retained translated text after off/{client}");
                Require(rawTarget.PlaceName == translatedTarget.PlaceName, "Disabling spell translation changed its place name");
                Loc.TranslateItemsAndActions = true;
                Require(notebook.Lookup(aoz.Action.RowId) == translatedSpell, "Notebook failed to restore translation");
                Require(sources.GetAll().First(r => r.ActionId == aoz.Action.RowId) == translatedTarget, "Spell source failed to restore translation");
                Require(Loc.IsRussian && AccessibilityStrings.UnknownCommand.StartsWith("Неизвестная"), "Plugin prompts lost Russian");
            }
            return checks;

            string Raw<T>(uint id, Func<T, ReadOnlySeString> field) where T : struct, IExcelRow<T>
                => field(data.GetExcelSheet<T>().GetRow(id)).ExtractText();
            string GameText<T>(uint id, Func<T, ReadOnlySeString> field, string column) where T : struct, IExcelRow<T>
            {
                var method = typeof(GameDescriptionService).Assembly.GetType("FF14Accessibility.Services.RussianGameText")!
                    .GetMethod("Text", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(T));
                return ((ReadOnlySeString)method.Invoke(null, [data, data.GetExcelSheet<T>().GetRow(id), field, column])!).ExtractText();
            }
            void Require(bool condition, string message)
            {
                if (!condition) throw new Exception(message);
                checks++;
            }
        }
        finally
        {
            Loc.Mode = savedMode;
            Loc.TranslateItemsAndActions = savedTranslation;
            data.Language = savedClient;
        }
    }
}
