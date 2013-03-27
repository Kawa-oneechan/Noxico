using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Tile
	{
		[ForJS(ForJSUsage.Either)]
		public Tile Noise()
		{
			return new Tile()
			{
				Character = this.Character,
				Foreground = this.Foreground.Darken(2 + (Random.NextDouble() / 2)),
				Background = this.Background.Darken(2 + (Random.NextDouble() / 2)),
				Wall = this.Wall,
				Water = this.Water,
				Ceiling = this.Ceiling,
				Cliff = this.Cliff,
				Fence = this.Fence,
				Grate = this.Grate,
				CanBurn = this.CanBurn,
				SpecialDescription = this.SpecialDescription,
			};
		}
	}

	public partial class Board : TokenCarrier
	{
		public static Jint.JintEngine DrawJS;

		[ForJS(ForJSUsage.Either)]
		public void Clear(int biomeID)
		{
			this.Entities.Clear();
			this.GetToken("biome").Value = biomeID;

			var biome = BiomeData.Biomes[biomeID];
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					this.Tilemap[col, row] = new Tile()
					{
						Character = biome.GroundGlyphs[Random.Next(biome.GroundGlyphs.Length)],
						Foreground = biome.Color.Darken(biome.DarkenPlus + (Random.NextDouble() / biome.DarkenDiv)),
						Background = biome.Color.Darken(biome.DarkenPlus + (Random.NextDouble() / biome.DarkenDiv)),
						CanBurn = biome.CanBurn,
						Water = biome.IsWater,
					};
				}
			}
		}

		[ForJS(ForJSUsage.Either)]
		public void Clear(string biomeName)
		{
			Clear(BiomeData.ByName(biomeName));
		}

		[ForJS(ForJSUsage.Only)]
		public void Line(int x1, int y1, int x2, int y2, string brush)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			var js = DrawJS;
			foreach (var point in Toolkit.Line(x1, y1, x2, y2))
			{
				js.SetParameter("x", point.X).SetParameter("y", point.Y).SetParameter("tile", this.Tilemap[point.Y, point.X]);
				this.Tilemap[point.Y, point.X] = (Tile)js.Run(brush);
			}
		}
		[ForJS(ForJSUsage.Only)]
		public void Line(int x1, int y1, int x2, int y2, Tile brush, bool noise)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			foreach (var point in Toolkit.Line(x1, y1, x2, y2))
			{
				this.Tilemap[point.Y, point.X] = noise ? brush.Noise() : brush;
			}
		}
		[ForJS(ForJSUsage.Only)]
		public void Line(int x1, int y1, int x2, int y2, Tile brush)
		{
			Line(x1, y1, x2, y2, brush, true);
		}

		[ForJS(ForJSUsage.Only)]
		public void Replace(string checker, string replacer)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			var js = DrawJS;

#if DEBUG
			js.SetDebugMode(false);
#endif

			var height = 25;
			var width = 80;
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					js.SetParameter("x", x).SetParameter("y", y).SetParameter("tile", this.Tilemap[x, y]);
					if ((bool)js.Run(checker))
						this.Tilemap[x,y] = (Tile)js.Run(replacer);
				}
			}


#if DEBUG
			js.SetDebugMode(true);
#endif
		}

		[ForJS(ForJSUsage.Only)]
		public void Floodfill(int startX, int startY, string checker, string replacer, bool allowDiagonals)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			var js = DrawJS;

#if DEBUG
			js.SetDebugMode(false);
#endif

			var height = 25;
			var width = 80;
			var stack = new Stack<Point>();
			stack.Push(new Point(startX, startY));
			while (stack.Count > 0)
			{
				var point = stack.Pop();
				var x = point.X;
				var y = point.Y;
				if (x < 0 || y < 0 || x >= width || y >= height)
					continue;

				//if (judge(data[y, x], x, y))
				js.SetParameter("x", x).SetParameter("y", y).SetParameter("tile", this.Tilemap[y, x]);
				if ((bool)js.Run(checker))
				{
					//this.Tilemap[y, x] = brush(data[y, x], x, y);
					js.Run(replacer);

					stack.Push(new Point(x - 1, y));
					stack.Push(new Point(x + 1, y));
					stack.Push(new Point(x, y - 1));
					stack.Push(new Point(x, y + 1));
					if (allowDiagonals)
					{
						stack.Push(new Point(x - 1, y - 1));
						stack.Push(new Point(x - 1, y + 1));
						stack.Push(new Point(x + 1, y - 1));
						stack.Push(new Point(x + 1, y + 1));
					}
				}
			}

#if DEBUG
			js.SetDebugMode(true);
