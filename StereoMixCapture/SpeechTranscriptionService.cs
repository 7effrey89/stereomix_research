using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription;
using NAudio.Wave;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StereoMixCapture
{
    /// <summary>
    /// Integrates AudioCaptureManager with Azure AI Foundry Speech real-time transcription.
    /// Runs two ConversationTranscriber instances in parallel — one for loopback (soundcard)
    /// audio and one for microphone — mirroring the NoteAssistantUI dual-transcription pattern
    /// but using AudioCaptureManager as the audio source instead of raw NAudio devices.
    /// </summary>
    public class SpeechTranscriptionService : IDisposable
    {
        private readonly AudioCaptureManager captureManager;
        private readonly SpeechConfig speechConfig;

        private PushAudioInputStream? loopbackPushStream;
        private PushAudioInputStream? microphonePushStream;
        private ConversationTranscriber? loopbackTranscriber;
        private ConversationTranscriber? microphoneTranscriber;

        private CancellationTokenSource? cts;
        private TaskCompletionSource<int>? stopLoopback;
        private TaskCompletionSource<int>? stopMicrophone;

        /// <summary>
        /// Raised when a partial (in-progress) transcription result is available.
        /// </summary>
        public event EventHandler<TranscriptionEventArgs>? Transcribing;

        /// <summary>
        /// Raised when a final transcription result is available.
        /// </summary>
        public event EventHandler<TranscriptionEventArgs>? Transcribed;

        /// <summary>
        /// Raised when the transcription session status changes (started, stopped, errors).
        /// </summary>
        public event EventHandler<string>? StatusChanged;

        public SpeechTranscriptionService(AudioCaptureManager captureManager, SpeechConfig speechConfig)
        {
            this.captureManager = captureManager;
            this.speechConfig = speechConfig;
        }

        /// <summary>
        /// Starts real-time transcription on both loopback and microphone audio streams.
        /// Call this after AudioCaptureManager.StartCapture() so formats are known.
        /// </summary>
        public async Task StartAsync()
        {
            cts = new CancellationTokenSource();

            // Subscribe to audio data events from the capture manager
            captureManager.LoopbackDataAvailable += OnLoopbackData;
            captureManager.MicrophoneDataAvailable += OnMicrophoneData;

            // Start loopback transcriber
            var loopbackTask = StartTranscriberAsync("Loopback", AudioSource.Loopback);

            // Start microphone transcriber
            var microphoneTask = StartTranscriberAsync("Microphone", AudioSource.Microphone);

            await Task.WhenAll(loopbackTask, microphoneTask);
        }

        private async Task StartTranscriberAsync(string label, AudioSource source)
        {
            // Azure Speech SDK expects 16kHz, 16-bit, mono PCM for ConversationTranscriber
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            var pushStream = AudioInputStream.CreatePushStream(audioFormat);
            var audioConfig = AudioConfig.FromStreamInput(pushStream);

            var transcriber = new ConversationTranscriber(speechConfig, audioConfig);
            var stopSignal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (source == AudioSource.Loopback)
            {
                loopbackPushStream = pushStream;
                loopbackTranscriber = transcriber;
                stopLoopback = stopSignal;
            }
            else
            {
                microphonePushStream = pushStream;
                microphoneTranscriber = transcriber;
                stopMicrophone = stopSignal;
            }

            // Wire up events
            transcriber.Transcribing += (s, e) =>
            {
                Transcribing?.Invoke(this, new TranscriptionEventArgs(
                    e.Result.Text, e.Result.SpeakerId, label, isFinal: false));
            };

            transcriber.Transcribed += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    TimeSpan offset = TimeSpan.FromTicks(e.Result.OffsetInTicks);
                    string line = $"{offset:hh\\:mm\\:ss} | [{label}] Speaker={e.Result.SpeakerId}: {e.Result.Text}";
                    Console.WriteLine(line);

                    Transcribed?.Invoke(this, new TranscriptionEventArgs(
                        e.Result.Text, e.Result.SpeakerId, label, isFinal: true));
                }
            };

            transcriber.Canceled += (s, e) =>
            {
                string msg = $"[{label}] CANCELED: Reason={e.Reason} ErrorCode={e.ErrorCode} Details={e.ErrorDetails}";
                Console.WriteLine(msg);
                StatusChanged?.Invoke(this, msg);

                if (e.Reason == CancellationReason.Error)
                {
                    stopSignal.TrySetResult(0);
                }
            };

            transcriber.SessionStarted += (s, e) =>
            {
                string msg = $"[{label}] Session started: {e.SessionId}";
                Console.WriteLine(msg);
                StatusChanged?.Invoke(this, msg);
            };

            transcriber.SessionStopped += (s, e) =>
            {
                string msg = $"[{label}] Session stopped: {e.SessionId}";
                Console.WriteLine(msg);
                StatusChanged?.Invoke(this, msg);
                stopSignal.TrySetResult(1);
            };

            await transcriber.StartTranscribingAsync().ConfigureAwait(false);

            // Wait until cancellation or session stop
            try
            {
                var delayTask = Task.Delay(Timeout.Infinite, cts!.Token);
                await Task.WhenAny(stopSignal.Task, delayTask).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Expected on stop
            }

            await transcriber.StopTranscribingAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Pushes loopback audio data to the speech push stream, converting format as needed.
        /// WASAPI loopback is typically 32-bit float, 48kHz, stereo — must convert to 16kHz 16-bit mono.
        /// </summary>
        private void OnLoopbackData(object? sender, AudioDataEventArgs e)
        {
            if (loopbackPushStream == null || e.BytesRecorded == 0) return;

            byte[] converted = ConvertToPcm16kMono(e.Buffer, e.BytesRecorded, e.Format);
            if (converted.Length > 0)
            {
                loopbackPushStream.Write(converted);
            }
        }

        /// <summary>
        /// Pushes microphone audio data to the speech push stream, converting format as needed.
        /// Microphone is 44100Hz 16-bit stereo — must convert to 16kHz 16-bit mono.
        /// </summary>
        private void OnMicrophoneData(object? sender, AudioDataEventArgs e)
        {
            if (microphonePushStream == null || e.BytesRecorded == 0) return;

            byte[] converted = ConvertToPcm16kMono(e.Buffer, e.BytesRecorded, e.Format);
            if (converted.Length > 0)
            {
                microphonePushStream.Write(converted);
            }
        }

        /// <summary>
        /// Converts audio from any NAudio WaveFormat to 16kHz, 16-bit, mono PCM
        /// as required by Azure Speech SDK ConversationTranscriber.
        /// </summary>
        private static byte[] ConvertToPcm16kMono(byte[] buffer, int bytesRecorded, NAudio.Wave.WaveFormat sourceFormat)
        {
            // Step 1: Create a RawSourceWaveStream from the buffer
            using var sourceStream = new RawSourceWaveStream(
                new MemoryStream(buffer, 0, bytesRecorded), sourceFormat);

            // Step 2: Convert to PCM 16-bit if source is IEEE float
            IWaveProvider pcmProvider;
            if (sourceFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                pcmProvider = new WaveFloatTo16Provider(sourceStream);
            }
            else if (sourceFormat.BitsPerSample != 16)
            {
                // Handle other bit depths by converting to float first, then to 16-bit
                pcmProvider = new WaveFloatTo16Provider(
                    new Wave16ToFloatProvider(sourceStream));
            }
            else
            {
                pcmProvider = sourceStream;
            }

            // Step 3: Convert stereo to mono if needed
            if (pcmProvider.WaveFormat.Channels > 1)
            {
                pcmProvider = new StereoToMonoProvider16(pcmProvider);
            }

            // Step 4: Resample to 16kHz if needed
            if (pcmProvider.WaveFormat.SampleRate != 16000)
            {
                var targetFormat = new NAudio.Wave.WaveFormat(16000, 16, 1);
                using var resampler = new MediaFoundationResampler(pcmProvider, targetFormat)
                {
                    ResamplerQuality = 60
                };

                return ReadAllBytes(resampler);
            }

            return ReadAllBytes(pcmProvider);
        }

        /// <summary>
        /// Reads all available bytes from a wave provider.
        /// </summary>
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

        /// <summary>
        /// Stops transcription and releases resources.
        /// </summary>
        public async Task StopAsync()
        {
            // Unsubscribe from audio events
            captureManager.LoopbackDataAvailable -= OnLoopbackData;
            captureManager.MicrophoneDataAvailable -= OnMicrophoneData;

            // Signal cancellation to both transcription tasks
            cts?.Cancel();

            // Close push streams to signal end-of-audio to the SDK
            loopbackPushStream?.Close();
            microphonePushStream?.Close();

            // Wait for both stop signals
            if (stopLoopback != null)
                await stopLoopback.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (stopMicrophone != null)
                await stopMicrophone.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            StatusChanged?.Invoke(this, "Transcription stopped.");
        }

        public void Dispose()
        {
            cts?.Cancel();

            captureManager.LoopbackDataAvailable -= OnLoopbackData;
            captureManager.MicrophoneDataAvailable -= OnMicrophoneData;

            loopbackPushStream?.Close();
            microphonePushStream?.Close();
            loopbackTranscriber?.Dispose();
            microphoneTranscriber?.Dispose();
            cts?.Dispose();
        }
    }

    /// <summary>
    /// Event args for transcription results.
    /// </summary>
    public class TranscriptionEventArgs : EventArgs
    {
        public string Text { get; }
        public string SpeakerId { get; }
        public string Source { get; }
        public bool IsFinal { get; }

        public TranscriptionEventArgs(string text, string speakerId, string source, bool isFinal)
        {
            Text = text;
            SpeakerId = speakerId;
            Source = source;
            IsFinal = isFinal;
        }
    }
}
