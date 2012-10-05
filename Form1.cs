using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Forms;
using System.Drawing.Imaging;

namespace Noxico
{
	static class Program
	{
		public static Action<string> Report = null;

		[STAThread]
		static void Main(string[] args)
		{
			Report = (s) => { return; };
			if (args.Length > 0)
			{
				if (args[0] == "spam")
				{
					Report = (s) => { System.Windows.Forms.MessageBox.Show("Startup report:\r\n" + s, Application.ProductName); };
					Report("You will get a lot of these messages. If the game crashes, they'll let Kawa pinpoint where that happened. Tell him which message came last.");
				}
			}

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
	}

    public class MainForm : Form
    {
		private Func<Color, Color> colorConverter;
		private Func<Char, Char> charConverter;

        private struct Cell
        {
            public char Character;
            public Color Foreground;
			public Color Background;
        }
        private Cell[,] image = new Cell[80, 25];
        private Cell[,] previousImage = new Cell[80, 25];
		private Bitmap backBuffer;
		private Bitmap scrollBuffer;
        public NoxicoGame Noxico;
        private ImageAttributes[] imageAttribs = new ImageAttributes[16 * 16];
		private bool starting = true;

        public bool Running = true;
		public int CellWidth = 8;
		public int CellHeight = 14;
		public int GlyphAdjustX = -2, GlyphAdjustY = -1;
		public bool ClearType = false;
		
#if ALLOW_PNG_MODE
		private bool pngMode = false;
		private string pngFont = "fixedsex";
		private Dictionary<int, Bitmap> pngFonts;
#endif
#if ALLOW_CANDYTRON
		private bool candytron = false;
		private Font candyFont;
		private System.Drawing.Rectangle viewPort, reflection;
		private System.Drawing.Drawing2D.LinearGradientBrush shadow, blue, red;
#endif

		private Dictionary<Keys, Keys> numpad = new Dictionary<Keys, Keys>()
			{
				{ Keys.NumPad8, Keys.Up },
				{ Keys.NumPad2, Keys.Down },
				{ Keys.NumPad4, Keys.Left },
				{ Keys.NumPad6, Keys.Right },
				{ Keys.NumPad5, Keys.OemPeriod },
				//{ Keys.Back, Keys.Escape },
			};

        public MainForm()
        {
			Program.Report("MainForm construct");
			this.Text = "Noxico";
			this.BackColor = System.Drawing.Color.Black;
			this.DoubleBuffered = true;
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
			this.Paint += new System.Windows.Forms.PaintEventHandler(this.Form1_Paint);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Form1_KeyDown);
			this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Form1_KeyPress);
			this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.Form1_KeyUp);
			this.Icon = global::Noxico.Properties.Resources.app;
			this.ClientSize = new Size(80 * CellWidth, 25 * CellHeight);
			this.Controls.Add(new Label()
			{
				Text = "Please hold...",
				AutoSize = true,
				Font = new System.Drawing.Font("Garamond", 16, FontStyle.Bold | FontStyle.Italic),
				ForeColor = System.Drawing.Color.Yellow,
				Visible = true,
				Location = new System.Drawing.Point(16,16)
			});

			Program.Report("Mix.Initialize");
			Mix.Initialize("Noxico");
			Program.Report("Game data check");
			if (!Mix.FileExists("noxico.xml"))
			{
				System.Windows.Forms.MessageBox.Show(this, "Could not find game data. Please redownload the game.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
				Close();
				return;
			}

			colorConverter = (c => c);
			charConverter = (c => c);

			var portable = false;
			var iniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "noxico.ini");
			Program.Report("Portable Mode check");
			if (File.Exists("portable"))
			{
				portable = true;
				var oldIniPath = iniPath;
				iniPath = "noxico.ini";
				Program.Report("Trying to determine if portable mode could work...");
				var fi = new FileInfo(Application.ExecutablePath);
				var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
				if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly || Application.ExecutablePath.StartsWith(pf))
				{
					var response = System.Windows.Forms.MessageBox.Show(this, "Trying to start in portable mode, but from a protected location. Use non-portable mode?" + Environment.NewLine + "Selecting \"no\" may cause errors.", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
					if (response == System.Windows.Forms.DialogResult.Cancel)
					{
						Close();
						return;
					}
					else if (response == System.Windows.Forms.DialogResult.Yes)
					{
						iniPath = oldIniPath;
						portable = false;
					}
				}
			}

			Program.Report("Checking for INI file");
			if (!File.Exists(iniPath))
				File.WriteAllText(iniPath, Mix.GetString("DefaultSettings.txt"));
			Program.Report("Loading INI file");
			IniFile.Load(iniPath);

