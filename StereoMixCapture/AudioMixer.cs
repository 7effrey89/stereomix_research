using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace StereoMixCapture
{
    /// <summary>
    /// Provides functionality to mix multiple audio streams together.
    /// </summary>
    public class AudioMixer
    {
        /// <summary>
        /// Mixes two audio files together into a single output file.
        /// </summary>
        public static void MixAudioFiles(string file1Path, string file2Path, string outputPath)
        {
            if (!File.Exists(file1Path) || !File.Exists(file2Path))
            {
                Console.WriteLine("One or both input files do not exist.");
                return;
            }

            try
            {
                using var reader1 = new AudioFileReader(file1Path);
                using var reader2 = new AudioFileReader(file2Path);
                
                // Create a mixer with both audio sources
                var mixer = new MixingSampleProvider(new[] { reader1, reader2 });

                // Write mixed audio to file
                WaveFileWriter.CreateWaveFile16(outputPath, mixer);
                
                Console.WriteLine($"Mixed audio saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error mixing audio files: {ex.Message}");
                Console.WriteLine($"Details: {ex.StackTrace}");
            }
        }
    }
}
