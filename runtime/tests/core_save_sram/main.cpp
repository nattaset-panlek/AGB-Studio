// Save system on Sram32K. The ID string tells emulators which save chip the cartridge has.
#define AGB_SAVE_TYPE Sram32K
__attribute__((used, aligned(4))) const char kSaveTypeId[] = "SRAM_V113";
#define AGB_SAVE_TEST_KEEP_ID() asm volatile("" ::"r"(kSaveTypeId))
#include "../save_common.hpp"
