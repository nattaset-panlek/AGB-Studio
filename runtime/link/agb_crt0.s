@ AGB Studio start code (P24.02): the cartridge header and what runs before main().
@
@ Written from the GBA's documented boot: the BIOS checks the header, then jumps to 0x08000000 in ARM state, with
@ interrupts off. This file sets the stacks of IRQ and System mode, clears the zeroed sections, copies the sections that
@ live in RAM from the ROM, runs the C++ constructors and calls main(). The addresses come from agb_cart.ld.

    .section .crt0, "ax", %progbits
    .arm
    .align 2
    .global _start
_start:
    b       agb_start

    @ 0x04 Nintendo logo (156 bytes), 0xA0 title (12), 0xAC game code (4), 0xB0 maker code (2): the build writes them
    @ (gbafix), so they start as zeros.
    .fill   156 + 12 + 4 + 2, 1, 0
    .byte   0x96            @ 0xB2 fixed value
    .byte   0x00            @ 0xB3 main unit code
    .byte   0x00            @ 0xB4 device type
    .fill   7, 1, 0         @ 0xB5 reserved
    .byte   0x00            @ 0xBC software version (the build writes it)
    .byte   0x00            @ 0xBD header checksum (the build writes it)
    .fill   2, 1, 0         @ 0xBE reserved

agb_start:                  @ 0xC0
    mov     r0, #0x04000000
    str     r0, [r0, #0x208]        @ REG_IME = 0: no interrupt until the runtime installs its handler

    mov     r0, #0x12               @ IRQ mode
    msr     cpsr_c, r0
    ldr     sp, =__sp_irq
    mov     r0, #0x1F               @ System mode, where the game runs
    msr     cpsr_c, r0
    ldr     sp, =__sp_usr

    ldr     r0, =__bss_start__      @ IWRAM variables that start at zero
    ldr     r1, =__bss_end__
    bl      agb_zero
    ldr     r0, =__sbss_start__     @ EWRAM variables that start at zero
    ldr     r1, =__sbss_end__
    bl      agb_zero

    ldr     r0, =__data_lma         @ IWRAM variables with a value
    ldr     r1, =__data_start__
    ldr     r2, =__data_end__
    bl      agb_copy
    ldr     r0, =__iwram_lma        @ code and data placed in IWRAM (*.iwram.cpp, AGB_IWRAM_CODE, the mixer)
    ldr     r1, =__iwram_start__
    ldr     r2, =__iwram_end__
    bl      agb_copy
    ldr     r0, =__ewram_lma        @ data placed in EWRAM
    ldr     r1, =__ewram_start
    ldr     r2, =__ewram_end
    bl      agb_copy

    ldr     r3, =__libc_init_array  @ C++ constructors of globals
    mov     lr, pc
    bx      r3
    mov     r0, #0
    mov     r1, #0
    ldr     r3, =main
    mov     lr, pc
    bx      r3
    swi     0x000000                @ main returned: SoftReset

@ r0 = start, r1 = end (word aligned): fills with zero.
agb_zero:
    mov     r2, #0
1:  cmp     r0, r1
    strlo   r2, [r0], #4
    blo     1b
    bx      lr

@ r0 = source, r1 = start, r2 = end (word aligned): copies words.
agb_copy:
    cmp     r0, r1
    bxeq    lr                      @ already in place
1:  cmp     r1, r2
    ldrlo   r3, [r0], #4
    strlo   r3, [r1], #4
    blo     1b
    bx      lr

    .pool
