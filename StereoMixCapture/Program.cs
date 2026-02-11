using StereoMixCapture;
using Azure.Identity;
using Microsoft.CognitiveServices.Speech;
using System;

namespace StereoMixCapture
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("   Stereo Mix Capture - Audio Recording");
            Console.WriteLine("   Captures Internal Audio + Microphone");
            Console.WriteLine("   With Real-Time AI Speech Transcription");
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

                // Azure AI Foundry Speech configuration
                Console.Write("\nEnable real-time transcription? (y/n, default n): ");
                string? enableTranscription = Console.ReadLine();

                SpeechTranscriptionService? transcriptionService = null;

                // Create and start audio capture
                using var captureManager = new AudioCaptureManager(outputDir);

                if (enableTranscription?.Trim().ToLower() == "y")
                {
                    Console.Write("Enter transcription language (e.g. en-US, default en-US): ");
                    string? language = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(language))
                    {
                        language = "en-US";
                    }

                    // Authenticate with Entra ID interactive browser login
                    Console.WriteLine("\nAuthenticating with Azure Entra ID (browser will open)...");
                    var credential = new InteractiveBrowserCredential();

                    // Use the Azure AI Foundry custom endpoint
                    var speechEndpoint = new Uri("https://na-foundry.cognitiveservices.azure.com");
                    var speechConfig = SpeechConfig.FromEndpoint(speechEndpoint, credential);
                    speechConfig.SpeechRecognitionLanguage = language;

                    Console.WriteLine($"Using endpoint: {speechEndpoint}");

                    transcriptionService = new SpeechTranscriptionService(captureManager, speechConfig);

                    transcriptionService.Transcribing += (s, e) =>
                    {
                        Console.Write($"\r  TRANSCRIBING [{e.Source}]: {e.Text}                    ");
                    };

                    transcriptionService.StatusChanged += (s, e) =>
                    {
                        Console.WriteLine($"\n  STATUS: {e}");
                    };
                }

                // Start audio capture first (so formats are determined)
                captureManager.StartCapture(loopbackIndex, micIndex);

                // Then start transcription if configured
                if (transcriptionService != null)
                {
                    Console.WriteLine("\nStarting real-time transcription...\n");
                    _ = transcriptionService.StartAsync();
                    // Give the transcribers a moment to connect
                    await Task.Delay(500);
                }

                // Wait for user to stop
                Console.ReadKey();

                // Stop transcription first, then capture
                if (transcriptionService != null)
                {
                    Console.WriteLine("\nStopping transcription...");
                    try
                    {
                        await transcriptionService.StopAsync();
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Transcription stop timed out, continuing...");
                    }
                    transcriptionService.Dispose();
                }

                // Stop capture (also auto-mixes files)
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
