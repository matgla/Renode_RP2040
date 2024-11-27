using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PSM : RP2040PeripheralBase, IKnownSize 
    {
        public PSM(Machine machine, ulong address) : base(machine, address)
        {
            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
        }

        private void DefineRegisters()
        {
            Registers.FRCE_ON.Define(this)
                .WithValueField(0, 32, out frceOn, name: "FORCE_ON");

            Registers.FRCE_OFF.Define(this)
                .WithValueField(0, 32, out frceOff, name: "FORCE_OFF");

            Registers.WDSEL.Define(this)
                .WithValueField(0, 32, out wdsel, name: "WDSEL");

            Registers.DONE.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => frceOn.Value & ~frceOff.Value, name: "DONE");
        } 

        private enum Registers 
        {
            FRCE_ON = 0x0,
            FRCE_OFF = 0x4,
            WDSEL = 0x8,
            DONE = 0xc
        };

        IValueRegisterField frceOn;
        IValueRegisterField frceOff;
        IValueRegisterField wdsel;
    }

}
