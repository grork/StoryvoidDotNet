Application Tests
=================

Created by following the guide [here](https://devblogs.microsoft.com/ifdef-windows/winui-desktop-unit-tests/).

Debugging
=========
For reasons that are unclear at the time of authoring, opening this project and
trying to debug a test will result in breakpoints not being hit despite the
debugger attaching (at least visually, and from the perspective of the Attach
to process dialog).

However, if you set it to be the "Startup Project" (Right click project,
"Set as Startup Project"), and **debug with F5 once**, you'll be able to use the
standard test debugging gestures (keyboard chords, context menus etc.)