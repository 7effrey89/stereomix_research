# Stereo Mix Audio Capture

A C# solution that captures both internal audio (stereo mix/loopback) and microphone inputs on Windows 11/10, similar to the classic Stereo Mix functionality.

## Features

- **Internal Audio Capture**: Captures system audio (what you hear) using WASAPI Loopback
- **Microphone Input Capture**: Captures audio from microphone or other input devices
- **Simultaneous Recording**: Records both sources at the same time in separate files
- **Audio Mixing**: Optionally mix both audio streams into a single output file
- **Device Selection**: Choose specific audio devices for recording
- **Real-Time Audio Access**: Low-latency events for real-time processing and transcription (20-30ms)
- **Configurable Latency**: Adjustable buffer sizes for latency vs. performance trade-offs
- **Flexible Output**: Optional file recording - supports real-time-only or file+real-time modes
- **Cross-compatible**: Works on Windows 10 and Windows 11

## Requirements

- Windows 10 or Windows 11
- .NET 8.0 SDK or later
- Audio playback and recording devices

## Installation

1. Clone the repository:
```bash
git clone https://github.com/7effrey89/stereomix_research.git
cd stereomix_research
```

2. Build the solution:
```bash
dotnet build StereoMixCapture/StereoMixCapture.csproj
```

3. Run the application:
```bash
dotnet run --project StereoMixCapture/StereoMixCapture.csproj
```

## Usage

1. **Launch the application** - It will display all available audio devices
2. **Select devices**:
   - Choose a loopback device (internal audio source)
   - Choose a microphone device (input source)
   - Specify output directory (optional)
3. **Start recording** - The application will begin capturing both audio streams
4. **Stop recording** - Press any key to stop the capture
5. **Mix audio** (optional) - Choose to mix both recordings into a single file

### Output Files

The application creates the following files:
- `loopback_YYYYMMDD_HHMMSS.wav` - Internal audio recording
- `microphone_YYYYMMDD_HHMMSS.wav` - Microphone recording
- `mixed_YYYYMMDD_HHMMSS.wav` - Combined audio (if mixing is selected)

## Technical Details

### Audio Capture Technologies

- **WASAPI Loopback**: Used for capturing internal audio (what's playing on the system)
- **WaveIn API**: Used for capturing microphone and line-in audio
- **NAudio Library**: Provides the audio processing capabilities
- **Real-Time Events**: Exposes audio data via events for low-latency processing

### Latency Performance

- **WASAPI Loopback**: 10-30ms typical latency
- **Microphone (WaveInEvent)**: 20-100ms configurable (default: 100ms)
- **Recommended for Real-Time Transcription**: 20-50ms buffer size
- See [REALTIME_TRANSCRIPTION.md](REALTIME_TRANSCRIPTION.md) for detailed guidance

### How It Works

1. **Internal Audio Capture (Loopback)**:
   - Uses Windows Audio Session API (WASAPI) in loopback mode
   - Captures the mixed audio output that would go to speakers
   - No need for "Stereo Mix" device to be enabled in Windows
   - Low latency suitable for real-time processing

2. **Microphone Capture**:
   - Uses standard WaveIn API for input devices
   - Supports any audio input device (microphone, line-in, etc.)
   - Configurable sample rate and quality

3. **Audio Mixing**:
   - Combines both audio streams into a single file
   - Handles format conversion automatically
   - Outputs standard WAV format

## Code Structure

```
StereoMixCapture/
├── Program.cs                  # Main application entry point
├── AudioCaptureManager.cs      # Manages audio capture operations
├── AudioMixer.cs               # Handles audio mixing functionality
└── StereoMixCapture.csproj     # Project configuration
```

## Use Cases

- **Real-Time Transcription**: Low-latency audio capture for speech-to-text services (Azure Speech, OpenAI Whisper, etc.)
- **Screen Recording**: Capture both system audio and commentary
- **Podcasting**: Record application audio along with microphone input
- **Gaming**: Record game audio and voice chat simultaneously
- **Tutorials**: Capture software audio and narration together
- **Music Production**: Record computer playback with live input
- **Live Streaming**: Real-time audio processing for streaming applications

## Dependencies

- NAudio 2.2.1 - Audio processing library

## Limitations

- Windows-only (uses Windows-specific audio APIs)
- Requires administrative privileges in some configurations
- Audio device must be active and not exclusively locked by another application

## Troubleshooting

### No audio devices found
- Ensure your audio devices are enabled and working
- Check Windows Sound settings
- Try running as administrator

### Access denied errors
- Close other applications that might be using the audio devices
- Run the application as administrator
- Check Windows privacy settings for microphone access

### Poor audio quality
- Check the recording format settings
- Ensure your audio drivers are up to date
- Adjust the sample rate in the code if needed

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License

This project is provided as-is for educational and personal use.

## Acknowledgments

- Built with [NAudio](https://github.com/naudio/NAudio) - An open-source .NET audio library