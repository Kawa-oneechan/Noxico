using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SystemMessageBox = System.Windows.Forms.MessageBox;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Noxico
{
	static class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			//Switch to Invariant so we get "¤1,000.50" instead of "€ 1.000,50" or "$1,000.50" by default.
			//Can't do this in certain cases, which should be inapplicable to this program.
			var customCulture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
			customCulture.NumberFormat.CurrencySymbol = "\x13B";
			System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

			if (args.Contains("-spreadem"))
			{
				Mix.SpreadEm();
				return;
			}

			/*
			if (Program.CanWrite())
			{
				try
				{
					var server = "http://helmet.kafuka.org/noxico/files/";
					var expectedVersion = Application.ProductVersion.Substring(0, 5);
					using (var wc = new System.Net.WebClient())
					{
						var gotVersion = wc.DownloadString(server + "version.txt");
						if (gotVersion.Contains(expectedVersion))
							Program.WriteLine("No update required.");
						else
						{
							var answer = SystemMessageBox.Show("A new version of the game is available. Would you like to download it now?" + Environment.NewLine + Environment.NewLine + "This could take a while.", Application.ProductName, MessageBoxButtons.YesNo);
							if (answer == DialogResult.Yes)
							{
								Application.Run(new UpdateForm());
								return;
							}
						}
					}
				}
				catch (System.Net.WebException)
				{
					Program.WriteLine("Couldn't check for updates.");
				}
			}
			*/

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			try
			{
				Application.Run(new MainForm());
			}
			catch (ObjectDisposedException)
			{
			}
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void WriteLine(string format, params object[] arg)
		{
			Console.WriteLine(format, arg);
		}
		[System.Diagnostics.Conditional("DEBUG")]
		public static void Write(string format, params object[] arg)
		{
			Console.Write(format, arg);
		}
		[System.Diagnostics.Conditional("DEBUG")]
		public static void WriteLine(object value)
		{
			Console.WriteLine(value.ToString());
		}

		public static bool CanWrite()
		{
			//var fi = new FileInfo(Application.ExecutablePath);
			//var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			//var isAdmin = UacHelper.IsProcessElevated;
			//var hereIsReadOnly = (fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
			try
			{
				var test = "test.txt";
				File.WriteAllText(test, test);
				File.Delete(test);
			}
			catch (UnauthorizedAccessException)
			{
				return false;
			}
			catch (Exception x)
			{
				SystemMessageBox.Show(x.ToString());
				return false;
			}
			return true;
		}
	}

	public class MainForm : Form, IGameHost
	{
		public NoxicoGame Noxico { get; set; }

		private struct Cell
		{
			public int Character;
			public Color Foreground;
			public Color Background;
#if DEBUG
			public override string ToString()
			{
				return string.Format("U+{0:X4} '{1}'", (int)Character, Character);
			}
#endif

			public static bool operator ==(Cell left, Cell right)
			{
				return left.Equals(right);
			}
			public static bool operator !=(Cell left, Cell right)
			{
				return !left.Equals(right);
			}
			public override bool Equals(object obj)
			{
				if (obj is Cell)
				{
					var objC = (Cell)obj;
					return objC.Character == this.Character &&
						objC.Foreground == this.Foreground &&
						objC.Background == this.Background;
				}
				return base.Equals(obj);
			}
			public override int GetHashCode()
			{
				return base.GetHashCode();
			}
			public void CopyFrom(Cell source)
			{
				this.Character = source.Character;
				this.Foreground = source.Foreground;
				this.Background = source.Background;
			}
		}
		private Cell[,] image = new Cell[100, 60];
		private Cell[,] previousImage = new Cell[100, 60];
		private Bitmap backBuffer;
		private Bitmap scrollBuffer;
		private bool starting = true, fatal = false;

		public bool Running { get; set; }

		private int CellWidth, CellHeight;
		private string pngFont = "8x8-thin";
		private byte[,] fontData;

		public string IniPath { get; set; }
		public new Point Cursor { get; set; }
		private Point prevCursor;
		private Pen[] cursorPens;

		private Dictionary<Keys, Keys> numpad = new Dictionary<Keys, Keys>()
			{
				{ Keys.NumPad8, Keys.Up },
				{ Keys.NumPad2, Keys.Down },
				{ Keys.NumPad4, Keys.Left },
				{ Keys.NumPad6, Keys.Right },
				{ Keys.NumPad5, Keys.OemPeriod },
				//{ Keys.Back, Keys.Escape },
			};

#if DEBUG
		public Timer timer;
#else
		private Timer timer;
#endif

		public int Frames = 0;
		private Timer fpsTimer;
		private bool youtube = false;
		private System.Drawing.Rectangle youtubeRect;

		public MainForm()
		{
#if !DEBUG
			try
#endif
			{
				this.Text = "Noxico";
				this.BackColor = System.Drawing.Color.Black;
				this.DoubleBuffered = true;
				this.FormBorderStyle = FormBorderStyle.FixedSingle;
				this.MaximizeBox = false; //it's about time, too!
				this.FormClosing += new FormClosingEventHandler(this.Form1_FormClosing);
				this.KeyDown += new KeyEventHandler(this.Form1_KeyDown);
				this.KeyPress += new KeyPressEventHandler(this.Form1_KeyPress);
				this.KeyUp += new KeyEventHandler(this.Form1_KeyUp);
				this.Icon = global::Noxico.Properties.Resources.app;
				this.ClientSize = new Size(80 * CellWidth, 50 * CellHeight);
				this.Controls.Add(new Label()
				{
					Text = "Loading...",
					AutoSize = true,
					Font = new System.Drawing.Font("Arial", 24, FontStyle.Bold | FontStyle.Italic),
					ForeColor = System.Drawing.Color.White,
					Visible = true,
					Location = new System.Drawing.Point(16, 16)
				});

				foreach (var reqDll in new[] { "Antlr3.Runtime.dll", "Jint.dll" })
					if (!File.Exists(reqDll))
						throw new FileNotFoundException("Required DLL " + reqDll + " is missing.");

				try
				{
					Mix.Initialize("Noxico");
				}
				catch (UnauthorizedAccessException)
				{
					if (!UacHelper.IsProcessElevated)
					{
						var proc = new System.Diagnostics.ProcessStartInfo();
						proc.UseShellExecute = true;
						proc.WorkingDirectory = Environment.CurrentDirectory;
						proc.FileName = Application.ExecutablePath;
						proc.Verb = "runas";
						try
						{
							System.Diagnostics.Process.Start(proc);
						}
						catch
						{
						}
					}
					Close();
					return;
				}

				if (!Mix.FileExists("credits.txt"))
				{
					SystemMessageBox.Show(this, "Could not find game data. Please redownload the game.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
					Close();
					return;
				}

				var portable = false;
				IniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "noxico.ini");
				if (File.Exists("portable"))
				{
					portable = true;
					var oldIniPath = IniPath;
					IniPath = "noxico.ini";
					Program.CanWrite();
					/*
					if (!Program.CanWrite())
					{
						var response = SystemMessageBox.Show(this, "Trying to start in portable mode, but from a protected location. Use non-portable mode?" + Environment.NewLine + "Selecting \"no\" may cause errors.", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
						if (response == DialogResult.Cancel)
						{
							Close();
							return;
						}
						else if (response == DialogResult.Yes)
						{
							IniPath = oldIniPath;
							portable = false;
						}
					}
					*/
				}

				if (!File.Exists(IniPath))
					File.WriteAllText(IniPath, Mix.GetString("noxico.ini"));
				IniFile.Load(IniPath);

				if (portable)
				{
					IniFile.SetValue("misc", "vistasaves", false);
					IniFile.SetValue("misc", "savepath", "./saves");
					IniFile.SetValue("misc", "shotpath", "./screenshots");
				}

				RestartGraphics();

				Noxico = new NoxicoGame();
				Noxico.Initialize(this);

				MouseUp += new MouseEventHandler(MainForm_MouseUp);
				MouseWheel += new MouseEventHandler(MainForm_MouseWheel);

				GotFocus += (s, e) => { Vista.GamepadFocused = true; };
				LostFocus += (s, e) => { Vista.GamepadFocused = false; };

				Vista.GamepadEnabled = IniFile.GetValue("misc", "xinput", true);

				Program.WriteLine("Environment: {0} {1}", Environment.OSVersion.Platform, Environment.OSVersion);
				Program.WriteLine("Application: {0}", Application.ProductVersion);
				if (Environment.OSVersion.Platform == PlatformID.Unix)
				{
					Program.WriteLine("*** You are running on a *nix system. ***");
					Program.WriteLine("Key repeat delays exaggerated.");
					NoxicoGame.Mono = true;
					Vista.GamepadEnabled = false;
				}

				this.Controls.Clear();
				starting = false;
				Running = true;

				Cursor = new Point(-1, -1);
				cursorPens = new Pen[16];
				cursorPens[0] = Pens.Black;
				for (var i = 1; i < 9; i++)
					cursorPens[i] = cursorPens[16 - i] = new Pen(Color.FromArgb(0, (i * 32) - 1, 0));

				fpsTimer = new Timer()
				{
					Interval = 1000,
					Enabled = true,
				};
				fpsTimer.Tick += (s, e) =>
				{
					this.Text = "Noxico - " + NoxicoGame.Updates + " updates, " + Frames + " frames";
					NoxicoGame.Updates = 0;
					Frames = 0;
				};
#if GAMELOOP
				while (Running)
				{
					Noxico.Update();
					Application.DoEvents();
				}
#else
				FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
				var speed = IniFile.GetValue("misc", "speed", 15);
				if (speed <= 0)
					speed = 15;
				timer = new Timer()
				{
					Interval = speed,
					Enabled = true,
				};
				timer.Tick += new EventHandler(timer_Tick);
#endif
			}
#if !DEBUG
			catch (Exception x)
			{
				new ErrorForm(x).ShowDialog(this);
				SystemMessageBox.Show(this, x.ToString(), Application.ProductName, MessageBoxButtons.OK);
				Running = false;
				fatal = true;
				Application.ExitThread();
			}
#endif
			if (!fatal)
			{
				Noxico.SaveGame();
			}
		}

		void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!fatal)
				Noxico.SaveGame();
		}

		void timer_Tick(object sender, EventArgs e)
		{
#if DEBUG
			Noxico.Update();
#else
			try
			{
				Noxico.Update();
			}
			catch (Exception x)
			{
				new ErrorForm(x).ShowDialog(this);
				SystemMessageBox.Show(this, x.ToString(), Application.ProductName, MessageBoxButtons.OK);
				Running = false;
				fatal = true;
			}
#endif
		}

		public void RestartGraphics()
		{
			pngFont = IniFile.GetValue("misc", "font", "8x8-thin");
			if (!Mix.FileExists("fonts\\" + pngFont + ".png"))
			{
				pngFont = "8x8-thin";
				if (!Mix.FileExists("fonts\\" + pngFont + ".png"))
				{
					SystemMessageBox.Show(this, "Could not find font bitmaps. Please redownload the game.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
					Close();
					return;
				}
			}
			var fontBitmap = Mix.GetBitmap("fonts\\" + pngFont + ".png");
			CellWidth = fontBitmap.Width / 32;
			CellHeight = fontBitmap.Height / 32;
			CachePNGFont(fontBitmap);

			youtube = IniFile.GetValue("misc", "youtube", false);
			ClientSize = new Size(100 * CellWidth, 60 * CellHeight);
			if (youtube)
			{
				//Find nearest YT size
				var eW = 100 * CellWidth;
				var eH = 60 * CellHeight;
				if (eW <= 854 || eH <= 480)
					ClientSize = new Size(854, 480);
				else if (eW <= 1280 || eH <= 720)
					ClientSize = new Size(1280, 720);
				else
					ClientSize = new Size(1920, 1080);

				var prime = Screen.FromRectangle(ClientRectangle).Bounds;
				if (ClientSize.Width == prime.Width && ClientSize.Height == prime.Height)
				{
					FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
					Left = Top = 0;
				}
				else
					FormBorderStyle = IniFile.GetValue("misc", "border", true) ? System.Windows.Forms.FormBorderStyle.FixedSingle : System.Windows.Forms.FormBorderStyle.None;
				youtubeRect = new System.Drawing.Rectangle((ClientSize.Width / 2) - (eW / 2), (ClientSize.Height / 2) - (eH / 2), eW, eH);
			}
			else
				FormBorderStyle = IniFile.GetValue("misc", "border", true) ? System.Windows.Forms.FormBorderStyle.FixedSingle : System.Windows.Forms.FormBorderStyle.None;

			Show();
			Refresh();

			backBuffer = new Bitmap(100 * CellWidth, 60 * CellHeight, PixelFormat.Format24bppRgb);
			scrollBuffer = new Bitmap(100 * CellWidth, 60 * CellHeight, PixelFormat.Format24bppRgb);
			for (int row = 0; row < 60; row++)
				for (int col = 0; col < 100; col++)
					previousImage[col, row].Character = '\uFFFE';
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if (backBuffer == null)
			{
				base.OnPaint(e);
				return;
			}

			var rect = youtube ? youtubeRect : ClientRectangle;
			var offX = rect.Left;
			var offY = rect.Top;
			e.Graphics.DrawImage(backBuffer, rect);

			//Moved here from Draw() to prevent mouse droppings. Bonus: this allows a slightly larger cursor.
			if (Cursor.X != prevCursor.X || Cursor.Y != prevCursor.Y)
				prevCursor = Cursor;
			if (Cursor.X >= 0 && Cursor.X < 100 && Cursor.Y >= 0 && Cursor.Y < 60)
			{
				var cSize = CellWidth;
				if (Cursor.X < 99 && image[Cursor.X + 1, Cursor.Y].Character == 0xE2FF)
					cSize *= 2;
				e.Graphics.DrawRectangle(cursorPens[(uint)Environment.TickCount % cursorPens.Length], offX + (Cursor.X * CellWidth) - 1, offY + (Cursor.Y * CellHeight) - 1, cSize + 1, CellHeight + 1);
			}
		}

		private void CachePNGFont(Bitmap source = null)
		{
			if (source == null)
				source = Mix.GetBitmap("fonts\\" + pngFont + ".png");
			var cWidth = source.Width / 32;
			var cHeight = source.Height / 32;
			fontData = new byte[1024, cWidth * cHeight];
			for (var ch = 0; ch < 1024; ch++)
			{
				var i = 0;
				var sX = (ch % 32) * cWidth;
				var sY = (ch / 32) * cHeight;
				for (var y = 0; y < cHeight; y++)
				{
					for (var x = 0; x < cWidth; x++)
					{
						fontData[ch, i] = (byte)(source.GetPixel(sX + x, sY + y).R); // > 127 ? 1 : 0);
						i++;
					}
				}
			}
		}

		public void SetCell(int row, int col, int character, Color foregroundColor, Color backgroundColor, bool forceRedraw = false)
		{
			if (col >= 100 || row >= 60 || col < 0 || row < 0)
				return;

			image[col, row].Character = character;
			image[col, row].Foreground = foregroundColor;
			if (backgroundColor != Color.Transparent)
				image[col, row].Background = backgroundColor;
		}

		public void Clear(char character, Color foregroundColor, Color backgroundColor)
		{
			for (int row = 0; row < 60; row++)
			{
				for (int col = 0; col < 100; col++)
				{
					image[col, row].Character = character;
					image[col, row].Foreground = foregroundColor;
					image[col, row].Background = backgroundColor;
				}
			}
		}
		public void Clear()
		{
			Clear(' ', Color.White, Color.Black);
		}

		public void Write(string text, Color foregroundColor, Color backgroundColor, int row = 0, int col = 0)
		{
			if (!text.IsNormalized())
				text = text.Normalize();
			text = text.FoldEntities();

			var rx = col;
			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];
				if (c == '\r')
					continue;
				if (c == '\n')
				{
					col = rx;
					row++;
					continue;
				}
				if (c == '<')
				{
					var gtPos = text.IndexOf('>', i + 1);
					if (gtPos != -1)
					{
						var tag = text.Substring(i + 1);
						i = gtPos;
						if (tag.StartsWith("nowrap"))
							continue;
						if (tag[0] == 'c')
						{
							var match = Regex.Match(tag, @"c(?<fore>\w+)(?:,(?<back>\w+))?");
							foregroundColor = !string.IsNullOrEmpty(match.Groups["fore"].Value) ? Color.FromName(match.Groups["fore"].Value) : Color.Silver;
							backgroundColor = !string.IsNullOrEmpty(match.Groups["back"].Value) ? Color.FromName(match.Groups["back"].Value) : Color.Transparent;
							continue;
						}
					}
				}
				SetCell(row, col, c, foregroundColor, backgroundColor, true);
				col++;
				if ((c >= 0x3000 && c < 0x4000) || (c >= 0x4E00 && c < 0xA000) || (c >= 0xE400 && c < 0xE500))
				{
					SetCell(row, col, '\uE2FF', Color.Black, Color.Black);
					col++;
				}
				if (col >= 100)
				{
					col = rx;
					row++;
				}
				if (row >= 60)
					return;
			}
		}

		private void DrawCell(byte[] scan0, int stride, int row, int col, Cell cell)
		{
			var sTX = col * CellWidth;
			var sTY = row * CellHeight;
			var b = cell.Background;
			var f = cell.Foreground;
			var c = cell.Character;

			if (c > 0x400)
				c = '#';

			if (c < 32)
				c += 0x1E0;
			c -= 32;

			var width = CellWidth;

			var sSX = (c % 32) * CellWidth;
			var sSY = (c / 32) * CellHeight;
			for (var y = 0; y < CellHeight; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var d = fontData[c, (y * width) + x];
					var color = (d == 0) ? b : (d == 255) ? f : Toolkit.Lerp(b, f, d / 256.0);
					var target = ((sTY + y) * stride) + ((sTX + x) * 3);
					if (target >= scan0.Length)
						continue;

					scan0[target + 0] = color.B;
					scan0[target + 1] = color.G;
					scan0[target + 2] = color.R;
				}
			}
		}

		public void Draw()
		{
			var lockData = backBuffer.LockBits(new System.Drawing.Rectangle(0, 0, backBuffer.Width, backBuffer.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
			var size = lockData.Stride * lockData.Height;
			var scan0 = new byte[size];
			Marshal.Copy(lockData.Scan0, scan0, 0, size);
			for (int row = 0; row < 60; row++)
			{
				for (int col = 0; col < 100; col++)
				{
					var here = image[col, row];
					if (here != previousImage[col, row])
					{
							DrawCell(scan0, lockData.Stride, row, col, here);
							previousImage[col, row].CopyFrom(here);
					}
				}
			}
			Marshal.Copy(scan0, 0, lockData.Scan0, size);
			backBuffer.UnlockBits(lockData);
			Frames++;
			this.Refresh();
		}

		public void ScrollUp(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal)
		{
			for (var row = topRow; row < bottomRow; row++)
			{
				for (var col = leftCol; col < rightCol; col++)
				{
					image[col, row].CopyFrom(image[col, row + 1]);
				}
			}
		}

		public void ScrollDown(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal)
		{
			for (var row = bottomRow; row > topRow; row--)
			{
				for (var col = leftCol; col < rightCol; col++)
				{
					image[col, row].CopyFrom(image[col, row - 1]);
				}
			}
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			Introduction.KillWorldgen();
			Running = false;
		}

		private void Form1_KeyDown(object sender, KeyEventArgs e)
		{
			if (starting)
				return;
			if (NoxicoGame.Mono && (DateTime.Now - NoxicoGame.KeyRepeat[e.KeyCode]).Milliseconds < 100)
				return;
			if (e.Control && (e.KeyCode == Keys.R || e.KeyCode == Keys.A))
				return;
			NoxicoGame.KeyRepeat[e.KeyCode] = DateTime.Now;
			NoxicoGame.KeyMap[e.KeyCode] = true;
			if (numpad.ContainsKey(e.KeyCode))
				NoxicoGame.KeyMap[numpad[e.KeyCode]] = true;
			if (e.Modifiers == Keys.Shift)
				NoxicoGame.Modifiers[0] = true;
			if (e.KeyValue == 191 && NoxicoGame.Mode == UserMode.Walkabout)
				NoxicoGame.KeyMap[Keys.L] = true;
		}

		private void Form1_KeyUp(object sender, KeyEventArgs e)
		{
			NoxicoGame.KeyMap[e.KeyCode] = false;
			NoxicoGame.KeyTrg[e.KeyCode] = true;
			if (numpad.ContainsKey(e.KeyCode))
			{
				NoxicoGame.KeyMap[numpad[e.KeyCode]] = false;
				NoxicoGame.KeyTrg[numpad[e.KeyCode]] = true;
			}
			if (e.Modifiers == Keys.Shift)
				NoxicoGame.Modifiers[0] = false;

			if (e.KeyCode == (Keys)NoxicoGame.KeyBindings[KeyBinding.Screenshot])
			{
				if (e.Modifiers == Keys.Shift)
				{
					using (var dumpFile = new StreamWriter("lol.txt", false, System.Text.Encoding.GetEncoding(437)))
					{
						for (int row = 0; row < 60; row++)
						{
							for (int col = 0; col < 100; col++)
							{
								dumpFile.Write(NoxicoGame.ingameTo437[image[col, row].Character]);
							}
							dumpFile.WriteLine();
						}
					}
					using (var dumpFile = new StreamWriter("lol.html"))
					{
						dumpFile.WriteLine("<pre>");
						for (int row = 0; row < 60; row++)
						{
							for (int col = 0; col < 100; col++)
							{
								dumpFile.Write("<span style=\"background:{1};color:{2}\">{0}</span>", NoxicoGame.ingameToUnicode[image[col, row].Character], image[col, row].Background.ToHex(), image[col, row].Foreground.ToHex());
							}
							dumpFile.WriteLine();
						}
						dumpFile.WriteLine("</pre>");
					}
					return;
				}

				var shotDir = IniFile.GetValue("misc", "shotpath", "screenshots");
				if (shotDir.StartsWith("$"))
					shotDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + shotDir.Substring(1);

				if (!Directory.Exists(shotDir))
					Directory.CreateDirectory(shotDir);
				int i = 1;
				while (File.Exists(Path.Combine(shotDir, "screenshot" + i.ToString("000") + ".png")))
					i++;
				backBuffer.Save(Path.Combine(shotDir, "screenshot" + i.ToString("000") + ".png"), ImageFormat.Png);
				Program.WriteLine("Screenshot saved.");
			}
			if (e.KeyValue == 191)
				NoxicoGame.KeyMap[Keys.L] = false;

			if (e.KeyCode == Keys.R && e.Control)
			{
				NoxicoGame.KeyMap[Keys.R] = false;
				for (int row = 0; row < 60; row++)
					for (int col = 0; col < 100; col++)
						previousImage[col, row].Character = '\uFFFE';
			}

			if (e.KeyCode == Keys.A && e.Control && NoxicoGame.Mode == UserMode.Walkabout)
			{
				NoxicoGame.KeyMap[Keys.A] = false;
				NoxicoGame.ShowMessageLog();
			}
		}

		private void Form1_KeyPress(object sender, KeyPressEventArgs e)
		{
			NoxicoGame.LastPress = e.KeyChar;
		}

		private void MainForm_MouseUp(object x, MouseEventArgs y)
		{
			var tx = y.X / (CellWidth);
			var ty = y.Y / (CellHeight);
			if (tx < 0 || ty < 0 || tx > 99 || ty > 59)
				return;
			if (NoxicoGame.Mode == UserMode.Walkabout)
			{
				if (tx < 80 && ty < 50)
				{
					if (y.Button == MouseButtons.Left)
						Noxico.Player.AutoTravelTo(tx, ty);
					else if (y.Button == MouseButtons.Right)
					{
						NoxicoGame.Cursor.ParentBoard = Noxico.CurrentBoard;
						NoxicoGame.Cursor.XPosition = tx;
						NoxicoGame.Cursor.YPosition = ty;
						NoxicoGame.Cursor.Point();
						NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
						NoxicoGame.Cursor.Update();
					}
					else if (y.Button == System.Windows.Forms.MouseButtons.Middle)
					{
						if (ty < 8)
						{
							Noxico.Player.AutoTravelTo(tx, 0);
							Noxico.Player.AutoTravelLeave = Direction.North;
						}
						else if (ty > 42)
						{
							Noxico.Player.AutoTravelTo(tx, 49);
							Noxico.Player.AutoTravelLeave = Direction.South;
						}
						else if (tx < 4)
						{
							Noxico.Player.AutoTravelTo(0, ty);
							Noxico.Player.AutoTravelLeave = Direction.West;
						}
						else if (tx > 72)
						{
							Noxico.Player.AutoTravelTo(79, ty);
							Noxico.Player.AutoTravelLeave = Direction.East;
						}
					}
				}
			}
			else if (NoxicoGame.Mode == UserMode.Subscreen)
			{
				if (y.Button == MouseButtons.Left)
				{
					//TODO: only do this for non-list MessageBoxes? That would allow clicking on an action, right?
					if (NoxicoGame.Subscreen == MessageBox.Handler || NoxicoGame.Subscreen == ActionList.Handler)
					{
						NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
						return;
					}
					Subscreens.MouseX = tx;
					Subscreens.MouseY = ty;
					Subscreens.Mouse = true;
				}
				else if (y.Button == MouseButtons.Right)
				{
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Back]] = true;
				}
			}
		}

		private void MainForm_MouseWheel(object x, MouseEventArgs y)
		{
			if (NoxicoGame.Mode == UserMode.Subscreen)
			{
				if (y.Delta < 0)
				{
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.ScrollDown]] = true;
					NoxicoGame.ScrollWheeled = true;
				}
				else if (y.Delta > 0)
				{
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.ScrollUp]] = true;
					NoxicoGame.ScrollWheeled = true;
				}
			}
		}
	}

}
