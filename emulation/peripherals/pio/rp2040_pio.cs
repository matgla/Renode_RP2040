using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using System.IO;
using System.Reflection;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.GPIOPort;

using Antmicro.Renode.Time;
using ELFSharp.ELF;
using Machine = Antmicro.Renode.Core.Machine;

using Antmicro.Renode.Peripherals.Miscellaneous.RP2040PIORegisters;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.Bus;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Antmicro.Renode.Peripherals.CPU
{
    // parts of this class can be left unmodified;
    // to integrate an external simulator you need to
    // look for comments in the code below
    public class RP2040PIOCPU : BaseCPU, IGPIOReceiver, ITimeSink, IDisposable, IDoubleWordPeripheral, IKnownSize
    {
        private static string GetSourceFileDirectory([CallerFilePath] string sourceFilePath = "")
        {
            return Path.GetDirectoryName(sourceFilePath);
        }

        private static string GetPioSimLibraryPath()
        {
            string libraryName = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libraryName = "libpiosim.dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libraryName = "libpiosim.dylib";
            }
            else
            {
                libraryName = "libpiosim.so";
            }
            return Path.GetFullPath(GetSourceFileDirectory() + "/../../../piosim/build/" + libraryName);
        }

        private static string GetPioSimDirectory()
        {
            return Path.GetFullPath(GetSourceFileDirectory() + "/../../../piosim");
        }

        private void CompilePioSim()
        {
            string buildPath = GetPioSimDirectory() + "/build";
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }
            Process find_cmake = new Process();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                find_cmake.StartInfo.FileName = "where";
            }
            else
            {
                find_cmake.StartInfo.FileName = "which";

            }
            find_cmake.StartInfo.Arguments = "cmake";
            find_cmake.StartInfo.UseShellExecute = false;
            find_cmake.StartInfo.RedirectStandardOutput = true;
            find_cmake.StartInfo.CreateNoWindow = true;
            find_cmake.Start();

            string cmake_command = "";
            while (!find_cmake.StandardOutput.EndOfStream)
            {
                cmake_command = find_cmake.StandardOutput.ReadLine();
            }

            if (!cmake_command.Contains("cmake"))
            {
                this.Log(LogLevel.Error, "Cannot find 'cmake' executable");
            }


            Process configure = new Process();
            configure.StartInfo.FileName = cmake_command;
            configure.StartInfo.Arguments = ".. -DCMAKE_BUILD_TYPE=Relase";
            configure.StartInfo.CreateNoWindow = false;
            configure.StartInfo.UseShellExecute = false;
            configure.StartInfo.WorkingDirectory = buildPath;
            configure.Start();
            configure.WaitForExit();
            if (configure.ExitCode != 0)
            {
                throw new Exception("CMake configuration failed");
            }


            Directory.CreateDirectory(buildPath);
            Process build = new Process();
            build.StartInfo.FileName = cmake_command;
            build.StartInfo.Arguments = "--build .";
            build.StartInfo.CreateNoWindow = false;
            build.StartInfo.UseShellExecute = true;
            build.StartInfo.WorkingDirectory = buildPath;
            build.Start();
            build.WaitForExit();
            if (build.ExitCode != 0)
            {
                throw new Exception("CMake build failed");
            }

        }

        public RP2040PIOCPU(string cpuType, IMachine machine, ulong address, GPIOPort.RP2040GPIO gpio, Endianess endianness = Endianess.LittleEndian, CpuBitness bitness = CpuBitness.Bits32)
            : base(0, cpuType, machine, endianness, bitness)
        {
            CompilePioSim();
            string libraryFile = GetPioSimLibraryPath();
            binder = new NativeBinder(this, libraryFile);
            machine.GetSystemBus(this).Register(this, new BusRangeRegistration(new Antmicro.Renode.Core.Range(address, (ulong)Size)));
            this.gpio = gpio;
        }

        public override void Start()
        {
            base.Start();

            PioInitialize();
        }

        public override void Reset()
        {
            this.Log(LogLevel.Error, "Reset");
            base.Reset();

            instructionsExecutedThisRound = 0;
            totalExecutedInstructions = 0;

            // [Here goes an invocation resetting the external simulator (if needed)]
            // [This can be used to revert the internal state of the simulator to the initial form]
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return (uint)PioReadMemory((uint)offset);
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            PioWriteMemory((uint)offset, (uint)value);
        }

        public override void Dispose()
        {
            PioDeinitialize();

            base.Dispose();
            // [Here goes an invocation disposing the external simulator (if needed)]
            // [This can be used to clean all unmanaged resources used to communicate with the simulator]
        }

        public void OnGPIO(int number, bool value)
        {
            if (!IsStarted)
            {
                return;
            }

            // [Here goes an invocation triggering an IRQ in the external simulator]
        }

        public virtual void SetRegisterValue32(int register, uint value)
        {
            // [Here goes an invocation setting the register value in the external simulator]
        }

        public virtual uint GetRegisterValue32(int register)
        {
            // [Here goes an invocation reading the register value from the external simulator]
            return 0;
        }

        protected override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            instructionsExecutedThisRound = 0UL;

            try
            {
                // [Here comes the invocation of the external simulator for the given amount of instructions]
                // [This is the place where simulation of acutal instructions is to be executed]
                instructionsExecutedThisRound = (ulong)PioExecute((uint)numberOfInstructionsToExecute);

            }
            catch (Exception)
            {
                this.NoisyLog("CPU exception detected, halting.");
                //InvokeHalted(new HaltArguments(HaltReason.Abort, this));
                return ExecutionResult.Aborted;
            }
            finally
            {
                numberOfExecutedInstructions = instructionsExecutedThisRound;
                totalExecutedInstructions += instructionsExecutedThisRound;
            }

            return ExecutionResult.Ok;
        }

        public override string Architecture => "RP2040_PIO";

        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32(PCRegisterId);
            }

            set
            {
                SetRegisterValue32(PCRegisterId, value);
            }
        }

        [Export]
        protected virtual void LogAsCpu(int level, string s)
        {
            this.Log((LogLevel)level, s);
        }

        [Export]
        protected virtual void GpioPinWriteBitset(uint bitset, uint bitmap)
        {
            this.gpio.SetGpioBitset(bitset, bitmap);
        }

        [Export]
        protected virtual void GpioPindirWriteBitset(uint bitset, uint bitmap)
        {
            this.gpio.SetPinDirectionBitset(bitset, bitmap);
        }

        [Export]
        protected virtual int GpioGetPinState(uint pin)
        {
            return Convert.ToInt32(this.gpio.GetGpioState(pin));
        }

        [Export]
        protected virtual uint GetGpioPinBitmap()
        {
            return (uint)this.gpio.GetGpioStateBitmap();
        }

        private GPIOPort.RP2040GPIO gpio;

        public override ulong ExecutedInstructions => totalExecutedInstructions;

        private ulong instructionsExecutedThisRound;
        private ulong totalExecutedInstructions;

        // [This needs to be mapped to the id of the Program Counter register used by the simulator]
        private const int PCRegisterId = 0;

        [Transient]
        private NativeBinder binder;

        public long Size { get { return 0x1000; } }

        [Import]
        private Action PioInitialize;

        [Import]
        private Action PioDeinitialize;


        [Import]
        private FuncUInt32UInt32 PioExecute;

        [Import]
        private ActionUInt32UInt32 PioWriteMemory;

        [Import]
        private FuncUInt32UInt32 PioReadMemory;
    }
}

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RP2040PIO : BasicDoubleWordPeripheral, IKnownSize
    {
        private enum Registers
        {
            CTRL = 0x00,
            FSTAT = 0x04,
            FDEBUG = 0x08,
            FLEVEL = 0x0c,
            IRQ = 0x30,
            IRQ_FORCE = 0x34,
            INPUT_SYNC_BYPASS = 0x38,
            SM0_CLKDIV = 0xc8,
            SM0_EXECCTRL = 0xcc,
            SM0_SHIFTCTRL = 0xd0,
            SM0_ADDR = 0xd4,
            SM0_INSTR = 0xd8,
            SM0_PINCTRL = 0xdc,
            SM1_CLKDIV = 0xe0,
            SM1_EXECCTRL = 0xe4,
            SM1_SHIFTCTRL = 0xe8,
            SM1_ADDR = 0xec,
            SM1_INSTR = 0xf0,
            SM1_PINCTRL = 0xf4,
            SM2_CLKDIV = 0xf8,
            SM2_EXECCTRL = 0xfc,
            SM2_SHIFTCTRL = 0x100,
            SM2_ADDR = 0x104,
            SM2_INSTR = 0x108,
            SM2_PINCTRL = 0x10c,
            SM3_CLKDIV = 0x110,
            SM3_EXECCTRL = 0x114,
            SM3_SHIFTCTRL = 0x118,
            SM3_ADDR = 0x11c,
            SM3_INSTR = 0x120,
            SM3_PINCTRL = 0x124,
        }

        public RP2040PIO(Machine machine, RP2040GPIO gpio) : base(machine)
        {
            this.Log(LogLevel.Error, "RP2040 PIO");
            IRQs = new GPIO[2];
            for (int i = 0; i < IRQs.Length; ++i)
            {
                IRQs[i] = new GPIO();
            }
            Instructions = new ushort[32];
            StateMachines = new PioStateMachine[4];

            this.gpio = gpio;
            this._irq = new PIOIRQ();
            this._pioIrqs = new bool[8];
            for (int i = 0; i < StateMachines.Length; ++i)
            {
                StateMachines[i] = new PioStateMachine(machine, Instructions, i, gpio, this.Log, this._pioIrqs);
            }

            DefineRegisters();
            Reset();
        }

        private PioStateMachine[] StateMachines;
        public long Size { get { return 0x1000; } }
        public GPIO[] IRQs { get; private set; }
        public GPIO IRQ0 => IRQs[0];
        public GPIO IRQ1 => IRQs[1];
        private RP2040GPIO gpio;
        private ushort[] Instructions;
        private PIOIRQ _irq;
        private bool[] _pioIrqs;

        public override void Reset()
        {
        }

        public void DefineRegisters()
        {
            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            this.Log(LogLevel.Info, "Writing to SM: " + key + " FIFO, val: " + value);
                            StateMachines[key].ShiftControl.PushTxFifo((uint)value);
                        },
                    name: "TXF" + i);
                RegistersCollection.AddRegister(0x10 + i * 0x4, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.PopRxFifo(),
                    name: "RXF" + i);
                RegistersCollection.AddRegister(0x20 + i * 0x4, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 4,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.StatusLevel,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.StatusLevel = (byte)value,
                        name: "SM" + key + "_EXECCTRL_STATUS_N")
                    .WithFlag(4,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.StatusSelect,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.StatusSelect = (bool)value,
                        name: "SM" + key + "_EXECCTRL_STATUS_SELECT")
                    .WithReservedBits(5, 2)
                    .WithValueField(7, 5,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.WrapBottom,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.WrapBottom = (byte)value,
                        name: "SM" + key + "_EXECCTRL_WRAP_BOTTOM")
                    .WithValueField(12, 5,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.WrapTop,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.WrapTop = (byte)value,
                        name: "SM" + key + "_EXECCTRL_WRAP_TOP")
                    .WithFlag(17,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.OutSticky,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.OutSticky = (bool)value,
                        name: "SM" + key + "_EXECCTRL_OUT_STICKY")
                    .WithFlag(18,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.OutInlineEnable,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.OutInlineEnable = (bool)value)
                    .WithValueField(19, 5,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.OutEnableSelect,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.OutEnableSelect = (byte)value)
                    .WithValueField(24, 5,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.JumpPin,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.JumpPin = (byte)value)
                    .WithFlag(29,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.SidePinDirection,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.SidePinDirection = (bool)value)
                    .WithFlag(30,
                        valueProviderCallback: _ => StateMachines[key].ExecutionControl.SideEnabled,
                        writeCallback: (_, value) => StateMachines[key].ExecutionControl.SideEnabled = (bool)value)
                    .WithFlag(31, FieldMode.Read,
                        valueProviderCallback: _ => { return false; });

                RegistersCollection.AddRegister(0x0cc + i * 0x18, reg);
            }



            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.AutoPush,
                        writeCallback: (_, value) => StateMachines[key].ShiftControl.AutoPush = (bool)value,
                        name: "SM" + key + "_SHIFTCTRL_AUTOPUSH")
                    .WithFlag(17,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.AutoPull,
                        writeCallback: (_, value) => StateMachines[key].ShiftControl.AutoPull = (bool)value,
                        name: "SM" + key + "_SHIFTCTRL_AUTOPULL")
                    .WithFlag(18,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.InShiftDirection == StateMachineShiftControl.Direction.Left ? false : true,
                        writeCallback: (_, value) => StateMachines[key].ShiftControl.InShiftDirection = value ? StateMachineShiftControl.Direction.Right : StateMachineShiftControl.Direction.Left,
                        name: "SM" + key + "_SHIFTCTRL_IN_SHIFTDIR")
                    .WithFlag(19,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.OutShiftDirection == StateMachineShiftControl.Direction.Left ? false : true,
                        writeCallback: (_, value) => StateMachines[key].ShiftControl.OutShiftDirection = value ? StateMachineShiftControl.Direction.Right : StateMachineShiftControl.Direction.Left,
                        name: "SM" + key + "_SHIFTCTRL_OUT_SHIFTDIR")
                    .WithValueField(20, 5,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.PushThreshold == 32 ? 0UL : (ulong)StateMachines[key].ShiftControl.PushThreshold,
                        writeCallback: (_, value) =>
                        {
                            StateMachines[key].ShiftControl.PushThreshold = (byte)value;
                            if (value == 0)
                            {
                                StateMachines[key].ShiftControl.PushThreshold = 32;
                            }
                        },
                        name: "SM" + key + "_SHIFTCTRL_PUSH_THRESH")
                    .WithValueField(25, 5,
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.PullThreshold == 32 ? 0UL : (ulong)StateMachines[key].ShiftControl.PullThreshold,
                        writeCallback: (_, value) =>
                        {
                            StateMachines[key].ShiftControl.PullThreshold = (byte)value;
                            if (value == 0)
                            {
                                StateMachines[key].ShiftControl.PullThreshold = 32;
                            }
                        },
                        name: "SM" + key + "_SHIFTCTRL_PULL_THRESH")
                    .WithFlag(30, writeCallback: (_, value) => StateMachines[key].ShiftControl.JoinTxFifo((bool)value),
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.FifoTxJoin)
                    .WithFlag(31, writeCallback: (_, value) => StateMachines[key].ShiftControl.JoinRxFifo((bool)value),
                        valueProviderCallback: _ => StateMachines[key].ShiftControl.FifoRxJoin);

                RegistersCollection.AddRegister(0x0d0 + i * 0x18, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            StateMachines[key].ExecuteInstruction((ushort)value);
                        },
                        valueProviderCallback: _ =>
                        {
                            return StateMachines[key].GetCurrentInstruction();
                        },
                    name: "SM" + i + "_INSTR");
                RegistersCollection.AddRegister(0x0d8 + i * 0x18, reg);
            }

            for (int i = 0; i < StateMachines.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.OutBase = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.OutBase,
                    name: "SM" + i + "_PINCTRL_OUTBASE")
                    .WithValueField(5, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.SetBase = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.SetBase,
                    name: "SM" + i + "_PINCTRL_SETBASE")
                    .WithValueField(10, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.SideSetBase = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.SideSetBase,
                    name: "SM" + i + "_PINCTRL_SIDESETBASE")
                    .WithValueField(15, 5, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.InBase = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.InBase,
                    name: "SM" + i + "_PINCTRL_INBASE")
                    .WithValueField(20, 6, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.OutCount = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.OutCount,
                    name: "SM" + i + "_PINCTRL_OUTCOUNT")
                    .WithValueField(26, 3, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.SetCount = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.SetCount,
                    name: "SM" + i + "_PINCTRL_SETCOUNT")
                    .WithValueField(29, 3, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                            StateMachines[key].PinControl.SideSetCount = (byte)value,
                        valueProviderCallback: _ =>
                            (uint)StateMachines[key].PinControl.SideSetCount,
                    name: "SM" + i + "_PINCTRL_SIDESETCOUNT");

                RegistersCollection.AddRegister(0x0dc + i * 0x18, reg);
            }


            Registers.CTRL.Define(this)
                .WithValueField(0, 4, FieldMode.Read | FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (((1ul << i) & (ulong)value) != 0ul)
                            {
                                StateMachines[i].Enable();
                            }
                        }
                    },
                    valueProviderCallback: _ =>
                    {
                        ulong enabledStateMachines = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            enabledStateMachines |= Convert.ToUInt32(StateMachines[i].Enabled) << i;
                        }
                        return enabledStateMachines;
                    },
                    name: "CTRL");
            for (int i = 0; i < Instructions.Length; ++i)
            {
                int key = i;
                var reg = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            Instructions[key] = (ushort)value;
                        },
                    name: "INSTR_MEM" + i);
                RegistersCollection.AddRegister(0x048 + i * 4, reg);
            }

            Registers.FSTAT.Define(this)
                .WithValueField(0, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].ShiftControl.RxFifoFull())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "RXFULL")
                .WithReservedBits(4, 4)
                .WithValueField(8, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].ShiftControl.RxFifoEmpty())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "RXEMPTY")
                .WithReservedBits(12, 4)
                .WithValueField(16, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].ShiftControl.TxFifoFull())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "TXFULL")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        ulong ret = 0;
                        for (int i = 0; i < StateMachines.Length; ++i)
                        {
                            if (StateMachines[i].ShiftControl.TxFifoEmpty())
                            {
                                ret |= (1ul << i);
                            }
                        }
                        return ret;
                    }, name: "TXEMPTY")
                .WithReservedBits(28, 4);

        }
    }
}
