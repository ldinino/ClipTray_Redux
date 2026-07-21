using System;
using System.IO;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ClipTray
{
    internal static class StartupRegistration
    {
        internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "ClipTray";

        public static bool IsEnabled()
        {
            return IsEnabled(
                Registry.CurrentUser,
                RunKeyPath,
                ValueName,
                Application.ExecutablePath);
        }

        public static void SetEnabled(bool enabled)
        {
            SetEnabled(
                Registry.CurrentUser,
                RunKeyPath,
                ValueName,
                Application.ExecutablePath,
                enabled);
        }

        internal static bool IsEnabled(
            RegistryKey root,
            string keyPath,
            string valueName,
            string executablePath)
        {
            using (var key = root.OpenSubKey(keyPath, false))
            {
                string command = key?.GetValue(
                    valueName,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                return IsCommandForExecutable(command, executablePath);
            }
        }

        internal static void SetEnabled(
            RegistryKey root,
            string keyPath,
            string valueName,
            string executablePath,
            bool enabled)
        {
            if (enabled)
            {
                using (var key = root.CreateSubKey(keyPath))
                {
                    if (key == null)
                        throw new IOException("Could not open the Windows startup registry key.");
                    key.SetValue(
                        valueName,
                        BuildCommand(executablePath),
                        RegistryValueKind.String);
                }
                return;
            }

            using (var key = root.OpenSubKey(keyPath, true))
                key?.DeleteValue(valueName, false);
        }

        internal static string BuildCommand(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Executable path is required.", nameof(executablePath));
            return "\"" + Path.GetFullPath(executablePath) + "\"";
        }

        internal static bool IsCommandForExecutable(string command, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;

            string expectedPath = Path.GetFullPath(executablePath);
            string candidate = command.Trim();
            return string.Equals(candidate, expectedPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    candidate,
                    BuildCommand(expectedPath),
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}