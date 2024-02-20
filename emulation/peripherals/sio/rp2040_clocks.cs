/*
 *   Copyright (c) 2024
 *   All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using PacketDotNet.Utils;
using Xwt;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{

public class RP2040Clocks : BasicDoubleWordPeripheral, IKnownSize
{
    public long Size { get { return 0x1000; }}
    private enum Registers
    {
        CLK_REF_CTRL = 0x30,
        CLK_REF_DIV = 0x34,
        CLK_REF_SELECTED = 0x38,
        CLK_SYS_CTRL = 0x3c,
        CLK_SYS_SELECTED = 0x44,
        CLK_SYS_RESUS_CTRL = 0x78,
    }

    private enum SysClockAuxSource
    {
        clksrc_pll_sys = 0,
        clksrc_pll_usb = 1,
        rosc_clksrc = 2,
        xosc_clksrc = 3,
        clksrc_gpin0 = 4,
        clksrc_gpin1 = 5,
    }

    private enum SysClockSource
    {
        clk_ref = 0,
        clksrc_clk_sys_aux = 1
    }
    private enum RefClockSource
    {
        rosc_clksrc_ph = 0,
        clksrc_clk_ref_aux = 1,
        xosc_clksrc = 2
    }

    private enum RefClockAuxSource
    {
        clksrc_pll_usb = 0,
        clksrc_gpin0 = 1,
        clksrc_gpin1 = 2
    }

    private RefClockSource refClockSource;
    private RefClockAuxSource refClockAuxSource;
    private SysClockAuxSource sysClockAuxSource;
    private SysClockSource sysClockSource;
    private long timeout;
    private bool resusEnable;
    public RP2040Clocks(Machine machine) : base(machine)
    {
        refClockSource = RefClockSource.rosc_clksrc_ph;
        refClockAuxSource = RefClockAuxSource.clksrc_pll_usb;
        sysClockAuxSource = SysClockAuxSource.clksrc_pll_sys;
        sysClockSource = SysClockSource.clk_ref;

        DefineRegisters();
    }

    private void DefineRegisters()
    {
        Registers.CLK_REF_CTRL.Define(this)
            .WithValueField(0, 2, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    refClockSource = (RefClockSource)value;
                },
                valueProviderCallback: _ =>
                {
                    return (ulong)refClockSource;
                },
                name: "CLK_REF_CTRL")
            .WithValueField(5, 2, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    refClockAuxSource = (RefClockAuxSource)value;
                },
                valueProviderCallback: _ =>
                {
                    return (ulong)refClockAuxSource;
                },
                name: "CLK_AUX_CTRL");
        Registers.CLK_REF_SELECTED.Define(this)
            .WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ =>
                {
                    return (ulong)(1 << (int)refClockSource);
                },
                name: "CLK_REF_SELECTED");
        Registers.CLK_SYS_RESUS_CTRL.Define(this)
            .WithValueField(0, 8, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    timeout = (long)value;
                },
                valueProviderCallback: _ =>
                {
                    return (ulong)timeout;
                },
                name: "CLK_SYS_RESUS_CTRL_TIMEOUT")
            .WithFlag(8, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    resusEnable = value;
                },
                valueProviderCallback: _ =>
                {
                    return resusEnable;
                }, name: "CLK_SYS_RESUS_CTRL_ENABLE")
            .WithFlag(12, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                },
                valueProviderCallback: _ =>
                {
                    return false;
                }, name: "CLK_SYS_RESUS_CTRL_FORCE")
            .WithFlag(16, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                },
                valueProviderCallback: _ =>
                {
                    return false;
                },
                name: "CLK_SYS_RESUS_CTRL_CLEAR");
        Registers.CLK_SYS_CTRL.Define(this)
            .WithValueField(0, 1, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    sysClockSource = (SysClockSource)value;
                },
                valueProviderCallback: _ =>
                {
                    return (ulong)sysClockSource;
                },
                name: "CLK_SYS_CTRL_SRC")
            .WithValueField(5, 3, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) =>
                {
                    sysClockAuxSource = (SysClockAuxSource)value;
                },
                valueProviderCallback: _ =>
                {
                    return (ulong)sysClockAuxSource;
                },
                name: "CLK_SYS_CTRL_AUXSRC");
        Registers.CLK_SYS_SELECTED.Define(this)
            .WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ =>
                {
                    return (ulong)(1 << (int)sysClockSource);
                },
                name: "CLK_SYS_SELECTED");
    }
}

}