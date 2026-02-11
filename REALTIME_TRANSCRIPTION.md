# Real-Time Transcription with Audio Capture

This guide explains how to use the Stereo Mix Audio Capture solution for real-time transcription scenarios.

## Overview

The `AudioCaptureManager` class supports real-time audio processing with low latency, making it suitable for:
- Real-time speech-to-text transcription
- Live audio analysis
- Streaming audio processing
- Real-time audio monitoring

## Latency Characteristics

### WASAPI Loopback (Internal Audio)
- **Typical latency**: 10-30ms
- **Buffer size**: Automatically managed by WASAPI
- **Best for**: High-quality, low-latency capture of system audio

### WaveInEvent (Microphone)
- **Typical latency**: 20-100ms (configurable)
- **Buffer size**: Configurable via `MicrophoneBufferMilliseconds` property
- **Recommended for real-time**: 20-50ms
- **Default**: 100ms (balanced for most use cases)

## Real-Time Usage Example

### Basic Real-Time Capture

```csharp
using StereoMixCapture;

class RealtimeTranscription
{
    static void Main()
    {
        // Create manager without file output for real-time only
        var manager = new AudioCaptureManager("./output")
        {
            EnableFileOutput = false, // Disable file writing for better performance
            MicrophoneBufferMilliseconds = 30 // Lower latency (30ms)
        };

        // Subscribe to real-time audio events
        manager.MicrophoneDataAvailable += OnMicrophoneData;
        manager.LoopbackDataAvailable += OnLoopbackData;

        // Start capture
        manager.StartCapture(loopbackDeviceIndex: 0, microphoneDeviceIndex: 0);

        Console.WriteLine("Real-time capture active. Press any key to stop...");
        Console.ReadKey();

        manager.StopCapture();
        manager.Dispose();
    }

    static void OnMicrophoneData(object? sender, AudioDataEventArgs e)
    {
        // Process audio data in real-time
        Console.WriteLine($"Microphone: {e.BytesRecorded} bytes, Format: {e.Format}");
        
        // Send to transcription service
        // await TranscribeAsync(e.Buffer, e.BytesRecorded, e.Format);
    }

    static void OnLoopbackData(object? sender, AudioDataEventArgs e)
    {
        // Process internal audio in real-time
        Console.WriteLine($"Loopback: {e.BytesRecorded} bytes");
    }
}
```

### Real-Time with File Recording

```csharp
// Keep file output enabled while also processing in real-time
var manager = new AudioCaptureManager("./recordings")
{
    EnableFileOutput = true, // Keep recording to files
    MicrophoneBufferMilliseconds = 30 // Low latency for real-time
};

manager.MicrophoneDataAvailable += (sender, e) =>
{
    // Audio is being written to file AND available here for real-time processing
    ProcessForTranscription(e.Buffer, e.BytesRecorded, e.Format);
};

manager.StartCapture();
```

### Integration with Azure Speech Services

```csharp
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using StereoMixCapture;

class AzureTranscription
{
    private AudioCaptureManager captureManager;
    private PushAudioInputStream pushStream;
    private SpeechRecognizer recognizer;

    public async Task StartTranscriptionAsync()
    {
        // Setup Azure Speech
        var speechConfig = SpeechConfig.FromSubscription("YourKey", "YourRegion");
        
        // Create push stream for audio input
        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(44100, 16, 2);
        pushStream = AudioInputStream.CreatePushStream(audioFormat);
        
        var audioConfig = AudioConfig.FromStreamInput(pushStream);
        recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        // Handle recognition results
        recognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"Transcription: {e.Result.Text}");
            }
        };

        // Start continuous recognition
        await recognizer.StartContinuousRecognitionAsync();

        // Setup audio capture
        captureManager = new AudioCaptureManager()
        {
            EnableFileOutput = false, // Real-time only
            MicrophoneBufferMilliseconds = 30 // Low latency
        };

        captureManager.MicrophoneDataAvailable += (sender, e) =>
        {
            // Push audio data to Azure Speech Service
            pushStream.Write(e.Buffer, e.BytesRecorded);
        };

        captureManager.StartCapture(microphoneDeviceIndex: 0);
    }

    public async Task StopTranscriptionAsync()
    {
        await recognizer.StopContinuousRecognitionAsync();
        captureManager.StopCapture();
        captureManager.Dispose();
        pushStream.Close();
    }
}
```

### Integration with OpenAI Whisper

