using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

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
        ClkSrcPllSys = 0,
        ClkSrcPllUsb = 1,
        ROSCClkSrc = 2,
        XOSCClkSrc = 3,
        ClkSrcGPin0 = 4,
        ClkSrcGPin1 = 5,
    }

    private enum SysClockSource
    {
        ClkRef = 0,
        ClkSrcClkSysAux = 1
    }
    private enum RefClockSource
    {
        ROSCClkSrcPh = 0,
        ClkSrcClkRefAux = 1,
        XOSCClkSrc = 2
    }

    private enum RefClockAuxSource
    {
        ClkSrcPllUsb = 0,
        ClkSrcGPin0 = 1,
        ClkSrcGPin1 = 2
    }

    private RefClockSource refClockSource;
    private RefClockAuxSource refClockAuxSource;
    private SysClockAuxSource sysClockAuxSource;
    private SysClockSource sysClockSource;
    private long timeout;
    private bool resusEnable;
    public RP2040Clocks(Machine machine) : base(machine)
    {
        refClockSource = RefClockSource.ROSCClkSrcPh;
        refClockAuxSource = RefClockAuxSource.ClkSrcPllUsb;
        sysClockAuxSource = SysClockAuxSource.ClkSrcPllSys;
        sysClockSource = SysClockSource.ClkRef;

        DefineRegisters();
    }

    private void DefineRegisters()
    {
        Registers.CLK_REF_CTRL.Define(this)
            .WithValueField(0, 2, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) => refClockSource = (RefClockSource)value,
                valueProviderCallback: _ => (ulong)refClockSource,
                name: "CLK_REF_CTRL")
            .WithReservedBits(2, 3)
            .WithValueField(5, 2, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) => refClockAuxSource = (RefClockAuxSource)value,
                valueProviderCallback: _ => (ulong)refClockAuxSource,
                name: "CLK_AUX_CTRL")
            .WithReservedBits(7, 25);

        Registers.CLK_REF_SELECTED.Define(this)
            .WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (ulong)(1 << (int)refClockSource),
                name: "CLK_REF_SELECTED");

        Registers.CLK_SYS_RESUS_CTRL.Define(this)
            .WithValueField(0, 8, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) => timeout = (long)value,
                valueProviderCallback: _ => (ulong)timeout,
                name: "CLK_SYS_RESUS_CTRL_TIMEOUT")
            .WithFlag(8, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) => resusEnable = value,
                valueProviderCallback: _ => resusEnable,
                name: "CLK_SYS_RESUS_CTRL_ENABLE")
            .WithReservedBits(9, 3)
            .WithFlag(12, FieldMode.Write | FieldMode.Read,
                valueProviderCallback: _ => false,
                name: "CLK_SYS_RESUS_CTRL_FORCE")
            .WithReservedBits(13, 3) 
            .WithFlag(16, FieldMode.Write | FieldMode.Read,
                valueProviderCallback: _ => false,
                name: "CLK_SYS_RESUS_CTRL_CLEAR")
            .WithReservedBits(17, 15);

        Registers.CLK_SYS_CTRL.Define(this)
            .WithValueField(0, 1, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) => sysClockSource = (SysClockSource)value,
                valueProviderCallback: _ => (ulong)sysClockSource,
                name: "CLK_SYS_CTRL_SRC")
            .WithReservedBits(1, 4)
            .WithValueField(5, 3, FieldMode.Write | FieldMode.Read,
                writeCallback: (_, value) => sysClockAuxSource = (SysClockAuxSource)value,
                valueProviderCallback: _ => (ulong)sysClockAuxSource,
                name: "CLK_SYS_CTRL_AUXSRC")
            .WithReservedBits(8, 24);

        Registers.CLK_SYS_SELECTED.Define(this)
            .WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (ulong)(1 << (int)sysClockSource),
                name: "CLK_SYS_SELECTED");
    }
}

}