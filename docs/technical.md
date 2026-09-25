# Technical notes

## Clash tiers

A hard clash is the same key with the same modifiers. A soft clash is one where one side is a single key with no modifiers, so it fires under the other combination too. A note is worth knowing but should not interfere. Pairs that are known to be active at the same time are marked confirmed.

Alt, Ctrl and Shift on their own are never reported as clashes, since mods share them on purpose to qualify clicks and other keys.

Five keys the game reads in its own code, rather than through a control, are listed with its binds and locked: F2 for the network panel, F9 to change gamepad layout, F11 for a screenshot, Ctrl+F1 to free the mouse and Ctrl+F3 to hide the HUD. They are read whatever screen is open, and clash with anything on the same key. With Extra Slots installed, its Rebind Connect Panel setting replaces F2 and stands in for the network panel.

Muting one is about the clash in front of you, not the pair for good. It records what was waved through, so the same pair is reported again if it turns into something worse, and the mute is dropped once the clash is gone rather than waiting to suppress a future one. Mutes for binds that are not loaded are always kept, since a bind a mod registers when a world loads is missing rather than gone. A mute from before 0.2.1 says only that the pair is fine; the first scan that finds the clash it was hiding records that clash against it, so it hides no more than it did before and no less.

## Free keys

A key pressed for a bind is saved at once when nothing else uses it, neither another bind nor the game on its own; anything else is shown first, with what it would run into. When it clashes, Bindrune offers up to three others, and picking one works the same way. The first is the same key with a modifier added, swapped or taken away, where the bind can hold one; the rest are the nearest keys on the keyboard, keeping any modifier that was held. A key is offered when nothing it would run into is worse than a note, so one shared with a bind that is never live at the same time is offered too, marked as shared; picking that one still shows the preview first.

A key a mod reads in its own code, with no setting behind it, cannot be seen, so a suggestion is free only as far as Bindrune can tell.

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

Keepsake, which keeps single config values through profile syncs, leaves keybinds to Bindrune while both are installed. A keybind kept in Keepsake becomes your own key here the next time Bindrune puts your keys back, with the key Keepsake recorded as the profile's, and its line is removed from `BepInEx/keepsake.pins`. So a key only ever has one keeper. Nothing is taken while Keepsake is not loaded. Without Bindrune, Keepsake offers to take over the keys that are yours here, reading the keys section of `bindrune.keys`.

