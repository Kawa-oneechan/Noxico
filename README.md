# Noxico
*Erotic Roguelike Misadventures*

----

## Building from source
This was made in and for Visual Studio 2010. The free Express edition is fine. If you have it installed, building is as simple as running `buildall.bat` if you don't want to bother with the VS IDE. First time you do it either way, it *may* fail because of the data files. In that case, just build it again. You should get debug and release builds, each in both 32 and 64 bits, in the `bin` folder, along with 7-Zip archives of the two release builds.

If you'd rather use a more recent version of Visual Studio, that's perfectly fine too. It *should* by all means compile just fine from the IDE, and `buildall.bat` has just the one line to adapt.

## Controls
Keyboard controls can be changed by editing `Noxico.ini`, or from the settings screen.

### Always
* (Debug only) <kbd>F3</kbd> dumps the current board to HTML for investigation.
* <kbd>F12</kbd> takes a screenshot.
* <kbd>Ctrl</kbd>-<kbd>R</kbd> forces a full redraw, just in case.
### In gameplay
* Arrow keys move the player character. Bumping into a friendly or neutral character displaces them. Bumping into a hostile attacks them.
* <kbd>Shift</kbd>-arrows with a long-range weapon equipped fires in that direction.
* Period (<kbd>.</kbd>) makes the PC rest for a turn.
* <kbd>Enter</kbd> activates things underneath the PC — there'll be a little popup in the corner if this is something you can do at the time. Activation could involve picking up dropped loot, looking into a container, taking a nap in a bed, or using an exit.
* Quotes (<kbd>'</kbd>) opens the inventory screen.
* Slash (<kbd>/</kbd>) switches to *Interact* mode.
* Comma (<kbd>,</kbd>) causes the PC to take off in flight or land again, if they have wings.
* Semicolon (<kbd>;</kbd>) opens the travel screen.
### In *Interact* mode
* Arrow keys move the cursor — that's the flashing green rectangle.
* <kbd>Tab</kbd> cycles through targets.
* <kbd>Enter</kbd> accepts this choice of target and pops up a menu of things you can do.
* <kbd>Esc</kbd> breaks out back to regular gameplay.
### In a subscreen or popup window
* <kbd>Tab</kbd> cycles through controls.
* <kbd>Enter</kbd> activates the active control. For a yes/no message box, this equals a yes.
* <kbd>Esc</kbd> breaks out to whatever you came from, if allowed. For a yes/no message box, this is a no.
* Arrow keys scroll through list items.

### Mouse controls
* Left-click with a message box or action list on screen maps directly to the <kbd>Enter</kbd> key, technically the *Accept* action.
* Left-click on a subscreen control does exactly what you'd think.
* Right-click, likewise, maps to <kbd>Escape</kbd>.
* In regular gameplay, left-clicking on a tile makes the PC navigate there.
* Middle-clicking near an edge makes the PC navigate to the edge and then cross.
* Right-clicking is considered equal to switching to *Interact* mode, aiming at that point, and pressing <kbd>Enter</kbd>. That is in fact exactly what happens.
* Turning the scroll wheel in a subscreen or popup actually scrolls through the active list control.

Given an XInput-compatible controller, the game'll automatically adjust the on-screen key indications to match. Controller maps can't be edited.