			if (portable)
			{
				IniFile.SetValue("misc", "vistasaves", false);
				IniFile.SetValue("misc", "savepath", "./saves");
				IniFile.SetValue("misc", "shotpath", "./screenshots");
			}

#if ALLOW_PNG_MODE
			pngMode = IniFile.GetBool("misc", "pngmode", false);
			pngFont = IniFile.GetString("misc", "pngfont", "fixedsex");
			pngFonts = new Dictionary<int, Bitmap>();
			if (pngMode)
			{
				Program.Report("PNG Mode is requested. Checking for font...");
				//if (File.Exists(Path.Combine("fonts", pngFont + "_00.png")))
				if (Mix.FileExists(pngFont + "_00.png"))
				{
					CachePNGFont('A');
					CellWidth = pngFonts[0x00].Width / 16;
					CellHeight = pngFonts[0x00].Height / 16;
				}
				else
					pngMode = false;
			}
			if (!pngMode)
			{
#endif
			Program.Report("Setting up TTF font...");
			var family = IniFile.GetString("font", "family", "Consolas");
			var emSize = IniFile.GetInt("font", "size", 11);
			var style = IniFile.GetBool("font", "bold", false) ? FontStyle.Bold : FontStyle.Regular;
			GlyphAdjustX = IniFile.GetInt("font", "x-adjust", -2);
			GlyphAdjustY = IniFile.GetInt("font", "y-adjust", -1);
			ClearType = IniFile.GetBool("font", "cleartype", true);
			Font = new Font(family, emSize, style);
			if (Font.FontFamily.Name != family)
				Font = new Font(FontFamily.GenericMonospace, emSize, style);
			using (var gfx = Graphics.FromHwnd(this.Handle))
			{
				var em = gfx.MeasureString("M", this.Font);
				CellWidth = (int)Math.Ceiling(em.Width * 0.75);
				CellHeight = (int)Math.Ceiling(em.Height * 0.85);
			}
			if (IniFile.GetInt("font", "cellwidth", 0) != 0)
				CellWidth = IniFile.GetInt("font", "cellwidth", 0);
			if (IniFile.GetInt("font", "cellheight", 0) != 0)
				CellHeight = IniFile.GetInt("font", "cellheight", 0);
#if ALLOW_PNG_MODE
			}
#endif

			Program.Report("Setting up filters...");
			switch (IniFile.GetString("filters", "color", "none").ToLowerInvariant())
			{
				case "cga": colorConverter = ToCGA; break;
				case "psp": colorConverter = ToPspPal; break;
				case "mono": colorConverter = ToMono; break;
				default: colorConverter = (c => c); break;
			}
			switch (IniFile.GetString("filters", "char", "none").ToLowerInvariant())
			{
				case "437": charConverter = To437; break;
				case "7bit": charConverter = To7Bit; break;
				default: charConverter = (c => c); break;
			}

			ClientSize = new Size(80 * CellWidth, 25 * CellHeight);
#if ALLOW_CANDYTRON
			if (IniFile.GetBool("misc", "candytron", false))
			{
				Program.Report("Setting up Candytron Mode...");
				candytron = true;
#if ALLOW_PNG_MODE
				pngMode = false;
				var emSize = 24;
				var style = FontStyle.Bold;
				var family = "Consolas";
#else
				emSize = 24;
				style = FontStyle.Bold;
#endif
				GlyphAdjustX = -5;
				GlyphAdjustY = 0;
				ClearType = false;
				CellWidth = 18;
				CellHeight = 37;
				FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
				WindowState = FormWindowState.Maximized;
				Font = new Font(family, emSize, style);
				if (Font.FontFamily.Name != family)
					Font = new Font(FontFamily.GenericMonospace, emSize, style);
				colorConverter = (c => c);
				charConverter = (c => c);
			}
#endif

			Show();
			Refresh();

			Program.Report("Preparing render buffers...");
			backBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
			scrollBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
			Program.Report("Creating NoxicoGame instance...");
			Noxico = new NoxicoGame(this);

