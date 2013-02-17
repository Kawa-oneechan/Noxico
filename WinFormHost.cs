using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Noxico
{
	static class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			//Switch to Invariant so we get "¤1,000.50" instead of "€ 1.000,50" or "$1,000.50" by default.
			//Can't do this in certain cases, which should be inapplicable to this program.
			System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

			if (args.Contains("-spreadem"))
			{
				Mix.SpreadEm();
				return;
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

    public class MainForm : Form, IGameHost
    {
		public NoxicoGame Noxico { get; set; }

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
		private bool starting = true;

        public bool Running { get; set; }
		private int CellWidth = 8;
		private int CellHeight = 14;
		private int GlyphAdjustX = -2, GlyphAdjustY = -1;
		private bool ClearType = false;

		public string IniPath { get; set; }

#if ALLOW_PNG_MODE
		private bool pngMode = false;
		private string pngFont = "fixedsex";
		private Dictionary<int, Bitmap> pngFonts;
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
			var fatal = false;
#if NICE_ERRORS
			try
#endif
			{
				this.Text = "Noxico";
				this.BackColor = System.Drawing.Color.Black;
				this.DoubleBuffered = true;
				this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
				this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
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
					Location = new System.Drawing.Point(16, 16)
				});

				foreach (var reqDll in new [] { "Antlr3.Runtime.dll", "Jint.dll" })
					if (!File.Exists(reqDll))
						throw new FileNotFoundException("Required DLL " + reqDll + " is missing.");

				Mix.Initialize("Noxico");
				if (!Mix.FileExists("noxico.xml"))
				{
					System.Windows.Forms.MessageBox.Show(this, "Could not find game data. Please redownload the game.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
							IniPath = oldIniPath;
							portable = false;
						}
					}
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

#if ALLOW_PNG_MODE
				pngMode = IniFile.GetValue("misc", "pngmode", false);
				pngFont = IniFile.GetValue("misc", "pngfont", "fixedsex");
				pngFonts = new Dictionary<int, Bitmap>();
				if (pngMode)
				{
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
					var family = IniFile.GetValue("font", "family", "Consolas");
					var emSize = IniFile.GetValue("font", "size", 11);
					var style = IniFile.GetValue("font", "bold", false) ? FontStyle.Bold : FontStyle.Regular;
					GlyphAdjustX = IniFile.GetValue("font", "x-adjust", -2);
					GlyphAdjustY = IniFile.GetValue("font", "y-adjust", -1);
					ClearType = IniFile.GetValue("font", "cleartype", true);
					Font = new Font(family, emSize, style);
					if (Font.FontFamily.Name != family)
						Font = new Font(FontFamily.GenericMonospace, emSize, style);
					using (var gfx = Graphics.FromHwnd(this.Handle))
					{
						var em = gfx.MeasureString("M", this.Font);
						CellWidth = (int)Math.Ceiling(em.Width * 0.75);
						CellHeight = (int)Math.Ceiling(em.Height * 0.85);
					}
					if (IniFile.GetValue("font", "cellwidth", 0) != 0)
						CellWidth = IniFile.GetValue("font", "cellwidth", 0);
					if (IniFile.GetValue("font", "cellheight", 0) != 0)
						CellHeight = IniFile.GetValue("font", "cellheight", 0);

#if ALLOW_PNG_MODE
				}
#endif

				ClientSize = new Size(80 * CellWidth, 25 * CellHeight);

				Show();
				Refresh();

				backBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
				scrollBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
				Noxico = new NoxicoGame();
				Noxico.Initialize(this);

				MouseUp += new MouseEventHandler(MainForm_MouseUp);
				MouseWheel += new MouseEventHandler(MainForm_MouseWheel);

				GotFocus += (s, e) => { Vista.GamepadFocused = true; };
				LostFocus += (s, e) => { Vista.GamepadFocused = false; };

				Vista.GamepadEnabled = IniFile.GetValue("misc", "xinput", true);

				Console.WriteLine("MONO CHECK: {0}", Environment.OSVersion.Platform);
				Console.WriteLine(Environment.OSVersion);
				if (Environment.OSVersion.Platform == PlatformID.Unix)
				{
					Console.WriteLine("*** You are running on a *nix system. ***");
					Console.WriteLine("Key repeat delays exaggerated.");
					NoxicoGame.Mono = true;
					Vista.GamepadEnabled = false;
				}

				Achievements.ProfilePath = "";
				if (!portable)
					GamerServices.Profile.Prepare();
				GamerServices.Profile.AskForOnline = IniFile.GetValue("profile", "askforonline", true);
				GamerServices.Profile.UseOnline = IniFile.GetValue("profile", "useonline", true);
				Achievements.Setup();

				this.Controls.Clear();
				starting = false;
				Running = true;
				while (Running)
				{
					Noxico.Update();
					Application.DoEvents();
				}
			}
#if NICE_ERRORS
			catch (Exception x)
			{
				new ErrorForm(x).ShowDialog(this);
				System.Windows.Forms.MessageBox.Show(this, x.ToString(), Application.ProductName, MessageBoxButtons.OK);
				Running = false;
				fatal = true;
				Application.ExitThread();
			}
#endif
			if (!fatal)
			{
				Noxico.SaveGame();
				Achievements.SaveProfile(true);
			}
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

		public void Write(string text, Color forecolor, Color backcolor, int row = 0, int col = 0)
		{
			if (!text.IsNormalized())
				text = text.Normalize();

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
							var match = Regex.Match(tag, @"c(?:(?:(?<fore>\w+)(?:(?:,(?<back>\w+))?))?)");
							forecolor = match.Groups["fore"].Value != "" ? Color.FromName(match.Groups["fore"].Value) : Color.Silver;
							backcolor = match.Groups["back"].Value != "" ? Color.FromName(match.Groups["back"].Value) : Color.Transparent;
							//forecolor = match.Groups["fore"].Value != "" ? int.Parse(match.Groups["fore"].Value) : 7;
							//backcolor = match.Groups["back"].Value != "" ? int.Parse(match.Groups["back"].Value) : 0;
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
				SetCell(row, col, c, forecolor, backcolor, true);
				col++;
				if (col >= 80)
				{
					col = rx;
					row++;
				}
				if (row >= 25)
					return;
			}
		}

        private void DrawCell(Graphics gfx, int row, int col, Cell cell)
        {
			var sTX = col * CellWidth;
            var sTY = row * CellHeight;
			var b = cell.Background;
			var f = cell.Foreground;
            var c = cell.Character;

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
					gfx.DrawString(c.ToString(), this.Font, foreBrush, sTX + GlyphAdjustX, sTY + GlyphAdjustY);
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

		public void ScrollUp(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal)
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
				gfx.FillRectangle(new SolidBrush(reveal), new System.Drawing.Rectangle(pixelL, (bottomRow * CellHeight) - CellHeight, pixelR - pixelL, CellHeight));
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
			//Refresh();
		}

		public void ScrollDown(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal)
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
				gfx.FillRectangle(new SolidBrush(reveal), new System.Drawing.Rectangle(pixelL, topRow * CellHeight, pixelR - pixelL, CellHeight));
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
			//Refresh();
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
			if (e.Control && (e.KeyCode == Keys.R || e.KeyCode == Keys.A))
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
				var shotDir = IniFile.GetValue("misc", "shotpath", "screenshots");
				if (shotDir.StartsWith("$"))
					shotDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + shotDir.Substring(1);

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
				NoxicoGame.KeyMap[(int)Keys.R] = false;
				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						previousImage[col, row].Character = (char)0x500;
			}
		
			if (e.KeyCode == Keys.A && e.Control && NoxicoGame.Mode == UserMode.Walkabout)
			{
				NoxicoGame.KeyMap[(int)Keys.A] = false;
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
			if (tx < 0 || ty < 0 || tx > 79 || ty > 24)
				return;
			if (NoxicoGame.Mode == UserMode.Walkabout)
			{
				if (y.Button == System.Windows.Forms.MouseButtons.Left)
					Noxico.Player.AutoTravelTo(tx, ty);
				else if (y.Button == System.Windows.Forms.MouseButtons.Right)
				{
					NoxicoGame.Cursor.ParentBoard = Noxico.CurrentBoard;
					NoxicoGame.Cursor.XPosition = tx;
					NoxicoGame.Cursor.YPosition = ty;
					NoxicoGame.Cursor.Point();
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
					NoxicoGame.Cursor.Update();
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
