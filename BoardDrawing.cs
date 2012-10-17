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
				Foreground = this.Foreground.Darken(2 + (Toolkit.Rand.NextDouble() / 2)),
				Background = this.Background.Darken(2 + (Toolkit.Rand.NextDouble() / 2)),
				Solid = this.Solid,
				CanBurn = this.CanBurn,
				IsWater = this.IsWater,
			};
		}
	}

	public partial class Board : TokenCarrier
	{
		public static Jint.JintEngine DrawJS;
		public static WorldGen WorldGen;

		[ForJS(ForJSUsage.Either)]
		public void Clear(int biomeID)
		{
			this.Entities.Clear();
			if (biomeID == -1)
			{
				if (!this.HasToken("x"))
					return;
				var bitmap = WorldGen.BiomeBitmap;
				var x = (int)this.GetToken("x").Value;
				var y = (int)this.GetToken("y").Value;
				for (int row = 0; row < 25; row++)
				{
					for (int col = 0; col < 80; col++)
					{
						var b = bitmap[(y * 25) + row, (x * 80) + col];
						var d = WorldGen.Biomes[b];
						var fg = d.Color.Darken();
						var bg = d.Color;
						if (d.DarkenPlus != 0 && d.DarkenDiv != 0)
						{
							fg = d.Color.Darken(d.DarkenPlus + (Toolkit.Rand.NextDouble() / d.DarkenDiv));
							bg = d.Color.Darken(d.DarkenPlus + (Toolkit.Rand.NextDouble() / d.DarkenDiv));
						}
						this.Tilemap[col, row] = new Tile()
						{
							Character = d.GroundGlyphs[Toolkit.Rand.Next(d.GroundGlyphs.Length)],
							Foreground = fg,
							Background = bg,
							CanBurn = d.CanBurn,
							IsWater = d.IsWater,
						};
					}
				}
				return;
			}

			var biome = WorldGen.Biomes[biomeID];
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					this.Tilemap[col, row] = new Tile()
					{
						Character = biome.GroundGlyphs[Toolkit.Rand.Next(biome.GroundGlyphs.Length)],
						Foreground = biome.Color.Darken(biome.DarkenPlus + (Toolkit.Rand.NextDouble() / biome.DarkenDiv)),
						Background = biome.Color.Darken(biome.DarkenPlus + (Toolkit.Rand.NextDouble() / biome.DarkenDiv)),
						CanBurn = biome.CanBurn,
						IsWater = biome.IsWater,
					};
				}
			}
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
			var js = DrawJS;
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

			var height = 25;
			var width = 80;
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					js.SetParameter("x", x).SetParameter("y", y).SetParameter("tile", this.Tilemap[x, y]);
					if ((bool)js.Run(checker))
						js.Run(replacer);
				}
			}
		}

		[ForJS(ForJSUsage.Only)]
		public void Floodfill(int startX, int startY, string checker, string replacer, bool allowDiagonals)
		{
			if (DrawJS == null)
				throw new NullReferenceException("Tried to use a board drawing routine with a null drawing machine.");
			var js = DrawJS;

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
		}
		[ForJS(ForJSUsage.Only)]
		public void Floodfill(int startX, int startY, string checker, string replacer)
		{
			Floodfill(startX, startX, checker, replacer, false);
		}
	}
}
