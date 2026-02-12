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
    /// Uses a single ConversationTranscriber fed from the mixed (loopback + microphone) audio
    /// stream produced by AudioCaptureManager, saving cost by running only one transcription session.
    /// </summary>
    public class SpeechTranscriptionService : IDisposable
    {
        private readonly AudioCaptureManager captureManager;
        private readonly SpeechConfig speechConfig;

        private PushAudioInputStream? pushStream;
        private ConversationTranscriber? transcriber;

        private CancellationTokenSource? cts;
        private TaskCompletionSource<int>? stopSignal;

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

            // Subscribe early so AudioCaptureManager sees the handler when StartCapture is called
            captureManager.MixedDataAvailable += OnMixedData;
        }

        /// <summary>
        /// Starts real-time transcription on the mixed audio stream.
        /// Call this after AudioCaptureManager.StartCapture() so formats are known.
        /// </summary>
        public async Task StartAsync()
        {
            cts = new CancellationTokenSource();

            await StartTranscriberAsync();
        }

        private async Task StartTranscriberAsync()
        {
            const string label = "Mixed";

            // Azure Speech SDK expects 16kHz, 16-bit, mono PCM for ConversationTranscriber
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            pushStream = AudioInputStream.CreatePushStream(audioFormat);
            var audioConfig = AudioConfig.FromStreamInput(pushStream);

            transcriber = new ConversationTranscriber(speechConfig, audioConfig);
            stopSignal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        /// Pushes mixed audio data (already 16kHz 16-bit mono PCM) to the speech push stream.
        /// </summary>
        private void OnMixedData(object? sender, AudioDataEventArgs e)
        {
            if (pushStream == null || e.BytesRecorded == 0) return;
            pushStream.Write(e.Buffer, e.BytesRecorded);
        }

        /// <summary>
        /// Stops transcription and releases resources.
        /// </summary>
        public async Task StopAsync()
        {
            // Unsubscribe from audio events
            captureManager.MixedDataAvailable -= OnMixedData;

            // Signal cancellation
            cts?.Cancel();

            // Close push stream to signal end-of-audio to the SDK
            pushStream?.Close();

            // Wait for the stop signal
            if (stopSignal != null)
                await stopSignal.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            StatusChanged?.Invoke(this, "Transcription stopped.");
        }

        public void Dispose()
        {
            cts?.Cancel();

            captureManager.MixedDataAvailable -= OnMixedData;

            pushStream?.Close();
            transcriber?.Dispose();
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
