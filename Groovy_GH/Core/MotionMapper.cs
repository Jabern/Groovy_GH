// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Groovy_GH.Core
{
    public class MotionOutput
    {
        public List<int> PanelIndices { get; set; } = new();
        public List<Vector3d> MotionVectors { get; set; } = new();
        public double TimeSeconds { get; set; }
    }

    public class MotionMapper
    {
        private readonly MotionMappingConfig _config;
        private readonly AudioAnalysisResult _analysis;
        private readonly List<List<int>> _panelGroups;
        private readonly int _numBands;
        private readonly Dictionary<int, Queue<double>> _smoothBufs = new();
        private readonly Dictionary<int, double> _smoothSums = new();
        private readonly Dictionary<int, double> _beatOnsets = new();
        private readonly Dictionary<int, bool> _wasInBeat = new();
        private int _configVersion;

        public MotionMapper(MotionMappingConfig config, AudioAnalysisResult analysis)
        {
            _config = config;
            _analysis = analysis;
            _configVersion = config.Version;
            _numBands = analysis.NumBands;
            _panelGroups = BuildPanelGroups(config);

            int win = config.Smoothing?.Enabled == true
                ? Math.Max(1, config.Smoothing.WindowSize) : 1;
            for (int g = 0; g < config.PanelGroups.Count; g++)
            {
                _smoothBufs[g] = new Queue<double>(win * 2);
                _smoothSums[g] = 0;
                _beatOnsets[g] = -1;
                _wasInBeat[g] = false;
            }
        }

        public bool ConfigIsStale => _configVersion != _config.Version;

        private static List<List<int>> BuildPanelGroups(MotionMappingConfig config)
        {
            var groups = new List<List<int>>();
            if (config.NumPanels <= 0 || config.PanelGroups.Count == 0)
                return groups;

            int numPanels = config.NumPanels;
            int numGroups = config.PanelGroups.Count;
            if (numPanels < numGroups) numPanels = numGroups;

            int assigned = 0;
            for (int g = 0; g < numGroups; g++)
            {
                bool isLast = (g == numGroups - 1);
                var groupCfg = config.PanelGroups[g];
                string band = groupCfg.Band?.ToLowerInvariant() ?? "mid";
                double fraction = 1.0 / numGroups;
                if (config.PanelGroupDistribution.TryGetValue(band, out double d))
                    fraction = d;

                int count = isLast
                    ? numPanels - assigned
                    : Math.Max(1, (int)(numPanels * fraction));
                var indices = new List<int>(count);
                for (int i = 0; i < count; i++)
                    indices.Add(assigned + i);
                groups.Add(indices);
                assigned += count;
            }

            return groups;
        }

        public MotionOutput GetFrame(double timeSeconds)
        {
            timeSeconds = MathUtils.Clamp(timeSeconds, 0, _analysis.Duration);
            var output = new MotionOutput { TimeSeconds = timeSeconds };
            var (frameA, frameB, frac) = _analysis.GetInterpolatedFrame(timeSeconds);

            for (int g = 0; g < _config.PanelGroups.Count && g < _panelGroups.Count; g++)
            {
                var groupCfg = _config.PanelGroups[g];
                var indices = _panelGroups[g];

                double bandEnergy = InterpolateBandEnergy(frameA, frameB, frac, g);
                double displacement = ApplyResponseCurve(bandEnergy, groupCfg);
                double impulse = ApplyBeatImpulse(timeSeconds,
                    frameA, frameB, frac, groupCfg, g);
                displacement += impulse;
                displacement = ApplySmoothing(displacement, g);
                displacement = Math.Max(0, displacement);

                var axis = new Vector3d(
                    groupCfg.Axis.Length > 0 ? groupCfg.Axis[0] : 0,
                    groupCfg.Axis.Length > 1 ? groupCfg.Axis[1] : 0,
                    groupCfg.Axis.Length > 2 ? groupCfg.Axis[2] : 1);
                if (axis.Length < 0.0001) axis = new Vector3d(0, 0, 1);

                var vector = axis * displacement;
                foreach (int idx in indices)
                {
                    output.PanelIndices.Add(idx);
                    output.MotionVectors.Add(vector);
                }
            }

            return output;
        }

        private double InterpolateBandEnergy(
            AudioAnalysisFrame a, AudioAnalysisFrame b,
            double frac, int bandIndex)
        {
            if (bandIndex >= _numBands) return 0;
            double va = bandIndex < a.Bands.Length ? a.Bands[bandIndex] : 0;
            double vb = bandIndex < b.Bands.Length ? b.Bands[bandIndex] : 0;
            double val = va + (vb - va) * frac;
            return val;
        }

// maps normalized energy 0 to 1 into the configured range with the chosen curve
        private static double ApplyResponseCurve(double energy, PanelGroupConfig cfg)
        {
            double rangeMin = cfg.RangeMm.Length > 0 ? cfg.RangeMm[0] : 0;
            double rangeMax = cfg.RangeMm.Length > 1 ? cfg.RangeMm[1] : 400;
            double range = rangeMax - rangeMin;
            if (range <= 0) return 0;

            double clamped = Math.Max(0, energy);
            double mapped = cfg.ResponseCurve switch
            {
                // pow on already normalized energy, no more saturation like the old broken version
                ResponseCurve.Exponential => Math.Pow(clamped, Math.Max(0.1, cfg.Exponent)),
                ResponseCurve.Sqrt => Math.Sqrt(clamped),
                _ => clamped
            };

            return MathUtils.Clamp(rangeMin + mapped * range, 0, rangeMax);
        }

// simple adsr: attack ramps up linearly for attackFrames, then decay ramps down
        private double ApplyBeatImpulse(double time,
            AudioAnalysisFrame frameA, AudioAnalysisFrame frameB,
            double frac, PanelGroupConfig groupCfg, int groupIndex)
        {
            if (!_config.BeatImpulse.Enabled || !groupCfg.BeatEnabled)
                return 0;

            if (!_beatOnsets.TryGetValue(groupIndex, out double onsetTime))
                return 0;

            double isBeat = frameA.IsBeat
                + (frameB.IsBeat - frameA.IsBeat) * frac;
            bool inBeat = isBeat >= _config.BeatImpulse.Threshold;

            if (!_wasInBeat[groupIndex] && inBeat)
            {
                double beatStart = frameA.Time
                    + (frameB.Time - frameA.Time) * frac;
                // remember when this beat started so we can compute the envelope from that point
                _beatOnsets[groupIndex] = beatStart;
                onsetTime = beatStart;
            }
            _wasInBeat[groupIndex] = inBeat;

            if (onsetTime < 0)
                return 0;

            double frameDur = _analysis.FrameDuration;
            if (frameDur <= 0) return 0;

            int att = Math.Max(1, _config.BeatImpulse.AttackFrames);
            int dec = Math.Max(1, _config.BeatImpulse.DecayFrames);
            double mag = _config.BeatImpulse.MagnitudeMm;

            double elapsedFrames = Math.Max(0,
                (time - onsetTime) / frameDur);

            if (elapsedFrames > att + dec)
                return 0;

            if (elapsedFrames <= att)
                return mag * (elapsedFrames / att);

            double decayPhase = (elapsedFrames - att) / dec;
            return mag * Math.Max(0, 1.0 - decayPhase);
        }

// moving average with a queue, keeps the motion from being too jerky
        private double ApplySmoothing(double value, int groupIndex)
        {
            if (!_smoothBufs.TryGetValue(groupIndex, out var buf))
                return value;

            int win = _config.Smoothing?.Enabled == true
                ? Math.Max(1, _config.Smoothing.WindowSize) : 1;
            if (win <= 1) return value;

            buf.Enqueue(value);
            _smoothSums[groupIndex] += value;
            while (buf.Count > win)
            {
                double old = buf.Dequeue();
                _smoothSums[groupIndex] -= old;
            }

            return _smoothSums[groupIndex] / buf.Count;
        }
    }
}
