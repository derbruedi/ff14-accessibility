# Controller crossbars

Normal crossbar sets 1–8 can be read and assigned through the spoken menu.
The menu uses the keyboard numpad, even when playing with a controller.

## Read your buttons

- In the game's controller mode, **Ctrl+F9** reads the current normal crossbar,
  including all 16 buttons and empty slots. In keyboard mode it still reads Hotbar 1.
- **`/acc crossread`** reads the normal crossbar directly in either mode.
- Changing normal sets in controller mode announces the new set once. Initial
  login and unchanged sets are silent.

Each button is described by set, trigger, direction or face-button position,
and current assignment. The set is identified as shared across jobs or job-specific.
Labels use standard PlayStation names (L2/R2, Square/Triangle/Circle/Cross) and
physical positions. On Xbox controllers use LT/RT and the corresponding face-button
positions. Custom controller remaps are not interpreted.

## Assign a button

1. Enter **`/acc crossbar`**, or press **Ctrl+Numpad0**. The shortcut initially
   selects the current crossbar in controller mode and Keyboard hotbars otherwise.
2. Use **Numpad8/2** to choose Crossbar 1–8 or Keyboard hotbars; **Numpad0** opens it.
3. Use **Numpad8/2** to hear each button and its current assignment. **Numpad4/6**
   switches bars. Press **Numpad0** on the button you want to change.
4. Use **Numpad4/6** to choose skills, inventory items, quest items, general actions,
   mounts, or companion commands. Use **Numpad8/2** to browse entries.
5. Press **Numpad0** to replace the chosen button's assignment. The menu returns
   to the button list so you can configure another button.

**Numpad decimal** goes back one step, then closes from the bar picker.
**Ctrl+Numpad0** also closes the menu. The plugin's configured shortcuts apply
if you changed them in settings. Changes to a shared set affect other jobs too.

The menu blocks PvP assignments and cancels confirmation after a job change or
logout. Reading is available without assigning anything. Expanded hold controls,
WXHB/double-cross selection, and the pet crossbar are not represented by the reader;
it reads the normal set, or reports it unavailable when the pet bar is displayed.

## Verification for contributors

Run `dotnet test tests/CrossHotbar.Tests/CrossHotbar.Tests.csproj -c Release`.
These tests cover set-change/reset behavior, boundaries, picker wrap, and slot
labels without requiring Dalamud. Build the plugin separately in Release with
`DALAMUD_HOME` pointing to the supported Dalamud assemblies. Debug builds in this
repository automatically deploy to the development plugin directory.

In-game verification is still required: compare all 16 labels to actual buttons,
read empty slots and sets 1/8, check keyboard mode and custom remaps, assign to a
chosen job-specific and shared slot, then reload/relog and verify persistence.
Check companion commands still browse and assign. Try a job change/logout while
the menu is open and verify PvP writes are refused. Offline tests do not establish
correct game memory mapping, speech playback, or saved assignment persistence.
