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
		private Bitmap fontBitmap;
		private Bitmap backBuffer;
		private Bitmap scrollBuffer;
        public NoxicoGame Noxico;
		private Color[] palette = new Color[16];
        private ImageAttributes[] imageAttribs = new ImageAttributes[16 * 16];

        public bool Running = true;
		public bool Double = false;
		public bool Interpolate = false;
		public int CellWidth = 8;
		public int CellHeight = 14;

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

			Double = IniFile.GetBool("video", "doublesize", false);
			Interpolate = IniFile.GetBool("video", "interpolate", false);

			//this.BackgroundImage = backbuffer;
			//this.BackgroundImageLayout = ImageLayout.Zoom;
			ReloadTileset();
			backBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
			scrollBuffer = new Bitmap(80 * CellWidth, 25 * CellHeight);
			var i = Double ? 2 : 1;
			ClientSize = new Size(80 * CellWidth * i, 25 * CellHeight * i);

            Noxico = new NoxicoGame(this);
            
			MouseUp += (x, y) =>
			{
				var d = Double ? 2 : 1;
				var tx = y.X / (CellWidth * d);
				var ty = y.Y / (CellHeight * d);
				if (NoxicoGame.Mode == UserMode.Walkabout && y.Button == System.Windows.Forms.MouseButtons.Left)
					Noxico.Player.AutoTravelTo(tx, ty);
				else if (NoxicoGame.Mode == UserMode.LookAt || NoxicoGame.Mode == UserMode.Walkabout && y.Button == System.Windows.Forms.MouseButtons.Right)
				{
					var target = Noxico.CurrentBoard.Entities.Find(z => (z is BoardChar || z is Dressing) && z.XPosition == tx && z.YPosition == ty);
					if (target != null)
					{
						Subscreens.UsingMouse = true;
						TextScroller.LookAt(target);
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
			e.Graphics.InterpolationMode = Interpolate ? System.Drawing.Drawing2D.InterpolationMode.Low : System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
			e.Graphics.DrawImage(backBuffer, ClientRectangle);
		}

		public void ReloadTileset()
		{
#if !USE_EXTENDED_TILES
			if (File.Exists("ascii.png"))
				fontBitmap = (Bitmap)Bitmap.FromFile("ascii.png");
			else
				fontBitmap = global::Noxico.Properties.Resources.Tileset;
			CellWidth = fontBitmap.Width / 32;
			CellHeight = fontBitmap.Height / 8;
#else
			Bitmap asciiPart, extendedPart;
			if (File.Exists(IniFile.GetString("video", "tileset", "ascii.png")))
				asciiPart = (Bitmap)Bitmap.FromFile(IniFile.GetString("video", "tileset", "ascii.png"));
			else
				asciiPart = global::Noxico.Properties.Resources.Tileset;
			if (File.Exists(IniFile.GetString("video", "extiles", "extended.png")))
				extendedPart = (Bitmap)Bitmap.FromFile(IniFile.GetString("video", "extiles", "extended.png"));
			else
				extendedPart = global::Noxico.Properties.Resources.ExtendedTiles;
			fontBitmap = new Bitmap(asciiPart.Width, asciiPart.Height + extendedPart.Height);
			using (var gfx = Graphics.FromImage(fontBitmap))
			{
				gfx.DrawImage(asciiPart, 0, 0, asciiPart.Width, asciiPart.Height);
				gfx.DrawImage(extendedPart, 0, asciiPart.Height, extendedPart.Width, extendedPart.Height);
			}
			CellWidth = asciiPart.Width / 32;
			CellHeight = asciiPart.Height / 8;
			fontBitmap.Save("combined.png", ImageFormat.Png);
#endif
		}

		public void MergeTileChunk(char startingTile, Bitmap chunk)
		{
			fontBitmap.Save("checka.png", ImageFormat.Png);
			var destTile = startingTile;
			//var srcRect = new System.Drawing.Rectangle(0, 0, 8, 14);
			var destX = (destTile % 32) * 8;
			var destY = (destTile / 32) * 14;
			//var destRect = new System.Drawing.Rectangle(destX, destY, chunk.Width, chunk.Height);
			using (var gfx = Graphics.FromImage(fontBitmap))
			{
				gfx.DrawImage(chunk, destX, destY, chunk.Width, chunk.Height);
			}
			fontBitmap.Save("checkb.png", ImageFormat.Png);
			for (int row = 0; row < 25; row++)
				for (int col = 0; col < 80; col++)
					previousImage[col, row].Character--;
			Draw();
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
							var match = Regex.Match(tag, @"g(?:(?:(?<chr>\w{1,2})(?:(?:,(?<ext>\w{1,3}))?))?)");
							var chr = int.Parse(match.Groups["chr"].Value, System.Globalization.NumberStyles.HexNumber);
							var ext = match.Groups["ext"].Value != "" ? int.Parse(match.Groups["ext"].Value, System.Globalization.NumberStyles.HexNumber) : 0;
#if !USE_EXTENDED_TILES
							c = (char)chr;
#else
							c = ext != 0 ? (char)ext : (char)chr;
#endif
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
            var charIndex = (int)cell.Character;

			var sTX = col * CellWidth;
            var sTY = row * CellHeight;
			var sSX = (charIndex % 32) * CellWidth;
			var sSY = (charIndex / 32) * CellHeight;
			var b = cell.Background;
			var f = cell.Foreground;
			for (var y = 0; y < CellHeight; y++)
			{
				for (var x = 0; x < CellWidth; x++)
				{
					var color = fontBitmap.GetPixel(sSX + x, sSY + y);
#if MAGENTA_IS_THE_NEW_BLACK
					if (color.R == 255 && color.G == 0 && color.B == 255)
						color = Color.Black;
					else
#endif
					color = color.R == 0 ? b : f; //palette[(color.R == 0) ? b : f];
					backBuffer.SetPixel(sTX + x, sTY + y, color);
				}
			}
        }

		public void Draw()
        {
            using (var gfx = Graphics.FromImage(backBuffer))
            {
                for (int row = 0; row < 25; row++)
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
			if (e.KeyCode == Keys.F2 && e.Modifiers == Keys.Control)
			{
				Double = !Double;
				var i = Double ? 2 : 1;
				ClientSize = new Size(80 * CellWidth * i, 25 * CellHeight * i);
				return;
			}

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

		public void LoadBin(byte[] resource)
		{
			var tsl = resource;
			var i = 0;
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var ch = tsl[i];
					var co = tsl[i + 1];
					var fg = palette[co & 0x0F]; //co & 0x0F;
					var bg = palette[(co & 0xF0) >> 4]; //(co & 0xF0) >> 4;
					i += 2;
					SetCell(row, col, (char)ch, fg, bg);
				}
			}
#if USE_EXTENDED_TILES
/*
			if(tsl.Length > 25 * 80 * 2 && tsl[0xFA0] == 'E')
			{
				i = 0xFA2;
				while (true)
				{
					i++;
					var col = tsl[i++] - 1;
					i++;
					var row = tsl[i++] - 1;
					var ch = tsl[i++];
					var co = tsl[i++];
					if (ch == 32 && co == 7)
						break;
					var fg = co & 0x0F;
					var bg = (co & 0xF0) >> 4;
					SetCell(row, col, (char)(0x100 + ch), (int)fg, (int)bg); 
				}
			}
*/
#endif
		}

		public void LoadBin(string file)
		{
			LoadBin(File.ReadAllBytes(file));
		}

		public void LoadBitmap(Bitmap bitmap)
		{
			for (var row = 0; row < 25; row ++)
			{
				for (var col = 0; col < 80; col++)
				{
					var top = bitmap.GetPixel(col, row * 2);
					var bot = bitmap.GetPixel(col, (row * 2)+ 1);
					SetCell(row, col, top == bot ? (char)0x20 : (char)0xDF, top, bot);
				}
			}
		}

		public void LoadBitmap(string file)
		{
			LoadBitmap((Bitmap)Bitmap.FromFile(file));
		}

    }
}
