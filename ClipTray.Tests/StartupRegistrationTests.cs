using System;
using Microsoft.Win32;
using ClipTray;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class StartupRegistrationTests
    {
        [TestMethod]
        public void BuildCommand_AlwaysQuotesAbsoluteExecutablePath()
        {
            const string executablePath = @"C:\Portable Apps\ClipTray\ClipTray.exe";

            Assert.AreEqual(
                "\"C:\\Portable Apps\\ClipTray\\ClipTray.exe\"",
                StartupRegistration.BuildCommand(executablePath));
            Assert.IsTrue(StartupRegistration.IsCommandForExecutable(
                executablePath,
                executablePath));
            Assert.IsTrue(StartupRegistration.IsCommandForExecutable(
                StartupRegistration.BuildCommand(executablePath),
                executablePath));
        }

        [TestMethod]
        public void SetEnabled_RegistersCurrentPathAndRemovesValue()
        {
            string keyPath = @"Software\ClipTray.Tests." + Guid.NewGuid().ToString("N");
            const string valueName = "ClipTrayTest";
            const string firstPath = @"C:\Portable Apps\ClipTray\ClipTray.exe";
            const string movedPath = @"D:\Tools\ClipTray.exe";

            try
            {
                StartupRegistration.SetEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    firstPath,
                    true);

                Assert.IsTrue(StartupRegistration.IsEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    firstPath));
                Assert.IsFalse(StartupRegistration.IsEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    movedPath));

                StartupRegistration.SetEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    movedPath,
                    true);

                Assert.IsTrue(StartupRegistration.IsEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    movedPath));
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                {
                    Assert.AreEqual(
                        StartupRegistration.BuildCommand(movedPath),
                        key.GetValue(valueName));
                }

                StartupRegistration.SetEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    movedPath,
                    false);
                Assert.IsFalse(StartupRegistration.IsEnabled(
                    Registry.CurrentUser,
                    keyPath,
                    valueName,
                    movedPath));
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
            }

            using (var leftover = Registry.CurrentUser.OpenSubKey(keyPath))
                Assert.IsNull(leftover, "Temporary startup test key was not removed.");
        }
    }
}