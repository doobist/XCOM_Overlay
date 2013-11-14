using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Shapes;
using WindowsInput;

public class ApplicationInteractionHelper
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    private string _processName;

    public ApplicationInteractionHelper(string processName)
    {
        _processName = processName;
    }

    public  void SendKeystroke(VirtualKeyCode key)
    {
        InputSimulator.SimulateKeyPress(key);
    }

    public  void SendKeystroke(VirtualKeyCode key1, VirtualKeyCode key2)
    {
        InputSimulator.SimulateModifiedKeyStroke(key1,key2);
    }

    public  void SendKeystroke(IEnumerable<VirtualKeyCode> keys, VirtualKeyCode key2)
    {
        InputSimulator.SimulateModifiedKeyStroke(keys,key2);
    }

    public  void SendKeyDown(VirtualKeyCode key)
    {
        InputSimulator.SimulateKeyDown(key);
    }

    public  void SendKeyUp(VirtualKeyCode key)
    {
        InputSimulator.SimulateKeyUp(key);
    }

    public void LeftMouseClick()
    {
        InputSimulator.SimulateKeyPress(VirtualKeyCode.LBUTTON);
    }

    public void RightMouseClick()
    {
        InputSimulator.SimulateKeyPress(VirtualKeyCode.RBUTTON);
    }

    public void SetProcessFocus()
    {
        foreach (Process p in Process.GetProcessesByName(_processName))
        {
            IntPtr handle = p.MainWindowHandle;
            int X = 50;
            int Y = 380;
            IntPtr lParam = (IntPtr)((Y << 16) | X);
            IntPtr wParam = IntPtr.Zero;

            SetForegroundWindow(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner
    }

    public void OverlayApplication(Window appToOverlayToProcess)
    {
        double dpiX = 1;
        double dpiY = 1;
        PresentationSource presentationsource = PresentationSource.FromVisual(appToOverlayToProcess);

        if (presentationsource != null)
        {
            dpiX = presentationsource.CompositionTarget.TransformFromDevice.M11;
            dpiY = presentationsource.CompositionTarget.TransformFromDevice.M22;
        }

        foreach (Process p in Process.GetProcessesByName(_processName))
        {
            IntPtr handle = p.MainWindowHandle;
            RECT rct = new RECT();
            GetWindowRect(handle, ref rct);

            appToOverlayToProcess.Top =  dpiY *  rct.Top + dpiY *  30;
            appToOverlayToProcess.Left =dpiX * (rct.Left);
            appToOverlayToProcess.Width = dpiX * rct.Right - dpiX * rct.Left;
            appToOverlayToProcess.Height = dpiY * rct.Bottom - dpiY * rct.Top - dpiY * 30;
        }
    }

    public bool IsProcessRunning()
    {
        foreach (Process p in Process.GetProcessesByName(_processName))
        {
            return true;
        }

        return false;
    }


}