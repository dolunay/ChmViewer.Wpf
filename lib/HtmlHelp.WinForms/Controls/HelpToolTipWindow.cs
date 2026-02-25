using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace HtmlHelp.WinForms.Controls
{
	/// <summary>
	/// The class <c>HelpToolTipWindow</c> implements a native tooltip window
	/// </summary>
	public class HelpToolTipWindow
	{
		/// <summary>
		/// Constant specifying the thread name for a tool window
		/// </summary>
		private const string ThreadName = "HelpToolTipwindow";
		/// <summary>
		/// Constant specifying the window class name
		/// </summary>
		private const string WindowClassName = "HelpToolTipwindow";
		/// <summary>
		/// Internal member for the current tooltip window
		/// </summary>
		private static HelpToolTipWindow current;
		/// <summary>
		/// Window procedure of the splash screen
		/// </summary>
		private static HtmlHelp.Interop.WNDPROC tooltipWindowProcedure;
		/// <summary>
		/// Internal member specifying if a shadow should be drawn
		/// </summary>
		private bool _showShadow;
		/// <summary>
		/// Internal member storing the max toolwindow width
		/// </summary>
		private int _widthMax = 250;
		/// <summary>
		/// Internal member storing the width of the window
		/// </summary>
		private int _width = 5;
		/// <summary>
		/// Internal member storing the height of the window
		/// </summary>
		private int _height = 5;
		/// <summary>
		/// Internal member storing the handle to the window
		/// </summary>
		private IntPtr _hwnd;
		/// <summary>
		/// Internal member storing the timer id
		/// </summary>
		private int _timer;
		/// <summary>
		/// Internal member specifying the maximum show duration 
		/// after loosing the focus.
		/// </summary>
		private int _maximumDuration=0;
		/// <summary>
		/// Internal member specifying if we wait for the timer
		/// </summary>
		private bool _waitingForTimer = true;
		/// <summary>
		/// Internal member specifying the window which should be activated
		/// </summary>
		private IWin32Window _windowToActivate;
		/// <summary>
		/// Intneral flag specifying if the toolwindow is visible or not
		/// </summary>
		private bool _visible=false;
		/// <summary>
		/// Internal memnber storing the window text
		/// </summary>
		private string _text = "";
		/// <summary>
		/// Internal member storing the display location
		/// </summary>
		private Point _location = new Point(10,10);

		/// <summary>
		/// Gets/Sets the window text
		/// </summary>
		public string Text
		{
			get { return _text; }
			set
			{
				if(value==null)
					value = string.Empty;

				_text = value;

				if (_hwnd != IntPtr.Zero)
				{
					HtmlHelp.Interop.UpdateWindow(_hwnd);
				}
			}
		}
		/// <summary>
		/// Gets/Sets the maximum display duration after loosing the focus
		/// </summary>
		public int MaximumDuration
		{
			get
			{
				return _maximumDuration;
			}

			set
			{
				if (value < 0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (_hwnd != IntPtr.Zero)
				{
					throw new InvalidOperationException();
				}
				_maximumDuration = value;
			}
		}

		/// <summary>
		/// Gets/Sets if a shadow should be drawn
		/// </summary>
		public bool ShowShadow
		{
			get
			{
				return _showShadow;
			}

			set
			{
				if (_hwnd != IntPtr.Zero)
				{
					throw new InvalidOperationException();
				}
				_showShadow = value;
			}
		}

		/// <summary>
		/// Gets a flag specifying if the toolwindow is currently visible or not
		/// </summary>
		public bool Visible
		{
			get
			{
				if(_hwnd == IntPtr.Zero)
				{
					return false;
				} 
				return _visible;
			}
		}

		/// <summary>
		/// Gets/Sets the window location
		/// </summary>
		public Point Location
		{
			get { return _location; }
			set { _location = value; }
		}

		/// <summary>
		/// Gets/Sets the maximum tooltip window width
		/// </summary>
		public int MaxWidth
		{
			get { return _widthMax; }
			set 
			{
				if(value >= 100)
					_widthMax = value;
			}
		}

		/// <summary>
		/// Creates a native window using the API-call CreateWindowEx()
		/// </summary>
		/// <returns>true if succeeded, otherwise false</returns>
		private bool CreateNativeWindow()
		{
			bool flag = false;
			int i1 = -1879048192;
			int j = HtmlHelp.Interop.WS_EX_TOOLWINDOW|HtmlHelp.Interop.WS_EX_TOPMOST;

			_hwnd = HtmlHelp.Interop.CreateWindowEx(j, HelpToolTipWindow.WindowClassName, "", i1, _location.X, _location.Y, _width, _height, IntPtr.Zero, IntPtr.Zero, HtmlHelp.Interop.GetModuleHandle(null), null);
			if (_hwnd != IntPtr.Zero)
			{
				CalculateWindowSize();
				HtmlHelp.Interop.ShowWindow(_hwnd, 1);
				HtmlHelp.Interop.UpdateWindow(_hwnd);
				flag = true;
			}
			return flag;
		}

		/// <summary>
		/// Shows the tooltip window
		/// </summary>
		public void Show()
		{
			if (_hwnd == IntPtr.Zero)
			{
				if(current._hwnd!=IntPtr.Zero)
					current.Hide(null);

				Thread thread = new Thread(new ThreadStart(ToolTipThreadProcedure));
				thread.Name = HelpToolTipWindow.ThreadName;
				thread.ApartmentState = ApartmentState.STA;
				thread.Start();
				thread.IsBackground = true;
				_visible = true;
			}
		}

		/// <summary>
		/// Hindes the window
		/// </summary>
		/// <param name="windowToActivate">window which should be activated</param>
		public void Hide(IWin32Window windowToActivate)
		{
			_windowToActivate = windowToActivate;
			if (_maximumDuration > 0)
			{
				if(_waitingForTimer)
				{
					_waitingForTimer=false;
					HtmlHelp.Interop.KillTimer(_hwnd, _timer);
				}
			}
			if (_hwnd != IntPtr.Zero)
			{
				HtmlHelp.Interop.DestroyWindow(_hwnd);
				HtmlHelp.Interop.PostMessage(_hwnd, HtmlHelp.Interop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
			}
			_visible = false;
		}

		/// <summary>
		/// Checks if a shadow can be drawn or not
		/// </summary>
		/// <returns>true if a shadow can be drawn</returns>
		private bool IsDropShadowSupported()
		{
			return Environment.OSVersion.Version.CompareTo(new Version(5, 1, 0, 0)) < 0 == false;
		}

		/// <summary>
		/// Registeres the window class using the API-call RegisterClass()
		/// </summary>
		/// <returns></returns>
		private bool RegisterWindowClass()
		{
			tooltipWindowProcedure = new HtmlHelp.Interop.WNDPROC(this.ToolTipWindowProcedure);
			bool flag = false;
			HtmlHelp.Interop.WNDCLASS interop_WNDCLASS = new HtmlHelp.Interop.WNDCLASS();
			interop_WNDCLASS.style = 0;
			interop_WNDCLASS.lpfnWndProc = tooltipWindowProcedure;
			interop_WNDCLASS.hInstance = HtmlHelp.Interop.GetModuleHandle(null);
			interop_WNDCLASS.hbrBackground = (IntPtr)6;
			interop_WNDCLASS.lpszClassName = HelpToolTipWindow.WindowClassName;
			interop_WNDCLASS.cbClsExtra = 0;
			interop_WNDCLASS.cbWndExtra = 0;
			interop_WNDCLASS.hIcon = IntPtr.Zero;
			interop_WNDCLASS.hCursor = Cursors.Arrow.Handle;
			interop_WNDCLASS.lpszMenuName = null;
			if (_showShadow && IsDropShadowSupported())
			{
				interop_WNDCLASS.style |= HtmlHelp.Interop.CS_DROPSHADOW;
			}
			if (HtmlHelp.Interop.RegisterClass(interop_WNDCLASS) != IntPtr.Zero)
			{
				flag = true;
			}
			return flag;
		}

		/// <summary>
		/// Windowthread for the tooltip
		/// </summary>
		private static void ToolTipThreadProcedure()
		{
			bool flag = true;
			if (tooltipWindowProcedure == null)
			{
				flag = current.RegisterWindowClass();
			}
			if (flag)
			{
				flag = current.CreateNativeWindow();
			}
			if (flag)
			{
				HtmlHelp.Interop.MSG interop_MSG = new HtmlHelp.Interop.MSG();
				while (HtmlHelp.Interop.GetMessage(ref interop_MSG, IntPtr.Zero, 0, 0))
				{
					HtmlHelp.Interop.TranslateMessage(ref interop_MSG);
					HtmlHelp.Interop.DispatchMessage(ref interop_MSG);
				}
				current._hwnd = IntPtr.Zero;
				if (current._windowToActivate != null)
				{
					IntPtr i = current._windowToActivate.Handle;
					current._windowToActivate = null;
					HtmlHelp.Interop.SetForegroundWindow(HtmlHelp.Interop.GetLastActivePopup(i));
				}
			}
		}

		/// <summary>
		/// Window procedure for the tooltip
		/// </summary>
		/// <param name="hwnd">handle of window</param>
		/// <param name="msg">WM_ constant</param>
		/// <param name="wParam">wParam of message</param>
		/// <param name="lParam">lParam of message</param>
		/// <returns>0 if message was handled, otherwise 1</returns>
		private int ToolTipWindowProcedure(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
		{
			int j = msg;
			if (j <= HtmlHelp.Interop.WM_PAINT)
			{
				switch (j)
				{
					case HtmlHelp.Interop.WM_SETFOCUS:
					{
						if (_maximumDuration > 0)
						{
							if(_timer>0)
								HtmlHelp.Interop.KillTimer(hwnd, current._timer);
						} 

					};break;
					case HtmlHelp.Interop.WM_KILLFOCUS:
					{
						if (_maximumDuration > 0)
						{
							current._timer = HtmlHelp.Interop.SetTimer(hwnd, 1, current._maximumDuration, IntPtr.Zero);
						}
						else 
						{
							current.Hide(null);
						}
					};break;
					case HtmlHelp.Interop.WM_CREATE:

						break;

					case HtmlHelp.Interop.WM_DESTROY:
						_visible=false;
						HtmlHelp.Interop.PostQuitMessage(0);
						break;

					default:
						if (j == HtmlHelp.Interop.WM_PAINT)
						{
							HtmlHelp.Interop.PAINTSTRUCT interop_PAINTSTRUCT = new HtmlHelp.Interop.PAINTSTRUCT();
							IntPtr i = HtmlHelp.Interop.BeginPaint(hwnd, out interop_PAINTSTRUCT);
							if (i != IntPtr.Zero)
							{
								Graphics graphics = Graphics.FromHdcInternal(i);
								Draw(graphics);
								graphics.Dispose();
							}
							HtmlHelp.Interop.EndPaint(hwnd, ref interop_PAINTSTRUCT);
							return 0;
						}
						break;
				}
			}
			else
			{
				if (j == HtmlHelp.Interop.WM_ERASEBKGND)
				{
					return 1;
				}
				if (j == HtmlHelp.Interop.WM_TIMER)
				{
					HtmlHelp.Interop.KillTimer(hwnd, current._timer);
					current._timer = 0;
					if (current._waitingForTimer)
					{
						//Interop.PostMessage(hwnd, Interop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
						//_visible=false;
						current.Hide(null);
					}
					return 0;
				}
			}
			return HtmlHelp.Interop.DefWindowProc(hwnd, msg, wParam, lParam);
		}

		/// <summary>
		/// Calculates the window size and position
		/// </summary>
		private void CalculateWindowSize()
		{
			if(current._hwnd == IntPtr.Zero)
				return;

			Graphics g = Graphics.FromHwnd(current._hwnd);

			Font fnt = new Font("Arial", 9f);

			StringFormat sf = new StringFormat();
			sf.Alignment = StringAlignment.Near;
			sf.LineAlignment = StringAlignment.Near;

			SizeF sz = g.MeasureString(current._text, fnt, _widthMax, sf);

			int nCX = (int) Math.Round((double)sz.Width) + 4;
			int nCY = (int) Math.Round((double)sz.Height) + 4;

			if((current._width < nCX)||(current._height < nCY))
			{
				current._width = nCX;
				current._height = nCY;
			HtmlHelp.Interop.MoveWindow(current._hwnd, _location.X, _location.Y, current._width,current._height, true);
			}

			fnt.Dispose();
		}

		/// <summary>
		/// Draws the tooltip window
		/// </summary>
		/// <param name="g"></param>
		private void Draw(Graphics g)
		{
			Font fnt = new Font("Arial", 9f);
			Pen penBlack = new Pen(Brushes.Black,1f);

			StringFormat sf = new StringFormat();
			sf.Alignment = StringAlignment.Near;
			sf.LineAlignment = StringAlignment.Near;

			g.FillRectangle(SystemBrushes.Info,0,0,current._width,current._height);
			g.DrawRectangle(penBlack,0,0,current._width-1,current._height-1);
			g.DrawString(current._text, fnt, Brushes.Black, new RectangleF(2f,2f,(float)current._width, (float)current._height),sf);

			fnt.Dispose();
			penBlack.Dispose();
		}

		/// <summary>
		/// Standard constructor
		/// </summary>
		public HelpToolTipWindow()
		{
			current = this;
		}
	}
}
