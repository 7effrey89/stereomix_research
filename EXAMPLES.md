# Audio Capture Examples

This document provides examples of how to use the Stereo Mix Audio Capture solution.

## Basic Usage Example

### 1. Simple Recording

The easiest way to start recording:

```bash
dotnet run --project StereoMixCapture/StereoMixCapture.csproj
```

Then follow the on-screen prompts:
- Press Enter to use default devices (device index 0 for both)
- Press Enter for current directory as output location
- Press any key when you want to stop recording

### 2. Selecting Specific Devices

When the application starts, it lists all available devices:

```
=== Audio Capture Devices ===

Internal Audio Devices (Loopback):
  [0] Speakers (Realtek High Definition Audio)
  [1] Headphones (USB Audio Device)

Microphone Input Devices:
  [0] Microphone (Realtek High Definition Audio)
  [1] USB Microphone
```

To record from specific devices:
- Enter `0` to use the first loopback device (Speakers)
- Enter `1` to use the second microphone device (USB Microphone)

### 3. Specifying Output Directory

```bash
# When prompted for output directory:
C:\Recordings\MySession

# The application will create:
# C:\Recordings\MySession\loopback_20260211_183000.wav
# C:\Recordings\MySession\microphone_20260211_183000.wav
```

## Use Case Examples

### Example 1: Screen Recording with Commentary

**Scenario**: Recording a tutorial video with both screen audio and your voice.

**Steps**:
1. Run the application
2. Select your main speakers as loopback device
3. Select your microphone as input device
4. Start recording
5. Open the application you want to demonstrate
6. Speak into your microphone while using the application
7. Press any key to stop
8. Choose 'y' to mix the files for a single audio track

**Result**: You'll get three files:
- System audio only
- Microphone audio only  
- Mixed audio with both

### Example 2: Podcast Recording

**Scenario**: Recording a podcast while playing background music from Spotify.

**Steps**:
1. Start playing music in Spotify
2. Run the audio capture application
3. Select default devices (or choose specific ones)
4. Record your podcast episode while music plays
5. Stop recording when done
6. Mix the files to create the final podcast audio

### Example 3: Gaming Session

**Scenario**: Recording game audio along with team chat/commentary.

**Steps**:
1. Run the application before starting your game
2. Select your headphone/speaker device for game audio
3. Select your microphone for voice
4. Start the game and play
5. All in-game sounds and your voice will be captured separately
6. Stop when gaming session is complete

### Example 4: Music Production

**Scenario**: Recording computer playback while adding live instrument input.

**Steps**:
1. Set up your DAW (Digital Audio Workstation) or music player
2. Connect your instrument to an audio interface (microphone input)
3. Run the capture application
4. Select your DAW output as loopback device
5. Select your instrument input as microphone device
6. Start recording and play along with the track
7. Mix the results for a combined recording

## Programmatic Usage

You can also use the classes directly in your own C# projects:

### Using AudioCaptureManager

```csharp
using StereoMixCapture;

// Create manager with output directory
var manager = new AudioCaptureManager("C:\\Recordings");

// Start capture with default devices
manager.StartCapture();

// ... do your recording ...

// Stop capture
manager.StopCapture();

// Don't forget to dispose
manager.Dispose();
```

### Using AudioMixer

```csharp
using StereoMixCapture;

// Mix two audio files
AudioMixer.MixAudioFiles(
    "loopback_20260211_183000.wav",
    "microphone_20260211_183000.wav",
    "mixed_output.wav"
);
```

### Listing Available Devices

```csharp
using StereoMixCapture;

// List all available audio devices
AudioCaptureManager.ListAudioDevices();
```

## Tips and Best Practices

### 1. Audio Quality
- For music: Use high sample rates (44100 Hz or 48000 Hz)
- For speech: 16000 Hz is usually sufficient
- Higher quality = larger file sizes

### 2. File Management
- Name your recordings descriptively
- Organize by date or project
- Clean up old recordings to save space

### 3. Testing
- Test your setup before important recordings
- Check audio levels in Windows Sound settings
- Ensure microphone isn't muted

### 4. Performance
- Close unnecessary applications to reduce CPU usage
- Ensure sufficient disk space for recordings
- Use SSD for better write performance

### 5. Privacy
- Be aware of what's being recorded (all system sounds)
- Close private communications before recording
- Review recordings before sharing

## Troubleshooting Examples

### Issue: No sound in loopback recording

**Solution**: 
- Ensure something is actually playing audio
- Check Windows volume mixer
- Verify the correct playback device is selected

### Issue: Microphone recording is silent

**Solution**:
- Check microphone is connected and working
- Verify it's not muted in Windows
- Test in Windows Sound settings first
- Check privacy settings for microphone access

### Issue: Files are huge

**Solution**:
- Use compressed formats (requires modifying code)
- Reduce sample rate for voice-only recordings
- Use lower bit depth if quality isn't critical

### Issue: Audio is out of sync

**Solution**:
- Both streams start at approximately the same time
- For critical sync, edit in audio software
- Consider using timecode for precision work

## Advanced Usage

### Custom Sample Rate

Modify `AudioCaptureManager.cs` to change the microphone sample rate:

```csharp
microphoneCapture = new WaveInEvent
{
    DeviceNumber = microphoneDeviceIndex,
    WaveFormat = new WaveFormat(48000, 24, 2) // 48kHz, 24-bit, stereo
};
```

### Multiple Simultaneous Captures

You can create multiple `AudioCaptureManager` instances to record from multiple device pairs simultaneously:

```csharp
var manager1 = new AudioCaptureManager("./Session1");
var manager2 = new AudioCaptureManager("./Session2");

manager1.StartCapture(0, 0); // First device pair
manager2.StartCapture(1, 1); // Second device pair

// ... record ...

manager1.StopCapture();
manager2.StopCapture();
```

## Output Format Specifications

All recordings are saved in WAV format:
- **Format**: PCM WAV
- **Loopback**: Matches system audio format (typically 44.1kHz or 48kHz, 16-bit, stereo)
- **Microphone**: 44.1kHz, 16-bit, stereo (configurable)
- **Mixed**: Matches the format of input files

## Command Line Reference

```bash
# Build the project
dotnet build StereoMixCapture/StereoMixCapture.csproj

# Run the project
dotnet run --project StereoMixCapture/StereoMixCapture.csproj

# Build for release
dotnet build -c Release StereoMixCapture/StereoMixCapture.csproj

# Run release version
dotnet run -c Release --project StereoMixCapture/StereoMixCapture.csproj

# Publish as single executable
dotnet publish -c Release -r win-x64 --self-contained
```
