// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using NAudio.Wave;
using System;

namespace Groovy_GH.Core
{
    public class GroovyState
    {
        private static readonly object _lock = new();

        public static AudioAnalysisResult? Analysis { get; set; }
        public static MotionMappingConfig ActiveConfig { get; set; }
            = MotionMappingConfig.CreateDefault();
        public static string? CurrentFilePath { get; set; }

        // volatile because the timer thread reads these while the gh thread writes them
        public static volatile IWavePlayer? Player;
        public static volatile AudioFileReader? AudioFile;

        public static bool IsPlaying { get; private set; }
        public static bool IsPaused { get; private set; }
        public static double ManualSeekTime { get; set; } = -1;
        public static double PreviewSeekTime { get; set; } = -1;
        public static double TotalDuration { get; set; }

        public static Action? ExpireAction { get; set; }
        public static Action? PlayAction { get; set; }
        public static Action? StopAction { get; set; }

        public static string LastError { get; set; } = "";

        public static double GetTimeSeconds()
        {
            lock (_lock)
            {
                if (!IsPlaying)
                    return PreviewSeekTime >= 0 ? PreviewSeekTime : 0;
                var af = AudioFile;
                if (af != null) return af.CurrentTime.TotalSeconds;
                return 0;
            }
        }

        public static bool NeedsAnalysis(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            if (Analysis == null) return true;
            if (CurrentFilePath != filePath) return true;
            if (Analysis.HasError) return true;
            return false;
        }

        public static bool StartPlayback()
        {
            StopPlayback();

            try
            {
                var reader = new AudioFileReader(CurrentFilePath);
                TotalDuration = reader.TotalTime.TotalSeconds;
                AudioFile = reader;
                var player = new WaveOutEvent();
                player.Init(reader);
                Player = player;
                lock (_lock)
                {
                    IsPlaying = true;
                    IsPaused = false;
                    ManualSeekTime = -1;
                    PreviewSeekTime = -1;
                }
                player.Play();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                StopPlayback();
                return false;
            }
        }

        public static void Pause()
        {
            try
            {
                var p = Player;
                if (p == null) return;
                lock (_lock)
                {
                    if (!IsPlaying) return;
                    if (IsPaused) { p.Play(); IsPaused = false; }
                    else { p.Pause(); IsPaused = true; }
                }
            }
            catch (Exception ex)
            {
                LastError = $"Pause error: {ex.Message}";
            }
        }

        public static void SeekTo(double seconds)
        {
            lock (_lock)
            {
                seconds = MathUtils.Clamp(seconds, 0, TotalDuration);
                ManualSeekTime = seconds;
                PreviewSeekTime = seconds;
            }

            var af = AudioFile;
            if (af != null)
            {
                try { af.CurrentTime = TimeSpan.FromSeconds(seconds); }
                catch { }
            }
        }

        public static void StopPlayback()
        {
            try
            {
                lock (_lock)
                {
                    IsPlaying = false;
                    IsPaused = false;
                    ManualSeekTime = -1;
                    PreviewSeekTime = -1;
                }

                var p = Player;
                if (p != null)
                {
                    try { p.Stop(); } catch { }
                    try { p.Dispose(); } catch { }
                }
                var af = AudioFile;
                if (af != null)
                {
                    try { af.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                LastError = $"Stop error: {ex.Message}";
            }
            // finally makes sure player and audiofile go null even if dispose throws
            finally
            {
                Player = null;
                AudioFile = null;
                TotalDuration = 0;
            }
        }

        public static void Reset()
        {
            StopPlayback();
            Analysis = null;
            CurrentFilePath = null;
            LastError = "";
        }

        public static void SetConfig(MotionMappingConfig newConfig)
        {
            newConfig.IncrementVersion();
            ActiveConfig = newConfig;
            ExpireAction?.Invoke();
        }
    }
}
