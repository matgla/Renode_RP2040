using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Analog;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ResistorDAC : IGPIOReceiver
    {
        public ResistorDAC(Machine machine, RP2040ADC adc, int adcChannel)
        {
            this.adc = adc;
            this.channel = adcChannel;
            Reset();
        }

        public void Reset()
        {
            this.data = 0;
        }

        // signal 100 means that parallel GPIO change was done
        public void OnGPIO(int number, bool value)
        {
            //this.Log(LogLevel.Error, "Resistor DAC called with: " + number + ", value: " + value);
            //this.adc.FeedVoltageSampleToChannel(this.channel, (decimal)3.3, 1);
            if (number == 100)
            {
                this.Log(LogLevel.Error, "Operation done: " + data);
            }
            else
            {
                if (value)
                {
                    data |= (1 << number);
                }
                else 
                {
                    data &= ~(1 << number);
                }
            }
        }

        private RP2040ADC adc;
        private int data;
        private int channel;
    }
}