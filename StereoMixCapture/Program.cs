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
