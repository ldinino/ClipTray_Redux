using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace ClipTray.ClipBar
{
    /// <summary>
    /// A global hotkey combination, parsed from and rendered to strings such as
    /// "Ctrl+Alt+Space". Pure logic with no Win32 calls so it can be unit tested.
    /// </summary>
    public sealed class HotKeyDefinition
    {
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModWindows = 0x0008;

        /// <summary>Suppresses auto-repeat while the combination is held down.</summary>
        public const uint ModNoRepeat = 0x4000;

        private static readonly Keys[] ModifierKeys =
        {
            Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
            Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
            Keys.Menu, Keys.LMenu, Keys.RMenu,
            Keys.LWin, Keys.RWin,
            Keys.None
        };

        public HotKeyDefinition(bool control, bool alt, bool shift, bool windows, Keys key)
        {
            Control = control;
            Alt = alt;
            Shift = shift;
            Windows = windows;
            Key = key;
        }

        public bool Control { get; }
        public bool Alt { get; }
        public bool Shift { get; }
        public bool Windows { get; }
        public Keys Key { get; }

        /// <summary>
        /// Ctrl+Alt+Space. Chosen because the Windows text-input stack already owns
        /// Ctrl+Win+Space, Win+Space, Ctrl+Shift+Space and Shift+Win+Space for
        /// language switching, so those cannot be registered.
        /// </summary>
        public static HotKeyDefinition Default
        {
            get { return new HotKeyDefinition(true, true, false, false, Keys.Space); }
        }

        /// <summary>A combination needs at least one modifier and a real key.</summary>
        public bool IsValid
        {
            get
            {
                if (Array.IndexOf(ModifierKeys, Key) >= 0) return false;
                if (!Enum.IsDefined(typeof(Keys), Key)) return false;
                return Control || Alt || Shift || Windows;
            }
        }

        public uint Modifiers
        {
            get
            {
                uint modifiers = ModNoRepeat;
                if (Control) modifiers |= ModControl;
                if (Alt) modifiers |= ModAlt;
                if (Shift) modifiers |= ModShift;
                if (Windows) modifiers |= ModWindows;
                return modifiers;
            }
        }

        public uint VirtualKey
        {
            get { return (uint)Key; }
        }

        public static bool TryParse(string text, out HotKeyDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            bool control = false, alt = false, shift = false, windows = false;
            Keys key = Keys.None;

            foreach (var rawPart in text.Split('+'))
            {
                var part = rawPart.Trim();
                if (part.Length == 0) return false;

                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        control = true;
                        continue;
                    case "alt":
                        alt = true;
                        continue;
                    case "shift":
                        shift = true;
                        continue;
                    case "win":
                    case "windows":
                        windows = true;
                        continue;
                }

                if (key != Keys.None) return false; // more than one non-modifier key
                if (!TryParseKey(part, out key)) return false;
            }

            var candidate = new HotKeyDefinition(control, alt, shift, windows, key);
            if (!candidate.IsValid) return false;

            definition = candidate;
            return true;
        }

        private static bool TryParseKey(string text, out Keys key)
        {
            key = Keys.None;

            if (text.Length == 1 && text[0] >= '0' && text[0] <= '9')
            {
                key = Keys.D0 + (text[0] - '0');
                return true;
            }

            Keys parsed;
            if (!Enum.TryParse(text, true, out parsed)) return false;
            if (!Enum.IsDefined(typeof(Keys), parsed)) return false;

            key = parsed;
            return true;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            if (Control) builder.Append("Ctrl+");
            if (Alt) builder.Append("Alt+");
            if (Shift) builder.Append("Shift+");
            if (Windows) builder.Append("Win+");
            builder.Append(DescribeKey(Key));
            return builder.ToString();
        }

        private static string DescribeKey(Keys key)
        {
            return Describe(key);
        }

        /// <summary>Display name for a key, matching what <see cref="TryParse"/> accepts.</summary>
        internal static string Describe(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((char)('0' + (key - Keys.D0))).ToString();
            return key.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as HotKeyDefinition;
            return other != null
                && other.Control == Control
                && other.Alt == Alt
                && other.Shift == Shift
                && other.Windows == Windows
                && other.Key == Key;
        }

        public override int GetHashCode()
        {
            int hash = (int)Key;
            if (Control) hash ^= 1 << 24;
            if (Alt) hash ^= 1 << 25;
            if (Shift) hash ^= 1 << 26;
            if (Windows) hash ^= 1 << 27;
            return hash;
        }
    }
}
