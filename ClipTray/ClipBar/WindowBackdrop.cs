using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipTray.ClipBar
{
    public enum BackdropMode
    {
        /// <summary>Fully opaque.</summary>
        None,

        /// <summary>Uniform translucency, no blur. Works on every Windows version.</summary>
        Translucent,

        /// <summary>Translucency plus a blur of whatever sits behind the window.</summary>
        Blur,

        /// <summary>Translucency plus a tinted acrylic blur, via the accent API.</summary>
        Acrylic,

        /// <summary>
        /// The Windows 11 compositor's own acrylic. Unlike <see cref="Acrylic"/> this
        /// does not rely on window-wide opacity, so the blur is genuinely visible and
        /// text stays fully crisp. The default where it is available.
        /// </summary>
        SystemAcrylic
    }

    /// <summary>
    /// Applies ClipBar's translucency, blur and rounded corners, degrading
    /// automatically on Windows versions that lack the newer compositing features.
    /// </summary>
    internal static class WindowBackdrop
    {
        // Acrylic arrived with Windows 10 1803.
        internal static readonly Version AcrylicMinimum = new Version(10, 0, 17134);

        // Blur-behind became usable with Windows 10 1709.
        internal static readonly Version BlurMinimum = new Version(10, 0, 16299);

        // Rounded corners are a Windows 11 feature.
        internal static readonly Version RoundedCornersMinimum = new Version(10, 0, 22000);

        // DWMWA_SYSTEMBACKDROP_TYPE is documented from Windows 11 22H2.
        internal static readonly Version SystemBackdropMinimum = new Version(10, 0, 22621);

        private const int WcaAccentPolicy = 19;
        private const int AccentEnableBlurBehind = 3;
        private const int AccentEnableAcrylicBlurBehind = 4;
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwaSystemBackdropType = 38;
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwcpRound = 2;
        private const int DwmsbtTransientWindow = 3;

        public const int MinTransparency = 50;
        public const int MaxTransparency = 100;

        /// <summary>
        /// Picks the richest backdrop the running Windows version supports. Pure
        /// logic, so the fallback chain is unit tested without needing those versions.
        /// </summary>
        internal static BackdropMode Resolve(BackdropMode requested, Version operatingSystem)
        {
            switch (requested)
            {
                case BackdropMode.SystemAcrylic:
                    if (operatingSystem >= SystemBackdropMinimum) return BackdropMode.SystemAcrylic;
                    if (operatingSystem >= AcrylicMinimum) return BackdropMode.Acrylic;
                    if (operatingSystem >= BlurMinimum) return BackdropMode.Blur;
                    return BackdropMode.Translucent;

                case BackdropMode.Acrylic:
                    if (operatingSystem >= AcrylicMinimum) return BackdropMode.Acrylic;
                    if (operatingSystem >= BlurMinimum) return BackdropMode.Blur;
                    return BackdropMode.Translucent;

                case BackdropMode.Blur:
                    return operatingSystem >= BlurMinimum
                        ? BackdropMode.Blur
                        : BackdropMode.Translucent;

                default:
                    return requested;
            }
        }

        /// <summary>
        /// Opacity that a given mode should settle at. Only fully opaque for
        /// <see cref="BackdropMode.None"/>.
        /// </summary>
        internal static double OpacityFor(BackdropMode mode, int transparencyPercent)
        {
            // The system backdrop is composited by DWM behind a fully opaque window,
            // so layered-window opacity must stay at 1 or it would wash out the text
            // for no benefit.
            if (mode == BackdropMode.None || mode == BackdropMode.SystemAcrylic) return 1D;

            int clamped = Math.Max(MinTransparency, Math.Min(MaxTransparency, transparencyPercent));
            return clamped / 100D;
        }

        /// <summary>The accent API takes its gradient colour as ABGR, not ARGB.</summary>
        internal static uint ToAbgr(Color color, int alphaPercent)
        {
            uint alpha = (uint)Math.Max(0, Math.Min(255, alphaPercent * 255 / 100));
            return (alpha << 24) | ((uint)color.B << 16) | ((uint)color.G << 8) | color.R;
        }

        /// <summary>
        /// Whether the accent blur policy is worth applying. Behind a fully opaque
        /// window the blur cannot show through at all, and leaving the policy applied
        /// makes GDI child controls - which paint no alpha - composite as fully
        /// transparent, which is what made the query box and its text disappear.
        /// </summary>
        internal static bool UsesAccentPolicy(BackdropMode effective, double opacity)
        {
            if (effective != BackdropMode.Blur && effective != BackdropMode.Acrylic)
                return false;

            return opacity < 1D;
        }

        /// <summary>
        /// Applies the backdrop and returns the mode actually achieved, which may be
        /// weaker than requested if the OS or the compositor refused it.
        /// </summary>
        public static BackdropMode Apply(Form form, BackdropMode requested, int transparencyPercent, Color tint, bool darkMode)
        {
            if (form == null || !form.IsHandleCreated) return BackdropMode.None;

            var effective = Resolve(requested, Environment.OSVersion.Version);

            // Opacity has to be set before the accent policy: both manipulate
            // WS_EX_LAYERED, and doing it the other way round drops the blur.
            double opacity = OpacityFor(effective, transparencyPercent);
            form.Opacity = opacity;

            if (effective == BackdropMode.SystemAcrylic && !TryApplySystemBackdrop(form.Handle, darkMode))
            {
                // Fall back to the accent implementation on the same window.
                effective = Resolve(BackdropMode.Acrylic, Environment.OSVersion.Version);
                opacity = OpacityFor(effective, transparencyPercent);
                form.Opacity = opacity;
            }

            if (effective == BackdropMode.Blur || effective == BackdropMode.Acrylic)
            {
                if (!UsesAccentPolicy(effective, opacity)) return BackdropMode.None;

                int state = effective == BackdropMode.Acrylic
                    ? AccentEnableAcrylicBlurBehind
                    : AccentEnableBlurBehind;

                if (!TryApplyAccent(form.Handle, state, ToAbgr(tint, 60)))
                    effective = BackdropMode.Translucent;
            }

            return effective;
        }

        /// <summary>
        /// Asks DWM for a transient-window acrylic backdrop. The glass region itself is
        /// applied separately by <see cref="ExtendGlassToBottom"/>, because extending it
        /// across the whole client area makes GDI child controls - which carry no alpha -
        /// render as see-through holes.
        /// </summary>
        private static bool TryApplySystemBackdrop(IntPtr handle, bool darkMode)
        {
            try
            {
                // Without this the compositor draws its light-mode acrylic, leaving
                // light text on a light panel.
                int dark = darkMode ? 1 : 0;
                NativeMethods.DwmSetWindowAttribute(
                    handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

                int backdropType = DwmsbtTransientWindow;
                return NativeMethods.DwmSetWindowAttribute(
                    handle, DwmwaSystemBackdropType, ref backdropType, sizeof(int)) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Extends the glass upward from the bottom edge by <paramref name="height"/>
        /// pixels, leaving the top of the window as ordinary opaque client area so the
        /// query box paints normally.
        /// </summary>
        public static void ExtendGlassToBottom(IntPtr handle, int height)
        {
            if (handle == IntPtr.Zero) return;

            var margins = new NativeMethods.Margins
            {
                Left = 0,
                Right = 0,
                Top = 0,
                Bottom = Math.Max(0, height)
            };

            try
            {
                NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        public static void ApplyRoundedCorners(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            if (Environment.OSVersion.Version < RoundedCornersMinimum) return;

            int preference = DwmwcpRound;
            try
            {
                NativeMethods.DwmSetWindowAttribute(
                    handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // dwmapi is absent on very old Windows; square corners are fine.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static bool TryApplyAccent(IntPtr handle, int accentState, uint gradientColor)
        {
            var policy = new NativeMethods.AccentPolicy
            {
                AccentState = accentState,
                AccentFlags = 2,
                GradientColor = gradientColor,
                AnimationId = 0
            };

            int size = Marshal.SizeOf(policy);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, buffer, false);
                var data = new NativeMethods.WindowCompositionAttributeData
                {
                    Attribute = WcaAccentPolicy,
                    Data = buffer,
                    SizeOfData = size
                };
                return NativeMethods.SetWindowCompositionAttribute(handle, ref data) != 0;
            }
            catch (EntryPointNotFoundException)
            {
                // Undocumented export; absent on Windows 8 and earlier.
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct AccentPolicy
            {
                public int AccentState;
                public int AccentFlags;
                public uint GradientColor;
                public int AnimationId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct WindowCompositionAttributeData
            {
                public int Attribute;
                public IntPtr Data;
                public int SizeOfData;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct Margins
            {
                public int Left;
                public int Right;
                public int Top;
                public int Bottom;
            }

            [DllImport("user32.dll")]
            public static extern int SetWindowCompositionAttribute(
                IntPtr hwnd, ref WindowCompositionAttributeData data);

            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(
                IntPtr hwnd, int attribute, ref int value, int size);

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(
                IntPtr hwnd, ref Margins margins);
        }
    }
}
