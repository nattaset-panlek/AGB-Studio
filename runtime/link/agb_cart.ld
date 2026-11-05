/* AGB Studio linker script for a cartridge ROM (P24.02), written from the GBA's documented memory map:
 *   ROM    0x08000000, up to 32 MB: code, constants, and the first copy of everything that lives in RAM
 *   IWRAM  0x03000000, 32 KB: fast code and variables; the top 256 bytes hold the stacks of IRQ and Supervisor mode and
 *          the BIOS's interrupt handler address, and the System mode stack grows down from 0x03007F00
 *   EWRAM  0x02000000, 256 KB: big variables (AGB_EWRAM_BSS, the screen arena)
 * Section and symbol names are the ones the runtime, LinkerMap and RamBudget read. */

OUTPUT_FORMAT("elf32-littlearm")
OUTPUT_ARCH(arm)
ENTRY(_start)

MEMORY
{
    rom   (rx)  : ORIGIN = 0x08000000, LENGTH = 32M
    iwram (rwx) : ORIGIN = 0x03000000, LENGTH = 0x7F00
    ewram (rwx) : ORIGIN = 0x02000000, LENGTH = 256K
}

__sp_usr = 0x03007F00;
__sp_irq = 0x03007FA0;

SECTIONS
{
    .crt0 : { KEEP(*(.crt0)) } > rom

    .init : { KEEP(*(SORT_NONE(.init))) } > rom

    /* Code of files named *.iwram.cpp goes to IWRAM below, the rest stays in ROM. */
    .text :
    {
        *(EXCLUDE_FILE(*.iwram.o) .text EXCLUDE_FILE(*.iwram.o) .text.*)
        *(.stub .gnu.linkonce.t.*)
        *(.glue_7t) *(.glue_7) *(.vfp11_veneer) *(.v4_bx)
    } > rom

    .fini : { KEEP(*(SORT_NONE(.fini))) } > rom

    .rodata : ALIGN(4) { *(.rodata .rodata.* .gnu.linkonce.r.*) } > rom

    .ARM.extab : { *(.ARM.extab* .gnu.linkonce.armextab.*) } > rom
    .ARM.exidx :
    {
        __exidx_start = .;
        *(.ARM.exidx* .gnu.linkonce.armexidx.*)
        __exidx_end = .;
    } > rom

    .preinit_array : ALIGN(4)
    {
        PROVIDE_HIDDEN(__preinit_array_start = .);
        KEEP(*(.preinit_array))
        PROVIDE_HIDDEN(__preinit_array_end = .);
    } > rom
    .init_array : ALIGN(4)
    {
        PROVIDE_HIDDEN(__init_array_start = .);
        KEEP(*(SORT_BY_INIT_PRIORITY(.init_array.*) SORT_BY_INIT_PRIORITY(.ctors.*)))
        KEEP(*(.init_array .ctors))
        PROVIDE_HIDDEN(__init_array_end = .);
    } > rom
    .fini_array : ALIGN(4)
    {
        PROVIDE_HIDDEN(__fini_array_start = .);
        KEEP(*(SORT_BY_INIT_PRIORITY(.fini_array.*) SORT_BY_INIT_PRIORITY(.dtors.*)))
        KEEP(*(.fini_array .dtors))
        PROVIDE_HIDDEN(__fini_array_end = .);
    } > rom
    .eh_frame : { KEEP(*(.eh_frame)) } > rom

    /* IWRAM: copied from the ROM at boot (agb_crt0.s). */
    .iwram : ALIGN(4)
    {
        __iwram_start__ = .;
        *(.iwram .iwram.*)
        *.iwram.o(.text .text.*)
        . = ALIGN(4);
        __iwram_end__ = .;
    } > iwram AT > rom
    __iwram_lma = LOADADDR(.iwram);

    .data : ALIGN(4)
    {
        __data_start__ = .;
        *(.data .data.* .gnu.linkonce.d.*)
        . = ALIGN(4);
        __data_end__ = .;
    } > iwram AT > rom
    __data_lma = LOADADDR(.data);

    .bss (NOLOAD) : ALIGN(4)
    {
        __bss_start__ = .;
        *(.bss .bss.* .gnu.linkonce.b.*)
        *(COMMON)
        . = ALIGN(4);
        __bss_end__ = .;
    } > iwram AT > iwram   /* no copy in the ROM: the start code clears it */
    __iheap_start = .;

    /* EWRAM: copied from the ROM at boot, then the variables that start at zero. */
    .ewram : ALIGN(4)
    {
        __ewram_start = .;
        *(.ewram .ewram.*)
        . = ALIGN(4);
        __ewram_end = .;
    } > ewram AT > rom
    __ewram_lma = LOADADDR(.ewram);
    __rom_end__ = LOADADDR(.ewram) + SIZEOF(.ewram);

    .sbss (NOLOAD) : ALIGN(4)
    {
        __sbss_start__ = .;
        *(.sbss .sbss.*)
        . = ALIGN(4);
        __sbss_end__ = .;
    } > ewram AT > ewram
    __eheap_start = .;
    PROVIDE(end = .);
    PROVIDE(__end__ = .);

    /DISCARD/ : { *(.note.GNU-stack) *(.gnu_debuglink) }
}
