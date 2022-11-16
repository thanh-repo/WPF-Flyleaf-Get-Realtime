﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FlyleafLib
{
    #pragma warning disable CA1401 // P/Invokes should not be visible
    public static partial class Utils
    {
        public static class NativeMethods
        {
            static NativeMethods()
            {
                if (IntPtr.Size == 4)
                {
                    GetWindowLong = GetWindowLongPtr32;
                    SetWindowLong = SetWindowLongPtr32;
                }
                else
                {
                    GetWindowLong = GetWindowLongPtr64;
                    SetWindowLong = SetWindowLongPtr64;
                }

                GetDPI(out DpiX, out DpiY);
            }

            public static Func<IntPtr, int, IntPtr, IntPtr> SetWindowLong;
            public static Func<IntPtr, int, IntPtr> GetWindowLong;

            [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
            public static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
            public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
            public static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] // , SetLastError = true
            public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll")]
            public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, UInt32 uFlags);

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            public static extern int StrCmpLogicalW(string psz1, string psz2);

            [DllImport("user32.dll")]
            public static extern int ShowCursor(bool bShow);

            [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
            public static extern uint TimeBeginPeriod(uint uMilliseconds);

            [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
            public static extern uint TimeEndPeriod(uint uMilliseconds);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto,SetLastError = true)]
            public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
            [FlagsAttribute]
            public enum EXECUTION_STATE :uint
            {
                ES_AWAYMODE_REQUIRED    = 0x00000040,
                ES_CONTINUOUS           = 0x80000000,
                ES_DISPLAY_REQUIRED     = 0x00000002,
                ES_SYSTEM_REQUIRED      = 0x00000001
            }

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

            [DllImport("User32.dll")]
            public static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

            [DllImport("User32.dll")]
            public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hwnd, ref RECT rectangle);

            [DllImport("user32.dll")]
            public static extern bool GetWindowInfo(IntPtr hwnd, ref WINDOWINFO pwi);

            [StructLayout(LayoutKind.Sequential)]
            public struct WINDOWINFO
            {
                public uint cbSize;
                public RECT rcWindow;
                public RECT rcClient;
                public uint dwStyle;
                public uint dwExStyle;
                public uint dwWindowStatus;
                public uint cxWindowBorders;
                public uint cyWindowBorders;
                public ushort atomWindowType;
                public ushort wCreatorVersion;

                public WINDOWINFO(Boolean? filler) : this()   // Allows automatic initialization of "cbSize" with "new WINDOWINFO(null/true/false)".
                {
                    cbSize = (UInt32)(Marshal.SizeOf(typeof( WINDOWINFO )));
                }

            }
            public struct RECT
            {
                public int Left     { get; set; }
                public int Top      { get; set; }
                public int Right    { get; set; }
                public int Bottom   { get; set; }
            }

            [Flags]
            public enum SetWindowPosFlags : uint
            {
                SWP_ASYNCWINDOWPOS = 0x4000,
                SWP_DEFERERASE = 0x2000,
                SWP_DRAWFRAME = 0x0020,
                SWP_FRAMECHANGED = 0x0020,
                SWP_HIDEWINDOW = 0x0080,
                SWP_NOACTIVATE = 0x0010,
                SWP_NOCOPYBITS = 0x0100,
                SWP_NOMOVE = 0x0002,
                SWP_NOOWNERZORDER = 0x0200,
                SWP_NOREDRAW = 0x0008,
                SWP_NOREPOSITION = 0x0200,
                SWP_NOSENDCHANGING = 0x0400,
                SWP_NOSIZE = 0x0001,
                SWP_NOZORDER = 0x0004,
                SWP_SHOWWINDOW = 0x0040,
            }

            [Flags]
            public enum WindowLongFlags : int
            {
                 GWL_EXSTYLE = -20,
                 GWLP_HINSTANCE = -6,
                 GWLP_HWNDPARENT = -8,
                 GWL_ID = -12,
                 GWL_STYLE = -16,
                 GWL_USERDATA = -21,
                 GWL_WNDPROC = -4,
                 DWLP_USER = 0x8,
                 DWLP_MSGRESULT = 0x0,
                 DWLP_DLGPROC = 0x4
            }

            public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            public static int SignedHIWORD(IntPtr n) => SignedHIWORD(unchecked((int)(long)n));
            public static int SignedLOWORD(IntPtr n) => SignedLOWORD(unchecked((int)(long)n));
            public static int SignedHIWORD(int n) => (short)((n >> 16) & 0xffff);
            public static int SignedLOWORD(int n) => (short)(n & 0xFFFF);

            #region DPI
            public static double DpiX, DpiY;
            const int LOGPIXELSX = 88, LOGPIXELSY = 90;
            [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
            public static extern int GetDeviceCaps(IntPtr hDC, int nIndex);
            public static void GetDPI(out double dpiX, out double dpiY) => GetDPI(IntPtr.Zero, out dpiX, out dpiY);
            public static void GetDPI(IntPtr handle, out double dpiX, out double dpiY)
            {
                Graphics GraphicsObject = Graphics.FromHwnd(handle); // DESKTOP Handle
                IntPtr dcHandle = GraphicsObject.GetHdc();
                dpiX = GetDeviceCaps(dcHandle, LOGPIXELSX) / 96.0;
                dpiY = GetDeviceCaps(dcHandle, LOGPIXELSY) / 96.0;
                GraphicsObject.ReleaseHdc(dcHandle);
                GraphicsObject.Dispose();
            }
            #endregion
        }
    }
    #pragma warning restore CA1401 // P/Invokes should not be visible
}