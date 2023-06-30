using System;
using System.Threading.Tasks;

namespace SearchMIDI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var midi = new MIDIController();
            await midi.GetDevices();

            foreach (var deviceInformation in midi.FoundDevices)
            {
                await midi.TryConnectDevice(deviceInformation);
            }

            Console.ReadLine();
        }
    }
}
