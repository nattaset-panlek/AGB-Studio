// Save system on Flash64K. The ID string tells emulators which save chip the cartridge has.
#define AGB_SAVE_TYPE Flash64K
__attribute__((used, aligned(4))) const char kSaveTypeId[] = "FLASH_V126";
#define AGB_SAVE_TEST_KEEP_ID() asm volatile("" ::"r"(kSaveTypeId))
#include "../save_common.hpp"
