using System;
using System.Runtime.InteropServices;

namespace ClipTray.ClipBar
{
    /// <summary>
    /// Sends Ctrl+V to whichever window currently has focus. Opt-in, because it types
    /// into whatever happens to be in front rather than a window ClipTray controls.
    /// </summary>
    internal static class AutoPaste
    {
        private const int InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const ushort VkControl = 0x11;
        private const ushort VkV = 0x56;

        public static void SendPaste()
        {
            var inputs = new[]
            {
                KeyEvent(VkControl, false),
                KeyEvent(VkV, false),
                KeyEvent(VkV, true),
                KeyEvent(VkControl, true)
            };

            NativeMethods.SendInput(
                (uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.Input)));
        }

        private static NativeMethods.Input KeyEvent(ushort virtualKey, bool keyUp)
        {
            return new NativeMethods.Input
            {
                Type = InputKeyboard,
                Data = new NativeMethods.InputUnion
                {
                    Keyboard = new NativeMethods.KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        ScanCode = 0,
                        Flags = keyUp ? KeyEventKeyUp : 0,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct Input
            {
                public int Type;
                public InputUnion Data;
            }

            // All three members overlap, which is what gives INPUT its correct size.
            [StructLayout(LayoutKind.Explicit)]
            public struct InputUnion
            {
                [FieldOffset(0)] public MouseInput Mouse;
                [FieldOffset(0)] public KeyboardInput Keyboard;
                [FieldOffset(0)] public HardwareInput Hardware;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MouseInput
            {
                public int X;
                public int Y;
                public uint Data;
                public uint Flags;
                public uint Time;
                public IntPtr ExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KeyboardInput
            {
                public ushort VirtualKey;
                public ushort ScanCode;
                public uint Flags;
                public uint Time;
                public IntPtr ExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HardwareInput
            {
                public uint Message;
                public ushort ParamL;
                public ushort ParamH;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint SendInput(uint count, Input[] inputs, int size);
        }
    }
}
