using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	/*
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
	*/

	public partial class Board : TokenCarrier
	{
		public static Jint.JintEngine DrawJS;

		public void Clear(int biomeID)
		{
			this.Entities.Clear();
			this.GetToken("biome").Value = biomeID;

			var biome = BiomeData.Biomes[biomeID];
			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					this.Tilemap[col, row] = new Tile()
					{
						Character = biome.GroundGlyphs[Random.Next(biome.GroundGlyphs.Length)],
						Foreground = biome.Color.Darken(),
						Background = biome.Color,
						CanBurn = biome.CanBurn,
						Water = biome.IsWater,
						Biome = biomeID,
					};
				}
			}
		}
		public void Clear(string biomeName)
		{
			Clear(BiomeData.ByName(biomeName));
		}
		public void Clear()
		{
			Clear((int)this.GetToken("biome").Value);
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
			var worldMapY = y * 50;
			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var b = generator.DetailedMap[worldMapY + row, worldMapX + col];
					var biome = BiomeData.Biomes[b];
					this.Tilemap[col, row] = new Tile()
					{
						Character = biome.GroundGlyphs[Random.Next(biome.GroundGlyphs.Length)],
						Foreground = biome.Color.Darken(),
						Background = biome.Color,
						CanBurn = biome.CanBurn,
						Water = biome.IsWater,
						Biome = b,
					};
				}
			}
		}

		public void SetTile(int x, int y, Tile tile)
		{
			if (x >= 80 || y >= 50 || x < 0 || y < 0)
				return;
			Tilemap[x, y] = tile.Clone();
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
		public void Line(int x1, int y1, int x2, int y2, Tile brush)
		{
			foreach (var point in Toolkit.Line(x1, y1, x2, y2))
				this.Tilemap[point.X, point.Y] = brush.Clone();
		}

		public void Replace(string checker, string replacer)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			var js = DrawJS;
#if DEBUG
			js.SetDebugMode(false);
#endif
			var height = 50;
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
		public void Replace(Func<Tile, int, int, bool> judge, Func<Tile, int, int, Tile> brush)
		{
			var height = 50;
			var width = 80;
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					if (judge(this.Tilemap[y, x], x, y))
						this.Tilemap[x, y] = brush(this.Tilemap[y, x], x, y);
				}
			}
		}

		public void Floodfill(int startX, int startY, string checker, string replacer, bool allowDiagonals)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			var js = DrawJS;
#if DEBUG
			js.SetDebugMode(false);
