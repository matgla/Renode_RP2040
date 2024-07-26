using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PowerOnInput : IGPIOReceiver
    {
        public PowerOnInput(Machine machine)
        {
            PowerOnLine = new bool();
            this.machine = machine;
            Reset();
        }

        private Machine machine;

        public void Reset()
        {
        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Error, "Received GPIO PowerOn signal " + value);

            if (value)
            {
                var cpus = machine.SystemBus.GetCPUs();
                foreach (var cpu in cpus)
                {
                    this.Log(LogLevel.Error, "Enable CPU: ");
                    cpu.IsHalted = false;
                }
            }
        }

        private readonly bool PowerOnLine;
    }

}
