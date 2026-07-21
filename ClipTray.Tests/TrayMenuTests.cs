using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using ClipTray.Models;
using ClipTray.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClipTray.Tests
{
    [TestClass]
    public class TrayMenuTests
    {
        [TestMethod]
        public void BuildMenu_StartWithWindowsItemReflectsRegistration()
        {
            var context = (TrayApplicationContext)FormatterServices.GetUninitializedObject(
                typeof(TrayApplicationContext));
            SetField(context, "_entries", new List<ClipEntry>());
            SetField(context, "_menuSize", 20);

            var buildMenu = typeof(TrayApplicationContext).GetMethod(
                "BuildMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            using (var menu = (ContextMenuStrip)buildMenu.Invoke(context, null))
            {
                var options = FindMenuItem(menu.Items, "Options");
                var startup = (ToolStripMenuItem)options.DropDownItems["startWithWindowsItem"];

                Assert.IsNotNull(startup);
                Assert.AreEqual("Start with Windows", startup.Text);
                Assert.AreEqual(StartupRegistration.IsEnabled(), startup.Checked);
            }
        }

        private static ToolStripMenuItem FindMenuItem(ToolStripItemCollection items, string text)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Text == text)
                    return menuItem;
            }
            Assert.Fail("Menu item not found: " + text);
            return null;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}