using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using SysRectangle = System.Drawing.Rectangle;

namespace Noxico
{
	internal class Marking
	{
		public string Type;
		public string[] Params;
		public int Owner;
		public string Name;
		public string Description;
	}

	internal class Template
	{
		public string Name;
		public int Inhabitants;
		public int Width, Height;
		public int PlotWidth, PlotHeight;
		public string[] MapScans;
		public Dictionary<char, Marking> Markings;
		public Template(XmlElement element)
		{
			Name = element.GetAttribute("name");
			Inhabitants = element.HasAttribute("inhabitants") ? int.Parse(element.GetAttribute("inhabitants")) : 0;
			var map = element.SelectSingleNode("map") as XmlElement;
			MapScans = map.InnerText.Trim().Split('\n').Select(x => x.Trim()).ToArray();
			Width = MapScans[0].Length;
			Height = MapScans.Length;
			PlotWidth = (int)Math.Ceiling(Width / 13.0);
			PlotHeight = (int)Math.Ceiling(Height / 16.0);
			Markings = new Dictionary<char, Marking>();
			foreach (var marking in element.SelectNodes("markings/marking").OfType<XmlElement>())
			{
				var c = marking.GetAttribute("char")[0];
				var t = marking.GetAttribute("type");
				var p = new string[0];
				var o = marking.HasAttribute("owner") ? int.Parse(marking.GetAttribute("owner")) : 0;
				if (t.Contains(','))
				{
					p = t.Substring(t.IndexOf(',') + 1).Split(',');
					t = t.Remove(t.IndexOf(','));
				}
				var n = marking.GetAttribute("name");
				var d = marking.InnerText.Trim();
				Markings.Add(c, new Marking() { Type = t, Params = p, Owner = o, Name = n, Description = d });
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
			var familyName = "";
			//var dontShareSurname = true;
			var areMarried = Random.NextDouble() > 0.7;
			var firstPlan = "";
			count = 2;
			for (var i = 0; i < count; i++)
			{
				Character c;
				var plan = culture.Bodyplans[Random.Next(culture.Bodyplans.Length)];
				if (i > 0 && Random.NextDouble() > 0.7)
					plan = firstPlan;
				c = Character.Generate(plan, count == 1 ? Gender.Random : (i == 0 ? Gender.Male : Gender.Female));
				if (i == 0)
				{
					familyName = c.Name.Surname;
					firstPlan = plan;
				}
				if (i == 1)
				{
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
				Scheduler.AddSchedule("villager", c);
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
		protected static XmlDocument xDoc;
		protected bool allowCaveFloor, includeWater, includeClutter;

		public void Create(BiomeData biome, string templateSet)
		{
			Culture = Culture.Cultures[Toolkit.PickOne(biome.Cultures)];
			this.biome = biome;
			map = new int[80, 50];
			plots = new Building[6, 3];

			if (templates == null)
			{
				templates = new Dictionary<string, List<Template>>();
				xDoc = Mix.GetXmlDocument("buildings.xml");
				foreach (var s in xDoc.SelectNodes("//set").OfType<XmlElement>())
				{
					var thisSet = s.GetAttribute("id");
					templates.Add(thisSet, new List<Template>());
					foreach (var t in s.ChildNodes.OfType<XmlElement>().Where(x => x.Name == "template"))
						templates[thisSet].Add(new Template(t));
				}
			}

			var spill = new Building("<spillover>", null, 0, 0, Culture.DefaultCulture);
			//var justPlaced = false;
			for (var row = 0; row < 3; row++)
			{
				for (var col = 0; col < 6; col++)
				{
					if (plots[col, row].BaseID == "<spillover>")
						continue;
					//Small chance of not having anything here, for variation.
					if (Random.NextDouble() < 0.2)
					{
						//justPlaced = false;
						continue;
					}
					var newTemplate = templates[templateSet][Random.Next(templates[templateSet].Count)];
					//TODO: check if chosen template spills over and if so, if there's room. For now, assume all templates are <= 8
					//Each plot is 8x8. Given that and the template size, we can wiggle them around a bit from 0 to (8 - tSize).
					var sX = newTemplate.Width < 13 ? Random.Next(1, 13 - newTemplate.Width) : 0;
					var sY = newTemplate.Height < 16 ? Random.Next(1, 16 - newTemplate.Height) : 0;
					
					//NEW: check for water in this plot.
					var water = 0;
					for (var y = 0; y < newTemplate.Height; y++)
					{
						for (var x = 0; x < newTemplate.Width; x++)
						{
							if (Board.Tilemap[(col * 13) + x, (row * 16) + y].Fluid != Fluids.Dry)
								water++;
						}
					}
					if (water > 0)
						continue;

					//Later on, we might be able to wiggle them out of their assigned plot a bit.
					//TODO: determine baseID from the first inhabitant's name.
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

			var cornerJunctions = new List<Point>();
			var doorCount = 0;

			var safeZones = new List<Rectangle>();

			for (var row = 0; row < 3; row++)
			{
				for (var col = 0; col < 6; col++)
				{
					if (plots[col, row].BaseID == null)
					{
						//Can clutter this up!
						if (includeClutter && Random.Flip())
							Board.AddClutter(col * 13, row * 16, (col * 13) + 13, (row * 16) + 16 + row);
						else
							safeZones.Add(new Rectangle() { Left = col * 13, Top = row * 16, Right = (col * 13) + 13, Bottom = (row * 16) + 16 + row });
						continue;
					}

					if (plots[col, row].BaseID == "<spillover>")
						continue;
					var building = plots[col, row];
					var template = building.Template;
					var sX = (col * 13) + building.XShift;
					var sY = (row * 16) + building.YShift;
					/*
					for (var y = 0; y < template.Height; y++)
					{
						for (var x = 0; x < template.Width; x++)
						{
							var tc = template.MapScans[y][x];
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
								case '.':
									bgd = woodFloor;
									cei = true;
									bur = true;
									chr = ' ';
									break;
								case '\\': //Exit -- can't be seen, coaxes walls into shape.
									bgd = woodFloor;
									chr = '\xFF';
									cei = true;
									bur = true;

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
											AsciiChar = '+',
										};
										Board.Entities.Add(door);
									}
									break;
								case '+':
									fgd = wall;
									bgd = woodFloor;
									wal = true;
									cei = true;
									bur = true;
									cornerJunctions.Add(new Point(sX + x, sY + y));
									break;
								case '-':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x105';
									wal = true;
									cei = true;
									bur = true;
									break;
								case '|':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x104';
									wal = true;
									cei = true;
									bur = true;
									break;
								case '~':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x110';
									wal = true;
									cei = true;
									bur = true;
									break;
								case ';':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x10F';
									wal = true;
									cei = true;
									bur = true;
									break;
								case '#':
									bgd = allowCaveFloor ? caveFloor : woodFloor;
									bur = !allowCaveFloor;
									chr = ' ';
									break;
								default:
									if (template.Markings.ContainsKey(tc))
									{
										#region Custom markings
										var m = template.Markings[tc];
										if (m.Type != "block" && m.Type != "floor" && m.Type != "water")
										{
											//Keep a floor here. The entity fills in the blank.
											bgd = woodFloor;
											chr = ' ';
											var owner = m.Owner == 0 ? null : building.Inhabitants[m.Owner - 1];
											if (m.Type == "bed")
											{
												var newBed = new Clutter()
												{
													AsciiChar = 0x147,
													XPosition = sX + x,
													YPosition = sY + y,
													Name = "Bed",
													ForegroundColor = Color.Black,
													BackgroundColor = bgd,
													ID = "Bed_" + (owner == null ? Board.Entities.Count.ToString() : owner.Name.ToID()),
													Description = owner == null ? "This is a free bed. Position yourself over it and press Enter to use it." : string.Format("This is {0}'s bed. If you want to use it, you should ask {1} for permission.", owner.Name.ToString(true), owner.HimHerIt()),
													ParentBoard = Board,
												};
												Board.Entities.Add(newBed);
											}
											if (m.Type == "container")
											{
												var c = m.Params.Last()[0];
												var type = c == '\x14B' ? "cabinet" : c == '\x14A' ? "chest" : "container";
												if (m.Params[0] == "clothes")
												{
													//if (type == "cabinet")
													type = "wardrobe";
												}
												var contents = DungeonGenerator.GetRandomLoot("container", type, new Dictionary<string, string>() { { "gender", owner.PreferredGender.ToString().ToLowerInvariant() } }); //InventoryItem.RollContainer(owner, type);  //new List<Token>();
												if (owner != null)
												{
													foreach (var content in contents)
														content.AddToken("owner", 0, owner.ID);
												}	
												var newContainer = new Container(owner == null ? type.Titlecase() : owner.Name.ToString(true) + "'s " + type, contents)
												{
													AsciiChar = m.Params.Last()[0],
													XPosition = sX + x,
													YPosition = sY + y,
													ForegroundColor = Color.Black,
													BackgroundColor = bgd,
													ID = "Container_" + type + "_" + (owner == null ? Board.Entities.Count.ToString() : owner.Name.ToID()),
													ParentBoard = Board,
												};
												Board.Entities.Add(newContainer);
											}
											else if (m.Type == "clutter")
											{
												var newClutter = new Clutter()
												{
													AsciiChar = m.Params.Last()[0],
													XPosition = sX + x,
													YPosition = sY + y,
													ForegroundColor = Color.Black,
													BackgroundColor = bgd,
													ParentBoard = Board,
													Name = m.Name,
													Description = m.Description,
													Blocking = m.Params.Contains("blocking"),
												};
												Board.Entities.Add(newClutter);
											}
										}
										else if (m.Type == "water")
										{
											chr = water.GroundGlyphs[Random.Next(water.GroundGlyphs.Length)];
											fgd = water.Color.Darken();
											bgd = water.Color;
											wat = water.IsWater;
										}
										else
										{
											fgd = m.Params[0] == "floor" ? woodFloor : m.Params[0] == "wall" ? wall : Color.FromName(m.Params[0]);
											bgd = m.Params[1] == "floor" ? woodFloor : m.Params[1] == "wall" ? wall : Color.FromName(m.Params[1]);
											chr = m.Params.Last()[0];
											wal = m.Type != "floor";
										}
										#endregion
									}
									break;
							}
							map[sX + x, sY + y] = new Tile()
							{
								Character = chr,
								Foreground = fgd,
								Background = bgd,
								Wall = wal,
								Water = wat,
								Ceiling = cei,
								Cliff = cli,
								Fence = fen,
								Grate = gra,
								CanBurn = bur,
							};
						}
					}
					*/

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
							var okay = false;
							var x = 0;
							var y = 0;
							var lives = 100;
							while (!okay)
							{
								lives--;
								x = (col * 13) + Random.Next(13);
								y = (row * 16) + Random.Next(16);
								if (lives == 0 || !map[x, y].Definition.Wall && map[x, y].Definition.Ceiling && map[x, y].Definition.Glyph == ' ' && Board.Entities.FirstOrDefault(e => e.XPosition == x && e.YPosition == y) == null)
									okay = true;
							}
							bc.XPosition = x;
							bc.YPosition = y;
							//bc.XPosition = (col * 13) + i;
							//bc.YPosition = row * 16;
						}
						bc.Character.AddToken("sectorlock");
						bc.ParentBoard = Board;
						bc.AdjustView();
						bc.Sector = string.Format("s{0}x{1}", row, col);
						Board.Entities.Add(bc);
					}
				}
			}

			/*
			//Fix up corners and junctions
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
			foreach (var cj in cornerJunctions)
			{
				var up = cj.Y > 0 ? map[cj.X, cj.Y - 1].Character : 'x';
				var down = cj.Y < 49 ? map[cj.X, cj.Y + 1].Character : 'x';
				var left = cj.X > 0 ? map[cj.X - 1, cj.Y].Character : 'x';
				var right = cj.X < 79 ? map[cj.X + 1, cj.Y].Character : 'x';
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

				map[cj.X, cj.Y].Character = (char)cjResults[mask];
			}
			*/

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

		private const int DIM_X = 79;
		private const int DIM_Y = 49;
		
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
				var room = new Room(new SysRectangle(Random.Next(1, DIM_X - w), Random.Next(1, DIM_Y - h), w, h), (RoomMaterials)Random.Next(Enum.GetValues(typeof(RoomMaterials)).Length));
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
			//TODO: make these biome-dependant (use this.biome)
			//var stoneStart = Color.FromArgb(119, 120, 141);
			//var stoneEnd = Color.FromArgb(144, 144, 158);
			//var stoneFloor = Color.FromArgb(65, 66, 87);
			//var woodFloor = Color.FromArgb(86, 63, 44);
			//var woodWall = Color.FromArgb(20, 15, 12);

			//var stoneTile = new Tile() { Character = ' ', Wall = false, Background = stoneFloor, Foreground = stoneFloor.Darken(), CanBurn = false };
			//var woodTile = new Tile() { Character = ' ', Wall = false, Background = woodFloor, Foreground = woodFloor, CanBurn = true };

			//Base fill
			var baseFill = TileDefinition.Find("stoneWall").Index;
			for (var row = 0; row < 50; row++)
				for (var col = 0; col < 80; col++)
					map[col, row].Index = baseFill;

			//TODO: add clutter.
			/* My idea: have a list of points. For each room, randomly scatter a few points around, and make sure there's a few around the edges.
			 * When carving corridors, remove any points in your way. Then, when done with corridors, add the actual clutter and special tiles.
			 */

			foreach (var room in rooms)
			{
				var bounds = room.Bounds;
				if (room.Material == RoomMaterials.Wood)
				{
					for (var row = bounds.Top; row <= bounds.Bottom; row++)
						for (var col = bounds.Left; col <= bounds.Right; col++)
							map[col, row].Definition = TileDefinition.Find("woodFloor");
				}
				else if (room.Material == RoomMaterials.Stone)
				{
					for (var row = bounds.Top; row <= bounds.Bottom; row++)
					{
						for (var col = bounds.Left; col <= bounds.Right; col++)
						{
							map[col, row].Definition = TileDefinition.Find("stoneFloor");
						}
					}		
				}
			}

			foreach (var room in rooms)
			{
				if (room.Material == RoomMaterials.Stone)
					continue;

				var bounds = room.Bounds;
				var inflated = new SysRectangle(bounds.Left - 1, bounds.Top - 1, bounds.Width + 2, bounds.Height + 2);
				/*
				//Leave this to the brute force wall fixer we haven't written yet? YES.
				for (var row = inflated.Top + 1; row < inflated.Bottom; row++)
				{
					MaybeSet(ref map, inflated.Left, row, "innerWoodWallVertical");
					MaybeSet(ref map, inflated.Right, row, "innerWoodWallVertical");
				}
				for (var col = inflated.Left + 1; col < inflated.Right; col++)
				{
					MaybeSet(ref map, col, inflated.Top, "innerWoodWallHorizontal");
					MaybeSet(ref map, col, inflated.Bottom, "innerWoodWallHorizontal");
				}
				MaybeSet(ref map, inflated.Left, inflated.Top, "innerWoodWallTopLeft");
				MaybeSet(ref map, inflated.Right, inflated.Top, "innerWoodWallTopRight");
				MaybeSet(ref map, inflated.Left, inflated.Bottom, "innerWoodWallBottomLeft");
				MaybeSet(ref map, inflated.Right, inflated.Bottom, "innerWoodWallBottomRight");
				*/
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
						there.Definition = TileDefinition.Find("woodFloor");
						inRoom = false;
					}
					if (here.Definition.Wall)
					{
						if (!here.Definition.CanBurn)
						{
							map[point.X, point.Y].Definition = TileDefinition.Find("stoneFloor");
							inRoom = false;
						}
						else if (!inRoom)
						{
							map[point.X, point.Y].Definition = TileDefinition.Find("woodFloor");
							inRoom = true;
						}	
					}
					there = here;
				}
			}

			#region Fade out the walls
			/*
			var dijkstra = new int[80, 50];
			for (var col = 0; col < 80; col++)
			{
				for (var row = 0; row < 50; row++)
				{
					if (!map[col, row].Wall)
						continue;
					dijkstra[col, row] = 9000; //(map[col, row].Wall && !map[col, row].CanBurn) ? 9000 : 0;
				}
			}

			Dijkstra.JustDoIt(ref dijkstra);

			for (var row = 0; row < 50; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					if (map[col, row].Fluid != Fluids.Dry)
						continue;
					if (map[col, row].Definition.Wall && !map[col, row].Definition.CanBurn)
					{
						if (dijkstra[col, row] > 1)
							map[col, row].Background = map[col, row].Background.LerpDarken(dijkstra[col, row] / 10.0);
						else
							map[col, row].SpecialDescription = 1;
					}
					if (map[col, row].Wall && map[col, row].CanBurn)
					{
						map[col, row].SpecialDescription = 2;
					}
				}
			}
			*/
			#endregion
		}
	}

	//It's not even a dungeon -- it's a town.
	class TownGenerator : BaseDungeonGenerator
	{
		public void Create(BiomeData biome)
		{
			Culture = Culture.Cultures[Toolkit.PickOne(biome.Cultures)];
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
			map = new int[80, 50];

			//Draw a nice border for the passes to work within
			for (var i = 0; i < 80; i++)
			{
				map[i, 0] = 1;
				map[i, 49] = 1;
			}
			for (var i = 0; i < 50; i++)
			{
				map[0, i] = 1;
				map[1, i] = 1;
				map[78, i] = 1;
				map[79, i] = 1;
			}

			//Scatter some seed tiles
			for (var i = 0; i < 50; i++)
				for (var j = 0; j < 80; j++)
					if (Random.NextDouble() < 0.25)
						map[j, i] = 1;

			//Melt the cave layout with a cellular automata system.
			for (var pass = 0; pass < 5; pass++)
			{
				for (var i = 1; i < 49; i++)
				{
					for (var j = 1; j < 79; j++)
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
			//TODO: make these biome-dependant (use this.biome)
			//var stoneFloor = Color.FromArgb(65, 66, 87);
			//var wallStart = Color.FromArgb(119, 120, 141);
			//var wallEnd = Color.FromArgb(144, 144, 158);
			//var floorCrud = "       \x146".ToCharArray();

			var tiles = new[] { TileDefinition.Find("stoneFloor").Index, TileDefinition.Find("dungeonWall").Index };

			for (var row = 0; row < 50; row++)
				for (var col = 0; col < 80; col++)
					map[col, row].Index = tiles[this.map[col, row]];

			/*
			var dijkstra = new int[80, 50];
			for (var col = 0; col < 80; col++)
				for (var row = 0; row < 50; row++)
					dijkstra[col, row] = this.map[col, row] == 0 ? 0 : 9000;
			Dijkstra.JustDoIt(ref dijkstra, diagonals: false);
			for (var row = 0; row < 50; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					if (this.map[col, row] == 1)
					{
						if (dijkstra[col, row] > 1)
							map[col, row].Background = map[col, row].Background.LerpDarken(dijkstra[col, row] / 10.0);
						else
							map[col, row].SpecialDescription = 1;
					}
				}
			}
			*/
		}

	}
}
