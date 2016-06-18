using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Board : TokenCarrier
	{
		public static Jint.JintEngine DrawJS;

		public void Clear(int biomeID)
		{
			this.Entities.Clear();
			this.GetToken("biome").Value = biomeID;
			this.GetToken("music").Text = BiomeData.Biomes[biomeID].Music;

			var ground = TileDefinition.Find(BiomeData.Biomes[biomeID].GroundTile, true);

			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var def = ground;
					if (Random.Flip() && def.Variants.Tokens.Count > 0)
					{
						var chance = Random.NextDouble();
						var variants = def.Variants.Tokens.Where(v => v.Value < chance).ToArray();
						if (variants.Length > 0)
							def = TileDefinition.Find(variants[Random.Next(variants.Length)].Name);
					}
					SetTile(row, col, def);
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
			var biomeID = generator.RoughBiomeMap[y, x];
			if (biomeID == -1)
				return;
			this.Entities.Clear();
			this.GetToken("biome").Value = biomeID;
			var worldMapX = x * 80;
			var worldMapY = y * 50;
			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var b = generator.DetailedMap[worldMapY + row, worldMapX + col];
					var h = generator.WaterMap[worldMapY + row, worldMapX + col];
					this.Tilemap[col, row].Index = TileDefinition.Find(BiomeData.Biomes[b].GroundTile).Index;
					this.Tilemap[col, row].Fluid = h == 1 ? Fluids.Water : Fluids.Dry;
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

		public void MergeBitmap(string fileName, string tiledefs)
		{
			var bitmap = Mix.GetBitmap(fileName);
			var tileset = Mix.GetString(tiledefs).Split('\n');
			var tiles = new Dictionary<string, string>();
			foreach (var tile in tileset)
			{
				var t = tile.Trim().Split('\t');
				tiles.Add("ff" + t[0].ToLowerInvariant(), t[1]);
			}
			for (var y = 0; y < 50; y++)
			{
				for (var x = 0; x < 80; x++)
				{
					var color = bitmap.GetPixel(x, y);
					if (color.Name == "ff000000" || color.A == 0)
						continue;
					if (!tiles.ContainsKey(color.Name))
						continue;
					this.Tilemap[x, y].Index = TileDefinition.Find(tiles[color.Name]).Index;

					if (tiles[color.Name].StartsWith("doorway"))
					{
						var door = new Door()
						{
							XPosition = x,
							YPosition = y,
							ForegroundColor = this.Tilemap[x, y].Definition.Background,
							BackgroundColor = this.Tilemap[x, y].Definition.Background.Darken(),
							ID = "mergeBitmap_Door" + x + "_" + y,
							ParentBoard = this,
							Closed = tiles[color.Name].EndsWith("Closed"),
							Glyph = '+'
						};
						this.Entities.Add(door);
					}
				}
			}
			this.ResolveVariableWalls();
		}

		public void AddClutter(int x1, int y1, int x2, int y2)
		{
			/*
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
			*/
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
					Tilemap[x, y].Fluid = Realm == Realms.Nox ? Fluids.Water : Fluids.KoolAid;
					/*
					Tilemap[x, y] = new Tile()
					{
						Character = water.GroundGlyphs[Random.Next(water.GroundGlyphs.Length)],
						Foreground = water.Color.Darken(), //.Darken(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv)),
						Background = water.Color, //(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv)),
						Water = true,
					};
					*/
				}
			}
		}
		public void AddWater()
		{
			AddWater(new List<Rectangle>() { new Rectangle() { Left = 0, Top = 0, Right = 79, Bottom = 24 } });
		}

		public void Drain()
		{
			for (var y = 0; y < 50; y++)
				for (var x = 0; x < 80; x++)
					Tilemap[x, y].Fluid = Fluids.Dry;
		}
	}
}
