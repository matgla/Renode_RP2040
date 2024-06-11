using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System;
using Antmicro.Renode.Time;
using Xwt;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class PowerOnInput: IGPIOReceiver
    {
        public PowerOnInput()
        {
           PowerOnLine = new bool();
           
           Reset();
        }

        public void Reset()
        {
        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Error, "Received GPIO PowerOn signal " + value);
        }
        
        private readonly bool PowerOnLine;
    }

}
