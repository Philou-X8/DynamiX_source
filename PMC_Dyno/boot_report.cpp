#include "boot_report.h"
#include <esp_system.h>
#include "command_flags.h"
#include "gui_link.h"

namespace {

const char* resetReasonName(esp_reset_reason_t reason) {
    switch (reason) {
        case ESP_RST_POWERON:   return "POWERON";
        case ESP_RST_EXT:       return "EXT_PIN";
        case ESP_RST_SW:        return "SW_RESET";
        case ESP_RST_PANIC:     return "PANIC";      // crash / exception
        case ESP_RST_INT_WDT:   return "INT_WDT";    // interrupt watchdog
        case ESP_RST_TASK_WDT:  return "TASK_WDT";   // task watchdog (a task starved the others)
        case ESP_RST_WDT:       return "OTHER_WDT";
        case ESP_RST_DEEPSLEEP: return "DEEPSLEEP";
        case ESP_RST_BROWNOUT:  return "BROWNOUT";   // supply dipped
        case ESP_RST_SDIO:      return "SDIO";
        default:                return "UNKNOWN";
    }
}

} // namespace

void reportBootReason(GuiLink& gui) {
    gui.sendf(Cmd::ESP_message, "BOOT: %s", resetReasonName(esp_reset_reason()));
}
