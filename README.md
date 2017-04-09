# Noxico
*Erotic Roguelike Misadventures*

----
## Building from source
* Shouldn't be anything difficult to it. First time around, build might fail because of missing .NOX files. Just try again.

## Controls
Keyboard controls can be changed by editing Noxico.ini.

## Always
* (Debug only) F3 dumps the current board to HTML for investigation.
* F12 takes a screenshot.
* Ctrl-R forces a full redraw, just in case.
### In gameplay
* Arrow keys move the player character. Bumping into a friendly or neutral character displaces them. Bumping into a hostile attacks them.
* Shift-arrows with a long-range weapon equipped fires in that direction.
* Period (.) makes the PC rest for a turn.
* Enter activates things underneath the PC -- there'll be a little popup in the corner if this is something you can do at the time. Activation could involve picking up dropped loot, looking into a container, taking a nap in a bed, or using an exit.
* Quotes ('") opens the inventory screen.
* Slash (/?) switches to Interact mode.
* Comma (,<) causes the PC to take off in flight or land again, if they have wings.
* Semicolon (;:) opens the travel screen.
## In Interact mode
* Arrow keys move the cursor -- that's the flashing green rectangle.
* Tab cycles through targets.
* Enter accepts this choice of target and pops up a menu of things you can do.
* Escape breaks out back to regular gameplay.
## In a subscreen or popup window
* Tab cycles through controls.
* Enter activates the active control. For a yes/no message box, this equals a yes.
* Escape breaks out to whatever you came from, if allowed. For a yes/no message box, this is a no.
* Arrow keys scroll through list items.

## Mouse controls
* Left-click with a message box or action list on screen maps directly to the Enter key -- technically the Accept action.
* Left-click on a subscreen control does exactly what you'd think.
* Right-click, likewise, maps to Escape.
* In regular gameplay, left-clicking on a tile makes the PC navigate there.
* Middle-clicking near an edge makes the PC navigate to the edge and then cross.
* Right-clicking is considered equal to switching to Interact mode, aiming at that point, and pressing Enter. That is in fact exactly what happens.
* Turning the scroll wheel in a subscreen or popup actually scrolls through the active list control.

Given an XInput-compatible controller, the game'll automatically adjust the on-screen key indications to match. Controller maps can't be edited.
