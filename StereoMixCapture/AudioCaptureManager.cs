using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
        Microphone,
        Mixed
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
        private string? loopbackFilePath;
        private string? microphoneFilePath;

        // Real-time mixing fields
        private static readonly WaveFormat MixedOutputFormat = new WaveFormat(16000, 16, 1);
        private BufferedWaveProvider? loopbackMixBuffer;
        private BufferedWaveProvider? microphoneMixBuffer;
        private Thread? mixThread;
        private volatile bool mixThreadRunning;

        /// <summary>
        /// Fired when loopback audio data is available. Use this for real-time processing/transcription.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? LoopbackDataAvailable;

        /// <summary>
        /// Fired when microphone audio data is available. Use this for real-time processing/transcription.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? MicrophoneDataAvailable;

        /// <summary>
        /// Fired when mixed (loopback + microphone) audio data is available in 16kHz 16-bit mono PCM.
        /// Use this for real-time transcription to avoid running two separate transcription sessions.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? MixedDataAvailable;

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
                        loopbackFilePath = Path.Combine(outputDirectory, $"loopback_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                        loopbackWriter = new WaveFileWriter(loopbackFilePath, loopbackCapture.WaveFormat);
                        Console.WriteLine($"Output file: {loopbackFilePath}");
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
                        microphoneFilePath = Path.Combine(outputDirectory, $"microphone_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                        microphoneWriter = new WaveFileWriter(microphoneFilePath, microphoneCapture.WaveFormat);
                        Console.WriteLine($"Output file: {microphoneFilePath}");
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

                // Start real-time mixing thread if anyone is listening
                if (MixedDataAvailable != null)
                {
                    StartMixThread();
                }

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

            // Stop the mixing thread before stopping capture sources
            StopMixThread();

            // Stop capture sources first so no new data arrives
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

            // Automatically mix the captured files into a single output
            if (loopbackFilePath != null && microphoneFilePath != null
                && File.Exists(loopbackFilePath) && File.Exists(microphoneFilePath))
            {
                string mixedFile = Path.Combine(outputDirectory, $"mixed_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                Console.WriteLine("\nMixing captured audio files...");
                AudioMixer.MixAudioFiles(loopbackFilePath, microphoneFilePath, mixedFile);
            }
        }

        public void Dispose()
        {
            StopCapture();
        }

        /// <summary>
        /// Initialises the mix buffers and starts a background thread that periodically
        /// drains both buffers, mixes them, and fires MixedDataAvailable.
        /// </summary>
        private void StartMixThread()
        {
            // Create buffered providers that accept raw PCM written from conversion helpers
            loopbackMixBuffer = new BufferedWaveProvider(MixedOutputFormat)
            {
                BufferLength = MixedOutputFormat.AverageBytesPerSecond * 5,
                DiscardOnBufferOverflow = true
            };
            microphoneMixBuffer = new BufferedWaveProvider(MixedOutputFormat)
            {
                BufferLength = MixedOutputFormat.AverageBytesPerSecond * 5,
                DiscardOnBufferOverflow = true
            };

            // Subscribe to the raw data events to convert and push into the mix buffers
            LoopbackDataAvailable += OnLoopbackForMix;
            MicrophoneDataAvailable += OnMicrophoneForMix;

            mixThreadRunning = true;
            mixThread = new Thread(MixThreadLoop)
            {
                IsBackground = true,
                Name = "AudioMixThread"
            };
            mixThread.Start();
        }

        private void OnLoopbackForMix(object? sender, AudioDataEventArgs e)
        {
            if (loopbackMixBuffer == null || e.BytesRecorded == 0) return;
            byte[] converted = ConvertToPcm16kMono(e.Buffer, e.BytesRecorded, e.Format);
            if (converted.Length > 0)
                loopbackMixBuffer.AddSamples(converted, 0, converted.Length);
        }

        private void OnMicrophoneForMix(object? sender, AudioDataEventArgs e)
        {
            if (microphoneMixBuffer == null || e.BytesRecorded == 0) return;
            byte[] converted = ConvertToPcm16kMono(e.Buffer, e.BytesRecorded, e.Format);
            if (converted.Length > 0)
                microphoneMixBuffer.AddSamples(converted, 0, converted.Length);
        }

        /// <summary>
        /// Background thread that reads from both mix buffers, sums the samples, and
        /// fires MixedDataAvailable at roughly 50ms intervals.
        /// </summary>
        private void MixThreadLoop()
        {
            // 50ms worth of 16kHz 16-bit mono = 16000 * 2 * 0.05 = 1600 bytes
            const int intervalMs = 50;
            int chunkBytes = MixedOutputFormat.AverageBytesPerSecond * intervalMs / 1000;
            byte[] loopbackBuf = new byte[chunkBytes];
            byte[] micBuf = new byte[chunkBytes];
            byte[] mixedBuf = new byte[chunkBytes];

            while (mixThreadRunning)
            {
                Thread.Sleep(intervalMs);

                if (loopbackMixBuffer == null || microphoneMixBuffer == null) continue;

                int loopbackAvailable = loopbackMixBuffer.BufferedBytes;
                int micAvailable = microphoneMixBuffer.BufferedBytes;
                int bytesToRead = Math.Min(chunkBytes, Math.Max(loopbackAvailable, micAvailable));
                if (bytesToRead == 0) continue;

                // Ensure even number of bytes (16-bit samples)
                bytesToRead = bytesToRead / 2 * 2;

                // Read what's available from each buffer, zero-filling if one source has less
                int loopbackRead = 0;
                if (loopbackMixBuffer.BufferedBytes > 0)
                    loopbackRead = loopbackMixBuffer.Read(loopbackBuf, 0, Math.Min(bytesToRead, loopbackMixBuffer.BufferedBytes));

                int micRead = 0;
                if (microphoneMixBuffer.BufferedBytes > 0)
                    micRead = microphoneMixBuffer.Read(micBuf, 0, Math.Min(bytesToRead, microphoneMixBuffer.BufferedBytes));

                int maxRead = Math.Max(loopbackRead, micRead);
                if (maxRead == 0) continue;

                // Mix by summing 16-bit samples with clipping
                for (int i = 0; i < maxRead; i += 2)
                {
                    short s1 = (i + 1 < loopbackRead)
                        ? (short)(loopbackBuf[i] | (loopbackBuf[i + 1] << 8))
                        : (short)0;
                    short s2 = (i + 1 < micRead)
                        ? (short)(micBuf[i] | (micBuf[i + 1] << 8))
                        : (short)0;

                    int mixed = s1 + s2;
                    if (mixed > short.MaxValue) mixed = short.MaxValue;
                    if (mixed < short.MinValue) mixed = short.MinValue;

                    mixedBuf[i] = (byte)(mixed & 0xFF);
                    mixedBuf[i + 1] = (byte)((mixed >> 8) & 0xFF);
                }

                MixedDataAvailable?.Invoke(this, new AudioDataEventArgs(
                    mixedBuf, maxRead, MixedOutputFormat, AudioSource.Mixed));
            }
        }

        /// <summary>
        /// Converts audio from any NAudio WaveFormat to 16kHz, 16-bit, mono PCM.
        /// </summary>
        private static byte[] ConvertToPcm16kMono(byte[] buffer, int bytesRecorded, WaveFormat sourceFormat)
        {
            using var sourceStream = new RawSourceWaveStream(
                new MemoryStream(buffer, 0, bytesRecorded), sourceFormat);

            IWaveProvider pcmProvider;
            if (sourceFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                pcmProvider = new WaveFloatTo16Provider(sourceStream);
            }
            else if (sourceFormat.BitsPerSample != 16)
            {
                pcmProvider = new WaveFloatTo16Provider(
                    new Wave16ToFloatProvider(sourceStream));
            }
            else
            {
                pcmProvider = sourceStream;
            }

            if (pcmProvider.WaveFormat.Channels > 1)
            {
                pcmProvider = new StereoToMonoProvider16(pcmProvider);
            }

            if (pcmProvider.WaveFormat.SampleRate != 16000)
            {
                var targetFormat = new WaveFormat(16000, 16, 1);
                using var resampler = new MediaFoundationResampler(pcmProvider, targetFormat)
                {
                    ResamplerQuality = 60
                };
                return ReadAllBytes(resampler);
            }

            return ReadAllBytes(pcmProvider);
        }

        private static byte[] ReadAllBytes(IWaveProvider provider)
        {
            using var ms = new MemoryStream();
            byte[] readBuffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = provider.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                ms.Write(readBuffer, 0, bytesRead);
            }
            return ms.ToArray();
        }

        private void StopMixThread()
        {
            mixThreadRunning = false;
            if (mixThread != null)
            {
                mixThread.Join(timeout: TimeSpan.FromSeconds(2));
                mixThread = null;
            }

            LoopbackDataAvailable -= OnLoopbackForMix;
            MicrophoneDataAvailable -= OnMicrophoneForMix;

            loopbackMixBuffer = null;
            microphoneMixBuffer = null;
        }
    }
}
