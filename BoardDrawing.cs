using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Tile
	{
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

		public void Clear(string biomeName)
		{
			Clear(BiomeData.ByName(biomeName));
		}

		public void ClearToWorld(WorldMapGenerator generator)
		{
			if (!this.HasToken("coordinate"))
				return;
			var coord = this.Coordinate;
			var x = coord.X;
			var y = coord.Y;
			this.Entities.Clear();
			var biomeID = generator.RoughBiomeMap[y, x];
			this.GetToken("biome").Value = biomeID;
			var worldMapX = x * 80;
			var worldMapY = y * 25;
			BiomeData biome;
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var b = generator.DetailedMap[worldMapY + row, worldMapX + col];
					biome = BiomeData.Biomes[b];
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
		public void Line(int x1, int y1, int x2, int y2, Tile brush, bool noise)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			foreach (var point in Toolkit.Line(x1, y1, x2, y2))
			{
				this.Tilemap[point.Y, point.X] = noise ? brush.Noise() : brush;
			}
		}
		public void Line(int x1, int y1, int x2, int y2, Tile brush)
		{
			Line(x1, y1, x2, y2, brush, true);
		}

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
		//HEADS UP: With the new Jint update we might not need this one -- allowDiagonals is assumed to be False, after all...
		public void Floodfill(int startX, int startY, string checker, string replacer)
		{
			Floodfill(startX, startX, checker, replacer, false);
		}

		public void MergeBitmap(string fileName)
		{
			var woodFloor = Color.FromArgb(86, 63, 44);
			var caveFloor = Color.FromArgb(65, 66, 87);
			var wall = Color.FromArgb(20, 15, 12);
			var cornerJunctions = new List<Point>();
			var cornerJunctionsI = new List<Point>();

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
							bgd = woodFloor;
							chr = ' ';
							cei = true;
							bur = true;
							break;
						case "ffff0000": //Red, outer | wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x2551';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ffff8080": //Light red, outer corner
							fgd = wall;
							bgd = woodFloor;
							wal = true;
							cei = true;
							bur = true;
							cornerJunctions.Add(new Point(x, y));
							break;
						case "ff800000": //Dark red, outer -- wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x2550';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ffffff00": //Light yellow, inner | wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x2502';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ff808000": //Dark yellow, inner -- wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x2500';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ffffffc0": //Pale yellow, inner corner
							fgd = wall;
							bgd = woodFloor;
							wal = true;
							cei = true;
							bur = true;
							cornerJunctionsI.Add(new Point(x, y));
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
			FixOuterJunctions(cornerJunctions);
			FixInnerJunctions(cornerJunctionsI);
		}

		private void FixOuterJunctions(List<Point> junctions)
		{
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
			foreach (var cj in junctions)
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
		private void FixInnerJunctions(List<Point> junctions)
		{
			var cjResults = new[]
			{
				(int)'x', //0 - none
				0x2502, //1 - only up
				0x2502, //2 - only down
				0x2502, //3 - up and down
				0x2500, //4 - only left
				0x2518, //5 - left and up
				0x2510, //6 - left and down
				0x2524, //7 - left, up, and down
				0x2500, //8 - only right
				0x2514, //9 - right and up
				0x250C, //10 - right and down
				0x251C, //11 - right, up, and down
				0x2500, //12 - left and right
				0x2534, //13 - left, right, and up
				0x252C, //14 - left, right, and down
				0x253C, //15 - all
			};
			var ups = new[] { 0x2502, 0x250C, 0x2510, 0x251C, 0x2524, 0x252C, 0x253C };
			var downs = new[] { 0x2502, 0x2514, 0x2518, 0x251C, 0x2524, 0x2534, 0x253C };
			var lefts = new[] { 0x2500, 0x250C, 0x2514, 0x251C, 0x252C, 0x2534, 0x253C };
			var rights = new[] { 0x2500, 0x2510, 0x2518, 0x2524, 0x252C, 0x2534, 0x253C };
			foreach (var cj in junctions)
			{
				var up = cj.Y > 0 ? this.Tilemap[cj.X, cj.Y - 1].Character : 'x';
				var down = cj.Y < 24 ? this.Tilemap[cj.X, cj.Y + 1].Character : 'x';
				var left = cj.X > 0 ? this.Tilemap[cj.X - 1, cj.Y].Character : 'x';
				var right = cj.X < 79 ? this.Tilemap[cj.X + 1, cj.Y].Character : 'x';
				var mask = 0;
				if (ups.Contains(up) || (up >= 0x2551 && up <= 0x2557) || (up >= 0x255E && up <= 0x2556) || (up >= 0x256A && up <= 0x256c))
					mask |= 1;
				if (downs.Contains(down) || (down >= 0x2558 && down <= 0x255D) || (down >= 0x255E && down <= 0x2563) || (down >= 0x2567 && down <= 0x256C))
					mask |= 2;
				if (lefts.Contains(left) || (left >= 0x2558 && left <= 0x255A) || (left >= 0x2552 && left <= 0x2554) || (left >= 0x255E && left <= 0x2560) || (left >= 0x2564 && left <= 0x256C))
					mask |= 4;
				if (rights.Contains(right) || (right >= 0x255B && right <= 0x255D) || (right >= 0x2561 && right <= 0x256C))
					mask |= 8;
				if (mask == 0)
					continue;

				this.Tilemap[cj.X, cj.Y].Character = (char)cjResults[mask];
			}
		}

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
						if (Tilemap[x, y].Water) //add checks to allow clutter to appear in water and/or ground
							continue;
						var bg = clutter.BackgroundColor == Color.Transparent ? Tilemap[x, y].Background : clutter.BackgroundColor;
						if (Random.NextDouble() < clutter.Chance)
						{
							Tilemap[x, y] = clutter.Noisy ? tile.Noise() : tile;
							Tilemap[x, y].Background = bg;
						}
					}
				}
			}
		}
		public void AddClutter()
		{
			AddClutter(0, 0, 79, 24);
		}

		public void AddWater(List<Rectangle> safeZones)
		{
			//TODO: Tweak some more to prevent... ahum... water damage.
			
			var biome = BiomeData.Biomes[(int)this.GetToken("biome").Value];
			var water = BiomeData.Biomes[BiomeData.ByName(biome.RealmID == "Nox" ? "Water" : "KoolAid")];
			var points = new List<Point>();
			var pointsPerZone = 4;
			var threshold = 0.66f;
			if (safeZones.Count == 1 && safeZones[0].Left == 0 && safeZones[0].Right == 79)
			{
				safeZones.Clear();
				pointsPerZone = 8;
				var x = Random.Next(0, 40);
				var y = Random.Next(0, 10);
				safeZones.Add(new Rectangle() { Left = x, Top = y, Right = x + 30, Bottom = y + 14 });
				threshold = 0.5f;
			}
			foreach (var zone in safeZones)
			{
				for (var i = 0; i < pointsPerZone; i++)
				{
					var x = Random.Next(zone.Left + 4, zone.Right - 4);
					var y = Random.Next(zone.Top + 4, zone.Bottom - 4);
					points.Add(new Point(x, y));
				}
			}

			var bitmap = new float[25, 80];
			var sum = 0.0;
			var radius = 0.4;
			#if DEBUG
			var temp = new System.Drawing.Bitmap(80, 25);
			var tempg = System.Drawing.Graphics.FromImage(temp);
			tempg.Clear(System.Drawing.Color.Black);
			#endif
			for (var y = 0; y < 25; y++)
			{
				for (var x = 0; x < 80; x++)
				{
					sum = 0;
					foreach (var point in points)
					{
						sum += radius / Math.Sqrt((x - point.X) * (x - point.X) + (y - point.Y) * (y - point.Y));
					}
					if (sum > 1.0)
						sum = 1.0;
					if (sum < 0.0)
						sum = 0.0;
					bitmap[y, x] = (float)sum;
					#if DEBUG
					var g = (byte)(sum * 255.0);
					temp.SetPixel(x, y, System.Drawing.Color.FromArgb(g, g, g));
					#endif
				}
			}
			#if DEBUG
			temp.Save("meta.png", System.Drawing.Imaging.ImageFormat.Png);
			#endif

			for (var y = 0; y < 25; y++)
			{
				for (var x = 0; x < 80; x++)
				{
					if (bitmap[y, x] < threshold || Tilemap[x, y].SolidToWalker)
						continue;
					Tilemap[x, y] = new Tile()
					{
						Character = water.GroundGlyphs[Random.Next(water.GroundGlyphs.Length)],
						Foreground = water.Color.Darken(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv)),
						Background = water.Color.Darken(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv)),
						Water = true,
					};
				}
			}
		}
		public void AddWater()
		{
			AddWater(new List<Rectangle>() { new Rectangle() { Left = 0, Top = 0, Right = 79, Bottom = 24 } });
		}
	}
}
