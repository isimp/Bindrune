# Technical notes

## Clash tiers

A hard clash is the same key with the same modifiers. A soft clash is one where one side is a single key with no modifiers, so it fires under the other combination too. A note is worth knowing but should not interfere. Pairs that are known to be active at the same time are marked confirmed.

Alt, Ctrl and Shift on their own are never reported as clashes, since mods share them on purpose to qualify clicks and other keys.

## Where a bind applies

A bind is described along two independent axes: where it is live (World, Build placement, Build menu, Inventory, Container, Crafting, Map, Chat, Vehicle, Custom) and what has to be in your hands (a skill, an item type, or one specific item). Two binds are only ruled out when one axis rules them out. If one describes a place and the other an item, nothing is proven and the pair is still reported.

The description comes from these sources, first one wins: the mod itself, if its author declared it (see [for-mod-authors.md](for-mod-authors.md)); what you set in the panel; the mod's Jotunn key hints; the shared list in `BepInEx/config/Bindrune/known.txt`; and for the game's own binds, a checked table plus what Bindrune reads from the game's code.

## The shared list

`BepInEx/config/Bindrune/known.txt` describes binds for mods that do not describe themselves. It ships empty, with the format in its header, one line per bind:

```
cfg:com.example.mod:General:DigKey<TAB>World<TAB>skill:Pickaxes
```

It lives in `config` on purpose, so it travels with a modpack and everyone following the pack gets it.

## Your keys and the profile's

Keys marked as your own are stored in `BepInEx/bindrune.keys`, outside `config` and with an extension no profile sync picks up, and reapplied once the game is running. Each bind can be switched between your own key and the profile's key, and both are remembered. Muted clashes and pinned hints sit in the same file, for the same reason: a sync would hand you the profile owner's and delete yours.

Versions before 0.2.1 kept those two in `BepInEx/config/Bindrune/`. They are moved into `bindrune.keys` on the first launch after the update and the old files are removed. A copy a sync puts back afterwards is ignored, and the log says so.

## Files

Settings are in `BepInEx/config/isimp.Bindrune.cfg`. Situations you set and the shared list are in `BepInEx/config/Bindrune/`, so they travel with a profile. Your own keys, muted clashes and pinned hints are in `BepInEx/bindrune.keys`, so they do not. Data derived from the game is cached in `BepInEx/cache/Bindrune/` and rebuilt after a game update. All of these are plain text and safe to edit or delete.

## How it reads the game

Bindrune reads the game's assemblies with Mono.Cecil, finds the code that reads each bind and derives where it is used. For example, `TabRight` is only read during build placement, so it cannot collide with `Use` elsewhere even though both are on E. The result is cached against the assemblies' file stamps and rebuilt after a game update. Mono.Cecil ships with BepInEx.

Jotunn is declared as a soft dependency, so Bindrune loads without it, but the panel and the hints are built with Jotunn's GUI and nothing is shown without it.

## Key names

Keys are shown the way the keyboard in use labels them, using the same names as the game's own controls settings. Config files and mods store keys by their position on a US keyboard instead, so on a German keyboard the key to the left of X is shown as Y but stored as Z, and the key to the right of L is shown as Ö but stored as Semicolon. Only keys that print a single character are renamed. Modifiers, function keys, arrows, mouse buttons and the numpad keep their usual names.

Searching by key uses the shown names, and a single typed character is read as a key. Show stored key names, under Advanced on the ? page, adds the stored name to every bind where the two differ. The names only affect what is displayed; how a key is stored, compared and written is the same either way. Setting `KeyboardLayoutLabels` under `[Display]` to false shows the stored names everywhere.

## Limits

A mod that writes its key directly into its code instead of a setting has nothing to read and does not appear. Keybind settings stored as free text are shown but cannot be changed from Bindrune. Gamepad buttons are listed while a controller is connected but are not editable and are left out of clashes. A bind with no description is compared with everything.

## Building

Requires the .NET SDK 8 or newer. To build against the reference stubs in `lib/`, with no game installation needed:

```
dotnet build -c Release -p:LibsDir=lib
```

To build against a local installation and copy the result into a BepInEx profile:

```
dotnet build -c Release -p:ValheimDir="<Valheim folder>" -p:ProfileDir="<profile folder>"
```

The `VALHEIM_DIR` environment variable can be used instead of `ValheimDir`. Without these, a default Steam installation and a default Gale profile are assumed.

The files in `lib/` contain metadata only: every method body is replaced and resources are removed, so they can be compiled against but not run. Regenerate them after a game or Jotunn update with `tools/strip-references.ps1`. Releasing is described in [releasing.md](releasing.md).
