using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StereoMixCapture
{
    /// <summary>
    /// Manages audio capture from multiple sources including internal audio (loopback) and microphone.
    /// </summary>
    public class AudioCaptureManager : IDisposable
    {
        private WasapiLoopbackCapture? loopbackCapture;
        private WaveInEvent? microphoneCapture;
        private WaveFileWriter? loopbackWriter;
        private WaveFileWriter? microphoneWriter;
        private WaveFileWriter? mixedWriter;
        private BufferedWaveProvider? loopbackBuffer;
        private BufferedWaveProvider? microphoneBuffer;
        private bool isCapturing = false;
        private string outputDirectory;

        public AudioCaptureManager(string outputDirectory = ".")
        {
            this.outputDirectory = outputDirectory;
        }

        /// <summary>
        /// Lists all available audio capture devices.
        /// </summary>
        public static void ListAudioDevices()
        {
            Console.WriteLine("\n=== Audio Capture Devices ===\n");

            // List WASAPI Loopback devices (for internal audio)
            Console.WriteLine("Internal Audio Devices (Loopback):");
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"  [{i}] {devices[i].FriendlyName}");
            }

            // List Microphone devices (WaveIn)
            Console.WriteLine("\nMicrophone Input Devices:");
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                Console.WriteLine($"  [{i}] {capabilities.ProductName} (Channels: {capabilities.Channels})");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Starts capturing audio from both internal audio and microphone.
        /// </summary>
        public void StartCapture(int loopbackDeviceIndex = 0, int microphoneDeviceIndex = 0)
        {
            if (isCapturing)
            {
                Console.WriteLine("Already capturing audio.");
                return;
            }

            try
            {
                // Initialize loopback capture (internal audio)
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                
                if (loopbackDeviceIndex >= 0 && loopbackDeviceIndex < devices.Count)
                {
                    var loopbackDevice = devices[loopbackDeviceIndex];
                    loopbackCapture = new WasapiLoopbackCapture(loopbackDevice);
                    
                    string loopbackFile = Path.Combine(outputDirectory, $"loopback_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                    loopbackWriter = new WaveFileWriter(loopbackFile, loopbackCapture.WaveFormat);
                    loopbackBuffer = new BufferedWaveProvider(loopbackCapture.WaveFormat);
                    
                    loopbackCapture.DataAvailable += (sender, e) =>
                    {
                        if (loopbackWriter != null)
                        {
                            loopbackWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        }
                        if (loopbackBuffer != null)
                        {
                            loopbackBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        }
                    };
                    
                    loopbackCapture.RecordingStopped += (sender, e) =>
                    {
                        Console.WriteLine("Loopback capture stopped.");
                    };

                    Console.WriteLine($"Starting loopback capture: {loopbackDevice.FriendlyName}");
                    Console.WriteLine($"Output file: {loopbackFile}");
                    loopbackCapture.StartRecording();
                }

                // Initialize microphone capture
                if (microphoneDeviceIndex >= 0 && microphoneDeviceIndex < WaveInEvent.DeviceCount)
                {
                    microphoneCapture = new WaveInEvent
                    {
                        DeviceNumber = microphoneDeviceIndex,
                        WaveFormat = new WaveFormat(44100, 16, 2) // Standard CD quality
                    };

                    string microphoneFile = Path.Combine(outputDirectory, $"microphone_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                    microphoneWriter = new WaveFileWriter(microphoneFile, microphoneCapture.WaveFormat);
                    microphoneBuffer = new BufferedWaveProvider(microphoneCapture.WaveFormat);

                    microphoneCapture.DataAvailable += (sender, e) =>
                    {
                        if (microphoneWriter != null)
                        {
                            microphoneWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        }
                        if (microphoneBuffer != null)
                        {
                            microphoneBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        }
                    };

                    microphoneCapture.RecordingStopped += (sender, e) =>
                    {
                        Console.WriteLine("Microphone capture stopped.");
                    };

                    Console.WriteLine($"Starting microphone capture: Device {microphoneDeviceIndex}");
                    Console.WriteLine($"Output file: {microphoneFile}");
                    microphoneCapture.StartRecording();
                }

                isCapturing = true;
                Console.WriteLine("\nCapturing audio... Press any key to stop.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting capture: {ex.Message}");
                StopCapture();
                throw;
            }
        }

        /// <summary>
        /// Stops all audio capture.
        /// </summary>
        public void StopCapture()
        {
            if (!isCapturing)
            {
                return;
            }

            Console.WriteLine("\nStopping audio capture...");

            if (loopbackCapture != null)
            {
                loopbackCapture.StopRecording();
                loopbackCapture.Dispose();
                loopbackCapture = null;
            }

            if (microphoneCapture != null)
            {
                microphoneCapture.StopRecording();
                microphoneCapture.Dispose();
                microphoneCapture = null;
            }

            if (loopbackWriter != null)
            {
                loopbackWriter.Dispose();
                loopbackWriter = null;
            }

            if (microphoneWriter != null)
            {
                microphoneWriter.Dispose();
                microphoneWriter = null;
            }

            if (mixedWriter != null)
            {
                mixedWriter.Dispose();
                mixedWriter = null;
            }

            isCapturing = false;
            Console.WriteLine("Audio capture stopped and files saved.");
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
