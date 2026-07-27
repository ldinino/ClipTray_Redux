using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ClipTray.Models;
using ClipTray.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    /// <summary>
    /// Drives the real ClipBar window's handlers and paint path without calling
    /// Show(), so the suite neither steals focus nor writes to the clipboard.
    /// </summary>
    [TestClass]
    public class ClipBarWindowTests
    {
        private static readonly Assembly AppAssembly = typeof(AppSettings).Assembly;

        private static Form CreateWindow(AppSettings settings, IEnumerable<ClipEntry> entries)
        {
            var type = AppAssembly.GetType("ClipTray.ClipBar.ClipBarWindow", true);
            var window = (Form)System.Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { settings },
                null);

            // Touching Handle creates the window without making it visible, which is
            // enough for the layout pass to read a real monitor and DPI.
            var unused = window.Handle;

            SetField(window, "_entries", new List<ClipEntry>(entries));
            Invoke(window, "UpdateMatches");
            Invoke(window, "ApplyLayout");
            return window;
        }

        private static List<ClipEntry> SampleEntries()
        {
            return new List<ClipEntry>
            {
                new ClipEntry { Title = "Meeting follow-up", Text = "Thanks for your time today." },
                new ClipEntry { Title = "Support signature", Text = "Best regards, Luis" },
                new ClipEntry { Title = "Escalation template", Text = "Escalated to Tier 2." }
            };
        }

        private static AppSettings Settings()
        {
            return new AppSettings { MaxResults = 5 };
        }

        [TestMethod]
        public void Window_LaysOutWithSaneDimensions()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                Assert.IsTrue(window.ClientSize.Width >= 480,
                    "Width should never collapse below the floor. Actual: " + window.ClientSize.Width);
                Assert.IsTrue(window.ClientSize.Height > 0);

                var workArea = Screen.FromHandle(window.Handle).WorkingArea;
                Assert.IsTrue(window.ClientSize.Width <= workArea.Width,
                    "Window must fit on screen");
                Assert.IsTrue(window.Left >= workArea.Left && window.Top >= workArea.Top,
                    "Window must sit inside the work area");
            }
        }

        [TestMethod]
        public void Window_ScalesWithSizeMultiplier()
        {
            var entries = SampleEntries();
            using (var normal = CreateWindow(new AppSettings { MaxResults = 5 }, entries))
            using (var doubled = CreateWindow(new AppSettings { MaxResults = 5, SizeMultiplier = 2F }, entries))
            {
                Assert.IsTrue(doubled.ClientSize.Width > normal.ClientSize.Width
                        || doubled.ClientSize.Width == (int)(Screen.FromHandle(doubled.Handle).WorkingArea.Width * 0.55F),
                    "A larger multiplier should widen the bar unless it hit the screen cap");
            }
        }

        [TestMethod]
        public void QueryText_FiltersMatches()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                SetQuery(window, "meet");

                var matches = Matches(window);
                Assert.AreEqual(1, matches.Count);
                Assert.AreEqual("Meeting follow-up", matches[0].Title);
            }
        }

        [TestMethod]
        public void QueryText_NoMatches_LeavesEmptyResultList()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                SetQuery(window, "zzzzqqq");

                Assert.AreEqual(0, Matches(window).Count);
            }
        }

        [TestMethod]
        public void ArrowKeys_MoveAndWrapSelection()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                Assert.AreEqual(0, SelectedIndex(window));

                SendKey(window, Keys.Down);
                Assert.AreEqual(1, SelectedIndex(window));

                SendKey(window, Keys.Down);
                Assert.AreEqual(2, SelectedIndex(window));

                SendKey(window, Keys.Down);
                Assert.AreEqual(0, SelectedIndex(window), "Selection should wrap past the end");

                SendKey(window, Keys.Up);
                Assert.AreEqual(2, SelectedIndex(window), "Selection should wrap before the start");
            }
        }

        [TestMethod]
        public void ArrowKeys_WithNoMatches_DoNotThrow()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                SetQuery(window, "zzzzqqq");

                SendKey(window, Keys.Down);
                SendKey(window, Keys.Up);

                Assert.AreEqual(0, Matches(window).Count);
            }
        }

        [TestMethod]
        public void Typing_ResetsSelectionToTheBestMatch()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                SendKey(window, Keys.Down);
                Assert.AreEqual(1, SelectedIndex(window));

                SetQuery(window, "e");
                Assert.AreEqual(0, SelectedIndex(window),
                    "A new query should select the top result again");
            }
        }

        [TestMethod]
        public void Escape_HidesWithoutRaisingEntryCopied()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                bool copied = false;
                Subscribe(window, (s, e) => copied = true);

                SendKey(window, Keys.Escape);

                Assert.IsFalse(copied, "Escape must not copy anything");
                Assert.IsFalse(window.Visible);
            }
        }

        [TestMethod]
        public void Paint_RendersEveryRowWithoutThrowing()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                RenderToBitmap(window);
            }
        }

        [TestMethod]
        public void Paint_RendersEmptyStateWithoutThrowing()
        {
            using (var window = CreateWindow(Settings(), SampleEntries()))
            {
                SetQuery(window, "zzzzqqq");
                RenderToBitmap(window);
            }
        }

        [TestMethod]
        public void BuildPreview_CollapsesWhitespaceAndHandlesBlanks()
        {
            var method = AppAssembly
                .GetType("ClipTray.ClipBar.ClipBarWindow", true)
                .GetMethod("BuildPreview", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.AreEqual("a b c", method.Invoke(null, new object[] { "a\r\nb   c", false }));
            Assert.AreEqual("Empty insert", method.Invoke(null, new object[] { "   ", false }));
            Assert.AreEqual("Empty insert", method.Invoke(null, new object[] { null, false }));
        }

        [TestMethod]
        public void BuildPreview_ResolvesTokensOnlyWhenAsked()
        {
            var method = AppAssembly
                .GetType("ClipTray.ClipBar.ClipBarWindow", true)
                .GetMethod("BuildPreview", BindingFlags.Static | BindingFlags.NonPublic);

            var raw = (string)method.Invoke(null, new object[] { "Logged {date:yyyy}", false });
            var resolved = (string)method.Invoke(null, new object[] { "Logged {date:yyyy}", true });

            Assert.AreEqual("Logged {date:yyyy}", raw);
            StringAssert.Matches(resolved, new System.Text.RegularExpressions.Regex(@"^Logged \d{4}$"));
        }

        // --- helpers -------------------------------------------------------

        private static void RenderToBitmap(Form window)
        {
            var onPaint = window.GetType().GetMethod(
                "OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);

            using (var bitmap = new Bitmap(
                System.Math.Max(1, window.ClientSize.Width),
                System.Math.Max(1, window.ClientSize.Height)))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var args = new PaintEventArgs(
                    graphics, new Rectangle(Point.Empty, window.ClientSize));
                onPaint.Invoke(window, new object[] { args });
            }
        }

        private static void SetQuery(Form window, string text)
        {
            var box = (TextBox)GetField(window, "_queryBox");
            box.Text = text; // fires TextChanged, the real filtering path
        }

        private static List<ClipEntry> Matches(Form window)
        {
            return (List<ClipEntry>)GetField(window, "_matches");
        }

        private static int SelectedIndex(Form window)
        {
            return (int)GetField(window, "_selectedIndex");
        }

        private static void SendKey(Form window, Keys key)
        {
            var method = window.GetType().GetMethod(
                "ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
            var args = new object[] { new Message(), key };
            method.Invoke(window, args);
        }

        private static void Subscribe(Form window, System.EventHandler<ClipEntry> handler)
        {
            window.GetType().GetEvent("EntryCopied").AddEventHandler(window, handler);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            target.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }
    }
}