			Program.Report("Setting up events...");
			MouseUp += (x, y) =>
			{
				var tx = y.X / (CellWidth);
				var ty = y.Y / (CellHeight);
#if ALLOW_CANDYTRON
				if (candytron)
				{
					//TODO: Make coordinates work in Candytron mode
					return;
				}
#endif
				if (tx < 0 || ty < 0 || tx > 79 || ty > 24)
					return; 
				if (NoxicoGame.Mode == UserMode.Walkabout && y.Button == System.Windows.Forms.MouseButtons.Left)
					Noxico.Player.AutoTravelTo(tx, ty);
				else if (NoxicoGame.Mode == UserMode.LookAt)
				{
					if (y.Button == System.Windows.Forms.MouseButtons.Left)
					{
						NoxicoGame.Cursor.XPosition = tx;
						NoxicoGame.Cursor.YPosition = ty;
						NoxicoGame.Cursor.Point();
						NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
					}
					else if (y.Button == System.Windows.Forms.MouseButtons.Right)
					{
						NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Back]] = true;
					}
				}
				else if (NoxicoGame.Mode == UserMode.Walkabout && y.Button == System.Windows.Forms.MouseButtons.Right)
				{
					var target = Noxico.CurrentBoard.Entities.Find(z => (z is BoardChar || z is Clutter) && z.XPosition == tx && z.YPosition == ty);
					if (target != null)
					{
						Subscreens.UsingMouse = true;
						if (target is BoardChar)
							TextScroller.LookAt((BoardChar)target);
						else if (target is Clutter)
						{
							var text = ((Clutter)target).Description;
							text = text.Trim();
							if (text == "")
								return;
							//var lines = text.Split('\n').Length;
							MessageBox.Message(text, true);
						}
					}
				}
				else if (NoxicoGame.Mode == UserMode.Subscreen)
				{
					if (y.Button == System.Windows.Forms.MouseButtons.Left)
					{
						if (NoxicoGame.Subscreen == MessageBox.Handler)
						{
							NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
							return;
						}
						Subscreens.MouseX = tx;
						Subscreens.MouseY = ty;
						Subscreens.Mouse = true;
					}
					else if (y.Button == System.Windows.Forms.MouseButtons.Right)
					{
						NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Back]] = true;
					}
				}
			};
			MouseWheel += (x, y) =>
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
			};
			FormClosed += (x, y) =>
			{
				Introduction.KillWorldgen();
			};

			Program.Report("Checking for Mono...");
			Console.WriteLine("MONO CHECK: {0}", Environment.OSVersion.Platform);
			Console.WriteLine(Environment.OSVersion);
			if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				Console.WriteLine("*** You are running on a *nix system. ***");
				Console.WriteLine("Key repeat delays exaggerated.");
				NoxicoGame.Mono = true;
			}

			Program.Report("Setting up achievements");
			Achievements.ProfilePath = "";
			if (!portable)
				GamerServices.Profile.Prepare();
			GamerServices.Profile.AskForOnline = IniFile.GetBool("profile", "askforonline", true);
			GamerServices.Profile.UseOnline = IniFile.GetBool("profile", "useonline", true);
			Achievements.Setup();

			this.Controls.Clear();
			starting = false;
			Program.Report("Starting game loop...");
			//try
			{
				while (Running)
				{
					Noxico.Update();
					Application.DoEvents();
				}
			}
			//catch (Exception x)
			{
			//	System.Windows.Forms.MessageBox.Show(this, x.ToString() + Environment.NewLine + Environment.NewLine + x.Message, Application.ProductName, MessageBoxButtons.OK);
			//	Running = false;
			}
			Program.Report("Saving for exit.");
			Noxico.SaveGame();
			Program.Report("Saving profile");
			Achievements.SaveProfile(true);
        }


#if ALLOW_PNG_MODE
		private void CachePNGFont(char p)
		{
			var block = (p >> 8);
			if (!pngFonts.ContainsKey(block))
			{
				//var file = Path.Combine("fonts", pngFont + "_" + block.ToString("X2") + ".png");
				//if (File.Exists(file))
				//	pngFonts.Add(block, (Bitmap)Bitmap.FromFile(file));
				var file = pngFont + "_" + block.ToString("X2") + ".png";
				if (Mix.FileExists(file))
					pngFonts.Add(block, Mix.GetBitmap(file));
				else
				{
					Console.WriteLine("Warning: {0} does not exist!");
					pngFonts.Add(block, new Bitmap(128, 256));
				}
			}
		}
#endif

		protected override void OnPaint(PaintEventArgs e)
		{
			if (backBuffer == null)
			{
				base.OnPaint(e);
				return;
			}
#if ALLOW_CANDYTRON
			if (candytron)
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
				if (viewPort.Width == 0)
				{
					var aspect = (float)backBuffer.Width / (float)backBuffer.Height;
					var h = ClientRectangle.Height / 1.5;
					var w = h * aspect;
					var x = (ClientRectangle.Width - w) / 2;
					var y = (ClientRectangle.Height - h) / 3;
					viewPort = new System.Drawing.Rectangle((int)x, (int)y, (int)w, (int)h);
					reflection = new System.Drawing.Rectangle((int)x, (int)(y + h), (int)w, (int)(h / 2));
					shadow = new System.Drawing.Drawing2D.LinearGradientBrush(new System.Drawing.Rectangle(0, 0, (int)w, (int)(h / 2)), Color.Black, Color.Transparent, 270);
					red = new System.Drawing.Drawing2D.LinearGradientBrush(new System.Drawing.Rectangle(0, 0, ClientRectangle.Width, ClientRectangle.Height / 3), Color.FromArgb(48, 8, 8), Color.Black, 90);
					blue = new System.Drawing.Drawing2D.LinearGradientBrush(new System.Drawing.Rectangle(0, ClientRectangle.Height / 4, ClientRectangle.Width, ClientRectangle.Height / 4), Color.FromArgb(32, 16, 64), Color.Transparent, 270);
					candyFont = new Font("Consolas", 16, FontStyle.Bold);
				}

				var player = Noxico.Player.Character;
				var board = Noxico.CurrentBoard;
				var statusLine = player != null ? string.Format("{0}, {1}    {5}, {6}    {7}\nHP: {2}/{3}   Stim: {4}", player.Name.ToString(true), player.Title, (int)player.GetToken("health").Value, player.GetMaximumHealth(), (int)player.GetToken("stimulation").Value, NoxicoGame.InGameTime.ToShortTimeString(), NoxicoGame.InGameTime.ToLongDateString(), board.Name) : "";

				e.Graphics.FillRectangle(red, 0, 0, ClientRectangle.Width, ClientRectangle.Height / 3);
				e.Graphics.FillRectangle(Brushes.Black, viewPort.Left - 4, viewPort.Top - 4, viewPort.Width + 8, viewPort.Height + 8);
				e.Graphics.DrawImage(backBuffer, viewPort);
				e.Graphics.DrawImage(backBuffer, reflection.Left, reflection.Top + reflection.Height, reflection.Width, -reflection.Height);
				e.Graphics.FillRectangle(shadow, reflection.Left, reflection.Top, reflection.Width, reflection.Height);
				e.Graphics.FillRectangle(blue, 0, ClientRectangle.Height - (ClientRectangle.Height / 4) + 2, ClientRectangle.Width, ClientRectangle.Height / 4);
				e.Graphics.DrawString(statusLine, candyFont, Brushes.Black, 6, 6);
				e.Graphics.DrawString(statusLine, candyFont, Brushes.Yellow, 4, 4);
			}
			else
