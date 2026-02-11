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

                // Resample both sources to a common format so MixingSampleProvider accepts them.
                // Use the higher sample rate of the two inputs to avoid losing quality.
                int targetSampleRate = Math.Max(reader1.WaveFormat.SampleRate, reader2.WaveFormat.SampleRate);
                int targetChannels = Math.Max(reader1.WaveFormat.Channels, reader2.WaveFormat.Channels);

                ISampleProvider source1 = reader1;
                ISampleProvider source2 = reader2;

                if (source1.WaveFormat.SampleRate != targetSampleRate)
                    source1 = new WdlResamplingSampleProvider(source1, targetSampleRate);
                if (source2.WaveFormat.SampleRate != targetSampleRate)
                    source2 = new WdlResamplingSampleProvider(source2, targetSampleRate);

                if (source1.WaveFormat.Channels == 1 && targetChannels == 2)
                    source1 = new MonoToStereoSampleProvider(source1);
                if (source2.WaveFormat.Channels == 1 && targetChannels == 2)
                    source2 = new MonoToStereoSampleProvider(source2);

                // Create a mixer with both audio sources
                var mixer = new MixingSampleProvider(new[] { source1, source2 });

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
