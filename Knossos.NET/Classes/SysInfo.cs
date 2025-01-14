﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;

namespace Knossos.NET
{


    public static class SysInfo
    {
        private struct LegacySettings
        {
            public string? base_path { get; set; }
        }

        private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        private static readonly bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static readonly bool cpuAVX = Avx.IsSupported;
        private static readonly bool cpuAVX2 = Avx2.IsSupported;
        private static string fsoPrefPath = string.Empty;


        /*
            Possible Values:
            https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.architecture?view=net-7.0
        */
        private static readonly string cpuArch = RuntimeInformation.OSArchitecture.ToString();


        public static bool IsWindows => isWindows;
        public static bool IsLinux => isLinux;
        public static bool IsMacOS => isMacOS;
        public static string CpuArch => cpuArch;
        public static bool CpuAVX => cpuAVX;
        public static bool CpuAVX2 => cpuAVX2;

        public static string GetKnossosDataFolderPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+ Path.DirectorySeparatorChar +"KnossosNET";
        }

        public static string GetFSODataFolderPath()
        {
            if(fsoPrefPath != string.Empty)
            {
                return fsoPrefPath;
            }
            else
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar +"HardLightProductions" + Path.DirectorySeparatorChar + "FreeSpaceOpen";
            }
            
        }

        public static void SetFSODataFolderPath(string path)
        {
            //FSO responds with this if there is no ini
            if(path != @".\")
                fsoPrefPath = path;
        }

        public static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

        public static String? GetBasePathFromKnossosLegacy()
        {
            try
            {
                string jsonPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "knossos" + Path.DirectorySeparatorChar + "settings.json";
                if (File.Exists(jsonPath))
                {
                    using FileStream jsonFile = File.OpenRead(jsonPath);
                    var jsonObject = JsonSerializer.Deserialize<LegacySettings>(jsonFile)!;
                    jsonFile.Close();
                    return jsonObject.base_path;
                }
            } 
            catch(Exception ex) 
            {
                Log.Add(Log.LogSeverity.Error, "Sysinfo.GetBasePathFromKnossosLegacy", ex);
            }
            return null;
        }

        public static bool Chmod(string filePath, string permissions = "+x", bool recursive = false)
        {
            string cmd;
            if (recursive)
                cmd = $"chmod -R {permissions} '{filePath}'";
            else
                cmd = $"chmod {permissions} '{filePath}'";

            try
            {
                using (Process proc = Process.Start("/bin/bash", $"-c \"{cmd}\""))
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string FormatBytes(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return String.Format("{0:0.##} {1}", dblSByte, suffix[i]);
        }

        public static string GetOSNameString()
        {
            if (isWindows)
                return "Windows";
            if (isLinux)
                return "Linux";
            if (isMacOS)
                return "MacOS";
            return "Unknown";
        }
    }
}
