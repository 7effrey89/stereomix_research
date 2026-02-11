using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StereoMixCapture
{
    /// <summary>
    /// Event arguments for real-time audio data.
    /// </summary>
    public class AudioDataEventArgs : EventArgs
    {
        public byte[] Buffer { get; set; }
        public int BytesRecorded { get; set; }
        public WaveFormat Format { get; set; }
        public AudioSource Source { get; set; }

        public AudioDataEventArgs(byte[] buffer, int bytesRecorded, WaveFormat format, AudioSource source)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
            Format = format;
            Source = source;
        }
    }

    /// <summary>
    /// Indicates the source of audio data.
    /// </summary>
    public enum AudioSource
    {
        Loopback,
        Microphone
    }

    /// <summary>
    /// Manages audio capture from multiple sources including internal audio (loopback) and microphone.
    /// Supports both file-based recording and real-time audio data access for transcription and processing.
    /// </summary>
    public class AudioCaptureManager : IDisposable
    {
        private WasapiLoopbackCapture? loopbackCapture;
        private WaveInEvent? microphoneCapture;
        private WaveFileWriter? loopbackWriter;
        private WaveFileWriter? microphoneWriter;
        private bool isCapturing = false;
        private string outputDirectory;
        private bool enableFileOutput = true;

        /// <summary>
        /// Fired when loopback audio data is available. Use this for real-time processing/transcription.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? LoopbackDataAvailable;

        /// <summary>
        /// Fired when microphone audio data is available. Use this for real-time processing/transcription.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? MicrophoneDataAvailable;

        /// <summary>
        /// Gets or sets the buffer size in milliseconds for microphone capture.
        /// Lower values = lower latency but higher CPU usage. Default is 100ms.
        /// For real-time transcription, consider 20-50ms.
        /// </summary>
        public int MicrophoneBufferMilliseconds { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to save audio to files. Set to false for real-time-only scenarios.
        /// Default is true.
        /// </summary>
        public bool EnableFileOutput
        {
            get => enableFileOutput;
            set => enableFileOutput = value;
        }

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
                    
                    // Only create file writer if file output is enabled
                    if (enableFileOutput)
                    {
                        string loopbackFile = Path.Combine(outputDirectory, $"loopback_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                        loopbackWriter = new WaveFileWriter(loopbackFile, loopbackCapture.WaveFormat);
                        Console.WriteLine($"Output file: {loopbackFile}");
                    }
                    
                    loopbackCapture.DataAvailable += (sender, e) =>
                    {
                        // Write to file if enabled
                        if (loopbackWriter != null)
                        {
                            loopbackWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        }
                        
                        // Raise event for real-time processing
                        LoopbackDataAvailable?.Invoke(this, new AudioDataEventArgs(
                            e.Buffer, 
                            e.BytesRecorded, 
                            loopbackCapture.WaveFormat, 
                            AudioSource.Loopback));
                    };
                    
                    loopbackCapture.RecordingStopped += (sender, e) =>
                    {
                        Console.WriteLine("Loopback capture stopped.");
                    };

                    Console.WriteLine($"Starting loopback capture: {loopbackDevice.FriendlyName}");
                    loopbackCapture.StartRecording();
                }

                // Initialize microphone capture
                if (microphoneDeviceIndex >= 0 && microphoneDeviceIndex < WaveInEvent.DeviceCount)
                {
                    microphoneCapture = new WaveInEvent
                    {
                        DeviceNumber = microphoneDeviceIndex,
                        WaveFormat = new WaveFormat(44100, 16, 2), // Standard CD quality
                        BufferMilliseconds = MicrophoneBufferMilliseconds // Configurable for latency control
                    };

                    // Only create file writer if file output is enabled
                    if (enableFileOutput)
                    {
                        string microphoneFile = Path.Combine(outputDirectory, $"microphone_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                        microphoneWriter = new WaveFileWriter(microphoneFile, microphoneCapture.WaveFormat);
                        Console.WriteLine($"Output file: {microphoneFile}");
                    }

                    microphoneCapture.DataAvailable += (sender, e) =>
                    {
                        // Write to file if enabled
                        if (microphoneWriter != null)
                        {
                            microphoneWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        }
                        
                        // Raise event for real-time processing
                        MicrophoneDataAvailable?.Invoke(this, new AudioDataEventArgs(
                            e.Buffer, 
                            e.BytesRecorded, 
                            microphoneCapture.WaveFormat, 
                            AudioSource.Microphone));
                    };

                    microphoneCapture.RecordingStopped += (sender, e) =>
                    {
                        Console.WriteLine("Microphone capture stopped.");
                    };

                    Console.WriteLine($"Starting microphone capture: Device {microphoneDeviceIndex}");
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

            isCapturing = false;
            Console.WriteLine("Audio capture stopped and files saved.");
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
