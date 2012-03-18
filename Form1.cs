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
		[STAThread]
		static void Main(string[] args)
		{
			if (args.Length == 2 && args[0] == "-postbuild")
			{
				if (args[1] == "1")
				{
					Console.WriteLine("Rebuilding library...");
					using (var bookDat = new CryptStream(new System.IO.Compression.GZipStream(File.Open("books.dat", FileMode.Create), System.IO.Compression.CompressionMode.Compress)))
					{
						var bookBytes = File.ReadAllBytes("books.xml");
						bookDat.Write(bookBytes, 0, bookBytes.Length);
						bookDat.Flush();
						bookDat.Close();
						//bookDat.Dispose();
					}
				}
				else if (args[1] == "2")
				{
#if DEBUG
					Console.WriteLine("No phase 2 on DEBUG builds."); 
#else
					Console.WriteLine("Packing up...");
					//"c:\Program Files\WinRAR\Rar.exe" u Noxico.rar fmodex64.dll FMODNet.dll music.xml noxico.xml books.dat Noxico.exe
					//"c:\Program Files\WinRAR\Rar.exe" u Noxico_music.rar fmodex64.dll FMODNet.dll music.xml noxico.xml books.dat Noxico.exe music sounds
					var rarsToMake = new Dictionary<string, string>()
					{
						{ "silent", "u Noxico.rar fmodex64.dll FMODNet.dll music.xml noxico.xml books.dat Noxico.exe"},
						{ "full music", "u Noxico_music.rar fmodex64.dll FMODNet.dll music.xml noxico.xml books.dat Noxico.exe music sounds" },
					};
					foreach (var rarToMake in rarsToMake)
					{
						Console.WriteLine("Creating {0} version...", rarToMake.Key);
						var rar = new System.Diagnostics.ProcessStartInfo(@"c:\Program Files\WinRAR\Rar.exe", rarToMake.Value);
						rar.UseShellExecute = false;
						rar.RedirectStandardError = true;
						rar.RedirectStandardOutput = true;
						rar.CreateNoWindow = true;
						var proc = new System.Diagnostics.Process();
						proc.StartInfo = rar;
						proc.Start();
						//Console.WriteLine(proc.StandardOutput.ReadToEnd());
					}
#endif
				}
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

    public class MainForm : Form
    {
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

        public bool Running = true;
		public int CellWidth = 8;
		public int CellHeight = 14;
		public int GlyphAdjustX = -2, GlyphAdjustY = -1;

		private Dictionary<Keys, Keys> numpad = new Dictionary<Keys, Keys>()
			{
				{ Keys.NumPad8, Keys.Up },
				{ Keys.NumPad2, Keys.Down },
				{ Keys.NumPad4, Keys.Left },
				{ Keys.NumPad6, Keys.Right },
				{ Keys.NumPad5, Keys.OemPeriod },
				{ Keys.Back, Keys.Escape },
			};

        public MainForm()
        {
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
			Show();
			Refresh();

			if (!File.Exists("noxico.ini"))
				File.WriteAllText("noxico.ini", global::Noxico.Properties.Resources.DefaultSettings);
			IniFile.Load("noxico.ini");

			var family = IniFile.GetString("font", "family", "Consolas");
			var emSize = IniFile.GetInt("font", "size", 11);
			var style = IniFile.GetBool("font", "bold", false) ? FontStyle.Bold : FontStyle.Regular;
			GlyphAdjustX = IniFile.GetInt("font", "x-adjust", -2);
			GlyphAdjustY = IniFile.GetInt("font", "y-adjust", -1);
			Font = new Font(family, emSize, style);
			if (Font.FontFamily.Name != family)
				Font = new Font(FontFamily.GenericMonospace, emSize, style);
			using (var gfx = Graphics.FromHwnd(this.Handle))
			{
				var em = gfx.MeasureString("M", this.Font);
				CellWidth = (int)Math.Ceiling(em.Width * 0.75);
				CellHeight = (int)Math.Ceiling(em.Height * 0.85);
			}

			backBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
			scrollBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
			ClientSize = new Size(80 * CellWidth, 25 * CellHeight);

            Noxico = new NoxicoGame(this);
            
			MouseUp += (x, y) =>
			{
				var tx = y.X / (CellWidth);
				var ty = y.Y / (CellHeight);
				if (NoxicoGame.Mode == UserMode.Walkabout && y.Button == System.Windows.Forms.MouseButtons.Left)
					Noxico.Player.AutoTravelTo(tx, ty);
				else if (NoxicoGame.Mode == UserMode.LookAt || NoxicoGame.Mode == UserMode.Walkabout && y.Button == System.Windows.Forms.MouseButtons.Right)
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
							NoxicoGame.KeyMap[(int)Keys.Enter] = true;
							return;
						}
						Subscreens.MouseX = tx;
						Subscreens.MouseY = ty;
						Subscreens.Mouse = true;
					}
					else if (y.Button == System.Windows.Forms.MouseButtons.Right)
					{
						NoxicoGame.KeyMap[(int)Keys.Escape] = true;
					}
				}
			};
			MouseWheel += (x, y) =>
			{
				if (NoxicoGame.Mode == UserMode.Subscreen)
				{
					if (y.Delta < 0)
					{
						NoxicoGame.KeyMap[(int)Keys.Down] = true;
						NoxicoGame.ScrollWheeled = true;
					}
					else if (y.Delta > 0)
					{
						NoxicoGame.KeyMap[(int)Keys.Up] = true;
						NoxicoGame.ScrollWheeled = true;
					}
				}
			};
			FormClosed += (x, y) =>
			{
				Introduction.KillWorldgen();
			};

			//TODO: Check for Mono at RUN TIME, not just #if MONO, so we can enable key repeat delays.
			//This was something with Environment, but WHAT!?
			//For now, we'll just check OSVersion.Platform.
			Console.WriteLine("MONO CHECK: {0}", Environment.OSVersion.Platform);
			if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				Console.WriteLine("*** You are running on a *nix system. ***");
				Console.WriteLine("Key repeat delays exaggerated.");
				NoxicoGame.Mono = true;
			}

			this.Controls.Clear();
            while (Running)
            {
                Noxico.Update();
                Application.DoEvents();
            }
        }

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
			var b = cell.Background;
			var f = cell.Foreground;
            var c = cell.Character;
			gfx.TextContrast = 0;
			gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
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

			if (e.KeyCode == Keys.F12)
			{
				if (!Directory.Exists("screenshots"))
					Directory.CreateDirectory("screenshots");
				int i = 1;
				while(File.Exists(Path.Combine("screenshots", "screenshot" + i.ToString("000") + ".png")))
					i++;
				backBuffer.Save(Path.Combine("screenshots", "screenshot" + i.ToString("000") + ".png"), ImageFormat.Png);
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

    }
}
