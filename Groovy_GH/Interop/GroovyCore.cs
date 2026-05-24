// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using System;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Groovy_GH.Interop
{
    public static class GroovyCore
    {
        private const string DllName = "GroovyCore.dll";

        // pass band edges as a json string because marshalling a struct over pinvoke is a pain
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Groovy_AnalyzeEx(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            int fftSize, int hopSize, int numBands,
            [MarshalAs(UnmanagedType.LPStr)] string? bandEdgesJson);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Groovy_GetDuration(
            [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Groovy_FreeResult(IntPtr result);

        public static string Analyze(
            string filePath, int fftSize = 2048, int hopSize = 512,
            int numBands = 3, string? bandEdgesJson = null)
        {
            IntPtr ptr = Groovy_AnalyzeEx(
                filePath, fftSize, hopSize, numBands, bandEdgesJson);
            if (ptr == IntPtr.Zero)
                return "{\"error\":\"null result from DLL\"}";
            // finally guarantees we free the native pointer even if the marshal blows up
            try
            {
                return Marshal.PtrToStringAnsi(ptr)
                    ?? "{\"error\":\"failed to marshal result\"}";
            }
            finally { Groovy_FreeResult(ptr); }
        }

        public static string GetDuration(string filePath)
        {
            IntPtr ptr = Groovy_GetDuration(filePath);
            if (ptr == IntPtr.Zero)
                return "{\"error\":\"null result from DLL\"}";
            try
            {
                return Marshal.PtrToStringAnsi(ptr)
                    ?? "{\"error\":\"failed to marshal result\"}";
            }
            finally { Groovy_FreeResult(ptr); }
        }
    }
}
