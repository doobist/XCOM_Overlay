using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for xComHelper.xaml
    /// </summary>
    public partial class xComHelper : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        //These allow us to set the window to the top, but not take focus away from the windows below.
        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        //This will assist with debugging so we can see where the cursor actually is
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            //Set the window style to noactivate.
            WindowInteropHelper helper = new WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE,
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
            _focuser.OverlayApplication(this);
        }

        private ApplicationInteractionHelper _focuser;

        private bool _writeToLog;
        private bool _invertScrolling = true;

        public xComHelper()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            string procName = "XComGame";

            //uncomment to allow to debug with notepad (to see the output as text)
            //procName = "notepad";

            _focuser = new ApplicationInteractionHelper(procName);

            if (!_focuser.IsProcessRunning())
            {
                MessageBox.Show("XCOM is not running. Please start before launching this application.");
                this.Close();
            }

            _focuser.SetProcessFocus();
            pnlNumbers.Visibility = System.Windows.Visibility.Hidden;

            //uncomment to view events on screen
            //_writeToLog = true;

            if (!_writeToLog)
            {
                lblEvent.Visibility = System.Windows.Visibility.Hidden;
            }

        }

        //Load the external assembly via resource so we can only have one .exe file
        System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(',') ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");

            dllName = dllName.Replace(".", "_");

            if (dllName.EndsWith("_resources")) return null;

            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());

            byte[] bytes = (byte[])rm.GetObject(dllName);

            return System.Reflection.Assembly.Load(bytes);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        private void Window_ManipulationStarting_1(object sender, ManipulationStartingEventArgs e)
        {
            e.ManipulationContainer = this;
            e.Handled = true;
        }

        [Flags]
        private enum Direction
        {
            Default = 0x0,
            Up = 0x1,
            Down = 0x2,
            Left = 0x4,
            Right = 0x8
        }

        //Write events somewhere (to textbox on screen for example)
        private void WriteEvent(object sender, string eventName)
        {
            var p = GetMousePosition();

            string name = "Object";

            if (sender as Control != null)
            {
                name = ((Control)sender).Name;
            }

            lblEvent.Text += string.Format(" ({0},{1})", (int)p.X, (int)p.Y) + name + " - " + eventName + Environment.NewLine;
            lblEvent.ScrollToEnd();
        }

        //These allow us to keep track of which way the user is scrolling with their finger. Old is to compare to the current
        private Direction _currentDirection;
        private Direction _oldDirection;        

        private void Window_ManipulationDelta_1(object sender, ManipulationDeltaEventArgs e)
        {
            //If not allowing scrolling, then just exit
            if (_scrollDisabled)
            {
                return;
            }

            double x = e.DeltaManipulation.Translation.X;
            double y = e.DeltaManipulation.Translation.Y;

            if (x == 0 && y == 0)
            {
                return;
            }

            if (!_invertScrolling)
            {
                x = -x;
                y = -y;
            }

            //These just assist with knowing now many pixels, ignoring orientation
            double nonNegativeX = x;
            double nonNegatieY = y;

            if (x < 0)
                nonNegativeX = -x;

            if (y < 0)
                nonNegatieY = -y;

            //how many pixels do we count as a scroll?
            double pixelLeeway = 5;

            _currentDirection = Direction.Default;

            if (nonNegativeX > pixelLeeway)
            {
                if (x > 0)
                {
                    _currentDirection |= Direction.Left;
                }
                else
                {
                    _currentDirection |= Direction.Right;
                }
            }

            if (nonNegatieY > pixelLeeway)
            {
                if (y > 0)
                {
                    _currentDirection |= Direction.Up;
                }
                else
                {
                    _currentDirection |= Direction.Down;
                }
            }

            //Press, or unpress the direction keys (w,a,s,d) based on direction
            if ((_currentDirection & Direction.Up) == Direction.Up)
            {
                if ((_oldDirection & Direction.Up) != Direction.Up)
                {
                    WriteEvent(cmdOverlay, string.Format("+Up ({0},{1})", x, y));
                    _focuser.SendKeyDown(WindowsInput.VirtualKeyCode.VK_W);
                }
            }
            else if ((_oldDirection & Direction.Up) == Direction.Up)
            {
                WriteEvent(cmdOverlay, string.Format("-Up ({0},{1})", x, y));
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_W);
            }

            if ((_currentDirection & Direction.Down) == Direction.Down)
            {
                if ((_oldDirection & Direction.Down) != Direction.Down)
                {
                    WriteEvent(cmdOverlay, string.Format("+Down ({0},{1})", x, y));
                    _focuser.SendKeyDown(WindowsInput.VirtualKeyCode.VK_S);
                }
            }
            else if ((_oldDirection & Direction.Down) == Direction.Down)
            {
                WriteEvent(cmdOverlay, string.Format("-Down ({0},{1})", x, y));
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_S);
            }

            if ((_currentDirection & Direction.Left) == Direction.Left)
            {
                if ((_oldDirection & Direction.Left) != Direction.Left)
                {
                    WriteEvent(cmdOverlay, string.Format("+Left ({0},{1})", x, y));
                    _focuser.SendKeyDown(WindowsInput.VirtualKeyCode.VK_A);
                }
            }
            else if ((_oldDirection & Direction.Left) == Direction.Left)
            {
                WriteEvent(cmdOverlay, string.Format("-Left ({0},{1})", x, y));
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_A);
            }

            if ((_currentDirection & Direction.Right) == Direction.Right)
            {
                if ((_oldDirection & Direction.Right) != Direction.Right)
                {
                    WriteEvent(cmdOverlay, string.Format("+Right ({0},{1})", x, y));
                    _focuser.SendKeyDown(WindowsInput.VirtualKeyCode.VK_D);
                }
            }
            else if ((_oldDirection & Direction.Right) == Direction.Right)
            {
                WriteEvent(cmdOverlay, string.Format("-Right ({0},{1})", x, y));
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_D);
            }

            _oldDirection = _currentDirection;
        }

        private void cmdClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void EscapeClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.ESCAPE);

            if (_scrollDisabled)
            {
                cmdEnableDisableScroll_TouchDown(sender, null);
            }

            WriteEvent(sender, eventName);
        }

        private void cmdEscape_TouchDown(object sender, TouchEventArgs e)
        {
            WriteEvent(sender, "TouchDown");
            EscapeClick(sender);
        }

        private void cmdEscape_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void cmdClick_TouchDown(object sender, TouchEventArgs e)
        {
            RightMouseClick(sender);
        }

        private void cmdClick_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void RightMouseClick(object sender, string eventName = "TouchDown")
        {
            _focuser.RightMouseClick();
        }

        private void LeftMouseClick(object sender, string eventName = "TouchDown")
        {
            _focuser.LeftMouseClick();
        }

        //allow passthrough of a right click
        private void cmdOverlay_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            WriteEvent(sender, "Mouse Right Button");
            RightMouseClick(sender);
        }

        //Handle the clicking and stop scrolling if necessary
        private void cmdOverlay_TouchUp(object sender, TouchEventArgs e)
        {
            WriteEvent(sender, "TouchUp");

            if ((_currentDirection & Direction.Up) == Direction.Up)
            {
                WriteEvent(cmdOverlay, "-Up");
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_W);
            }

            if ((_currentDirection & Direction.Down) == Direction.Down)
            {
                WriteEvent(cmdOverlay, "-Down");
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_S);
            }

            if ((_currentDirection & Direction.Left) == Direction.Left)
            {
                WriteEvent(cmdOverlay, "-Left");
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_A);
            }

            if ((_currentDirection & Direction.Right) == Direction.Right)
            {
                WriteEvent(cmdOverlay, "-Right");
                _focuser.SendKeyUp(WindowsInput.VirtualKeyCode.VK_D);
            }

            _currentDirection = Direction.Default;
            _oldDirection = Direction.Default;

            if (!_scrollDisabled)
            {
                //DoubleTapHelper(e);
                _focuser.LeftMouseClick();
            }
        }

        private Stopwatch _timer = new Stopwatch();
        private Point _lastPoint;

        //Currently not using this
        private void DoubleTapHelper(TouchEventArgs e)
        {
            var currentPoint = e.GetTouchPoint(this).Position;

            bool isDouble = false;

            if (_lastPoint.X == 0 && _lastPoint.Y == 0)
            {
                _timer.Stop();
            }
            else
            {
                bool tapsAreCloseInDistance = false;

                double distance = 20;

                if (_lastPoint.X + distance > currentPoint.X && _lastPoint.X - distance < currentPoint.X)
                {
                    if (_lastPoint.Y + distance > currentPoint.Y && _lastPoint.Y - distance < currentPoint.Y)
                    {
                        tapsAreCloseInDistance = true;
                    }
                }

                if (tapsAreCloseInDistance)
                {
                    if (!_timer.IsRunning)
                    {
                        _timer.Reset();
                        _timer.Start();
                    }
                    else
                    {
                        TimeSpan elapsed = _timer.Elapsed;

                        //now check timing
                        bool tapsAreCloseInTime = (elapsed != TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(0.7));

                        if (tapsAreCloseInTime)
                        {
                            isDouble = true;
                        }

                        _timer.Stop();
                    }
                }
                else
                {
                    _timer.Stop();
                }

                _lastPoint = currentPoint;
            }

            _lastPoint = currentPoint;

            if (isDouble)
            {
                WriteEvent(cmdOverlay, "Double tap");
                _focuser.RightMouseClick();
                _lastPoint = new Point();
            }
            else
            {
                _focuser.LeftMouseClick();
            }
        }

        private void RotateLeftClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.VK_E);
        }

        private void cmdRotateLeft_TouchDown(object sender, TouchEventArgs e)
        {
            RotateLeftClick(sender);
        }

        private void cmdRotateLeft_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void RotateRightClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.VK_Q);
        }

        private void cmdRotateRight_TouchDown(object sender, TouchEventArgs e)
        {
            RotateRightClick(sender);
        }

        private void cmdRotateRight_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void TabLeftClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.SHIFT, WindowsInput.VirtualKeyCode.TAB);
        }

        private void cmdTabLeft_TouchDown(object sender, TouchEventArgs e)
        {
            TabLeftClick(sender);
        }

        private void cmdTabLeft_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void TabRightClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.TAB);
        }
        
        private void cmdTabRight_TouchDown(object sender, TouchEventArgs e)
        {
            TabRightClick(sender);
        }

        private void cmdTabRight_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void UpClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.VK_F);
        }

        private void cmdUp_TouchDown(object sender, TouchEventArgs e)
        {
            UpClick(sender);
        }

        private void cmdUp_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void DownClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.VK_C);
        }

        private void cmdDown_TouchDown(object sender, TouchEventArgs e)
        {
            DownClick(sender);
        }

        private void cmdDown_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void ZoomInClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.VK_T);
        }

        private void cmdZoomIn_TouchDown(object sender, TouchEventArgs e)
        {
            ZoomInClick(sender);
        }

        private void cmdZoomIn_TouchUp(object sender, TouchEventArgs e)
        {
        }

        private void ZoomOutClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.VK_G);
        }

        private void cmdZoomOut_TouchDown(object sender, TouchEventArgs e)
        {
            ZoomOutClick(sender);
        }

        private void cmdZoomOut_TouchUp(object sender, TouchEventArgs e)
        {
        }


        private void HideMoreClick(object sender, string eventName = "TouchDown")
        {
            pnlMoreHolder.Visibility = Visibility.Hidden;
            cmdClose.Visibility = System.Windows.Visibility.Hidden;
            cmdShowMore.Visibility = System.Windows.Visibility.Visible;
            cmdHideMore.Visibility = System.Windows.Visibility.Hidden;
        }

        private void cmdHideMore_TouchDown(object sender, TouchEventArgs e)
        {
            HideMoreClick(sender);
        }

        private void ShowMoreClick(object sender, string eventName = "TouchDown")
        {
            pnlMoreHolder.Visibility = Visibility.Visible;
            cmdClose.Visibility = System.Windows.Visibility.Visible;
            cmdShowMore.Visibility = System.Windows.Visibility.Hidden;
            cmdHideMore.Visibility = System.Windows.Visibility.Visible;
        }

        private void cmdShowMore_TouchDown(object sender, TouchEventArgs e)
        {
            ShowMoreClick(sender);
        }

        private void OkClick(object sender, string eventName = "TouchDown")
        {
            _focuser.SendKeystroke(WindowsInput.VirtualKeyCode.RETURN);

            if (_scrollDisabled)
            {
                EnableDisableScrollClick(sender);
            }
        }

        private void cmdOK_TouchDown(object sender, TouchEventArgs e)
        {
            OkClick(sender);
        }

        private bool _scrollDisabled;

        //used to help change the color of the text.
        Brush _brushHolder;

        //Need to hide certain items and change the button notification text
        private void EnableDisableScrollClick(object sender, string eventName = "TouchDown")
        {
            if (!_scrollDisabled)
            {
                if (_brushHolder == null)
                {
                    _brushHolder = cmdEnableDisableScroll.Foreground;
                }

                cmdEnableDisableScroll.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#ff0000"));

                if (pnlNumbers.Visibility != System.Windows.Visibility.Visible)
                {
                    ShowNumbersClick(sender);
                }

                cmdEnableDisableScroll_Text2.Text = "On. (Scrolling Disabled)";

                pnlNonAbilityButtons.Visibility = System.Windows.Visibility.Hidden;

                cmdClick.Visibility = System.Windows.Visibility.Hidden;
                cmdOK.Visibility = System.Windows.Visibility.Hidden;
                cmdEscape.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                if (pnlNumbers.Visibility == System.Windows.Visibility.Visible)
                {
                    ShowNumbersClick(sender);
                }

                cmdEnableDisableScroll_Text2.Text = "Off";
                cmdEnableDisableScroll.Foreground = _brushHolder;

                pnlNonAbilityButtons.Visibility = System.Windows.Visibility.Visible;

                cmdClick.Visibility = System.Windows.Visibility.Visible;
                cmdOK.Visibility = System.Windows.Visibility.Visible;
                cmdEscape.Visibility = System.Windows.Visibility.Visible;
            }

            _scrollDisabled = !_scrollDisabled;
        }

        private void cmdEnableDisableScroll_TouchDown(object sender, TouchEventArgs e)
        {
            EnableDisableScrollClick(sender);
        }

        //Handles all numbers
        private void NumberClick(object sender, WindowsInput.VirtualKeyCode code, string eventName = "TouchDown")
        {
            if (_scrollDisabled)
            {
                pnlNumbers.Visibility = System.Windows.Visibility.Hidden;
                pnlNonAbilityButtons.Visibility = System.Windows.Visibility.Visible;
                cmdOK.Visibility = System.Windows.Visibility.Visible;
                cmdEscape.Visibility = System.Windows.Visibility.Visible;
            }

            _focuser.SendKeystroke(code);
        }

        private void cmd1_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_1);
        }

        private void cmd2_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_2);
        }

        private void cmd3_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_3);
        }

        private void cmd4_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_4);
        }

        private void cmd5_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_5);
        }

        private void cmd6_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_6);
        }

        private void cmd7_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_7);
        }

        private void cmd8_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_8);
        }

        private void cmd9_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_9);
        }

        private void cmd10_TouchDown(object sender, TouchEventArgs e)
        {
            NumberClick(sender, WindowsInput.VirtualKeyCode.VK_0);
        }

        private void ShowNumbersClick(object sender, string eventName = "TouchDown")
        {
            if (pnlNumbers.Visibility == System.Windows.Visibility.Hidden)
            {
                pnlNumbers.Visibility = System.Windows.Visibility.Visible;
                cmdShowNumbers_Text.Text = "Hide #'s";
            }
            else
            {
                pnlNumbers.Visibility = System.Windows.Visibility.Hidden;
                cmdShowNumbers_Text.Text = "Show #'s";
            }
        }

        private void cmdShowNumbers_TouchDown(object sender, TouchEventArgs e)
        {
            ShowNumbersClick(sender);
        }


        //All these are trying to simulate a click so that a mouse can be used as well, but to no avail
        private void cmdOK_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OkClick(sender);
        }

        private void cmdEnableDisableScroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            EnableDisableScrollClick(sender);
        }

        private void cmdRotateRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RotateRightClick(sender);
        }

        private void cmdRotateLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RotateLeftClick(sender);
        }

        private void cmdTabLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TabLeftClick(sender);
        }

        private void cmdTabRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TabRightClick(sender);
        }

        private void cmdUp_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UpClick(sender);
        }

        private void cmdDown_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DownClick(sender);
        }

        private void cmdZoomIn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ZoomInClick(sender);
        }

        private void cmdZoomOut_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ZoomOutClick(sender);
        }

        private void cmdHideMore_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HideMoreClick(sender);
        }

        private void cmdShowMore_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowMoreClick(sender);
        }

    }
}
