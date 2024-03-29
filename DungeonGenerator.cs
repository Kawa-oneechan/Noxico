using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SysRectangle = System.Drawing.Rectangle;

/* A'ight but consider this:
 * 
 * Given a blank slate, be it open ground, open water, or a massive 50*80 slab of granite...
 * Have each different generator be available as operations.
 * So you could have like an open grassland board and use the cell cave generator to make a lake in it.
 * Or you could do the opposite and have an island.
 * Or you could have open water with an island and *another* cell-made forest on top.
 * And on top of all that you could have the StoneDungeon and Town generators only place rooms.
 * They could leave markers for corridor endpoints.
 * Another function might then take those endpoint markers and draw pathways between them.
 * 
 * To do a populated town:
 * 1. start with open ground
 * 2. drain it?
 * 3. run the residential generator -> draw fully furnished and cluttered homes, get zones, inhabitants, endpoints
 * 4. run the pathway generator, limit 4 -> draw unconnected bits of path extending up to four tiles from the doors
 * 
 * Underground town?
 * 1. start with granite
 * 2. run the residential generator
 * 3. run the pathway generator, no limit
 */

namespace Noxico
{
	internal class Template
	{
		public string Name;
		public int Inhabitants;
		public int Width, Height;
		public int PlotWidth, PlotHeight;
		public bool AllowOutside;
		public string[] MapScans;
		public Dictionary<char, Token> Markings;
		public Template(Token token)
		{
			this.Name = token.Text;
			this.Inhabitants = token.HasToken("inhabitants") ? (int)token.GetToken("inhabitants").Value : 0;
			var map = token.GetToken("map").Tokens[0].Text;
			MapScans = map.Trim().Split('\n').Select(x => x.Trim()).ToArray();
			Width = MapScans[0].Length;
			Height = MapScans.Length;
			PlotWidth = (int)Math.Ceiling(Width / 13.0);
			PlotHeight = (int)Math.Ceiling(Height / 16.0);
			AllowOutside = token.HasToken("allowOutside");
			Markings = new Dictionary<char, Token>();
			if (!token.HasToken("markings"))
				return;
			foreach (var marking in token.GetToken("markings").Tokens)
			{
				var c = marking.Name[0];
				Markings.Add(c, marking);
			}
		}
	}

	internal struct Building
	{
		public int XShift, YShift;
		public Template Template;
		public string BaseID;
		public List<Character> Inhabitants;

		public Building(string baseID, Template template, int x, int y, Culture culture)
		{
			Template = template;
			XShift = x;
			YShift = y;
			Inhabitants = new List<Character>();
			if (template != null && culture != null && template.Inhabitants > 0)
			{
				Inhabitants = GetInhabitants(template.Inhabitants, culture);
				BaseID = string.Format("{0}_{1}", baseID, Inhabitants[0].Name.Surname);
			}
			else
				BaseID = baseID;
		}

		private static List<Character> GetInhabitants(int count, Culture culture)
		{
			var r = new List<Character>();
			var familyName = string.Empty;
			//var dontShareSurname = true;
			var areMarried = Random.NextDouble() > 0.7;
			var firstPlan = string.Empty;
			//count = 2;
			for (var i = 0; i < count; i++)
			{
				Character c;
				var plan = culture.Bodyplans.PickWeighted().Name;
				if (i > 0 && Random.NextDouble() > 0.7)
					plan = firstPlan;
				Realms world;
				switch (culture.ID)
				{
					case "human": world = Realms.Nox; break;
					case "seradevar": world = Realms.Seradevari; break;
					default: world = Realms.Nox; break;
				}
				var myGender = count == 1 ? Gender.RollDice : (i == 0 ? Gender.Male : Gender.Female);
				c = Character.Generate(plan, myGender, myGender, world);
				if (i == 0)
				{
					familyName = c.Name.Surname;
					firstPlan = plan;
				}
				if (i == 1)
				{
					/* Just for the record, because I've had the wildest time trying to figure this out years later:
					 * 			0		0,25	0,5		0,75	1		<- culture.Marriage
					 * 	0		friend	spouse	spouse	spouse	spouse
					 * 	0,25	friend	friend	spouse	spouse	spouse
					 * 	0,5		friend	friend	friend	spouse	spouse
					 * 	0,75	friend	friend	friend	friend	spouse
					 * 	1		friend	friend	friend	friend	friend
					 * 	^- Random.NextDouble()
					 * So a culture with Marriage set to 0 would NEVER marry, and a culture with Marriage set to 1
					 * NEARLY ALWAYS marries.
					 */
					var shipType = Random.NextDouble() < culture.Marriage ? "spouse" : "friend";
					//if we chose spouse, handle the wife taking the surname of the husband.
					var ship = new Token(c.ID);
					ship.AddToken(shipType);
					r[0].Path("ships").Tokens.Add(ship);
					ship = new Token(r[0].ID);
					ship.AddToken(shipType);
					c.Path("ships").Tokens.Add(ship);
				}
				r.Add(c);
				//Scheduler.AddSchedule("villager", c);
			}
			return r;
		}
	}

