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
        }

        public void Reset()
        {

        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Error, "Resistor DAC called with: " + number + ", value: " + value);
            this.adc.FeedVoltageSampleToChannel(this.channel, (decimal)3.3, 1);
        }

        private RP2040ADC adc;
        private int channel;
    }
}