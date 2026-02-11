using StereoMixCapture;
using System;

namespace StereoMixCapture
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("   Stereo Mix Capture - Audio Recording");
            Console.WriteLine("   Captures Internal Audio + Microphone");
            Console.WriteLine("===========================================\n");

            try
            {
                // List available devices
                AudioCaptureManager.ListAudioDevices();

                // Get user input for device selection
                Console.Write("Select loopback device index (or press Enter for default 0): ");
                string? loopbackInput = Console.ReadLine();
                int loopbackIndex = 0;
                if (!string.IsNullOrWhiteSpace(loopbackInput) && !int.TryParse(loopbackInput, out loopbackIndex))
                {
                    Console.WriteLine("Invalid input. Using default device 0.");
                    loopbackIndex = 0;
                }

                Console.Write("Select microphone device index (or press Enter for default 0): ");
                string? micInput = Console.ReadLine();
                int micIndex = 0;
                if (!string.IsNullOrWhiteSpace(micInput) && !int.TryParse(micInput, out micIndex))
                {
                    Console.WriteLine("Invalid input. Using default device 0.");
                    micIndex = 0;
                }

                Console.Write("\nEnter output directory (or press Enter for current directory): ");
                string? outputDir = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(outputDir))
                {
                    outputDir = ".";
                }

                // Ensure output directory exists
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine($"Created output directory: {outputDir}");
                }

                // Create and start audio capture
                using var captureManager = new AudioCaptureManager(outputDir);
                captureManager.StartCapture(loopbackIndex, micIndex);

                // Wait for user to stop
                Console.ReadKey();

                // Stop capture
                captureManager.StopCapture();

                // Ask if user wants to mix the files
                Console.Write("\nWould you like to mix the captured audio files? (y/n): ");
                string? mixResponse = Console.ReadLine();
                
                if (mixResponse?.ToLower() == "y")
                {
                    // Find the most recent loopback and microphone files
                    var loopbackFiles = Directory.GetFiles(outputDir, "loopback_*.wav")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToList();
                    
                    var microphoneFiles = Directory.GetFiles(outputDir, "microphone_*.wav")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToList();

                    if (loopbackFiles.Any() && microphoneFiles.Any())
                    {
                        string mixedOutputPath = Path.Combine(outputDir, $"mixed_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                        Console.WriteLine($"\nMixing audio files...");
                        AudioMixer.MixAudioFiles(loopbackFiles[0], microphoneFiles[0], mixedOutputPath);
                    }
                    else
                    {
                        Console.WriteLine("Could not find both loopback and microphone files to mix.");
                    }
                }

                Console.WriteLine("\nDone! Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("\nPress any key to exit.");
                Console.ReadKey();
            }
        }
    }
}
