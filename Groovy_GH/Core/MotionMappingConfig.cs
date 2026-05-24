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
    public enum ResponseCurve { Linear, Exponential, Sqrt }

    public class PanelGroupConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("band")]
        public string Band { get; set; } = "mid";

        [JsonPropertyName("axis")]
        public double[] Axis { get; set; } = { 0, 0, 1 };

        [JsonPropertyName("rangeMm")]
        public double[] RangeMm { get; set; } = { 0, 400 };

        [JsonPropertyName("responseCurve")]
        public string ResponseCurveStr { get; set; } = "linear";

        public ResponseCurve ResponseCurve =>
            ResponseCurveStr?.ToLowerInvariant() switch
            {
                "exponential" => ResponseCurve.Exponential,
                "sqrt" => ResponseCurve.Sqrt,
                _ => ResponseCurve.Linear
            };

        [JsonPropertyName("exponent")]
        public double Exponent { get; set; } = 1.5;

        [JsonPropertyName("beatEnabled")]
        public bool BeatEnabled { get; set; } = false;
    }

    public class BeatImpulseConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("threshold")]
        public double Threshold { get; set; } = 0.5;

        [JsonPropertyName("magnitudeMm")]
        public double MagnitudeMm { get; set; } = 300;

        [JsonPropertyName("attackFrames")]
        public int AttackFrames { get; set; } = 2;

        [JsonPropertyName("decayFrames")]
        public int DecayFrames { get; set; } = 8;
    }

    public class SmoothingConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("windowSize")]
        public int WindowSize { get; set; } = 3;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "movingAverage";
    }

    public class FrequencyBandDef
    {
        [JsonPropertyName("lowHz")]
        public double LowHz { get; set; }

        [JsonPropertyName("highHz")]
        public double HighHz { get; set; }
    }

    public class MotionMappingConfig
    {
        [JsonPropertyName("preset")]
        public string Preset { get; set; } = "Default";

        [JsonPropertyName("fps")]
        public int Fps { get; set; } = 30;

        [JsonPropertyName("numPanels")]
        public int NumPanels { get; set; } = 100;

        [JsonPropertyName("panelGroupDistribution")]
        public Dictionary<string, double> PanelGroupDistribution { get; set; } = new()
        {
            ["bass"] = 0.25,
            ["mid"] = 0.50,
            ["treble"] = 0.25,
        };

        [JsonPropertyName("frequencyBands")]
        public Dictionary<string, double[]> FrequencyBands { get; set; } = new()
        {
            ["bass"] = new[] { 20.0, 250.0 },
            ["mid"] = new[] { 250.0, 4000.0 },
            ["treble"] = new[] { 4000.0, 20000.0 },
        };

        [JsonPropertyName("panelGroups")]
        public List<PanelGroupConfig> PanelGroups { get; set; } = new();

        [JsonPropertyName("beatImpulse")]
        public BeatImpulseConfig BeatImpulse { get; set; } = new();

        [JsonPropertyName("smoothing")]
        public SmoothingConfig Smoothing { get; set; } = new();

        [JsonIgnore]
        public int Version { get; set; }

        public void IncrementVersion() => Version++;

        public static MotionMappingConfig FromJson(string json)
        {
            return JsonSerializer.Deserialize<MotionMappingConfig>(json) ?? CreateDefault();
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }

        public static MotionMappingConfig CreateDefault()
        {
            return new MotionMappingConfig
            {
                Preset = "Default",
                Fps = 30,
                NumPanels = 100,
                PanelGroupDistribution = new Dictionary<string, double>
                {
                    ["bass"] = 0.25,
                    ["mid"] = 0.50,
                    ["treble"] = 0.25,
                },
                FrequencyBands = new Dictionary<string, double[]>
                {
                    ["bass"] = new[] { 20.0, 250.0 },
                    ["mid"] = new[] { 250.0, 4000.0 },
                    ["treble"] = new[] { 4000.0, 20000.0 },
                },
                PanelGroups = new List<PanelGroupConfig>
                {
                    new() { Name = "Bass Panels", Band = "bass", RangeMm = new[] { 0.0, 600.0 }, ResponseCurveStr = "exponential", Exponent = 2.0, BeatEnabled = true },
                    new() { Name = "Mid Panels", Band = "mid", RangeMm = new[] { 0.0, 400.0 }, ResponseCurveStr = "linear" },
                    new() { Name = "Treble Panels", Band = "treble", RangeMm = new[] { 0.0, 200.0 }, ResponseCurveStr = "sqrt" },
                },
                BeatImpulse = new BeatImpulseConfig(),
                Smoothing = new SmoothingConfig(),
            };
        }
    }
}
