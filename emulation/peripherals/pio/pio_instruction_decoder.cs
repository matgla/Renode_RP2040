using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous 
{

public class PioDecodedInstruction
{
    public enum Opcode 
    {
        Jmp = 0x0,
        Wait = 0x1,
        In = 0x2,
        Out = 0x3,
        Push = 0x4,
        Pull = 0x5,
        Mov = 0x6,
        Irq = 0x7,
        Set = 0x8
    };

    public Opcode OpCode { get; }
    public uint ImmediateData { get; }
    public uint DelayOrSideSet { get; }
    public PioDecodedInstruction(ushort instruction)
    {
        OpCode = (Opcode)((instruction >> 13) & 0x7);
        DelayOrSideSet = (uint)((instruction >> 8 ) & 0x31);
        ImmediateData = (uint)(instruction & 0xff);
    }
}

}
