#pragma once

class GuiLink;

// Sends "BOOT: <reason>" to the GUI log. Opening the serial port from the GUI
// resets the ESP (DTR/RTS), so EXT_PIN / POWERON at connect time is normal.
// TASK_WDT, INT_WDT, PANIC or BROWNOUT in the middle of a run says what
// restarted it (the reason can only be read after the restart).
void reportBootReason(GuiLink& gui);