	abstract class BaseDungeonGenerator
	{
		protected static Dictionary<string, List<Template>> templates = null;
		public Board Board { get; set; }
		public Culture Culture { get; set; }
		protected int[,] map;
		protected BiomeData biome;
		protected Building[,] plots;
		protected bool allowCaveFloor, includeWater, includeClutter;

		private int plotWidth = 13, plotHeight = 16;
		private int plotCols, plotRows;

		private void CreateRotationsAndFlips(List<Token> templateSource)
		{
			var nl = new[] { '\r', '\n' };
			foreach (var set in templateSource.Where(x => x.Name == "set"))
			{
				for (var i = 0; i < set.Tokens.Count; i++) //can't foreach cos we're editing the list as we go :<
				{
					if (set.Tokens[i].Name != "template")
						continue;
					var template = set.Tokens[i];
					if (template.HasToken("flip-h"))
					{
						template.RemoveToken("flip-h");
						var newTemplate = template.Clone(true);
						newTemplate.Text += " (flip-h)";
						var map = newTemplate.GetToken("map").Tokens[0];
						var lines = map.Text.Split(nl, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
						for (var l = 0; l < lines.Length; l++)
							lines[l] = new string(lines[l].Reverse().ToArray());
						map.Text = string.Join("\n", lines);
						set.Tokens.Insert(i + 1, newTemplate);
					}
					if (template.HasToken("flip-v"))
					{
						template.RemoveToken("flip-v");
						var newTemplate = template.Clone(true);
						newTemplate.Text += " (flip-v)";
						var map = newTemplate.GetToken("map").Tokens[0];
						var lines = map.Text.Split(nl, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
						lines = lines.Reverse().ToArray();
						map.Text = string.Join("\n", lines);
						set.Tokens.Insert(i + 1, newTemplate);
					}
				}
			}
		}

		public void Create(BiomeData biome, string templateSet)
		{
			Culture = Culture.Cultures[biome.Cultures.PickOne()];
			DungeonGenerator.DungeonGeneratorBiome = BiomeData.Biomes.IndexOf(biome);
			this.biome = biome;
			map = new int[Board.Width, Board.Height];

			plotCols = Board.Width / plotWidth;
			plotRows = Board.Height / plotHeight;
			if (plotCols * plotWidth < Board.Width) plotWidth = Board.Width / plotCols;
			if (plotRows * plotHeight < Board.Height) plotHeight = Board.Height / plotRows;

			plots = new Building[plotCols, plotRows];

			if (templates == null)
			{
				templates = new Dictionary<string, List<Template>>();
				var templateSource = Mix.GetTokenTree("buildings.tml");
				CreateRotationsAndFlips(templateSource);
				foreach (var set in templateSource.Where(x => x.Name == "set"))
				{
					var thisSet = new List<Template>();
					foreach (var template in set.Tokens.Where(x => x.Name == "template"))
						thisSet.Add(new Template(template));
					templates.Add(set.Text, thisSet);
				}
			}

			var spill = new Building("<spillover>", null, 0, 0, Culture.DefaultCulture);
			//var justPlaced = false;

			for (var row = 0; row < plotRows; row++)
			{
				for (var col = 0; col < plotCols; col++)
				{
					if (plots[col, row].BaseID == "<spillover>")
						continue;
					//Small chance of not having anything here, for variation.
					if (Random.NextDouble() < 0.2)
					{
						//justPlaced = false;
						continue;
					}
					var newTemplate = templates[templateSet].PickOne();
					//TODO: check if chosen template spills over and if so, if there's room. For now, assume all templates are <= 8
					//Each plot is 8x8. Given that and the template size, we can wiggle them around a bit from 0 to (8 - tSize).
					var sX = newTemplate.Width < plotWidth ? Random.Next(1, plotWidth - newTemplate.Width) : 0;
					var sY = newTemplate.Height < plotHeight ? Random.Next(1, plotHeight - newTemplate.Height) : 0;

					//NEW: check for water in this plot.
					var water = 0;
					for (var y = 0; y < newTemplate.Height; y++)
					{
						for (var x = 0; x < newTemplate.Width; x++)
						{
							if (Board.Tilemap[(col * plotWidth) + x, (row * plotHeight) + y].Fluid != Fluids.Dry)
								water++;
						}
					}
					if (water > 0)
						continue;

					//Later on, we might be able to wiggle them out of their assigned plot a bit.
					var newBuilding = new Building(string.Format("house{0}x{1}", row, col), newTemplate, sX, sY, Culture);
					plots[col, row] = newBuilding;
					//justPlaced = true;
				}
			}
		}

		public virtual void ToTilemap(ref Tile[,] map)
		{
			var woodFloor = Color.FromArgb(86, 63, 44);
			var caveFloor = Color.FromArgb(65, 66, 87);
			var wall = Color.FromArgb(20, 15, 12);
			var water = BiomeData.Biomes[BiomeData.ByName(biome.Realm == Realms.Nox ? "Water" : "KoolAid")];
			allowCaveFloor = false;

			Clutter.ParentBoardHack = Board;

			var doorCount = 0;

			var safeZones = new List<Rectangle>();

			for (var row = 0; row < plotRows; row++)
			{
				for (var col = 0; col < plotCols; col++)
				{
					if (plots[col, row].BaseID == null)
					{
						//Can clutter this up!
						if (includeClutter && Random.Flip())
							Board.AddClutter(col * plotWidth, row * plotHeight, (col * plotWidth) + plotWidth, (row * plotHeight) + plotHeight + row);
						else
							safeZones.Add(new Rectangle() { Left = col * plotWidth, Top = row * plotHeight, Right = (col * plotWidth) + plotWidth, Bottom = (row * plotHeight) + plotHeight + row });
						continue;
					}

					if (plots[col, row].BaseID == "<spillover>")
						continue;
					var building = plots[col, row];
					var template = building.Template;
					var sX = (col * plotWidth) + building.XShift;
					var sY = (row * plotHeight) + building.YShift;
					for (var y = 0; y < template.Height; y++)
					{
						for (var x = 0; x < template.Width; x++)
						{
							var tc = template.MapScans[y][x];
							var def = string.Empty;
							var fluid = Fluids.Dry;

							var addDoor = true;
							if (tc == '/')
							{
								addDoor = false;
								tc = '\\';
							}
							switch (tc)
							{
								case '\'':
									continue;
								case ',':
									def = "pathWay"; // FIXME: kind of ugly but does the job
									break;
								case '.':
									def = "woodFloor";
									break;
								case '+': //Exit -- can't be seen, coaxes walls into shape.
									def = "doorwayClosed";

									if (addDoor)
									{
										doorCount++;
										var door = new Door()
										{
											XPosition = sX + x,
											YPosition = sY + y,
											ForegroundColor = woodFloor,
											BackgroundColor = woodFloor.Darken(),
											ID = building.BaseID + "_Door" + doorCount,
											ParentBoard = Board,
											Closed = true,
											Glyph = '+'
										};
										Board.Entities.Add(door);
									}
									break;
								case '=':
									def = "outerWoodWall";
									break;
								case '-':
									def = "innerWoodWall";
									break;
								case '#':
									def = allowCaveFloor ? "stoneFloor" : "woodFloor";
									break;
								default:
									if (template.Markings.ContainsKey(tc))
									{
										#region Custom markings
										var m = template.Markings[tc];
										if (m.Text == "block")
											throw new Exception("Got a BLOCK-type marking in a building template.");

										if (m.Text != "tile" && m.Text != "floor" && m.Text != "water")
										{
											//Keep a floor here. The entity fills in the blank.
											def = "woodFloor";
											var tileDef = TileDefinition.Find(def, false);
											map[sX + x, sY + y].Index = tileDef.Index;
											//var owner = m.Owner == 0 ? null : building.Inhabitants[m.Owner - 1];
											var owner = (Character)null;
											if (m.HasToken("owner"))
											{
												var ot = m.GetToken("owner");
												if (ot.IntValue > building.Inhabitants.Count)
													ot.IntValue = building.Inhabitants.Count;
												owner = building.Inhabitants[ot.IntValue - 1];
											}
											if (m.Text == "bed")
											{
												var newBed = new Clutter()
												{
													XPosition = sX + x,
													YPosition = sY + y,
													Name = "Bed",
													ID = "Bed_" + (owner == null ? Board.Entities.Count.ToString() : owner.Name.ToID()),
													Description = owner == null ? i18n.GetString("freebed") : i18n.Format("someonesbed", owner.Name.ToString(true)),
													ParentBoard = Board,
												};
												Clutter.ResetToKnown(newBed);
												Board.Entities.Add(newBed);
											}
											if (m.Text == "container")
											{
												//var type = c == '\x14B' ? "cabinet" : c == '\x14A' ? "chest" : "container";
												var type = "chest";
												if (m.HasToken("wardrobe"))
													type = "wardrobe";
												var contents = DungeonGenerator.GetRandomLoot("container", type, new Dictionary<string, string>()
												{
													{ "gender", owner.PreferredGender.ToString().ToLowerInvariant() },
													{ "biome", BiomeData.Biomes[DungeonGenerator.DungeonGeneratorBiome].Name.ToLowerInvariant() },
												});
												if (owner != null)
												{
													foreach (var content in contents)
														content.AddToken("owner", 0, owner.ID);
												}
												var newContainer = new Container(type, contents) //owner == null ? type.Titlecase() : owner.Name.ToString(true) + "'s " + type, contents)
												{
													XPosition = sX + x,
													YPosition = sY + y,
													ID = "Container_" + type + "_" + (owner == null ? Board.Entities.Count.ToString() : owner.Name.ToID()),
													ParentBoard = Board,
												};
												Clutter.ResetToKnown(newContainer);
												Board.Entities.Add(newContainer);
											}
											else if (m.Text == "clutter")
											{
												if (m.HasToken("id"))
												{
													var newClutter = new Clutter()
													{
														XPosition = sX + x,
														YPosition = sY + y,
														ParentBoard = Board,
														ID = m.GetToken("id").Text,
														Name = string.Empty,
													};
													Clutter.ResetToKnown(newClutter);
													Board.Entities.Add(newClutter);
												}
												else
												{
													var newClutter = new Clutter()
													{
														Glyph = (char)m.GetToken("char").Value, //m.Params.Last()[0],
														XPosition = sX + x,
														YPosition = sY + y,
														ForegroundColor = Color.Black,
														BackgroundColor = tileDef.Background,
														ParentBoard = Board,
														Name = m.GetToken("name").Text, //Name,
														Description = m.HasToken("description") ? m.GetToken("description").Text : string.Empty,
														Blocking = m.HasToken("blocking"),
													};
													Board.Entities.Add(newClutter);
												}
											}
										}
										else if (m.Text == "water")
										{
											fluid = Fluids.Water;
										}
										else
										{
											def = TileDefinition.Find((int)m.GetToken("index").Value).Name;
										}
										#endregion
									}
									break;
							}
							map[sX + x, sY + y].Index = TileDefinition.Find(def).Index;
							map[sX + x, sY + y].Fluid = fluid;
						}
					}

					for (var i = 0; i < building.Inhabitants.Count; i++)
					{
						var inhabitant = building.Inhabitants[i];
						//Find each inhabitant's bed so we can give them a starting place.
						//Alternatively, place them anywhere there's a ' ' within their sector.
						var bc = new BoardChar(inhabitant);
						//var bedID = building.BaseID + "_Bed_" + inhabitant.Name.FirstName;
						//var bed = Board.Entities.OfType<Clutter>().FirstOrDefault(b => b.ID == bedID);
						//if (bed != null)
						//{
						//	bc.XPosition = bed.XPosition;
						//	bc.YPosition = bed.YPosition;
						//}
						//else
						{
							//var okay = false;
							var x = 0;
							var y = 0;
							var lives = 100;
							while (lives > 0)
							{
								lives--;
								x = (col * plotWidth) + Random.Next(plotWidth);
								y = (row * plotHeight) + Random.Next(plotHeight);
								if (!map[x, y].Definition.Wall &&
									(!template.AllowOutside && map[x, y].Definition.Ceiling) &&
									Board.Entities.FirstOrDefault(e => e.XPosition == x && e.YPosition == y) == null)
									break;
							}
							bc.XPosition = x;
							bc.YPosition = y;
						}
						bc.Character.AddToken("sectorlock");
						bc.ParentBoard = Board;
						bc.AdjustView();
						bc.Sector = string.Format("s{0}x{1}", row, col);
						Board.Entities.Add(bc);
					}
				}
			}

			Board.ResolveVariableWalls();

			if (safeZones.Count > 0 && includeWater)
				Board.AddWater(safeZones);
		}

		public virtual void ToSectorMap(Dictionary<string, Rectangle> sectors)
		{
			//throw new NotImplementedException("BaseDungeon.ToSectorMap() must be overridden in a subclass.");
		}
	}

	public class Room
	{
		public RoomMaterials Material { get; set; }
		public SysRectangle Bounds { get; set; }

		public Room(SysRectangle bounds, RoomMaterials material)
		{
			this.Bounds = bounds;
			this.Material = material;
		}
	}
	public enum RoomMaterials
	{
		Stone, Wood
	}

	//Ye Olde Generic Dungeon -- mostly by Xolroc
	class StoneDungeonGenerator : BaseDungeonGenerator
	{
		private const int MAX_ROOMS = 30;
		private const int ROOM_MAX_X = 10;
		private const int ROOM_MIN_X = 5;
		private const int ROOM_MAX_Y = 6;
		private const int ROOM_MIN_Y = 3;

		private static Point Center(Room rect)
		{
			return new Point((int)(rect.Bounds.X + 0.5 * rect.Bounds.Width), (int)(rect.Bounds.Y + 0.5 * rect.Bounds.Height));
		}

		private List<Room> rooms;
		private List<Room> corridors;

		public void Create(BiomeData biome)
		{
			rooms = new List<Room>(MAX_ROOMS);
			corridors = new List<Room>(2 * MAX_ROOMS);

			for (var i = 0; i < MAX_ROOMS; i++)
			{
				var w = Random.Next(ROOM_MIN_X, ROOM_MAX_X - 2);
				var h = Random.Next(ROOM_MIN_Y, ROOM_MAX_Y - 2);
				var l = Random.Next(1, Board.Width - w);
				var t = Random.Next(1, Board.Height - h);
				if (l + w >= Board.Width - 1) w = Board.Width - l - 2;
				if (t + h >= Board.Height - 1) h = Board.Height - t - 2;
				var room = new Room(new SysRectangle(l, t, w, h), (RoomMaterials)Random.Next(Enum.GetValues(typeof(RoomMaterials)).Length));
				var pass = false;
				if (i > 0)
				{
					for (var j = 0; j < rooms.Count; j++)
						if (room.Bounds.IntersectsWith(rooms[j].Bounds))
							pass = true;
				}
				if (pass)
					continue;
				rooms.Add(room);
				if (i > 0)
				{
					var firstToLast = Center(rooms[rooms.Count - 1]);
					var secondToLast = Center(rooms[rooms.Count - 2]);

					if (Random.Flip())
					{
						corridors.Add(new Room(new SysRectangle(firstToLast.X, firstToLast.Y, secondToLast.X - firstToLast.X, 1), RoomMaterials.Stone));
						corridors.Add(new Room(new SysRectangle(secondToLast.X, firstToLast.Y, 1, secondToLast.Y - firstToLast.Y), RoomMaterials.Stone));
					}
					else
					{
						corridors.Add(new Room(new SysRectangle(firstToLast.X, firstToLast.Y, 1, secondToLast.Y - firstToLast.Y), RoomMaterials.Stone));
						corridors.Add(new Room(new SysRectangle(firstToLast.X, secondToLast.Y, secondToLast.X - firstToLast.X, 1), RoomMaterials.Stone));
					}
				}
			}
		}

		private void MaybeSet(ref Tile[,] map, int x, int y, int index)
		{
			if (map[x, y].Definition.Wall && !map[x, y].Definition.CanBurn)
				map[x, y].Index = index;
		}
		private void MaybeSet(ref Tile[,] map, int x, int y, string tileName)
		{
			if (map[x, y].Definition.Wall && !map[x, y].Definition.CanBurn)
				map[x, y].Index = TileDefinition.Find(tileName).Index;
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			//TODO: make these biome-dependent (use this.biome)

			//Base fill
			var baseFill = TileDefinition.Find("stoneWall").Index;
			for (var row = 0; row < Board.Height; row++)
				for (var col = 0; col < Board.Width; col++)
					map[col, row].Index = baseFill;

			//TODO: add clutter.
			/* My idea: have a list of points. For each room, randomly scatter a few points around, and make sure there's a few around the edges.
			 * When carving corridors, remove any points in your way. Then, when done with corridors, add the actual clutter and special tiles.
			 */

			foreach (var room in rooms)
			{
				var bounds = room.Bounds;
				var tileType = "woodFloor";
				if (room.Material == RoomMaterials.Stone)
					tileType = "stoneFloor";
				for (var row = bounds.Top; row <= bounds.Bottom; row++)
					for (var col = bounds.Left; col <= bounds.Right; col++)
						map[col, row].Index = TileDefinition.Find(tileType).Index;
			}

			foreach (var room in rooms)
			{
				if (room.Material == RoomMaterials.Stone)
					continue;

				var bounds = room.Bounds;
				var inflated = new SysRectangle(bounds.Left - 1, bounds.Top - 1, bounds.Width + 2, bounds.Height + 2);
				for (var row = inflated.Top; row <= inflated.Bottom; row++)
				{
					MaybeSet(ref map, inflated.Left, row, "innerWoodWall");
					MaybeSet(ref map, inflated.Right, row, "innerWoodWall");
				}
				for (var col = inflated.Left; col <= inflated.Right; col++)
				{
					MaybeSet(ref map, col, inflated.Top, "innerWoodWall");
					MaybeSet(ref map, col, inflated.Bottom, "innerWoodWall");
				}
			}

			foreach (var corridor in corridors)
			{
				var bounds = corridor.Bounds;
				var inRoom = false;
				Tile there = null;
				foreach (var point in Toolkit.Line(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, true))
				{
					var here = map[point.X, point.Y];
					if (there != null && there.Definition.Wall && there.Definition.CanBurn && here.Definition.Wall && !here.Definition.CanBurn)
					{
						there.Index = TileDefinition.Find("woodFloor").Index;
						inRoom = false;
					}
					if (here.Definition.Wall)
					{
						if (!here.Definition.CanBurn)
						{
							map[point.X, point.Y].Index = TileDefinition.Find("stoneFloor").Index;
							inRoom = false;
						}
						else if (!inRoom)
						{
							map[point.X, point.Y].Index = TileDefinition.Find("woodFloor").Index;
							inRoom = true;
						}
					}
					there = here;
				}
			}

			Board.ResolveVariableWalls();

			#region Fade out the walls
			var dijkstra = new int[Board.Width, Board.Height];
			for (var col = 0; col < Board.Width; col++)
			{
				for (var row = 0; row < Board.Height; row++)
				{
					if (!map[col, row].SolidToWalker)
						continue;
					dijkstra[col, row] = 9000;
				}
			}

			Dijkstra.JustDoIt(ref dijkstra, Board.Height, Board.Width);

			for (var row = 0; row < Board.Height; row++)
			{
				for (var col = 0; col < Board.Width; col++)
				{
					//if (map[col, row].Fluid != Fluids.Dry)
					//	continue;
					if (map[col, row].Definition.Wall && !map[col, row].Definition.CanBurn)
					{
						//if (dijkstra[col, row] > 1)
						map[col, row].InherentLight = dijkstra[col, row];
					}
				}
			}
			#endregion
		}
	}

	//It's not even a dungeon -- it's a town.
	class TownGenerator : BaseDungeonGenerator
	{
		public void Create(BiomeData biome)
		{
			Culture = Culture.Cultures[biome.Cultures.PickOne()];
			base.Create(biome, "town");
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			includeWater = true;
			includeClutter = true;
			base.ToTilemap(ref map);
		}

		public override void ToSectorMap(Dictionary<string, Rectangle> sectors)
		{
			for (var row = 0; row < 3; row++)
			{
				for (var col = 0; col < 6; col++)
				{
					var key = string.Format("s{0}x{1}", row, col);
					sectors.Add(key, new Rectangle() { Left = col * 13, Right = (col * 13) + 13, Top = row * 16, Bottom = (row * 16) + 16 });
				}
			}
		}
	}

	//And now for something completely different.
	class CaveGenerator : BaseDungeonGenerator
	{
		public void Create(BiomeData biome)
		{
			this.biome = biome;

			//Do NOT use BaseDungeon.Create() -- we want a completely different method here.
			map = new int[Board.Width, Board.Height];

			//Draw a nice border for the passes to work within
			for (var i = 0; i < Board.Width; i++)
			{
				map[i, 0] = 1;
				map[i, Board.Height - 1] = 1;
			}
			for (var i = 0; i < Board.Height; i++)
			{
				map[0, i] = 1;
				map[1, i] = 1;
				map[Board.Width - 2, i] = 1;
				map[Board.Width - 1, i] = 1;
			}

			//Scatter some seed tiles
			for (var i = 0; i < Board.Height; i++)
				for (var j = 0; j < Board.Width; j++)
					if (Random.NextDouble() < 0.25)
						map[j, i] = 1;

			//Melt the cave layout with a cellular automata system.
			for (var pass = 0; pass < 5; pass++)
			{
				for (var i = 1; i < Board.Height - 1; i++)
				{
					for (var j = 1; j < Board.Width - 1; j++)
					{
						//Count the neighboring live cells
						var neighbors = 0;
						for (int ni = -1; ni <= 1; ni++)
						{
							for (int nj = -1; nj <= 1; nj++)
							{
								if (ni == 0 && nj == 0)
									continue;
								neighbors += map[j + nj, i + ni];
							}
						}

						//Apply the rule
						map[j, i] = ((map[j, i] == 1 && neighbors >= 2) ||
									 (map[j, i] == 0 && neighbors >= 5)) ? 1 : 0;
					}
				}
			}

			//TODO: define discrete rooms somehow?
			//TODO: get rid of small enclosed areas (total surface < 4 tiles or so)
			//Possibility to kill two birds with one stone: floodfills!
			//1. Prepare a checking map, initially a copy of the actual map.
			//2. Find the first open space on the checking map.
			//3. From there, run a floodfill. Mark every open space found on the checking map and count how many open spaces you find.
			//4. If you found only one, it's a goner. Set that spot to solid on the actual map. This is distinct from step 5 for efficiency.
			//5. If you counted only a few, it's a small enclosed space that ought to be filled in. Run the floodfill again, from the same spot, but on the actual map, setting all open spaces found to solid.
			//6. Repeat until the checking map is fully colored.
			//How to do step 6 in a fairly efficient way:
			//When making the initial copy, count the amount of solid spaces as you go. Remember that value. While floodfilling, increase the count. When the count equals 80*50, you have them all.
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			//TODO: make these biome-dependent (use this.biome)
			//var stoneFloor = Color.FromArgb(65, 66, 87);
			//var wallStart = Color.FromArgb(119, 120, 141);
			//var wallEnd = Color.FromArgb(144, 144, 158);
			//var floorCrud = "       \x146".ToCharArray();

			var tiles = new[] { "stoneFloor", "stoneWall" };

			for (var row = 0; row < Board.Height; row++)
				for (var col = 0; col < Board.Width; col++)
					map[col, row].Index = TileDefinition.Find(tiles[this.map[col, row]]).Index;

			#region Fade out the walls
			var dijkstra = new int[Board.Width, Board.Height];
			for (var col = 0; col < Board.Width; col++)
			{
				for (var row = 0; row < Board.Height; row++)
				{
					if (!map[col, row].SolidToWalker)
						continue;
					dijkstra[col, row] = 9000;
				}
			}

			Dijkstra.JustDoIt(ref dijkstra, Board.Height, Board.Width);

			for (var row = 0; row < Board.Height; row++)
			{
				for (var col = 0; col < Board.Width; col++)
				{
					//if (map[col, row].Fluid != Fluids.Dry)
					//	continue;
					//if (map[col, row].Definition.Wall && !map[col, row].Definition.CanBurn)
					{
						//if (dijkstra[col, row] > 1)
						map[col, row].InherentLight = dijkstra[col, row];
					}
				}
			}
			#endregion
		}

	}
}
