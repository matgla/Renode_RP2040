using Antmicro.Renode.Core;
using System;
using System.IO;
using Antmicro.Renode.Logging;

using Antmicro.Renode.Time;
using ELFSharp.ELF;

using Antmicro.Renode.Peripherals.Miscellaneous;
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
    public class RP2040PIOCPU : BaseCPU, IRP2040Peripheral, IGPIOReceiver, ITimeSink, IDisposable, IDoubleWordPeripheral, IKnownSize
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
            configure.StartInfo.Arguments = ".. -DCMAKE_BUILD_TYPE=Release";
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

        public RP2040PIOCPU(string cpuType, IMachine machine, ulong address, GPIOPort.RP2040GPIO gpio, uint id, RP2040Clocks clocks, Endianess endianness = Endianess.LittleEndian, CpuBitness bitness = CpuBitness.Bits32)
            : base(id + 100, cpuType, machine, endianness, bitness)
        {
            CompilePioSim();
            pioId = (int)id;
            string libraryFile = GetPioSimLibraryPath();
            binder = new NativeBinder(this, libraryFile);
            machine.GetSystemBus(this).Register(this, new BusRangeRegistration(new Antmicro.Renode.Core.Range(address, (ulong)Size)));
            this.gpio = gpio;
            this.gpioFunction = id == 0 ? GPIOPort.RP2040GPIO.GpioFunction.PIO0 : GPIOPort.RP2040GPIO.GpioFunction.PIO1;
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + xorAliasOffset, aliasSize, "XOR"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + setAliasOffset, aliasSize, "SET"));
            machine.GetSystemBus(this).Register(this, new BusMultiRegistration(address + clearAliasOffset, aliasSize, "CLEAR"));
            gpio.ReevaluatePioActions.Add((uint steps) =>
            {
                totalExecutedInstructions += PioExecute(pioId, steps);
            });
            clocks.OnSystemClockChange(UpdateClocks);
        }

        private void UpdateClocks(long systemClockFrequency)
        {
            uint newPerformance = (uint)(systemClockFrequency / 1000000);
            if (newPerformance == 0)
            {
                newPerformance = 1;
            }
            this.PerformanceInMips = newPerformance;
            this.Log(LogLevel.Debug, "Changing clock frequency to: " + newPerformance + " MIPS");
        }

        public override void Start()
        {
            base.Start();

            PioInitialize(pioId);
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
            lock (this)
            {
                return (uint)PioReadMemory(pioId, (uint)offset);
            }
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            lock(this)
            { 
                PioWriteMemory(pioId, (uint)offset, (uint)value);
            }
        }

        public override void Dispose()
        {
            lock(this)
            {

            PioDeinitialize(pioId);

            base.Dispose();
            }
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

        public override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            try
            {
                // [Here comes the invocation of the external simulator for the given amount of instructions]
                // [This is the place where simulation of acutal instructions is to be executed]
                lock (this)
                { 
                instructionsExecutedThisRound += (ulong)PioExecute(pioId, (uint)numberOfInstructionsToExecute);
                }
            }
            catch (Exception)
            {
                this.NoisyLog("CPU exception detected, halting.");
                //InvokeHalted(new HaltArguments(HaltReason.Abort, this));
                instructionsExecutedThisRound = 0UL;
                return ExecutionResult.Aborted;
            }
            finally
            {
                numberOfExecutedInstructions = instructionsExecutedThisRound;
                totalExecutedInstructions += instructionsExecutedThisRound;
                instructionsExecutedThisRound = 0UL;
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
            this.gpio.SetGpioBitset(bitset, gpioFunction, bitmap);
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
        private int pioId;
        private GPIOPort.RP2040GPIO.GpioFunction gpioFunction;

        [Transient]
        private NativeBinder binder;

        public long Size { get { return 0x1000; } }
        public const ulong aliasSize = 0x1000; 
        public const ulong xorAliasOffset = 0x1000;
        public const ulong setAliasOffset = 0x2000;
        public const ulong clearAliasOffset = 0x3000;

        [Import]
        private ActionInt32 PioInitialize;

        [Import]
        private ActionInt32 PioDeinitialize;


        [Import]
        private FuncUInt32Int32UInt32 PioExecute;

        [Import]
        private ActionInt32UInt32UInt32 PioWriteMemory;

        [Import]
        private FuncUInt32Int32UInt32 PioReadMemory;


    }
}

