using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Board : TokenCarrier
	{
		public void Clear(int biomeID)
		{
			this.Entities.Clear();
			this.GetToken("biome").Value = biomeID;
			this.GetToken("music").Text = BiomeData.Biomes[biomeID].Music;

			var ground = TileDefinition.Find(BiomeData.Biomes[biomeID].GroundTile, true);

			for (int row = 0; row < Height; row++)
			{
				for (int col = 0; col < Width; col++)
				{
					var def = ground;
					if (Random.Flip() && def.Variants.Tokens.Count > 0)
					{
						var chance = Random.NextDouble();
						var variants = def.Variants.Tokens.Where(v => v.Value < chance).ToArray();
						if (variants.Length > 0)
							def = TileDefinition.Find(variants.PickOne().Name);
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
			this.Width = 80; //TODO: determine better overworld board size?
			this.Height = 50;
			this.GetToken("biome").Value = biomeID;
			var worldMapX = x * Width;
			var worldMapY = y * Height;
			for (int row = 0; row < Height; row++)
			{
				for (int col = 0; col < Width; col++)
				{
					var b = generator.DetailedMap[worldMapY + row, worldMapX + col];
					var h = generator.WaterMap[worldMapY + row, worldMapX + col];
					this.Tilemap[col, row].Index = TileDefinition.Find(BiomeData.Biomes[b].GroundTile).Index;
					this.Tilemap[col, row].Fluid = h == 1 ? Fluids.Water : Fluids.Dry;
				}
			}
		}

		/// <summary>
		/// Draws a line from one point to another.
		/// </summary>
		/// <param name="x1">The starting row.</param>
		/// <param name="y1">The starting column.</param>
		/// <param name="x2">The ending row.</param>
		/// <param name="y2">The ending column.</param>
		/// <param name="brush">A <see cref="Noxico.TileDefinition"/>, tile name as a string, or tile number, or a Lua callback function.</param>
		/// <remarks>The callback function takes the tile number and TileDefinition at the current coordinates in the line and the coordinates, and must return a TileDef, string, or int.</remarks>
		public void Line(int x1, int y1, int x2, int y2, object brush)
		{
			if (brush is TileDefinition)
				brush = ((TileDefinition)brush).Index;
			else if (brush is string)
				brush = TileDefinition.Find((string)brush).Index;
			if (brush is int)
			{
				foreach (var point in Toolkit.Line(x1, y1, x2, y2))
					SetTile(point.X, point.Y, (int)brush);
			}
			else if (brush is Func<object, object, object, object, Neo.IronLua.LuaResult>)
			{
				var callback = (Func<object, object, object, object, Neo.IronLua.LuaResult>)brush;
				foreach (var point in Toolkit.Line(x1, y1, x2, y2))
				{
					var tileHere = Tilemap[point.Y, point.Y];
					brush = callback(tileHere.Index, tileHere.Definition, point.X, point.Y)[0];
					if (brush is TileDefinition)
						brush = ((TileDefinition)brush).Index;
					else if (brush is string)
						brush = TileDefinition.Find((string)brush).Index;
					SetTile(point.X, point.Y, (int)brush);
				}
			}
		}

		/// <summary>
		/// Replaces one tile with another across the board.
		/// </summary>
		/// <param name="replaceThis">A <see cref="Noxico.TileDefinition"/>, tile name as a string, or tile number, or a Lua callback function.</param>
		/// <param name="withThis">A <see cref="Noxico.TileDefinition"/>, tile name as a string, or tile number, or a Lua callback function.</param>
		/// <remarks>The callback functions takes the tile number and TileDefinition at the current coordinates in the line and the coordinates. One must return a boolean, the other a TileDef, string, or int.</remarks>
		public void Replace(object replaceThis, object withThis)
		{
			if (replaceThis is TileDefinition)
				replaceThis = ((TileDefinition)replaceThis).Index;
			else if (replaceThis is string)
				replaceThis = TileDefinition.Find((string)replaceThis).Index;
			if (withThis is TileDefinition)
				withThis = ((TileDefinition)withThis).Index;
			else if (withThis is string)
				withThis = TileDefinition.Find((string)withThis).Index;
			for (var y = 0; y < Width; y++)
			{
				for (var x = 0; x < Height; x++)
				{
					if (replaceThis is int)
					{
						if (Tilemap[y, x].Index != (int)replaceThis)
							continue;
					}
					else if (replaceThis is Func<object, object, object, object, Neo.IronLua.LuaResult>)
					{
						var callback = (Func<object, object, object, object, Neo.IronLua.LuaResult>)replaceThis;
						var tileHere = Tilemap[y, x];
						if (!(bool)callback(tileHere.Index, tileHere.Definition, x, y)[0])
							continue;
					}

					var w = withThis;
					if (w is Func<object, object, object, object, Neo.IronLua.LuaResult>)
					{
						var callback = (Func<object, object, object, object, Neo.IronLua.LuaResult>)withThis;
						var tileHere = Tilemap[y, x];
						w = callback(tileHere.Index, tileHere.Definition, x, y)[0];
					}
					SetTile(x, y, (int)w);
				}
			}
		}
		
		/// <summary>
		/// Replaces every tile found in a floodfill with another.
		/// </summary>
		/// <param name="startX">The starting row.</param>
		/// <param name="startY">The starting column.</param>
		/// <param name="replaceThis">Null/nil, a <see cref="Noxico.TileDefinition"/>, tile name as a string, or tile number, or a Lua callback function.</param>
		/// <param name="withThis">A <see cref="Noxico.TileDefinition"/>, tile name as a string, or tile number, or a Lua callback function.</param>
		/// <param name="allowDiagonals">If true, flood in eight directions, passing through more gaps.</param>
		/// <remarks>The callback functions takes the tile number and TileDefinition at the current coordinates in the line and the coordinates. One must return a boolean, the other a TileDef, string, or int. If a null/nil is given, the tile number at the starting coordinates is used.</remarks>
		public void Floodfill(int startX, int startY, object replaceThis, object withThis, bool allowDiagonals)
		{
			if (replaceThis is TileDefinition)
				replaceThis = ((TileDefinition)replaceThis).Index;
			else if (replaceThis is string)
				replaceThis = TileDefinition.Find((string)replaceThis).Index;
			if (withThis is TileDefinition)
				withThis = ((TileDefinition)withThis).Index;
			else if (withThis is string)
				withThis = TileDefinition.Find((string)withThis).Index;

			if (replaceThis == null)
			{
				replaceThis = Tilemap[startY, startX].Index;
			}

			var stack = new Stack<Point>();
			stack.Push(new Point(startX, startY));
			while (stack.Count > 0)
			{
				var point = stack.Pop();
				var x = point.X;
				var y = point.Y;
				if (x < 0 || y < 0 || x >= Height || y >= Width)
					continue;

				if (replaceThis is int)
				{
					if (Tilemap[y, x].Index != (int)replaceThis)
						continue;
				}
				else if (replaceThis is Func<object, object, object, object, Neo.IronLua.LuaResult>)
				{
					var callback = (Func<object, object, object, object, Neo.IronLua.LuaResult>)replaceThis;
					var tileHere = Tilemap[y, x];
					if (!(bool)callback(tileHere.Index, tileHere.Definition, x, y)[0])
						continue;
				}
				
				{
					var w = withThis;
					if (w is Func<object, object, object, object, Neo.IronLua.LuaResult>)
					{
						var callback = (Func<object, object, object, object, Neo.IronLua.LuaResult>)withThis;
						var tileHere = Tilemap[y, x];
						w = callback(tileHere.Index, tileHere.Definition, x, y)[0];
					}
					SetTile(x, y, (int)w);

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

		public void MergeBitmap(string fileName, string tiledefs)
		{
			var bitmap = Mix.GetBitmap(fileName);
			var tileset = Mix.GetString(tiledefs).Split('\n');
			var tiles = new Dictionary<string, string>();
			var clutter = new Dictionary<string, string>();
			var unique = new Dictionary<string, string>();
			foreach (var tile in tileset)
			{
				if (tile.IsBlank())
					continue;
				var t = tile.Trim().Split('\t');
				if (t[1].Contains('+')) //tileID +clut[prop:val, ...]
				{
					var t1 = t[1];
					tiles.Add("ff" + t[0].ToLowerInvariant(), t1.Remove(t1.IndexOf('+')).Trim());
					t1 = t1.Substring(t1.IndexOf('+'));
					while (t1.StartsWith('+'))
					{
						var skip = t1.Substring(t1.IndexOf("[") + 1); //skip to after the [
						var key = "ff" + t[0].ToLowerInvariant();
						var value = skip.Substring(0, skip.IndexOf(']'));

						if (t1.StartsWith("+clut"))
							clutter.Add(key,value);
						else if (t1.StartsWith("+unique"))
							unique.Add(key, value);
						t1 = t1.Substring(t1.IndexOf(']') + 1).Trim(); // loop other possible '+'es
						//TODO: allow other kinds of entities such as dropped items, generic npcs, etc
					}
				}
				else
					tiles.Add("ff" + t[0].ToLowerInvariant(), t[1]);
			}
			for (var y = 0; y < Height; y++)
			{
				for (var x = 0; x < Width; x++)
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

					if (clutter.ContainsKey(color.Name))
					{
						var nc = new Clutter()
						{
							XPosition = x,
							YPosition = y,
							ParentBoard = this
						};
						this.Entities.Add(nc);
						var properties = clutter[color.Name].SplitQ();
						foreach (var property in properties)
						{
							var key = property.Substring(0, property.IndexOf(':'));
							var value = property.Substring(property.IndexOf(':') + 1);
							switch (key.ToLowerInvariant())
							{
								case "id": nc.ID = value; break;
								case "name": nc.Name = value; break;
								case "desc": nc.Description = value; break;
								case "glyph":
									if (value.StartsWith("0x"))
										nc.Glyph = int.Parse(value.Substring(2), System.Globalization.NumberStyles.HexNumber);
									else
										nc.Glyph = int.Parse(value);
									break;
								case "fg":
									if (value.StartsWith('#'))
										nc.ForegroundColor = Color.FromCSS(value);
									else
										nc.ForegroundColor = Color.FromName(value);
									break;
								case "bg":
									if (value.StartsWith('#'))
										nc.BackgroundColor = Color.FromCSS(value);
									else
										nc.BackgroundColor = Color.FromName(value);
									break;
								case "block": nc.Blocking = true; break;
								case "burns": nc.CanBurn = true; break;
							}
						}
					}

					if (unique.ContainsKey(color.Name))
					{
						var properties = unique[color.Name].SplitQ();
						string uniquename = string.Empty;
						foreach (var property in properties)
						{
							var key = property.Substring(0, property.IndexOf(':'));
							var value = property.Substring(property.IndexOf(':') + 1);
							switch (key.ToLowerInvariant())
							{
								case "id":
									uniquename = value; break;
							}
						}
						if (uniquename != string.Empty)
						{
							var newChar = new BoardChar(Character.GetUnique(uniquename))
							{
								XPosition = x,
								YPosition = y,
								ParentBoard = this
							};
							this.Entities.Add(newChar);
							newChar.AssignScripts(uniquename);
							newChar.ReassignScripts();
						}
					}
				}
			}
			this.ResolveVariableWalls();
		}

		public void AddClutter(int x1, int y1, int x2, int y2)
		{
			//TODO: Reimplement this? Is currently covered by Variants.
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

			var bitmap = new float[Height, Width];
			var sum = 0.0;
			var radius = 0.4;
			for (var y = 0; y < Height; y++)
			{
				for (var x = 0; x < Width; x++)
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

			for (var y = 0; y < Height; y++)
			{
				for (var x = 0; x < Width; x++)
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
			for (var y = 0; y < Height; y++)
				for (var x = 0; x < Width; x++)
					Tilemap[x, y].Fluid = Fluids.Dry;
		}
	}
}