#endif
			e.Graphics.DrawImage(backBuffer, ClientRectangle);
		}

        public void SetCell(int row, int col, char character, Color forecolor, Color backcolor, bool force = false)
        {
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;

            image[col, row].Character = character;
            image[col, row].Foreground = forecolor;
			if (backcolor != Color.Transparent)
	            image[col, row].Background = backcolor;
			if (force)
				previousImage[col, row].Character = (char)(character + 4);
        }

		public void DarkenCell(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;
			image[col, row].Background = image[col, row].Background.Darken();
			image[col, row].Foreground = image[col, row].Foreground.Darken();
		}

		public void Clear(char character, Color forecolor, Color backcolor)
		{
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					image[col, row].Character = character;
					image[col, row].Foreground = forecolor;
					image[col, row].Background = backcolor;
				}
			}
		}
		public void Clear()
		{
			Clear(' ', Color.White, Color.Black);
		}

		public void Write(string text, Color forecolor, Color backcolor, int x = 0, int y = 0)
		{
			var rx = x;
			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];
				if (c == '\r')
					continue;
				if (c == '\n')
				{
					x = rx;
					y++;
					continue;
				}
				if (c == '\xFE')
				{
					//forecolor = (forecolor + 8) % 16;
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
							var match = Regex.Match(tag, @"c(?:(?:(?<fore>\w+)(?:(?:,(?<back>\w+))?))?)");
							forecolor = match.Groups["fore"].Value != "" ? Color.FromName(match.Groups["fore"].Value) : Color.Silver;
							backcolor = match.Groups["back"].Value != "" ? Color.FromName(match.Groups["back"].Value) : Color.Transparent;
							//forecolor = match.Groups["fore"].Value != "" ? int.Parse(match.Groups["fore"].Value) : 7;
							//backcolor = match.Groups["back"].Value != "" ? int.Parse(match.Groups["back"].Value) : 0;
							continue;
						}
						else if (tag[0] == 'b')
						{
							//forecolor ^= 8;
							continue;
						}
						else if (tag[0] == 'g')
						{
							var match = Regex.Match(tag, @"g(?:(?:(?<chr>\w{1,4}))?)");
							var chr = int.Parse(match.Groups["chr"].Value, System.Globalization.NumberStyles.HexNumber);
							c = (char)chr;
						}
					}
				}
				SetCell(y, x, c, forecolor, backcolor, true);
				x++;
				if (x >= 80)
				{
					x = rx;
					y++;
				}
				if (y >= 25)
					return;
			}
		}

        private void DrawCell(Graphics gfx, int row, int col, Cell cell)
        {
			var sTX = col * CellWidth;
            var sTY = row * CellHeight;
			var b = colorConverter(cell.Background);
			var f = colorConverter(cell.Foreground);
            var c = charConverter(cell.Character);

#if ALLOW_PNG_MODE
			if (pngMode)
			{
				CachePNGFont(c);
				var block = c >> 8;
				var c2 = c & 0xFF;
				var fontBitmap = pngFonts[block];
				var sSX = (c2 %	16) * CellWidth;
				var sSY = (c2 / 16) * CellHeight;
				for (var y = 0; y < CellHeight; y++)
				{
					for (var x = 0; x < CellWidth; x++)
					{
						var color = fontBitmap.GetPixel(sSX + x, sSY + y);
						color = (color.R == 0) ? b : f;
						backBuffer.SetPixel(sTX + x, sTY + y, color);
					}
				}
				return;
			}
#endif

			gfx.TextContrast = 0;
			gfx.TextRenderingHint = ClearType ? System.Drawing.Text.TextRenderingHint.ClearTypeGridFit : System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
			using (var backBrush = new SolidBrush(b))
			{
				gfx.FillRectangle(backBrush, sTX, sTY, CellWidth, CellHeight);
				using (var foreBrush = new SolidBrush(f))
				{
#if FAKE_LINE_DRAWING
					#region Line Drawing and such
					if (c >= 0x2500 && c <= 0x2593)
					{
						CellHeight--;

						using (var pen = new Pen(foreBrush))
						{
							switch (c)
							{
								case '\u2500': //Box Drawings Light Horizontal
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2), sTX + CellWidth, sTY + (CellHeight / 2));
									break;
								case '\u2501': //Box Drawings Light Vertical
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + CellHeight);
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + CellHeight);
									break;
								case '\u2502': //Box Drawings Light Down and Right
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2), sTX + (CellWidth / 2), sTY + CellHeight); //down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2), sTX + (CellWidth / 2) + 1, sTY + CellHeight); //down
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2), sTX + CellWidth, sTY + (CellHeight / 2)); //right
									break;
								case '\u2503': //Box Drawings Light Down and Left
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2), sTX + (CellWidth / 2) + 1, sTY + CellHeight); //down
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2), sTX + (CellWidth / 2), sTY + CellHeight); //down
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2), sTX + (CellWidth / 2), sTY + (CellHeight / 2)); //left
									break;
								case '\u2514': //Box Drawings Light Up And Right
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + (CellHeight / 2)); //up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2)); //up
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2), sTX + CellWidth, sTY + (CellHeight / 2)); //right
									break;
								case '\u2518': //Box Drawings Light Up And Left
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2)); //up
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + (CellHeight / 2)); //up
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2), sTX + (CellWidth / 2), sTY + (CellHeight / 2)); //left
									break;
								case '\u251C': //Box Drawings Light Vertical And Right
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + CellHeight); //vertical
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //vertical
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2), sTX + CellWidth, sTY + (CellHeight / 2)); //right
									break;
								case '\u2524': //Box Drawings Light Vertical And Left
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + CellHeight); //vertical
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //vertical
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2), sTX + (CellWidth / 2), sTY + (CellHeight / 2)); //left
									break;

								case '\u2550': //Box Drawings Double Horizontal
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) - 1, sTX + CellWidth, sTY + (CellHeight / 2) - 1); //upper
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) + 1, sTX + CellWidth, sTY + (CellHeight / 2) + 1); //lower
									break;
								case '\u2551': //Box Drawings Double Vertical
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 3, sTY, sTX + (CellWidth / 2) - 3, sTY + CellHeight); //left
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 2, sTY, sTX + (CellWidth / 2) - 2, sTY + CellHeight); //left
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //right
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 2, sTY, sTX + (CellWidth / 2) + 2, sTY + CellHeight); //right
									break;
								case '\u2554': //Box Drawings Double Down And Right
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 3, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2) - 3, sTY + CellHeight); //outer down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 2, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2) - 2, sTY + CellHeight); //outer down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //inner down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 2, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2) + 2, sTY + CellHeight); //inner down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 1, sTY + (CellHeight / 2) - 1, sTX + CellWidth, sTY + (CellHeight / 2) - 1); //outer right
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) + 1, sTX + CellWidth, sTY + (CellHeight / 2) + 1); //inner right
									break;
								case '\u2557': //Box Drawings Double Down And Left
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 3, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2) - 3, sTY + CellHeight); //outer down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 2, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2) - 2, sTY + CellHeight); //outer down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //inner down
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 2, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2) + 2, sTY + CellHeight); //inner down
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) - 1); //outer left
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2) - 2, sTY + (CellHeight / 2) + 1); //inner left
									break;
								case '\u255A': //Box Drawings Double Up And Right
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 3, sTY, sTX + (CellWidth / 2) - 3, sTY + (CellHeight / 2) + 1); //outer up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 2, sTY, sTX + (CellWidth / 2) - 2, sTY + (CellHeight / 2) + 1); //outer up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) - 1); //inner up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 2, sTY, sTX + (CellWidth / 2) + 2, sTY + (CellHeight / 2) - 1); //inner up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) - 1, sTX + CellWidth, sTY + (CellHeight / 2) - 1); //outer right
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 1, sTY + (CellHeight / 2) + 1, sTX + CellWidth, sTY + (CellHeight / 2) + 1); //inner right
									break;
								case '\u255D': //Box Drawings Double Up And Left
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 2, sTY, sTX + (CellWidth / 2) + 2, sTY + (CellHeight / 2) + 1); //outer up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) + 1); //outer up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 2, sTY, sTX + (CellWidth / 2) - 2, sTY + (CellHeight / 2) - 1); //inner up
									gfx.DrawLine(pen, sTX + (CellWidth / 2) - 3, sTY, sTX + (CellWidth / 2) - 3, sTY + (CellHeight / 2) - 1); //inner up
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2) - 2, sTY + (CellHeight / 2) - 1); //outer left
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2) + 1, sTY + (CellHeight / 2) + 1); //inner left
									break;
								case '\u255E': //Box Drawings Vertical Single And Right Double
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + CellHeight); //vertical single
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //vertical single
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2) - 1, sTX + CellWidth, sTY + (CellHeight / 2) - 1); //outer right
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY + (CellHeight / 2) + 1, sTX + CellWidth, sTY + (CellHeight / 2) + 1); //inner right
									break;
								case '\u2561': //Box Drawings Vertical Single And Left Double
									gfx.DrawLine(pen, sTX + (CellWidth / 2) + 1, sTY, sTX + (CellWidth / 2) + 1, sTY + CellHeight); //vertical single
									gfx.DrawLine(pen, sTX + (CellWidth / 2), sTY, sTX + (CellWidth / 2), sTY + CellHeight); //vertical single
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) - 1, sTX + (CellWidth / 2), sTY + (CellHeight / 2) - 1); //outer left
									gfx.DrawLine(pen, sTX, sTY + (CellHeight / 2) + 1, sTX + (CellWidth / 2), sTY + (CellHeight / 2) + 1); //inner left
									break;

								case '\u2580': //Upper Half Block
									gfx.FillRectangle(foreBrush, sTX, sTY, CellWidth, CellHeight / 2);
									break;
								case '\u2584': //Lower Half Block
									gfx.FillRectangle(foreBrush, sTX, sTY + (CellHeight / 2) + 2, CellWidth, CellHeight / 2);
									break;
								case '\u2588': //Full Block
									gfx.FillRectangle(foreBrush, sTX, sTY, CellWidth, CellHeight + 1);
									break;
								case '\u258C': //Left Half Block
									gfx.FillRectangle(foreBrush, sTX, sTY, CellWidth / 2, CellHeight + 1);
									break;
								case '\u2590': //Right Half Block
									gfx.FillRectangle(foreBrush, sTX + (CellWidth / 2) + 1, sTY, CellWidth / 2, CellHeight + 1);
									break;

								case '\u2591': //Light Shade
									gfx.FillRectangle(new SolidBrush(Color.FromArgb(64, f)), sTX, sTY, CellWidth, CellHeight);
									break;
								case '\u2593': //Medium Shade
									gfx.FillRectangle(new SolidBrush(Color.FromArgb(128, f)), sTX, sTY, CellWidth, CellHeight);
									break;
								case '\u2594': //Dark Shade
									gfx.FillRectangle(new SolidBrush(Color.FromArgb(192, f)), sTX, sTY, CellWidth, CellHeight);
									break;
							}
							CellHeight++;
						}
						return;
					}
					#endregion
					//Adjust certain characters that are off-center... it's this or custom-draw them.
					//if (c >= 0x2190 && c <= 0x263B)
					//	sTX -= 1;
