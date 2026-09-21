# Changelog

## 0.3.1

Five keys the game reads in its own code, such as F11 for screenshots and Ctrl+F1 to free the mouse, are now listed with its binds and reported when a mod uses them too.

Editing `situations.txt` or `bindrune.keys` by hand while the game runs is no longer undone by the next change in the panel, and opening the panel picks the edit up.

Putting your keys back after a profile sync now scans at most twice a session, at the main menu and once your character is in the world, instead of every 30 seconds for up to four minutes. Keys whose mod only binds them in the world are now restored however long the main menu stays open.

Key suggestions in the rebind preview are much cheaper to work out, and the on-screen hints no longer allocate anything while nothing on screen changes.

The panel now plays the game's own sounds when it opens and closes, when a key you press is set straight away, and when the hints key toggles the hints.

## 0.3.0

When a key you press for a bind clashes, Bindrune now suggests up to three nearby that would not, adding a modifier where the bind can take one. It prefers the modifiers your setup already uses, and never puts one on a key the game reads regardless of modifiers. A key shared with a bind that is never live at the same time is offered too, marked as shared.

A key nothing else uses is now bound as soon as you press it.

Keys the game reads on its own, such as F2 for the network panel or F11 for screenshots, are no longer shown as free.

The keys that open the map, inventory, chat and build menu now count as live in the world, where you press them to open those screens. A world bind on the same key used to be reported as harmless.

## 0.2.1

Muted clashes and pinned hints now live in `BepInEx/bindrune.keys` with your own keys, where a profile sync cannot replace them with the profile owner's. Existing ones are moved there on the first launch.

A mute now covers the clash you looked at rather than the pair for good. It is reported again if it turns into something worse, and dropped once it is gone.

A note about two binds sharing a key now names both of them.

Cancel now cancels a rebind instead of binding the left mouse button, and the Press key search does the same when clicked again. Left click is still bindable anywhere else.

## 0.2.0

Every clash a bind has now carries a Go to button that switches to the other bind in the pair, and scrolls its row into view.

## 0.1.0

First release.
