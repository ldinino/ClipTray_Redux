using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipTray.ClipBar
{
    /// <summary>
    /// Owns a system-wide hotkey. ClipTray's root object is an ApplicationContext
    /// rather than a window, so this creates its own message-only window to receive
    /// WM_HOTKEY.
    /// </summary>
    internal sealed class GlobalHotKey : IDisposable
    {
        /// <summary>Returned by RegisterHotKey when another app already owns the combination.</summary>
        internal const int ErrorHotKeyAlreadyRegistered = 1409;

        private const int WmHotKey = 0x0312;
        private const int HotKeyId = 0xC1_17;

        private readonly MessageSink _sink;
        private bool _registered;

        public GlobalHotKey()
        {
            _sink = new MessageSink(OnHotKeyMessage);
        }

        public event EventHandler Pressed;

        /// <summary>The combination currently registered, or null when inactive.</summary>
        public HotKeyDefinition Current { get; private set; }

        /// <summary>
        /// The Win32 error from the last failed registration - 1409 means another
        /// application owns the combination.
        /// </summary>
        public int LastError { get; private set; }

        public bool TryRegister(HotKeyDefinition definition)
        {
            Unregister();

            LastError = 0;
            if (definition == null || !definition.IsValid) return false;

            if (!NativeMethods.RegisterHotKey(
                    _sink.Handle, HotKeyId, definition.Modifiers, definition.VirtualKey))
            {
                LastError = Marshal.GetLastWin32Error();
                return false;
            }

            _registered = true;
            Current = definition;
            return true;
        }

        public void Unregister()
        {
            if (!_registered) return;

            NativeMethods.UnregisterHotKey(_sink.Handle, HotKeyId);
            _registered = false;
            Current = null;
        }

        /// <summary>
        /// Reports whether a combination could be claimed right now, by briefly taking
        /// and releasing it on a throwaway window. Returns false when another
        /// application already owns it - including this one, so callers should special
        /// case the shortcut they have already registered.
        /// </summary>
        public static bool IsAvailable(HotKeyDefinition definition)
        {
            if (definition == null || !definition.IsValid) return false;

            using (var probe = new ProbeWindow())
            {
                const int probeId = 0xC1_18;
                if (!NativeMethods.RegisterHotKey(
                        probe.Handle, probeId, definition.Modifiers, definition.VirtualKey))
                    return false;

                NativeMethods.UnregisterHotKey(probe.Handle, probeId);
                return true;
            }
        }

        private void OnHotKeyMessage()
        {
            var handler = Pressed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Unregister();
            _sink.ReleaseHandle();
        }

        /// <summary>
        /// A message-only window (HWND_MESSAGE parent). It is never shown, costs
        /// nothing, and still receives posted messages such as WM_HOTKEY.
        /// </summary>
        private sealed class MessageSink : NativeWindow
        {
            private static readonly IntPtr HwndMessage = new IntPtr(-3);

            private readonly Action _onHotKey;

            public MessageSink(Action onHotKey)
            {
                _onHotKey = onHotKey;
                CreateHandle(new CreateParams { Parent = HwndMessage });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmHotKey && m.WParam.ToInt32() == HotKeyId)
                {
                    _onHotKey();
                    return;
                }
                base.WndProc(ref m);
            }
        }

        /// <summary>A short-lived message-only window used only to test a combination.</summary>
        private sealed class ProbeWindow : NativeWindow, IDisposable
        {
            private static readonly IntPtr HwndMessage = new IntPtr(-3);

            public ProbeWindow()
            {
                CreateHandle(new CreateParams { Parent = HwndMessage });
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero) DestroyHandle();
            }
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        }
    }
}