```csharp
using StereoMixCapture;
using System.Collections.Generic;

class WhisperTranscription
{
    private AudioCaptureManager captureManager;
    private List<byte> audioBuffer = new List<byte>();
    private const int CHUNK_SIZE_SECONDS = 5;
    private int bytesPerSecond;

    public void StartCapture()
    {
        captureManager = new AudioCaptureManager()
        {
            EnableFileOutput = false,
            MicrophoneBufferMilliseconds = 50 // Good balance for Whisper
        };

        captureManager.MicrophoneDataAvailable += OnAudioData;
        captureManager.StartCapture();

        // Calculate bytes per second (44100 Hz * 2 bytes * 2 channels)
        bytesPerSecond = 44100 * 2 * 2;
    }

    private void OnAudioData(object? sender, AudioDataEventArgs e)
    {
        // Accumulate audio data
        audioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));

        // Process in 5-second chunks
        int chunkSize = bytesPerSecond * CHUNK_SIZE_SECONDS;
        if (audioBuffer.Count >= chunkSize)
        {
            var chunk = audioBuffer.Take(chunkSize).ToArray();
            audioBuffer.RemoveRange(0, chunkSize);

            // Send to Whisper API
            _ = TranscribeChunkAsync(chunk, e.Format);
        }
    }

    private async Task TranscribeChunkAsync(byte[] audioData, WaveFormat format)
    {
        // Convert to WAV format and send to Whisper API
        // var transcription = await whisperClient.TranscribeAsync(audioData);
        // Console.WriteLine($"Transcription: {transcription}");
    }
}
```

## Performance Optimization for Real-Time

### 1. Disable File Output When Not Needed
```csharp
manager.EnableFileOutput = false; // Reduces I/O overhead
```

### 2. Adjust Buffer Size
```csharp
// For lowest latency (more CPU intensive)
manager.MicrophoneBufferMilliseconds = 20;

// For balanced performance
manager.MicrophoneBufferMilliseconds = 50;

// For lower CPU usage (higher latency)
manager.MicrophoneBufferMilliseconds = 100;
```

### 3. Process Audio Asynchronously
```csharp
manager.MicrophoneDataAvailable += async (sender, e) =>
{
    // Don't block the audio thread
    await Task.Run(() => ProcessAudio(e.Buffer, e.BytesRecorded));
};
```

### 4. Use Efficient Buffer Handling
```csharp
manager.MicrophoneDataAvailable += (sender, e) =>
{
    // Copy buffer immediately if processing asynchronously
    byte[] copy = new byte[e.BytesRecorded];
    Array.Copy(e.Buffer, copy, e.BytesRecorded);
    
    // Process the copy
    ProcessAudioAsync(copy);
};
```

## Latency Measurement

Monitor actual latency in your application:

```csharp
using System.Diagnostics;

var stopwatch = new Stopwatch();
long eventCount = 0;

manager.MicrophoneDataAvailable += (sender, e) =>
{
    stopwatch.Stop();
    eventCount++;
    
    if (eventCount % 10 == 0) // Log every 10 events
    {
        Console.WriteLine($"Time since last event: {stopwatch.ElapsedMilliseconds}ms");
    }
    
    stopwatch.Restart();
    
    // Your processing here
};

stopwatch.Start();
manager.StartCapture();
```

## Best Practices for Real-Time Transcription

1. **Test Latency Requirements**: Different transcription services have different optimal buffer sizes
2. **Handle Backpressure**: If processing can't keep up, consider dropping frames or increasing buffer size
3. **Monitor CPU Usage**: Lower buffer sizes increase CPU load
4. **Network Considerations**: If sending to cloud services, account for network latency
5. **Error Handling**: Always handle exceptions in event handlers to prevent capture interruption
6. **Thread Safety**: The DataAvailable events fire on audio thread - keep processing minimal

## Audio Format Details

### Microphone Capture
- **Sample Rate**: 44100 Hz (CD quality)
- **Bit Depth**: 16-bit
- **Channels**: 2 (Stereo)
- **Data Rate**: ~176 KB/s

### Loopback Capture
- **Sample Rate**: Matches system audio (typically 44100 or 48000 Hz)
- **Bit Depth**: Varies by system (typically 16 or 24-bit)
- **Channels**: Matches output device (typically stereo)

## Troubleshooting

### High Latency
- Reduce `MicrophoneBufferMilliseconds`
- Disable file output if not needed
- Check for CPU bottlenecks in processing code
- Ensure audio thread isn't blocked

### Audio Dropouts
- Increase `MicrophoneBufferMilliseconds`
- Move heavy processing to background threads
- Check system CPU usage

### Synchronization Issues
- Both streams start simultaneously but may drift over time
- Use timestamps if precise sync is critical
- Consider using WASAPI for both sources for better sync

## Example: Complete Transcription Application

See the `examples/RealtimeTranscription` folder for a complete working example that demonstrates:
- Real-time audio capture
- Azure Speech Services integration
- Latency monitoring
- Error handling
- Performance optimization

## Summary

The audio capture manager provides:
- ✅ Low latency (20-30ms achievable)
- ✅ Real-time data access via events
- ✅ Configurable buffer sizes
- ✅ Optional file recording
- ✅ Support for both internal audio and microphone
- ✅ Compatible with popular transcription services

This makes it well-suited for real-time transcription scenarios while maintaining flexibility for other use cases.
