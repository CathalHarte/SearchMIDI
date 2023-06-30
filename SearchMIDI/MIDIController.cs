using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using Windows.Storage.Streams;

namespace SearchMIDI
{
    public class MIDIController
    {
        public readonly List<DeviceInformation> FoundDevices = new();
        private readonly List<MidiInPort> _connectedDevices = new();
        private readonly object _midiConnectionLock = new();

        private DeviceWatcher _watcher;
        private bool _enumerationCompleted = false;
        /// <summary>
        /// Get all MIDI devices (all paired BLE, and all wired USB devices should appear)
        /// </summary>
        public async Task GetDevices()
        {
            _enumerationCompleted = false;
            _watcher = DeviceInformation.CreateWatcher(
                MidiInPort.GetDeviceSelector());

            _watcher.Added += OnMidiInputDeviceAdded;
            _watcher.EnumerationCompleted += EnumerationCompleted;
            _watcher.Start();

            while (!_enumerationCompleted)
                await Task.Delay(1000);
        }

        private void EnumerationCompleted(DeviceWatcher sender, object args)
        {
            _enumerationCompleted = true;
        }

        /// <summary>
        /// When the watcher finds a midi input device, it appears here.
        /// Bluetooth devices, which is what we are primarily interested in,
        /// will be found immediately, even if turned off, since they are found
        /// in the "paired devices" list.
        ///
        /// Next, we launch a task to open the port of the found midi device
        /// This is an async task which can run indefinitely, however it is
        /// found by experiment that if it doesn't succeed quickly, here we
        /// choose 1 second, then it will never succeed. We therefore decide
        /// to Cancel and restart that process, which resets our chances of
        /// success, indefinitely until it does succeed.
        ///
        /// When in the process of trying to connect to a MidiInPort, it appears
        /// that messages sent from the other BLE MIDI devices are delayed, or
        /// blocked. Therefore we cannot continually be scanning and attempting
        /// to connect to BLE MIDI devices while simultaneously having proper
        /// functionality of those already connected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnMidiInputDeviceAdded(
            DeviceWatcher sender, DeviceInformation args)
        {
            // handle the addition of a new MIDI input device
            Task.Run(async () =>
            {
                if (FoundDevices.FirstOrDefault(d => d.Id == args.Id) != default)
                    return;

                Console.WriteLine("Found MIDI device " + args.Id + " " + args.Name);
                FoundDevices.Add(args);
            });
        }

        public async Task TryConnectDevice(DeviceInformation device)
        {
            var success = false;
            var numTries = 0;
            while (!success && numTries < 3)
            {
                Console.WriteLine("Attempting to connect" + device.Id);
                var asyncOp = MidiInPort.FromIdAsync(device.Id);
                MidiInPort inPort = null;
                await Task.WhenAny(Task.Delay(1000), Task.Run(async () =>
                {
                    inPort = await asyncOp;
                    success = true;
                }));

                if (!success)
                {
                    asyncOp.Cancel();
                    numTries++;

                    continue;
                }

                inPort.MessageReceived += InPortOnMessageReceived;
                _connectedDevices.Add(inPort);
                Console.WriteLine("Succeeded in connecting " + device.Id);
            }
        }

        public void DisconnectDevices()
        {
            foreach (var device in _connectedDevices)
            {
                device.MessageReceived -= InPortOnMessageReceived;
            }
            _connectedDevices.Clear();
        }

        private void InPortOnMessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            Console.WriteLine(sender.DeviceId);
            if (args.Message.Type == MidiMessageType.NoteOn)
            {
                var dataReader = DataReader.FromBuffer(args.Message.RawData);
                byte[] bytes = new byte[args.Message.RawData.Length];
                dataReader.ReadBytes(bytes);
                // bytes[1] is the note played, bytes[2] is the velocity 0-127
                Console.WriteLine("Note On " + bytes[1] + " " + bytes[2]);
               
            }

            if (args.Message.Type == MidiMessageType.ControlChange)
            {
                var dataReader = DataReader.FromBuffer(args.Message.RawData);
                byte[] bytes = new byte[args.Message.RawData.Length];
                dataReader.ReadBytes(bytes);
                // bytes[1] is the fader number, bytes[2] is the value
                Console.WriteLine("Control Change " + bytes[1] + " " + bytes[2]);
            }
        }
    }
}
