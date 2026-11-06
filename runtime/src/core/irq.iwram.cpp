// Interrupt dispatcher (P24.03), in place of libtonc's isr_master. ARM code in IWRAM (*.iwram.cpp).
//
// The BIOS takes an interrupt in IRQ mode, saves r0-r3, r12 and lr on the IRQ stack and calls the address stored at
// 0x03007FFC; when that returns (bx lr), it restores them and returns to the interrupted code. The IRQ stack is small
// (it grows down from 0x03007FA0 to the System stack's top at 0x03007F00), so handlers run on the System mode stack,
// with interrupts still off: a handler is never interrupted by another one.
#include "agb/hw.hpp"

extern "C" void agb_irq_master();
// The handlers, one per interrupt (irq.cpp keeps them: only this function needs to be in IWRAM).
extern "C" IrqHandler agb_irq_table[II_MAX];

// Acknowledges every pending, enabled interrupt (REG_IF and the BIOS copy at 0x03007FF8, which VBlankIntrWait reads),
// then calls their handlers lowest bit first.
extern "C" __attribute__((naked, target("arm"))) void agb_irq_master() {
    asm volatile(
        "mov    r3, #0x04000000         \n"
        "add    r3, r3, #0x200          \n"  // r3 = &REG_IE
        "ldr    r2, [r3]                \n"  // IE | IF << 16
        "and    r1, r2, r2, lsr #16     \n"  // pending and enabled
        "strh   r1, [r3, #2]            \n"  // REG_IF: writing 1 clears
        "ldr    r3, =0x03007FF8         \n"
        "ldrh   r2, [r3]                \n"
        "orr    r2, r2, r1              \n"
        "strh   r2, [r3]                \n"  // BIOS IF for IntrWait / VBlankIntrWait
        "mrs    r2, spsr                \n"
        "stmfd  sp!, {r2, lr}           \n"  // IRQ mode: SPSR and the return into the BIOS
        "mov    r0, #0x9F               \n"  // System mode, IRQ off
        "msr    cpsr_c, r0              \n"
        "stmfd  sp!, {r4, r5, lr}       \n"
        "mov    r4, r1                  \n"  // bits still to serve
        "ldr    r5, =agb_irq_table      \n"
        "1:                             \n"
        "movs   r4, r4, lsr #1          \n"  // next bit into carry
        "bcc    2f                      \n"
        "ldr    r3, [r5]                \n"
        "cmp    r3, #0                  \n"
        "movne  lr, pc                  \n"
        "bxne   r3                      \n"  // handlers are Thumb or ARM: bx picks the state
        "2:                             \n"
        "add    r5, r5, #4              \n"
        "cmp    r4, #0                  \n"
        "bne    1b                      \n"
        "ldmfd  sp!, {r4, r5, lr}       \n"
        "mov    r0, #0x92               \n"  // back to IRQ mode, IRQ off
        "msr    cpsr_c, r0              \n"
        "ldmfd  sp!, {r2, lr}           \n"
        "msr    spsr_cf, r2             \n"
        "bx     lr                      \n"
        ".pool                          \n");
}
