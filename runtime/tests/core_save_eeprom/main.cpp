// Save system on Eeprom8K. The ID string tells emulators which save chip the cartridge has.
#define AGB_SAVE_TYPE Eeprom8K
__attribute__((used, aligned(4))) const char kSaveTypeId[] = "EEPROM_V124";
#define AGB_SAVE_TEST_KEEP_ID() asm volatile("" ::"r"(kSaveTypeId))
#include "../save_common.hpp"