#endif
			var height = 50;
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
		public void Floodfill(int startX, int startY, Func<Tile, int, int, bool> judge, Func<Tile, int, int, Tile> brush, bool allowDiagonals)
		{
			var height = 50;
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

				if (judge(this.Tilemap[y, x], x, y))
				{
					this.Tilemap[y, x] = brush(this.Tilemap[y, x], x, y);

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
		}
		//HEADS UP: With the new Jint update we might not need this one -- allowDiagonals is assumed to be False, after all...
		public void Floodfill(int startX, int startY, string checker, string replacer)
		{
			Floodfill(startX, startX, checker, replacer, false);
		}
		public void Floodfill(int startX, int startY, Func<Tile, int, int, bool> judge, Func<Tile, int, int, Tile> brush)
		{
			Floodfill(startX, startX, judge, brush, false);
		}

		public void MergeBitmap(string fileName)
		{
			var woodFloor = Color.FromArgb(86, 63, 44);
			var caveFloor = Color.FromArgb(65, 66, 87);
			var wall = Color.FromArgb(20, 15, 12);
			var cornerJunctions = new List<Point>();
			var cornerJunctionsI = new List<Point>();

			var bitmap = Mix.GetBitmap(fileName);
			for (var y = 0; y < 50; y++)
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
						case "ffff00ff": //Magenta, biome floor, removes obstacles
							bgd = this.Tilemap[x, y].Background;
							fgd = this.Tilemap[x, y].Foreground;
							bur = this.Tilemap[x, y].CanBurn;
							chr = ' ';
							break;
						case "ff800080": //Purple, floor
							bgd = woodFloor;
							chr = ' ';
							cei = true;
							bur = true;
							break;
						case "ffff0000": //Red, outer | wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x104';
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
							chr = '\x105';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ffffff00": //Light yellow, inner | wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x10F';
							wal = true;
							cei = true;
							bur = true;
							break;
						case "ff808000": //Dark yellow, inner -- wall
							fgd = wall;
							bgd = woodFloor;
							chr = '\x110';
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
				0x104, //1 - only up
				0x104, //2 - only down
				0x104, //3 - up and down
				0x105, //4 - only left
				0x103, //5 - left and up
				0x101, //6 - left and down
				0x10A, //7 - left, up, and down
				0x105, //8 - only right
				0x102, //9 - right and up
				0x100, //10 - right and down
				0x106, //11 - right, up, and down
				0x105, //12 - left and right
				0x109, //13 - left, right, and up
				0x107, //14 - left, right, and down
				0x108, //15 - all
			};
			foreach (var cj in junctions)
			{
				var up = cj.Y > 0 ? this.Tilemap[cj.X, cj.Y - 1].Character : 'x';
				var down = cj.Y < 24 ? this.Tilemap[cj.X, cj.Y + 1].Character : 'x';
				var left = cj.X > 0 ? this.Tilemap[cj.X - 1, cj.Y].Character : 'x';
				var right = cj.X < 79 ? this.Tilemap[cj.X + 1, cj.Y].Character : 'x';
				var mask = 0;
				if (new[] { 0x3F, 0xFF, 0x100, 0x101, 0x104, 0x106, 0x107, 0x108, 0x10A, 0x10B, 0x10C, 0x10F, 0x111, 0x112, 0x113, 0x115 }.Contains(up))
					mask |= 1;
				if (new[] { 0x3F, 0xFF, 0x102, 0x103, 0x104, 0x106, 0x108, 0x109, 0x10A, 0x10D, 0x10E, 0x10F, 0x111, 0x113, 0x114, 0x115 }.Contains(down))
					mask |= 2;
				if (new[] { 0x3F, 0xFF, 0x100, 0x102, 0x105, 0x106, 0x107, 0x108, 0x109, 0x10B, 0x10D, 0x110, 0x111, 0x112, 0x113, 0x114 }.Contains(left))
					mask |= 4;
				if (new[] { 0x3F, 0xFF, 0x101, 0x103, 0x105, 0x107, 0x108, 0x109, 0x10A, 0x10C, 0x10E, 0x110, 0x112, 0x113, 0x114, 0x115 }.Contains(right))
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
				0x10F, //1 - only up
				0x10F, //2 - only down
				0x10F, //3 - up and down
				0x110, //4 - only left
				0x10E, //5 - left and up
				0x10C, //6 - left and down
				0x115, //7 - left, up, and down
				0x110, //8 - only right
				0x10D, //9 - right and up
				0x10B, //10 - right and down
				0x111, //11 - right, up, and down
				0x110, //12 - left and right
				0x114, //13 - left, right, and up
				0x112, //14 - left, right, and down
				0x113, //15 - all
			};
			foreach (var cj in junctions)
			{
				var up = cj.Y > 0 ? this.Tilemap[cj.X, cj.Y - 1].Character : 'x';
				var down = cj.Y < 24 ? this.Tilemap[cj.X, cj.Y + 1].Character : 'x';
				var left = cj.X > 0 ? this.Tilemap[cj.X - 1, cj.Y].Character : 'x';
				var right = cj.X < 79 ? this.Tilemap[cj.X + 1, cj.Y].Character : 'x';
				var mask = 0;
				if (new[] { 0x3F, 0xFF, 0x100, 0x101, 0x104, 0x106, 0x107, 0x108, 0x10A, 0x10B, 0x10C, 0x10F, 0x111, 0x112, 0x113, 0x115 }.Contains(up))
					mask |= 1;
				if (new[] { 0x3F, 0xFF, 0x102, 0x103, 0x104, 0x106, 0x108, 0x109, 0x10A, 0x10D, 0x10E, 0x10F, 0x111, 0x113, 0x114, 0x115 }.Contains(down))
					mask |= 2;
				if (new[] { 0x3F, 0xFF, 0x100, 0x102, 0x105, 0x106, 0x107, 0x108, 0x109, 0x10B, 0x10D, 0x110, 0x111, 0x112, 0x113, 0x114 }.Contains(left))
					mask |= 4;
				if (new[] { 0x3F, 0xFF, 0x101, 0x103, 0x105, 0x107, 0x108, 0x109, 0x10A, 0x10C, 0x10E, 0x110, 0x112, 0x113, 0x114, 0x115 }.Contains(right))
					mask |= 8;
				if (mask == 0)
					continue;

				this.Tilemap[cj.X, cj.Y].Character = (char)cjResults[mask];
			}
		}

		public void AddClutter(int x1, int y1, int x2, int y2)
		{
			if (y2 >= 50)
				y2 = 49;
			for (var x = x1; x < x2; x++)
			{
				for (var y = y1; y < y2; y++)
				{
					var biomeData = BiomeData.Biomes[Tilemap[x, y].Biome];
					if (biomeData.Clutter == null)
						continue;
					foreach (var clutter in biomeData.Clutter)
					{
						if (Random.NextDouble() < clutter.Chance)
						{
							if (Tilemap[x, y].SolidToDryWalker) //TODO: add a bool appearsInWater to clutter.
								continue;
							Tilemap[x, y] = new Tile()
							{
								Character = clutter.Character,
								Foreground = clutter.ForegroundColor,
								Background = clutter.BackgroundColor == Color.Transparent ? Tilemap[x, y].Background : clutter.BackgroundColor,
								CanBurn = clutter.CanBurn,
								Wall = clutter.Wall,
								Fence = clutter.Fence,
								SpecialDescription = clutter.Description,
							};
						}
					}
				}
			}
		}
		public void AddClutter()
		{
			AddClutter(0, 0, 79, 49);
		}

		public void AddWater(List<Rectangle> safeZones)
		{
			//TODO: Tweak some more to prevent... ahum... water damage.
			
			var biome = BiomeData.Biomes[(int)this.GetToken("biome").Value];
			var water = BiomeData.Biomes[BiomeData.ByName(biome.Realm == Realms.Nox ? "Water" : "KoolAid")];
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

			var bitmap = new float[50, 80];
			var sum = 0.0;
			var radius = 0.4;
			for (var y = 0; y < 50; y++)
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
				}
			}

			for (var y = 0; y < 50; y++)
			{
				for (var x = 0; x < 80; x++)
				{
					if (bitmap[y, x] < threshold || Tilemap[x, y].SolidToWalker)
						continue;
					Tilemap[x, y] = new Tile()
					{
						Character = water.GroundGlyphs[Random.Next(water.GroundGlyphs.Length)],
						Foreground = water.Color.Darken(), //.Darken(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv)),
						Background = water.Color, //(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv)),
						Water = true,
					};
				}
			}
		}
		public void AddWater()
		{
			AddWater(new List<Rectangle>() { new Rectangle() { Left = 0, Top = 0, Right = 79, Bottom = 24 } });
		}

		public void Drain()
		{
			var b = (int)GetToken("biome").Value;
			var biome = BiomeData.Biomes[b];
			var tile = new Tile()
			{
				Character = biome.GroundGlyphs[Random.Next(biome.GroundGlyphs.Length)],
				Foreground = biome.Color.Darken(),
				Background = biome.Color,
				CanBurn = biome.CanBurn,
				Biome = b,
			};

			for (var y = 0; y < 50; y++)
			{
				for (var x = 0; x < 80; x++)
				{
					if (Tilemap[x, y].Water)
					{
						Tilemap[x, y] = tile.Clone();
					}
				}
			}
		}
	}
}
