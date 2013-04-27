using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

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
			PlotWidth = (int)Math.Ceiling(Width / 10.0);
			PlotHeight = (int)Math.Ceiling(Height / 12.0);
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
		protected bool allowCaveFloor;

		public void Create(BiomeData biome, string templateSet)
		{
			this.biome = biome;
			map = new int[80, 25];
			plots = new Building[8, 2];

			if (templates == null)
			{
				templates = new Dictionary<string, List<Template>>();
				xDoc = Mix.GetXMLDocument("buildings.xml");
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
			for (var row = 0; row < 2; row++)
			{
				for (var col = 0; col < 8; col++)
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
					var sX = newTemplate.Width < 10 ? Random.Next(1, 10 - newTemplate.Width) : 0;
					var sY = newTemplate.Height < 12 ? Random.Next(1, 12 - newTemplate.Height) : 0;
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
			var water = BiomeData.Biomes[BiomeData.ByName(biome.RealmID == "Nox" ? "Water" : "KoolAid")];

			var cornerJunctions = new List<Point>();
			var doorCount = 0;

			var safeZones = new List<Rectangle>();

			for (var row = 0; row < 2; row++)
			{
				for (var col = 0; col < 8; col++)
				{
					if (plots[col, row].BaseID == null)
					{
						//Can clutter this up!
						Board.AddClutter(col * 10, row * 12, (col * 10) + 10, (row * 12) + 12 + row);
						safeZones.Add(new Rectangle() { Left = col * 10, Top = row * 12, Right = (col * 10) + 10, Bottom = (row * 12) + 12 + row });
						continue;
					}

					if (plots[col, row].BaseID == "<spillover>")
						continue;
					var building = plots[col, row];
					var template = building.Template;
					var sX = (col * 10) + building.XShift;
					var sY = (row * 12) + building.YShift;
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
									chr = '\xA0';
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
									chr = '\x2550';
									wal = true;
									cei = true;
									bur = true;
									break;
								case '|':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x2551';
									wal = true;
									cei = true;
									bur = true;
									break;
								case '~':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x2500';
									wal = true;
									cei = true;
									bur = true;
									break;
								case ';':
									fgd = wall;
									bgd = woodFloor;
									chr = '\x2502';
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
													AsciiChar = '\x0398',
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
												var type = c == '\x006C' ? "cabinet" : c == '\x03C0' ? "chest" : "container";
												if (m.Params[0] == "clothes")
												{
													//if (type == "cabinet")
													type = "wardrobe";
												}
												var contents = WorldGen.GetRandomLoot("container", type, new Dictionary<string, string>() { { "gender", owner.Gender.ToString().ToLowerInvariant() } }); //InventoryItem.RollContainer(owner, type);  //new List<Token>();
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
											fgd = water.Color.Darken(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv));
											bgd = water.Color.Darken(water.DarkenPlus + (Random.NextDouble() / water.DarkenDiv));
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
							while (!okay)
							{
								x = (col * 10) + Random.Next(10);
								y = (row * 12) + Random.Next(12);
								if (!map[x, y].Wall && map[x,y].Ceiling && map[x, y].Character == ' ' && Board.Entities.FirstOrDefault(e => e.XPosition == x && e.YPosition == y) == null)
									okay = true;
							}
							bc.XPosition = x;
							bc.YPosition = y;
							//bc.XPosition = (col * 10) + i;
							//bc.YPosition = row * 12;
						}
						bc.Movement = Motor.WanderSector;
						bc.ParentBoard = Board;
						bc.AdjustView();
						bc.Sector = string.Format("s{0}x{1}", row, col);
						Board.Entities.Add(bc);
					}
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
				var up = cj.Y > 0 ? map[cj.X, cj.Y - 1].Character : 'x';
				var down = cj.Y < 24 ? map[cj.X, cj.Y + 1].Character : 'x';
				var left = cj.X > 0 ? map[cj.X - 1, cj.Y].Character : 'x';
				var right = cj.X < 79 ? map[cj.X + 1, cj.Y].Character : 'x';
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

				map[cj.X, cj.Y].Character = (char)cjResults[mask];
			}

			if (safeZones.Count > 0)
				Board.AddWater(safeZones);
		}

		public virtual void ToSectorMap(Dictionary<string, Rectangle> sectors)
		{
			//throw new NotImplementedException("BaseDungeon.ToSectorMap() must be overridden in a subclass.");
		}
	}

	//Ye Olde Generic Dungeon
	class StoneDungeonGenerator : BaseDungeonGenerator
	{
		public void Create(BiomeData biome)
		{
			allowCaveFloor = true;
			base.Create(biome, "dungeon");
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			//TODO: make these biome-dependant (use this.biome)
			//FxCop: removed bunch of vars, unused since template system.
			//var floorStart = Color.FromArgb(65, 66, 87);
			//var floorEnd = Color.FromArgb(88, 89, 122);
			var wallStart = Color.FromArgb(119, 120, 141);
			var wallEnd = Color.FromArgb(144, 144, 158);
			//var wall = Color.FromArgb(71, 50, 33);
			var path = Color.FromArgb(32, 32, 32);
			//var floorCrud = new[] { ',', '\'', '`', '.', };

			//Base fill
			for (var row = 0; row < 25; row++)
				for (var col = 0; col < 80; col++)
					map[col, row] = new Tile() { Character = ' ', Wall = true, Background = Toolkit.Lerp(wallStart, wallEnd, Random.NextDouble()) };

			base.ToTilemap(ref map);

			//Connect plots
			var colStart = 40;
			var colEnd = 40;
			for (var row = 0; row < 2; row++)
			{
				for (var col = 0; col < 8; col++)
				{
					if (plots[col, row].BaseID == null)
						continue; //I dunno, place a hub pathway or something?

					var building = plots[col, row];
					//var x = (col * 10) + building.XShift + 2 + Randomizer.Next(building.Template.Width - 4);
					//var y = (row * 12) + building.YShift + 2 + Randomizer.Next(building.Template.Height - 4);
					var x = (col * 10) + building.XShift + (building.Template.Width / 2);
					var y = (row * 12) + building.YShift + (building.Template.Height / 2);
					//map[x, y].Background = Color.Magenta;

					var direction = Random.NextDouble() > 0.3 ? (row == 0 ? Direction.South : Direction.North) : (Random.NextDouble() > 0.5 ? Direction.East : Direction.West);
					if (col == 0 && direction == Direction.West)
						direction = Random.NextDouble() > 0.3 ? (row == 0 ? Direction.South : Direction.North) : Direction.East;
					else if (col == 7 && direction == Direction.East)
						direction = Random.NextDouble() > 0.3 ? (row == 0 ? Direction.South : Direction.North) : Direction.West;
					if ((direction == Direction.East && plots[col + 1, row].Template == null) ||
						(direction == Direction.West && plots[col - 1, row].Template == null))
						direction = row == 0 ? Direction.South : Direction.North;

					Toolkit.PredictLocation(x, y, direction, ref x, ref y);
					while (!map[x, y].Wall)
						Toolkit.PredictLocation(x, y, direction, ref x, ref y);

					if (x < colStart)
						colStart = x;
					else if (x > colEnd)
						colEnd = x + 1;

					if (direction == Direction.North)
					{
						if (map[x, y].CanBurn) //means we started in a walled building
						{
							map[x, y] = new Tile() { Character = ' ', Background = map[x, y].Background };
							y--;
						}
						while (y > Random.Next(4, 8))
						{
							if (map[x, y].Character == ' ')
								map[x, y] = new Tile() { Character = '#', Background = Color.Black, Foreground = path };
							else
								map[x, y] = new Tile() { Character = ' ', Background = map[x, y].Background };
							y--;
							if (!map[x, y].Wall)
								break;
						}
					}
					else if (direction == Direction.South)
					{
						if (map[x, y].CanBurn)
						{
							map[x, y] = new Tile() { Character = '!', Background = map[x, y].Background };
							y++;
						}
						while (y < Random.Next(12, 20))
						{
							if (map[x, y].Character == ' ')
								map[x, y] = new Tile() { Character = '#', Background = Color.Black, Foreground = path };
							else
								map[x, y] = new Tile() { Character = ' ', Background = map[x, y].Background };
							y++;
							if (!map[x, y].Wall)
								break;
						}
					}
					else if (direction == Direction.West)
					{
						if (map[x, y].CanBurn) //means we started in a walled building
						{
							map[x, y] = new Tile() { Character = ' ', Background = map[x, y].Background };
							x--;
						}
						while (x > 1 && map[x, y].Wall)
						{
							if (map[x, y].Character == ' ')
								map[x, y] = new Tile() { Character = '#', Background = Color.Black, Foreground = path };
							else
								map[x, y] = new Tile() { Character = ' ', Background = map[x,y].Background };
							x--;
						}
					}
					else if (direction == Direction.East)
					{
						if (map[x, y].CanBurn)
						{
							map[x, y] = new Tile() { Character = '!', Background = map[x, y].Background };
							x++;
						}
						while (x < 79 && map[x, y].Wall)
						{
							if (map[x, y].Character == ' ')
								map[x, y] = new Tile() { Character = '#', Background = Color.Black, Foreground = path };
							else
								map[x, y] = new Tile() { Character = ' ', Background = map[x,y].Background };
							x++;
						}
					}
				}
			}
			var yShift = 0;
			for (var x = colStart; x < colEnd; x++)
			{
				map[x, 12 + yShift] = new Tile() { Character = '#', Background = Color.Black, Foreground = path };
				if (x % 7 == 6)
				{
					yShift = Random.Next(-1, 1);
					map[x, 12 + yShift] = new Tile() { Character = '#', Background = Color.Black, Foreground = path };
				}
			}

			//Prepare to fade out the walls
			var dijkstra = new int[80, 25];
			for (var col = 0; col < 80; col++)
			{
				for (var row = 0; row < 25; row++)
				{
					if (map[col, row].SolidToProjectile)
						continue;
					dijkstra[col, row] = (map[col, row].Wall && !map[col, row].CanBurn) ? 9000 : 0;
				}
			}

			//Get the data
			Dijkstra.JustDoIt(ref dijkstra);

			//Use it!
			for (var row = 0; row < 25; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					if (map[col, row].Water)
						continue;
					if (map[col, row].Wall && !map[col, row].CanBurn)
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
		}
	}

	//It's not even a dungeon -- it's a town.
	class TownGenerator : BaseDungeonGenerator
	{
		public void Create(BiomeData biome)
		{
			base.Create(biome, "town");
		}
		
		public override void ToSectorMap(Dictionary<string, Rectangle> sectors)
		{
			for (var row = 0; row < 2; row++)
			{
				for (var col = 0; col < 8; col++)
				{
					sectors.Add(string.Format("s{0}x{1}", row, col), new Rectangle() { Left = col * 10, Right = (col * 10) + 10, Top = row * 12, Bottom = (row * 12) + 12 });
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
			map = new int[80, 25];

			//Draw a nice border for the passes to work within
			for (var i = 0; i < 80; i++)
			{
				map[i, 0] = 1;
				map[i, 24] = 1;
			}
			for (var i = 0; i < 25; i++)
			{
				map[0, i] = 1;
				map[1, i] = 1;
				map[78, i] = 1;
				map[79, i] = 1;
			}

			//Scatter some seed tiles
			for (var i = 0; i < 25; i++)
				for (var j = 0; j < 80; j++)
					if (Random.NextDouble() < 0.25)
						map[j, i] = 1;

			//Melt the cave layout with a cellular automata system.
			for (var pass = 0; pass < 5; pass++)
			{
				for (var i = 1; i < 24; i++)
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
			//When making the initial copy, count the amount of solid spaces as you go. Remember that value. While floodfilling, increase the count. When the count equals 80*25, you have them all.
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			//TODO: make these biome-dependant (use this.biome)
			var floorStart = Color.FromArgb(65, 66, 87);
			var floorEnd = Color.FromArgb(88, 89, 122);
			var wallStart = Color.FromArgb(119, 120, 141);
			var wallEnd = Color.FromArgb(144, 144, 158);
			var floorCrud = new[] { ',', '\'', '`', '.', };

			for (var row = 0; row < 25; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					if (this.map[col, row] == 1)
					{
						map[col, row] = new Tile() { Character = ' ', Wall = true, Background = Toolkit.Lerp(wallStart, wallEnd, Random.NextDouble()) };
					}
					else
					{
						map[col, row] = new Tile() { Character = floorCrud[Random.Next(floorCrud.Length)], Wall = false, Background = Toolkit.Lerp(floorStart, floorEnd, Random.NextDouble()), Foreground = Toolkit.Lerp(floorStart, floorEnd, Random.NextDouble()) };
					}
				}
			}

			var dijkstra = new int[80, 25];
			for (var col = 0; col < 80; col++)
				for (var row = 0; row < 25; row++)
					dijkstra[col, row] = this.map[col, row] == 0 ? 0 : 9000;
			Dijkstra.JustDoIt(ref dijkstra, diagonals: false);
			for (var row = 0; row < 25; row++)
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
		}

	}
}