The two files are read across the mods, so their formats have to stay in step: `keepsake.pins` (first line `# keepsake pins v1`, then cfg file, section, setting, value and optionally the profile's value, tab separated) and the keys section of `bindrune.keys` (first line `# bindrune state v3`). Each mod reads the other's file only in the version it knows, and otherwise leaves it alone and says so once in the log. Both repositories keep identical samples in `tests/contract`, which the build workflow compares with Keepsake's on every push and the tests check against. The same list is in Keepsake's technical notes.

Versions before 0.2.1 kept those two in `BepInEx/config/Bindrune/`. They are moved into `bindrune.keys` on the first launch after the update and the old files are removed. A copy a sync puts back afterwards is ignored, and the log says so.

## Profile updates

| Update | Your own keys |
|---|---|
| Gale profile sync, or a Gale import into the same profile | Yes |
| Installing or updating a modpack, in any mod manager | Yes |
| Thunderstore Mod Manager or r2modman, Update existing profile | Yes, with a spare copy |
| Importing as a new profile, in any mod manager | Carried over by hand |

The same goes for moved hotbar keys, muted clashes and pinned hints, which share `bindrune.keys`. The file lives in the profile outside `config`, so syncs and modpacks, which write config files, leave it alone, and Bindrune puts your keys back from it every time the game runs, whatever the config files hold. Update existing profile in Thunderstore Mod Manager and r2modman builds the imported profile in a folder of its own, deletes the whole existing profile folder and moves the new one in its place under the same name, so the file is gone with it.

For profiles of those two managers, told by the `mods.yml` both keep in every profile folder, in a folder named `profiles`, Bindrune can keep a spare copy of `bindrune.keys` outside the profile. It goes in the manager's folder for the game, beside its `cache` and `exports` folders, as `Bindrune/<profile name>`; not inside `profiles`, where the managers list every folder as a profile. Any other profile, Gale's included, gets none, since Gale never replaces a profile folder. Nothing is written there until you agree: once such a profile holds something of yours, the panel asks in a box of its own over the panel, No thanks asks once more before it counts, and the Spare copy button at the top, shown only for such profiles, changes the answer later. The answer is kept in `BepInEx/bindrune.spare` rather than in the cfg file a sync hands out and an export carries, and it is part of the spare copy. The copy follows every save of `bindrune.keys` and is brought up to date as the plugin starts, only from a profile that has the file. When the plugin starts in a profile without `bindrune.keys` and a spare copy under the profile's name is there, the copy comes back before anything reads the file, and a line in the corner says so once your character appears. Turning it off removes the spare copy; a profile deleted in the manager leaves its spare copy behind, to be deleted by hand.

A profile imported as new starts without it. Copying `bindrune.keys` from the old profile's `BepInEx` folder into the new one's carries everything over, since nothing in it depends on where the profile is.

## Files

Settings are in `BepInEx/config/isimp.Bindrune.cfg`. Situations you set and the shared list are in `BepInEx/config/Bindrune/`, so they travel with a profile. Your own keys, moved hotbar keys, muted clashes and pinned hints are in `BepInEx/bindrune.keys`, so they do not; whether to keep a spare copy of it is in `BepInEx/bindrune.spare`. A file of Bindrune's that cannot be read, such as one held open by another program, is never written over: a change made meanwhile is not saved, and the file is read again at the next look. Data derived from the game is cached in `BepInEx/cache/Bindrune/` and rebuilt after a game update. All of these are plain text and safe to edit or delete, also while the game runs: an edited file is read again when the panel opens, and before Bindrune next writes to it.

## How it reads the game

Bindrune reads the game's assemblies with Mono.Cecil, finds the code that reads each bind and derives where it is used. For example, `TabRight` is only read during build placement, so it cannot collide with `Use` elsewhere even though both are on E. The result is cached against the assemblies' file stamps and rebuilt after a game update. Mono.Cecil ships with BepInEx.

Jotunn is declared as a soft dependency, so Bindrune loads without it, but the panel and the hints are built with Jotunn's GUI and nothing is shown without it.

Everywhere else Bindrune only reads the game, and it changes it in four places. It holds the start menu's keyboard handling back while you type in the panel, puts the hotbar keys it keeps back after the game loads its controls, hands an alternate hotbar key back to the game before the game's own screen rebinds it, and relabels the hotbar while any slot's key has been moved. Each finds what it changes when the game starts, and a game update that has moved it switches that one off with a warning in the log rather than stopping Bindrune.

## Hotbar keys

The game fixes the hotbar keys to the digits 1 to 8. Its controls screen does not offer them, and it drops any change to them each time it loads its controls, which it does at startup and when you leave that screen. Bindrune can move or clear them anyway. The key you set is kept in `bindrune.keys` and put back after every load, and after the game's own reset of its controls, since that screen does not list the hotbar keys. Default on the bind gives the digit back. Nothing is written to the game's own settings, so without Bindrune the digits are the game's again.

Each slot also answers to its alternate key, the one the controls screen does offer and keeps. Either key can carry one modifier, such as Alt + 1, which the game's own format has no room for: Bindrune builds the combination itself and keeps it in `bindrune.keys` too. An alternate key without a modifier stays the game's to keep, and rebinding an alternate key in the game's controls screen hands it back to the game. A key with a modifier fires while that modifier is held, whatever else is held with it.

A moved slot is labelled on the bar with its new key, and a cleared one with its alternate key, or nothing when neither has a key. While any slot is moved, the whole bar's labels take the layout ExtraSlots gives its bars, so the two read alike side by side. Bars that other mods add are left alone. The prompts that name the hotbar keys, such as cooking or attaching an item to a stand, list the keys when they come to one or two runs, such as Alt + 1-8, and otherwise name the hotbar in the game's own word for it.

A mod that reads the digit keys itself is not affected by moving them. A mod that holds the game's keys back while one of its own is pressed wins a combo the two share; the clash is reported either way.

## Key names

Keys are shown the way the keyboard in use labels them, using the same names as the game's own controls settings. Config files and mods store keys by their position on a US keyboard instead, so on a German keyboard the key to the left of X is shown as Y but stored as Z, and the key to the right of L is shown as Ö but stored as Semicolon. Only keys that print a single character are renamed. Modifiers, function keys, arrows, mouse buttons and the numpad keep their usual names.

Searching by key uses the shown names, and a single typed character is read as a key. Show stored key names, under Advanced on the ? page, adds the stored name to every bind where the two differ. The names only affect what is displayed; how a key is stored, compared and written is the same either way. Setting `KeyboardLayoutLabels` under `[Display]` to false shows the stored names everywhere.

## Limits

A mod that writes its key directly into its code instead of a setting has nothing to read and does not appear. Keybind settings stored as free text are shown but cannot be changed from Bindrune. Gamepad buttons are listed while a controller is connected but are not editable and are left out of clashes. A bind with no description is compared with everything.

## Tests

`tests/Bindrune.Tests` covers what needs no game: the keys section of `bindrune.keys` and taking keybinds over from Keepsake, checked against the samples in `tests/contract`; `bindrune.keys` and the situations file kept across games, taken over from older versions, picked up when edited by hand and never written over when unreadable; muted clashes, which show again once they get worse and are forgotten once gone; hints; key combos, bind ids and the key grid behind suggestions; and the spare copy through Update existing profile, in a profile laid out like Thunderstore Mod Manager's. Tests state what has to hold and are written before the code that makes it hold. Your own key and the profile's on a mod's setting are tested through the same writes the panel makes: setting your key, Whole profile and Mine, and putting your key back after a sync. Putting keys into the game's own binds, finding binds, and the panel need the game and are checked there. The tests run on .NET 8 against the real `BepInEx.dll` of a local profile, which is not part of the repository, so they run locally rather than on the build server:

```
dotnet test tests/Bindrune.Tests
```

`.githooks/pre-push` runs them before every push and stops the push when one fails; without a local `BepInEx.dll` it lets the push through with a warning. Git uses it once told to, per clone: `git config core.hooksPath .githooks`.

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