#endif

		}
		[ForJS(ForJSUsage.Only)]
		public void Floodfill(int startX, int startY, string checker, string replacer)
		{
			Floodfill(startX, startX, checker, replacer, false);
		}

		[ForJS(ForJSUsage.Only)]
		public void MergeBitmap(string fileName)
		{
			var floorStart = Color.FromArgb(123, 92, 65);
			var floorEnd = Color.FromArgb(143, 114, 80);
			//var caveStart = Color.FromArgb(65, 66, 87);
			//var caveEnd = Color.FromArgb(88, 89, 122);
			var wall = Color.FromArgb(71, 50, 33);
			var cornerJunctions = new List<Point>();

			var bitmap = Mix.GetBitmap(fileName);
			for (var y = 0; y < 25; y++)
			{
				for (var x = 0; x < 80; x++)
				{
					var fgd = Color.Black;
					var bgd = Color.Silver;
					var chr = '?';
					var wal = false;
					var wat = false;
					var cei = false;
					var cli = false;
					var fen = false;
					var gra = false;
					var bur = false;
					var color = bitmap.GetPixel(x, y);

					if (color.Name == "ff000000" || color.A == 0)
						continue;

					if (color.R == color.G && color.R == color.B)
					{
						//Grayscale -- draw as-is for later Replace()ment.
						this.Tilemap[x, y] = new Tile() { Character = 'X', Foreground = color, Background = color };
						continue;
					}

					switch (color.Name)
					{
						case "ff800080": //Purple, floor
							bgd = Toolkit.Lerp(floorStart, floorEnd, Random.NextDouble());
							chr = ' ';
							cei = true;
							bur = true;
							break;
						case "ffff0000": //Red, outer | wall
							fgd = wall;
							bgd = Toolkit.Lerp(floorStart, floorEnd, Random.NextDouble());
							chr = '\x2551';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ffff8080": //Light red, outer corner
							fgd = wall;
							bgd = Toolkit.Lerp(floorStart, floorEnd, Random.NextDouble());
							wal = true;
							cei = true;
							bur = true;
							cornerJunctions.Add(new Point(x, y));
							break;
						case "ff800000": //Dark red, outer -- wall
							fgd = wall;
							bgd = Toolkit.Lerp(floorStart, floorEnd, Random.NextDouble());
							chr = '\x2550';
							wal = true;
							cei = true;
							bur = true;
							break;
					}
					this.Tilemap[x, y] = new Tile()
					{
						Character = chr, Foreground = fgd, Background = bgd,
						Wall = wal, Water = wat, Ceiling = cei,
						Cliff = cli, Fence = fen, Grate = gra,
						CanBurn = bur,
					};
				}
			}

			//Fix up corners and junctions
			var cjResults = new[]
			{
				(int)'x', //0 - none
				0x2551, //1 - only up
				0x2551, //2 - only down
				0x2551, //3 - up and down
				0x2550, //4 - only left
				0x255D, //5 - left and up
				0x2557, //6 - left and down
				0x2563, //7 - left, up, and down
				0x2550, //8 - only right
				0x255A, //9 - right and up
				0x2554, //10 - right and down
				0x2560, //11 - right, up, and down
				0x2550, //12 - left and right
				0x2569, //13 - left, right, and up
				0x2566, //14 - left, right, and down
				0x256C, //15 - all
			};
			foreach (var cj in cornerJunctions)
			{
				var up = cj.Y > 0 ? this.Tilemap[cj.X, cj.Y - 1].Character : 'x';
				var down = cj.Y < 24 ? this.Tilemap[cj.X, cj.Y + 1].Character : 'x';
				var left = cj.X > 0 ? this.Tilemap[cj.X - 1, cj.Y].Character : 'x';
				var right = cj.X < 79 ? this.Tilemap[cj.X + 1, cj.Y].Character : 'x';
				var mask = 0;
				if (up == 0x3F || up == 0xA0 || up == 0x2551 || up == 0x2502 || (up >= 0x2551 && up <= 0x2557) || (up >= 0x255E && up <= 0x2566) || (up >= 0x256A && up <= 0x256C))
					mask |= 1;
				if (down == 0x3F || down == 0xA0 || down == 0x2551 || down == 0x2502 || (down >= 0x2558 && down <= 0x255D) || (down >= 0x255E && down <= 0x2563) || (down >= 0x2567 && down <= 0x256C))
					mask |= 2;
				if (left == 0x3F || left == 0xA0 || left == 0x2550 || left == 0x2500 || (left >= 0x2558 && left <= 0x255A) || (left >= 0x2552 && left <= 0x2554) || (left >= 0x255E && left <= 0x2560) || (left >= 0x2564 && left <= 0x256C))
					mask |= 4;
				if (right == 0x3F || right == 0xA0 || right == 0x2550 || right == 0x2500 || (right >= 0x255B && right <= 0x255D) || (right >= 0x2561 && right <= 0x256C))
					mask |= 8;
				if (mask == 0)
					continue;

				this.Tilemap[cj.X, cj.Y].Character = (char)cjResults[mask];
			}
		}

		[ForJS(ForJSUsage.Either)]
		public void AddClutter(int x1, int y1, int x2, int y2)
		{
			var biomeData = BiomeData.Biomes[(int)GetToken("biome").Value];
			if (biomeData.Clutter == null)
				return;
			foreach (var clutter in biomeData.Clutter)
			{
				var tile = new Tile()
				{
					Character = clutter.Character,
					Foreground = clutter.ForegroundColor,
					CanBurn = clutter.CanBurn,
					Wall = clutter.Wall,
					Fence = clutter.Fence,
					SpecialDescription = clutter.Description,
				};
				for (var x = x1; x < x2; x++)
				{
					for (var y = y1; y < y2; y++)
					{
						var bg = clutter.BackgroundColor == Color.Transparent ? Tilemap[x, y].Background : clutter.BackgroundColor;
						if (Random.NextDouble() < clutter.Chance)
						{
							Tilemap[x, y] = clutter.Noisy ? tile.Noise() : tile;
							Console.WriteLine(Tilemap[x, y].Character + " " + Tilemap[x, y].SpecialDescription);
							Tilemap[x, y].Background = bg;
						}
					}
				}
			}
		}
		[ForJS(ForJSUsage.Either)]
		public void AddClutter()
		{
			AddClutter(0, 0, 79, 24);
		}
	}
}
