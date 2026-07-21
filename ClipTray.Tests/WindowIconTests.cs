using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClipTray.Models;
using ClipTray.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class WindowIconTests
    {
        private sealed class TestClipTrayForm : ClipTrayForm
        {
            public TestClipTrayForm()
            {
                ConfigureDpiScaling();
            }
        }

        [TestMethod]
        public void AllApplicationForms_InheritClipTrayForm()
        {
            var formTypes = typeof(EntriesDialog).Assembly
                .GetTypes()
                .Where(type => type.Namespace == "ClipTray.UI")
                .Where(type => !type.IsAbstract && typeof(Form).IsAssignableFrom(type))
                .ToArray();

            Assert.AreEqual(5, formTypes.Length);
            foreach (var formType in formTypes)
            {
                Assert.IsTrue(
                    typeof(ClipTrayForm).IsAssignableFrom(formType),
                    formType.FullName + " does not inherit ClipTrayForm.");
            }
        }

        [TestMethod]
        public void ClipTrayForm_UsesEmbeddedExecutableIcon()
        {
            using (var expected = Icon.ExtractAssociatedIcon(typeof(ClipTrayForm).Assembly.Location))
            using (var form = new TestClipTrayForm())
            {
                Assert.IsNotNull(expected);
                Assert.IsNotNull(form.Icon);

                using (var expectedBitmap = expected.ToBitmap())
                using (var actualBitmap = form.Icon.ToBitmap())
                {
                    Assert.AreEqual(expectedBitmap.Size, actualBitmap.Size);
                    for (int y = 0; y < expectedBitmap.Height; y++)
                    {
                        for (int x = 0; x < expectedBitmap.Width; x++)
                        {
                            Assert.AreEqual(
                                expectedBitmap.GetPixel(x, y).ToArgb(),
                                actualBitmap.GetPixel(x, y).ToArgb(),
                                "Icon pixel mismatch at " + x + "," + y + ".");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void ClipTrayForm_UsesDpiAutoScaling()
        {
            using (var form = new TestClipTrayForm())
            {
                Assert.AreEqual(AutoScaleMode.Dpi, form.AutoScaleMode);
                Assert.AreEqual(new SizeF(96F, 96F), form.AutoScaleDimensions);
            }
        }

        [TestMethod]
        public void AllApplicationForms_ApplyDpiAutoScaling()
        {
            var forms = new ClipTrayForm[]
            {
                new EntriesDialog(
                    new List<ClipEntry>(),
                    Path.Combine(Path.GetTempPath(), "cliptray-dpi-test.txt"),
                    20),
                new HyperlinkDialog("https://example.com", "Example"),
                new PreviewDialog("Preview", "Text"),
                new TokenFormatDialog("date", "yyyy-MM-dd", new[] { "yyyy-MM-dd" }),
                new AboutDialog()
            };

            try
            {
                foreach (var form in forms)
                {
                    Assert.AreEqual(AutoScaleMode.Dpi, form.AutoScaleMode, form.GetType().Name);
                    Assert.AreEqual(
                        new SizeF(96F, 96F),
                        form.AutoScaleDimensions,
                        form.GetType().Name);
                }
            }
            finally
            {
                foreach (var form in forms) form.Dispose();
            }
        }
    }
}