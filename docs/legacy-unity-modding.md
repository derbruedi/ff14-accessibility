# Modding Old Unity Versions

## Unity versions and mod loader compatibility

- **Unity 2019+**: MelonLoader, BepInEx, Doorstop - all work
- **Unity 2017-2018**: MelonLoader, BepInEx, Doorstop - all work
- **Unity 5.x**: MelonLoader partially, BepInEx partially, possibly Doorstop, assembly patching as fallback
- **Unity 4.x or older**: Only assembly patching works

## Check before setup

### 1. Determine the Unity version

The Unity version is found in:
- `[Game]_Data/output_log.txt` (first line: "Initialize engine version: X.X.X")
- Or in the crash log
- Or in the MelonLoader log after the first start

### 2. Check the architecture

- `Mono/` folder = 32-bit, old Mono
- `MonoBleedingEdge/` folder = 64-bit, newer Mono
- Game in "Program Files (x86)" = often 32-bit

### 3. Community research

**Always check first:**
- Do mods for the game already exist?
- Which framework does the community use?
- Is there an official mod system?

**Search terms:**
- "[game name] modding guide"
- "[game name] BepInEx"
- "[game name] MelonLoader"
- "[game name] dll mod"

**Where to search:**
- Steam discussions
- Nexus Mods
- ModDB
- GitHub
- The developer's official forums

## Try mod loaders in priority order

### For Unity 2017+

1. **MelonLoader** - simplest option for Unity games
2. **BepInEx** - very widespread, good documentation
3. **Doorstop** - if the others do not work

### For Unity 5.x

1. **BepInEx 5.x** with net35 compatibility
2. **Doorstop v3** (legacy branch)
3. **Assembly patching** as fallback

### For Unity 4.x or older

1. **Look for a community solution** - maybe someone has already found something
2. **Assembly patching** - usually the only option that works

## Assembly patching (always works)

### What it is

Inserting the mod code directly into the game DLL (`Assembly-CSharp.dll`).

### Advantages

- Works with any Unity version
- No external tools at runtime
- No proxy DLLs needed

### Disadvantages

- Modifies original files (make a backup!)
- Has to be re-patched on game updates
- Steam integrity check detects the change

### Tools

- **dnSpy** - GUI-based, can edit and save
- **ILSpy + Reflexil** - alternative

### Procedure

1. Back up `[Game]_Data/Managed/Assembly-CSharp.dll`
2. Open the DLL in dnSpy
3. Find a suitable spot (e.g. MainMenu.Start or Awake)
4. Insert code (edit a method or add a new class)
5. Save (save module)
6. Test

### Suitable entry points

- `Awake()` or `Start()` of a class loaded early
- Main menu class (always loaded)
- Singleton initialisation

## Known problems

### "mono.dll Access Violation"

- Occurs on Unity 4.x with BepInEx/Doorstop
- Mono runtime too old for modern tools
- Solution: assembly patching

### "Hooked into null"

- MelonLoader cannot hook in
- Unity version not supported
- Solution: another framework or assembly patching

### Game does not start (0xc0000142)

- Proxy DLL incompatible
- Solution: remove the proxy DLL, choose another approach

## C# language features and old Mono runtimes

Unity ships its own Mono runtime. On old Unity versions that runtime is so old that certain C# features do **compile** but **crash at runtime**. That is particularly treacherous because no compiler error appears — the build succeeds, and then the game crashes without a clear error message.

### How do you tell which runtime you have?

- `Mono/` folder in the game directory = old runtime (Unity 5.x and earlier)
- `MonoBleedingEdge/` folder = newer runtime (Unity 2017+, but still limited until roughly 2019)
- From Unity 2021+ with `MonoBleedingEdge/`, practically all modern C# features are safe

### Known limitations (Unity 2017 and older)

**LINQ with lambdas** — crashes at runtime:
```csharp
// CRASHES on the old Mono runtime:
var active = myList.Where(x => x.IsActive).ToList();
var names = myList.Select(x => x.Name).ToList();

// SAFE — a classic loop instead:
var active = new List<MyType>();
foreach (var item in myList)
{
    if (item.IsActive) active.Add(item);
}
```