#endif

					//gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
					//gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
					//gfx.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
					//gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
					gfx.DrawString(c.ToString(), this.Font, foreBrush, sTX + GlyphAdjustX, sTY + GlyphAdjustY);
					//BufferCharacter(c);
					//gfx.DrawImage(glyphBuffer[c], sTX, sTY);
				}
			}
        }

		public void Draw()
        {
            using (var gfx = Graphics.FromImage(backBuffer))
            {
				for (int row = 0; row < 25; row++)
				{
					for (int col = 0; col < 80; col++)
					{
						if (image[col, row].Character != previousImage[col, row].Character ||
							image[col, row].Foreground != previousImage[col, row].Foreground ||
							image[col, row].Background != previousImage[col, row].Background)
						{
							DrawCell(gfx, row, col, image[col, row]);
							previousImage[col, row].Character = image[col, row].Character;
							previousImage[col, row].Foreground = image[col, row].Foreground;
							previousImage[col, row].Background = image[col, row].Background;
						}
					}
				}
            }
            this.Refresh();
        }

		public void ScrollUp(int topRow, int bottomRow, int leftCol, int rightCol)
		{
			var pixelT = CellHeight + (topRow * CellHeight);
			var pixelB = (bottomRow * CellHeight);
			var pixelL = leftCol * CellWidth;
			var pixelR = rightCol * CellWidth;
			var scrollSize = new Size(pixelR - pixelL, pixelB - pixelT - CellHeight);
			var scrollPosFrom = new System.Drawing.Point(pixelL, pixelT + CellHeight);
			using (var sGfx = Graphics.FromImage(scrollBuffer))
			{
				sGfx.DrawImage(backBuffer, 0, 0, new System.Drawing.Rectangle(scrollPosFrom, scrollSize), GraphicsUnit.Pixel);
			}
			using (var gfx = Graphics.FromImage(backBuffer))
			{
				gfx.DrawImage(scrollBuffer, pixelL, pixelT);
				gfx.FillRectangle(Brushes.Black, new System.Drawing.Rectangle(pixelL, (bottomRow * CellHeight) - CellHeight, pixelR - pixelL, CellHeight));
			}
			for (var row = topRow; row < bottomRow; row++)
			{
				for (var col = leftCol; col < rightCol; col++)
				{
					previousImage[col, row].Character = image[col, row].Character = image[col, row + 1].Character;
					previousImage[col, row].Foreground = image[col, row].Foreground = image[col, row + 1].Foreground;
					previousImage[col, row].Background = image[col, row].Background = image[col, row + 1].Background;
				}
			}
			Refresh();
		}

		public void ScrollDown(int topRow, int bottomRow, int leftCol, int rightCol)
		{
			var pixelT = (topRow * CellHeight);
			var pixelB = (bottomRow * CellHeight);
			var pixelL = leftCol * CellWidth;
			var pixelR = rightCol * CellWidth;
			var scrollSize = new Size(pixelR - pixelL, pixelB - pixelT - CellHeight);
			var scrollPosFrom = new System.Drawing.Point(pixelL, pixelT);
			using (var sGfx = Graphics.FromImage(scrollBuffer))
			{
				sGfx.DrawImage(backBuffer, 0, 0, new System.Drawing.Rectangle(scrollPosFrom, scrollSize), GraphicsUnit.Pixel);
			}
			using (var gfx = Graphics.FromImage(backBuffer))
			{
				gfx.DrawImage(scrollBuffer, pixelL, pixelT + CellHeight);
				gfx.FillRectangle(Brushes.Black, new System.Drawing.Rectangle(pixelL, topRow * CellHeight, pixelR - pixelL, CellHeight));
			}
			for (var row = topRow + 1; row <= bottomRow; row++)
			{
				for (var col = leftCol; col < rightCol; col++)
				{
					previousImage[col, row].Character = image[col, row].Character = image[col, row - 1].Character;
					previousImage[col, row].Foreground = image[col, row].Foreground = image[col, row - 1].Foreground;
					previousImage[col, row].Background = image[col, row].Background = image[col, row - 1].Background;
				}
			}
			Refresh();
		}

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Running = false;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
			if (starting)
				return;
			if (NoxicoGame.Mono && (DateTime.Now - NoxicoGame.KeyRepeat[(int)e.KeyCode]).Milliseconds < 100)
				return;
			NoxicoGame.KeyRepeat[(int)e.KeyCode] = DateTime.Now;
			NoxicoGame.KeyMap[(int)e.KeyCode] = true;
			if (numpad.ContainsKey(e.KeyCode))
				NoxicoGame.KeyMap[(int)numpad[e.KeyCode]] = true;
			if (e.Modifiers == Keys.Shift)
				NoxicoGame.Modifiers[0] = true;
			if (e.KeyValue == 191 && NoxicoGame.Mode == UserMode.Walkabout)
				NoxicoGame.KeyMap[(int)Keys.L] = true;
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            NoxicoGame.KeyMap[(int)e.KeyCode] = false;
			NoxicoGame.KeyTrg[(int)e.KeyCode] = true;
			if (numpad.ContainsKey(e.KeyCode))
			{
				NoxicoGame.KeyMap[(int)numpad[e.KeyCode]] = false;
				NoxicoGame.KeyTrg[(int)numpad[e.KeyCode]] = true;
			}
			if (e.Modifiers == Keys.Shift)
				NoxicoGame.Modifiers[0] = false;

			if (e.KeyCode == (Keys)NoxicoGame.KeyBindings[KeyBinding.Screenshot])
			{
				var shotDir = IniFile.GetString("misc", "shotpath", "screenshots");
				if (shotDir.StartsWith("$"))
					shotDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + shotDir.Substring(1);

				if (!Directory.Exists(shotDir))
					Directory.CreateDirectory(shotDir);
				int i = 1;
				while (File.Exists(Path.Combine(shotDir, "screenshot" + i.ToString("000") + ".png")))
					i++;
				backBuffer.Save(Path.Combine(shotDir, "screenshot" + i.ToString("000") + ".png"), ImageFormat.Png);
				Console.WriteLine("Screenshot saved.");
			}
			if (e.KeyValue == 191)
				NoxicoGame.KeyMap[(int)Keys.L] = false;

			if (e.KeyCode == Keys.R && e.Control)
			{
				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						previousImage[col, row].Character = (char)0x500;
			}
        }

		private void Form1_KeyPress(object sender, KeyPressEventArgs e)
		{
			NoxicoGame.LastPress = e.KeyChar;
		}

		public void LoadBitmap(Bitmap bitmap)
		{
			for (var row = 0; row < 25; row ++)
			{
				for (var col = 0; col < 80; col++)
				{
					var top = bitmap.GetPixel(col, row * 2);
					var bot = bitmap.GetPixel(col, (row * 2)+ 1);
					SetCell(row, col, top == bot ? (char)0x20 : (char)0x2580, top, bot);
				}
			}
		}

		public void LoadBitmap(string file)
		{
			LoadBitmap((Bitmap)Bitmap.FromFile(file));
		}

		
		//Filters

		private Color ToCGA(Color color)
		{
			var cga = new[,]
			{
				{ 0x00, 0x00, 0x00 },
				{ 0x00, 0x00, 0xAA },
				{ 0x00, 0xAA, 0x00 },
				{ 0x00, 0xAA, 0xAA },
				{ 0xAA, 0x00, 0x00 },
				{ 0xAA, 0x00, 0xAA },
				{ 0xAA, 0x55, 0x00 },
				{ 0xAA, 0xAA, 0xAA },

				{ 0x55, 0x55, 0x55 },
				{ 0x55, 0x55, 0xFF },
				{ 0x55, 0xFF, 0x55 },
				{ 0x55, 0xFF, 0xFF },
				{ 0xFF, 0x55, 0x55 },
				{ 0xFF, 0x55, 0xFF },
				{ 0xFF, 0xFF, 0x55 },
				{ 0xFF, 0xFF, 0xFF },
			};

			var r = color.R;
			var g = color.G;
			var b = color.B;

			var lowestDist = 9999d;
			var bestMatch = -1;
			for (var i = 0; i < 16; i++)
			{
				var dR = Math.Pow(r - cga[i, 0], 2);
				var dG = Math.Pow(g - cga[i, 1], 2);
				var dB = Math.Pow(b - cga[i, 2], 2);
				var dist = Math.Sqrt(dR + dG + dB);
				if (dist < lowestDist)
				{
					lowestDist = dist;
					bestMatch = i;
				}
			}
			return Color.FromArgb(cga[bestMatch, 0], cga[bestMatch, 1], cga[bestMatch, 2]);
		}

		private byte[,] pspPal;
		private Color ToPspPal(Color color)
		{
			if (pspPal == null)
			{
				var palName = IniFile.GetString("filters", "palfile", "noxico.PspPalette");
				if (!File.Exists(palName))
				{
					colorConverter = (c => c);
					return color;
				}
				using (var palFile = new StreamReader(palName))
				{
					var i = palFile.ReadLine();
					if (i != "JASC-PAL")
					{
						colorConverter = (c => c);
						return color;
					}
					i = palFile.ReadLine();
					if (i != "0100")
					{
						colorConverter = (c => c);
						return color;
					}
					i = palFile.ReadLine();
					var numColors = int.Parse(i);
					pspPal = new byte[numColors, 3];
					for (var c = 0; c < numColors; c++)
					{
						i = palFile.ReadLine();
						var vals = i.Split(' ').Select(x => byte.Parse(x)).ToArray();
						pspPal[c, 0] = vals[0];
						pspPal[c, 1] = vals[1];
						pspPal[c, 2] = vals[2];
					}
				}
			}

			var r = color.R;
			var g = color.G;
			var b = color.B;

			var lowestDist = 9999d;
			var bestMatch = -1;
			for (var i = 0; i < pspPal.GetLength(0); i++)
			{
				var dR = Math.Pow(r - pspPal[i, 0], 2);
				var dG = Math.Pow(g - pspPal[i, 1], 2);
				var dB = Math.Pow(b - pspPal[i, 2], 2);
				var dist = Math.Sqrt(dR + dG + dB);
				if (dist < lowestDist)
				{
					lowestDist = dist;
					bestMatch = i;
				}
			}
			return Color.FromArgb(pspPal[bestMatch, 0], pspPal[bestMatch, 1], pspPal[bestMatch, 2]);
		}

		private Color ToMono(Color color)
		{
			var mono = (11 * color.R + 16 * color.G + 5 * color.B) / 32; 
			return Color.FromArgb(mono, mono, mono);
		}

		private char To437(char codePoint)
		{
			if (codePoint < 128)
				return codePoint;
			var uni = Encoding.UTF8;
			var dos = Encoding.GetEncoding(437);
			var oldBytes = uni.GetBytes(codePoint.ToString());
			var newBytes = Encoding.Convert(uni, dos, oldBytes);
			var newCode = dos.GetChars(newBytes)[0];
			return newCode;
		}

		private char To7Bit(char codePoint)
		{
			if (codePoint < 127)
				return codePoint;
			if (new[] { 0x2502, 0x2551 }.Contains(codePoint))
				return '|';
			if (new[] { 0x2509, 0x2550 }.Contains(codePoint))
				return '-';
			if (codePoint > 0x2500 && codePoint <= 0x256C)
				return '+';
			if (codePoint >= 0x2580 && codePoint <= 0x2593)
				return ' ';
			return '?';
		}
    }
}
