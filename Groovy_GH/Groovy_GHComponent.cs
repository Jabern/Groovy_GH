// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using Grasshopper;
using Grasshopper.Kernel;
using Groovy_GH.Core;
using Groovy_GH.Interop;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Groovy_GH
{
    public class Groovy_GHComponent : GH_Component
    {
        private MotionMapper? _mapper;
        private int _configVersion = -1;
        private System.Threading.Timer? _timer;
        private bool _autoStopPending;
        private readonly List<string> _debugLog = new();

        public Groovy_GHComponent()
          : base("Groovy", "Groovy",
            "Audio-driven kinetic panel motion system.",
            "Groovy", "Panel")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid
            => new("5d2c7ff1-6611-4ad5-a981-ec7ea18339c3");

        public override void AddedToDocument(GH_Document document)
        {
            GroovyState.ExpireAction = () =>
                Rhino.RhinoApp.InvokeOnUiThread(
                    (Action)(() => ExpireSolution(true)));
            GroovyState.PlayAction = () =>
                Rhino.RhinoApp.InvokeOnUiThread(
                    (Action)(StartPlaybackLoop));
            GroovyState.StopAction = () =>
                Rhino.RhinoApp.InvokeOnUiThread(
                    (Action)(StopPlaybackLoop));
            base.AddedToDocument(document);
        }

        protected override void RegisterInputParams(
            GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("FilePath", "F",
                "Path to .wav audio file", GH_ParamAccess.item);
            pManager.AddTextParameter("Settings", "S",
                "JSON config from Groovy Settings (optional)",
                GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("FFTSize", "N",
                "FFT size, power of 2 (default 2048)",
                GH_ParamAccess.item, 2048);
            pManager[2].Optional = true;
            pManager.AddBooleanParameter("Debug", "D",
                "Toggle to dump diagnostic info", GH_ParamAccess.item, false);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(
            GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("PanelIdx", "I",
                "Panel index", GH_ParamAccess.list);
            pManager.AddVectorParameter("MotionVec", "V",
                "Motion vector (mm, Z-axis default)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Time", "T",
                "Current playback time (seconds)", GH_ParamAccess.item);
            pManager.AddTextParameter("DebugOut", "Dbg",
                "Diagnostic log", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _debugLog.Clear();
            GroovyState.LastError = "";

            string filePath = "";
            string settingsJson = "";
            int fftSize = 2048;
            bool debug = false;
            DA.GetData(0, ref filePath);
            DA.GetData(1, ref settingsJson);
            DA.GetData(2, ref fftSize);
            DA.GetData(3, ref debug);

            _debugLog.Add(
                $"File: {(string.IsNullOrEmpty(filePath) ? "(none)" : filePath)}");
            _debugLog.Add(
                $"Settings wired: {!string.IsNullOrEmpty(settingsJson)}");
            _debugLog.Add(
                $"Analysis cached: {GroovyState.Analysis != null}");
            _debugLog.Add(
                $"IsPlaying: {GroovyState.IsPlaying}, " +
                $"IsPaused: {GroovyState.IsPaused}");
            _debugLog.Add(
                $"Mapper: {(_mapper != null ? "yes" : "no")}, " +
                $"ConfigVer: {_configVersion} vs {GroovyState.ActiveConfig.Version}");

            if (string.IsNullOrEmpty(filePath))
            {
                _debugLog.Add("ERROR: no file path");
                if (debug) DA.SetData(3, DumpDebug());
                return;
            }

            if (!System.IO.File.Exists(filePath))
            {
                _debugLog.Add("ERROR: file not found");
                GroovyState.LastError = $"File not found: {filePath}";
                if (debug) DA.SetData(3, DumpDebug());
                return;
            }

            if (!string.IsNullOrEmpty(settingsJson))
            {
                try
                {
                    GroovyState.SetConfig(
                        MotionMappingConfig.FromJson(settingsJson));
                    _debugLog.Add("Config parsed OK");
                }
                catch (Exception ex)
                {
                    _debugLog.Add($"Config parse ERROR: {ex.Message}");
                }
            }

            if (GroovyState.NeedsAnalysis(filePath))
            {
                _debugLog.Add("Running analysis...");
                GroovyState.Reset();
                GroovyState.CurrentFilePath = filePath;

                int hopSize = Math.Max(64, fftSize / 4);
                int numBands = GroovyState.ActiveConfig.FrequencyBands.Count;
                if (numBands < 1) numBands = 3;
                string bandsJson = SerializeBandEdges(
                    GroovyState.ActiveConfig.FrequencyBands);
                try
                {
                    string json = GroovyCore.Analyze(
                        filePath, fftSize, hopSize, numBands, bandsJson);
                    var analysis = AudioAnalysisResult.FromJson(json);
                    if (analysis.HasError)
                    {
                        _debugLog.Add(
                            $"Analysis ERROR: {analysis.Error}");
                        GroovyState.LastError = analysis.Error ?? "unknown";
                        if (debug) DA.SetData(3, DumpDebug());
                        return;
                    }
                    GroovyState.Analysis = analysis;
                    _configVersion = -1;
                    _debugLog.Add(
                        $"Analysis OK: {analysis.Frames.Count} frames, " +
                        $"{analysis.Duration:F1}s");
                }
                catch (Exception ex)
                {
                    _debugLog.Add(
                        $"Analysis EXCEPTION: {ex.Message}");
                    GroovyState.LastError = ex.Message;
                    if (debug) DA.SetData(3, DumpDebug());
                    return;
                }
            }

            if (GroovyState.Analysis == null)
            {
                _debugLog.Add("No analysis data");
                if (debug) DA.SetData(3, DumpDebug());
                return;
            }

            int currentVer = GroovyState.ActiveConfig.Version;
            bool configChanged = _mapper != null &&
                _configVersion != currentVer;
            if (_mapper == null || configChanged)
            {
                _mapper = new MotionMapper(
                    GroovyState.ActiveConfig, GroovyState.Analysis);
                _configVersion = currentVer;
                _debugLog.Add(
                    $"Mapper rebuilt. " +
                    $"Panels: {GroovyState.ActiveConfig.NumPanels}, " +
                    $"Groups: {GroovyState.ActiveConfig.PanelGroups.Count}");

                if (configChanged && GroovyState.IsPlaying
                    && _timer != null)
                {
                    int newFps = MathUtils.Clamp(
                        GroovyState.ActiveConfig.Fps, 1, 120);
                    int newInterval = Math.Max(1, 1000 / newFps);
                    _debugLog.Add(
                        $"Timer restarted at {newInterval}ms " +
                        $"(FPS={newFps})");
                    RecreateTimer(newInterval);
                }
            }

            if (_autoStopPending)
            {
                _autoStopPending = false;
                StopPlaybackLoop();
                DA.SetDataList(0, new List<int>());
                DA.SetDataList(1, new List<Vector3d>());
                DA.SetData(2, 0.0);
                if (debug) DA.SetData(3, DumpDebug());
                return;
            }

            if (GroovyState.IsPlaying)
            {
                double time = GroovyState.GetTimeSeconds();
                if (time > GroovyState.Analysis.Duration)
                {
                    _debugLog.Add("Playback reached end — auto-stop");
                    _autoStopPending = true;
                }
                else
                {
                    var output = _mapper.GetFrame(time);
                    DA.SetDataList(0, output.PanelIndices);
                    DA.SetDataList(1, output.MotionVectors);
                    DA.SetData(2, time);
                    Message = $"{time:F1}s / " +
                        $"{GroovyState.Analysis.Duration:F1}s";
                    _debugLog.Add(
                        $"Frame: t={time:F2}s, " +
                        $"{output.PanelIndices.Count} panels");
                }
            }
            else
            {
                DA.SetDataList(0, new List<int>());
                DA.SetDataList(1, new List<Vector3d>());
                DA.SetData(2, 0.0);
                Message = "Ready — open Settings to play";
                _debugLog.Add("Idle — waiting for play command");
            }

            if (debug) DA.SetData(3, DumpDebug());
        }

        public void StartPlaybackLoop()
        {
            _debugLog.Add("StartPlaybackLoop called");
            try
            {
                if (!GroovyState.StartPlayback())
                {
                    _debugLog.Add(
                        $"StartPlayback FAILED: {GroovyState.LastError}");
                    return;
                }

                _timer?.Dispose();

                int fps = MathUtils.Clamp(GroovyState.ActiveConfig.Fps, 1, 120);
                int interval = Math.Max(1, 1000 / fps);
                RecreateTimer(interval);
                _debugLog.Add($"Timer started at {interval}ms");
            }
            catch (Exception ex)
            {
                _debugLog.Add(
                    $"StartPlaybackLoop ERROR: {ex.Message}");
                GroovyState.LastError = ex.Message;
                StopPlaybackLoop();
            }
        }

        public void StopPlaybackLoop()
        {
            _debugLog.Add("StopPlaybackLoop called");
            GroovyState.StopPlayback();
            _timer?.Dispose();
            _timer = null;
            ExpireSolution(true);
        }

        private void RecreateTimer(int intervalMs)
        {
            _timer?.Dispose();
            // uses threading timer instead of forms timer because rhino has no winforms message pump
            _timer = new System.Threading.Timer(_ =>
            {
                Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    if (_autoStopPending)
                    {
                        _autoStopPending = false;
                        StopPlaybackLoop();
                        return;
                    }
                    if (!GroovyState.IsPlaying)
                    {
                        _timer?.Dispose();
                        return;
                    }
                    ExpireSolution(true);
                }));
            }, null, intervalMs, intervalMs);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            StopPlaybackLoop();
            GroovyState.ExpireAction = null;
            GroovyState.PlayAction = null;
            GroovyState.StopAction = null;
            base.RemovedFromDocument(document);
        }

        private string DumpDebug()
        {
            var sb = new StringBuilder();
            foreach (var line in _debugLog) sb.AppendLine(line);
            if (!string.IsNullOrEmpty(GroovyState.LastError))
                sb.Append("LastError: ").AppendLine(GroovyState.LastError);
            return sb.ToString();
        }

// converts the band dictionary into a pseudo json string to pass to the dll
        private static string SerializeBandEdges(
            System.Collections.Generic.Dictionary<string, double[]> bands)
        {
            var list = new System.Text.StringBuilder();
            list.Append('[');
            int i = 0;
            foreach (var kv in bands)
            {
                if (i > 0) list.Append(',');
                list.Append("{\"low\":");
                list.Append(kv.Value[0].ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
                list.Append(",\"high\":");
                list.Append(kv.Value[1].ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
                list.Append('}');
                i++;
            }
            list.Append(']');
            return list.ToString();
        }

        protected override System.Drawing.Bitmap Icon => null;
    }
}
