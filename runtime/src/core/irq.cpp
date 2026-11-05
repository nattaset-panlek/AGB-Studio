// Interrupt handlers and turning interrupts on (P24.03); the dispatcher that calls them is in irq.iwram.cpp.
#include "agb/hw.hpp"

extern "C" {
IrqHandler agb_irq_table[II_MAX];
void agb_irq_master();
}

// Where each interrupt has to be asked for besides REG_IE: DISPSTAT for the display ones, the timer's control.
void irq_add(IrqIndex index, IrqHandler handler) {
    const u16 ime = REG_IME;
    REG_IME = 0;
    agb_irq_table[index] = handler;
    if (index == II_VBLANK) REG_DISPSTAT |= 0x0008;
    else if (index == II_HBLANK) REG_DISPSTAT |= 0x0010;
    else if (index == II_VCOUNT) REG_DISPSTAT |= 0x0020;
    else if (index >= II_TIMER0 && index <= II_TIMER3) *(vu16*)(REG_BASE + 0x0102 + 4 * (index - II_TIMER0)) |= TM_IRQ;
    REG_IE |= 1 << index;
    REG_IME = ime;
}

void irq_init(IrqHandler isr) {
    (void)isr;
    REG_IME = 0;
    for (auto& h : agb_irq_table) h = nullptr;
    REG_IE = 0;
    REG_IF = 0xFFFF;
    *(IrqHandler volatile*)0x03007FFC = agb_irq_master;
    REG_IME = 1;
}
