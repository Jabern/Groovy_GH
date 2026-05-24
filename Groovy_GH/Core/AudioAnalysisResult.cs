// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Groovy_GH.Core
{
    public class AudioAnalysisFrame
    {
        [JsonPropertyName("time")]
        public double Time { get; set; }

        [JsonPropertyName("rms")]
        public double Rms { get; set; }

        [JsonPropertyName("onsetStrength")]
        public double OnsetStrength { get; set; }

        [JsonPropertyName("isBeat")]
        public double IsBeat { get; set; }

        [JsonPropertyName("bands")]
        public double[] Bands { get; set; } = Array.Empty<double>();

        [JsonPropertyName("spectralCentroid")]
        public double SpectralCentroid { get; set; }

        [JsonPropertyName("spectralFlux")]
        public double SpectralFlux { get; set; }

        [JsonPropertyName("spectralRolloff")]
        public double SpectralRolloff { get; set; }
    }

    public class AudioAnalysisResult
    {
        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("sampleRate")]
        public int SampleRate { get; set; }

        [JsonPropertyName("numFrames")]
        public int NumFrames { get; set; }

        [JsonPropertyName("frameDuration")]
        public double FrameDuration { get; set; }

        [JsonPropertyName("numBands")]
        public int NumBands { get; set; }

        [JsonPropertyName("frames")]
        public List<AudioAnalysisFrame> Frames { get; set; } = new();

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        public bool HasError => !string.IsNullOrEmpty(Error);

        public static AudioAnalysisResult FromJson(string json)
        {
            return JsonSerializer.Deserialize<AudioAnalysisResult>(json)
                   ?? new AudioAnalysisResult { Error = "Failed to parse JSON" };
        }

        public AudioAnalysisFrame GetFrameAtTime(double timeSeconds)
        {
            if (Frames.Count == 0) return new AudioAnalysisFrame();

            int idx = (int)(timeSeconds / FrameDuration);
            idx = MathUtils.Clamp(idx, 0, Frames.Count - 1);
            return Frames[idx];
        }

// linear interpolation between two frames, frac is how far between idx0 and idx1
        public (AudioAnalysisFrame a, AudioAnalysisFrame b, double t) GetInterpolatedFrame(double timeSeconds)
        {
            if (Frames.Count == 0)
                return (new AudioAnalysisFrame(), new AudioAnalysisFrame(), 0);

            double pos = timeSeconds / FrameDuration;
            int idx0 = (int)pos;
            int idx1 = idx0 + 1;
            double frac = pos - idx0;

            idx0 = MathUtils.Clamp(idx0, 0, Frames.Count - 1);
            idx1 = MathUtils.Clamp(idx1, 0, Frames.Count - 1);

            return (Frames[idx0], Frames[idx1], frac);
        }
    }
}