**string.Join with a List** — crashes at runtime:
```csharp
// CRASHES — old Mono only knows the array overload:
string result = string.Join(", ", myList);

// SAFE:
string result = string.Join(", ", myList.ToArray());
```

**Switch expressions** — do not compile, or crash:
```csharp
// CRASHES:
string label = state switch { State.Active => "on", State.Off => "off", _ => "?" };

// SAFE:
string label;
switch (state)
{
    case State.Active: label = "on"; break;
    case State.Off: label = "off"; break;
    default: label = "?"; break;
}
```

**Sort with a lambda** — crashes at runtime:
```csharp
// CRASHES:
myList.Sort((a, b) => a.Distance.CompareTo(b.Distance));

// SAFE — your own IComparer class:
private class DistanceComparer : IComparer<MyType>
{
    public int Compare(MyType a, MyType b)
    {
        return a.Distance.CompareTo(b.Distance);
    }
}
myList.Sort(new DistanceComparer());
```

**Reflection null checks** — the comparison does not work:
```csharp
// DOES NOT WORK on old Mono — always returns false:
if (fieldInfo == null) { ... }

// SAFE — explicit cast:
if ((object)fieldInfo == null) { ... }
```

### Which Unity version needs what?

- **Unity 4.x:** Heavily limited. No LINQ, no lambdas, no `var` in some contexts. Treat it like C# 3.0.
- **Unity 5.x:** Basic LINQ works (Where, Select without lambdas), but lambda syntax is often problematic. Sort lambdas are unsafe.
- **Unity 2017-2018:** LINQ with lambdas usually works, but string.Join with a List and switch expressions do not. Reflection null checks are unsafe.
- **Unity 2019+:** Most modern C# features work. Test anyway.
- **Unity 2021+:** Practically no limitations left.

### Why this works

This is not a hack. For `list.Where(x => ...)` the C# compiler emits different intermediate code (IL) than for a `foreach` loop. The old Mono runtime can execute the simpler IL but not the more complex one. Both variants do exactly the same thing — the loop is the older, compatible way of getting the same result.

This only concerns the **game's Mono runtime**, not the mod loader. MelonLoader and BepInEx run on that runtime — they cannot change or bypass it.

### Recommendation

On Unity 2018 and older: use the compatible variants (loops instead of LINQ lambdas, arrays instead of lists for string.Join, switch blocks instead of switch expressions). This is not a stylistic compromise but a technical necessity, because the game runtime does not understand the modern IL instructions. If a feature does work against expectations, note in the code that it was tested — that saves the next developer the same research.

---

## Peculiarities of UI analysis (old Unity versions)

Older Unity versions can present additional challenges:

- **Older UI systems:** Unity 4.x often still uses the old `OnGUI` system instead of uGUI/Canvas
- **Different component names:** `GUIText` instead of `Text`, `GUITexture` instead of `Image`
- **Missing features:** TextMeshPro may not exist
- **Reflection differences:** private fields may follow different naming conventions

### The OnGUI system (Unity 4.x and earlier)

The old OnGUI system works completely differently from modern Unity UI:

```csharp
void OnGUI() {
    if (GUI.Button(new Rect(10, 10, 100, 50), "Click me")) {
        // Button was clicked
    }
    GUI.Label(new Rect(10, 70, 100, 20), "Some text");
}
```

**Challenges:**
- The UI is redrawn every frame (immediate mode)
- No persistent GameObjects for UI elements
- Harder to hook than modern UI
- The text is often only known inside the OnGUI call

**Possible solutions:**
- Harmony patch on the OnGUI method
- Analyse GUI.skin and GUIStyle
- Your own tracking logic for UI state

## Checklist for old games

- [ ] Unity version determined
- [ ] Architecture checked (32/64-bit)
- [ ] Community solutions researched
- [ ] Official mod system checked
- [ ] Mod loaders tried in order
- [ ] On failure: assembly patching prepared
- [ ] Backup of the original DLLs created
