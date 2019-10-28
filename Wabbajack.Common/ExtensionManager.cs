﻿using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public class ExtensionManager
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static string Extension = ".wabbajack";

        private static readonly string ExtRegPath = $"Software\\Classes\\{Extension}";
        private static readonly string AppRegPath = "Software\\Classes\\Applications\\Wabbajack.exe";
        private static readonly string AppAssocRegPath =
            $"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{Extension}";

        public static bool IsExtensionAssociated()
        {
            return (Registry.CurrentUser.OpenSubKey(AppAssocRegPath, false) == null);
        }

        public static void AssociateExtension(string iconPath, string appPath)
        {
            var winVersion = new Version(6, 2, 9200, 0);

            var extReg = Registry.CurrentUser.CreateSubKey(ExtRegPath);
            if (Environment.OSVersion.Platform >= PlatformID.Win32NT && Environment.OSVersion.Version >= winVersion)
                extReg?.SetValue("", Extension.Replace(".", "") + "_auto_file");
            var appReg = Registry.CurrentUser.CreateSubKey(AppRegPath);
            var appAssocReg = Registry.CurrentUser.CreateSubKey(AppAssocRegPath);

            extReg?.CreateSubKey("DefaultIcon")?.SetValue("", iconPath);
            extReg?.CreateSubKey("PerceivedType")?.SetValue("", "Archive");

            appReg?.CreateSubKey("shell\\open\\command")?.SetValue("", $"\"{appPath}\" -i %i");
            appReg?.CreateSubKey("DefaultIcon")?.SetValue("", iconPath);

            appAssocReg?.CreateSubKey("UserChoice")?.SetValue("Progid", "Applications\\Wabbajack.exe");
            SHChangeNotify(0x000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            if (Environment.OSVersion.Platform < PlatformID.Win32NT ||
                Environment.OSVersion.Version < winVersion) return;
            var win8FileReg =
                Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{Extension.Replace(".", "")}_auto_file");
            win8FileReg?.CreateSubKey("shell\\open\\command")?.SetValue("", $"\"{appPath}\" -i %i");
        }
    }
}
