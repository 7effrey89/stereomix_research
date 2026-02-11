# Quick Start Guide

## Installation & First Run

### Step 1: Prerequisites
- Windows 10 or Windows 11
- .NET 8.0 SDK or later ([Download](https://dotnet.microsoft.com/download))

### Step 2: Clone and Build
```bash
# Clone the repository
git clone https://github.com/7effrey89/stereomix_research.git
cd stereomix_research

# Build the project
dotnet build StereoMixCapture/StereoMixCapture.csproj

# Or build for release
dotnet build -c Release StereoMixCapture/StereoMixCapture.csproj
```

### Step 3: Run the Application
```bash
dotnet run --project StereoMixCapture/StereoMixCapture.csproj
```

## First Recording Session

1. **Application starts and shows available devices:**
   ```
   ===========================================
      Stereo Mix Capture - Audio Recording
      Captures Internal Audio + Microphone
   ===========================================

   === Audio Capture Devices ===

   Internal Audio Devices (Loopback):
     [0] Speakers (Realtek High Definition Audio)
     [1] Headphones (USB Audio Device)

   Microphone Input Devices:
     [0] Microphone (Realtek High Definition Audio)
     [1] USB Microphone
   ```

2. **Select your devices:**
   ```
   Select loopback device index (or press Enter for default 0): 0
   Select microphone device index (or press Enter for default 0): 0
   Enter output directory (or press Enter for current directory): 
   ```

3. **Recording starts automatically:**
   ```
   Starting loopback capture: Speakers (Realtek High Definition Audio)
   Output file: ./loopback_20260211_183000.wav
   Starting microphone capture: Device 0
   Output file: ./microphone_20260211_183000.wav

   Capturing audio... Press any key to stop.
   ```

4. **Do your recording:**
   - Play audio on your computer (YouTube, Spotify, etc.)
   - Speak into your microphone
   - Both will be recorded simultaneously

5. **Stop recording:**
   - Press any key
   ```
   Stopping audio capture...
   Loopback capture stopped.
   Microphone capture stopped.
   Audio capture stopped and files saved.
   ```

6. **Optional mixing:**
   ```
   Would you like to mix the captured audio files? (y/n): y
   Mixing audio files...
   Mixed audio saved to: ./mixed_20260211_183000.wav
   
   Done! Press any key to exit.
   ```

## Output Files

After recording, you'll have:
- `loopback_YYYYMMDD_HHMMSS.wav` - System audio only
- `microphone_YYYYMMDD_HHMMSS.wav` - Microphone audio only
- `mixed_YYYYMMDD_HHMMSS.wav` - Combined audio (if you chose to mix)

## Common First-Time Issues

### Issue: "No audio devices found"
**Solution:** 
- Make sure your audio devices are connected and enabled in Windows Sound settings
- Right-click the speaker icon in taskbar → Sound settings → Check device status

### Issue: "Access denied" or permission errors
**Solution:**
- Try running PowerShell or Command Prompt as Administrator
- Check Windows Privacy Settings → Microphone → Allow apps to access microphone

### Issue: No sound in the recorded files
**Solution:**
- **For loopback:** Make sure audio is actually playing during recording
- **For microphone:** Test microphone in Windows Sound settings first
- Check that devices aren't muted in Windows volume mixer

### Issue: Application won't start on non-Windows systems
**Solution:**
- This application only works on Windows (uses Windows audio APIs)
- WASAPI and WaveIn are Windows-specific technologies

## Next Steps

- Read [EXAMPLES.md](EXAMPLES.md) for detailed use cases
- Read [README.md](README.md) for technical documentation
- Experiment with different audio sources
- Try mixing different recordings together

## Tips for Best Results

1. **Test first:** Do a short 5-second test recording to verify everything works
2. **Monitor levels:** Check Windows volume mixer to ensure audio isn't too loud/quiet
3. **Close unnecessary apps:** Reduce CPU usage for smoother recording
4. **Use good equipment:** Quality microphone = quality recording
5. **Quiet environment:** Reduce background noise for clearer microphone audio

## Need Help?

- Check the troubleshooting section in [README.md](README.md)
- Review examples in [EXAMPLES.md](EXAMPLES.md)
- Open an issue on GitHub if you encounter problems

## Happy Recording! 🎙️🎵
