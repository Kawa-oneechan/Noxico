using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Xml;

namespace Noxico
{
	enum SplitDirection
	{
		Horizontal, Vertical
	}

	class Boundary
	{
		public int Left { get; set; }
		public int Right { get; set; }
		public int Top { get; set; }
		public int Bottom { get; set; }
		public Boundary(int l, int t, int r, int b)
		{
			Left = l;
			Top = t;
			Right = r;
			Bottom = b;
		}
		public Rectangle ToRectangle()
		{
			return new Rectangle()
			{
				Left = this.Left,
				Right = this.Right - 1,
				Top = this.Top,
				Bottom = this.Bottom - 1,
			};
		}
	}

	//A single node in a BSP tree.
	class BSPNode
	{
		public BSPNode Left { get; set; }
		public BSPNode Right { get; set; }
		public BSPNode Parent { get; set; }
		public BSPNode Sibling { get; set; }
		public SplitDirection SplitDirection { get; set; }
		public Boundary Bounds { get; set; }
		public Room Room { get; set; }

		public void Split(Random rand, int level, List<BSPNode> terminals, int maxLevel = 3, int minDist = 5)
		{
			//SplitDirection = rand.NextDouble() > 0.5 ? Direction.Horizonal : Direction.Vertical;
			SplitDirection = Parent == null ? rand.NextDouble() > 0.5 ? SplitDirection.Horizontal : SplitDirection.Vertical : Parent.SplitDirection == SplitDirection.Horizontal ? SplitDirection.Vertical : SplitDirection.Horizontal;

			Left = new BSPNode() { Parent = this };
			Right = new BSPNode() { Parent = this };
			Left.Sibling = Right;
			Right.Sibling = Left;

			try
			{
				var dist = 0;
				var start = Environment.TickCount;
				if (SplitDirection == SplitDirection.Vertical)
				{
					while (dist < minDist)
					{
						if ((Environment.TickCount - start) > 100)
							throw new ArgumentOutOfRangeException("Timer");
						dist = Bounds.Right - rand.Next(Bounds.Left + 5, Bounds.Right - 5);
					}
					Left.Bounds = new Boundary(Bounds.Left, Bounds.Top, Bounds.Left + dist, Bounds.Bottom);
					Right.Bounds = new Boundary(Bounds.Left + dist, Bounds.Top, Bounds.Right, Bounds.Bottom);
				}
				else
				{
					while (dist < minDist)
					{
						if ((Environment.TickCount - start) > 100)
							throw new ArgumentOutOfRangeException("Timer");
						dist = Bounds.Bottom - rand.Next(Bounds.Top + 5, Bounds.Bottom - 5);
					}
					Left.Bounds = new Boundary(Bounds.Left, Bounds.Top, Bounds.Right, Bounds.Top + dist);
					Right.Bounds = new Boundary(Bounds.Left, Bounds.Top + dist, Bounds.Right, Bounds.Bottom);
				}

				if (level < maxLevel)
				{
					Left.Split(rand, level + 1, terminals, maxLevel, minDist);
					Right.Split(rand, level + 1, terminals, maxLevel, minDist);
				}
				else
				{
					terminals.Add(Left);
					terminals.Add(Right);
				}
			}
			catch (ArgumentOutOfRangeException)
			{
				//Couldn't split smaller than this, so trim the leaves and bail.
				terminals.Remove(Left);
				terminals.Remove(Right);
				Left = null;
				Right = null;
				terminals.Add(this);
			}
		}
	}

	//It's a bit more abstract than that.
	class Room
	{
		public BSPNode Parent { get; set; }
		public Boundary Bounds { get; set; }
		public string ID { get; set; }
		public RoomExit Exit { get; set; }

		public Room(Random rand, BSPNode parent, int minSize = 3, int margin = 0)
		{
			Parent = parent;
			var b = parent.Bounds;

			if (b.Right - b.Left <= minSize || b.Bottom - b.Top <= minSize)
			{
				Bounds = new Boundary(b.Left + margin, b.Top + margin, b.Right - margin, b.Bottom - margin);
				return;
			}

			while (true)
			{
				var left = rand.Next(b.Left, b.Left + ((b.Right - b.Left) / 2));
				var right = rand.Next(b.Left + ((b.Right - b.Left) / 2) + 5, b.Right + 5);
				if (right >= b.Right)
					right = b.Right - 1;

				var top = rand.Next(b.Top, b.Top + ((b.Bottom - b.Top) / 2));
				var bottom = rand.Next(b.Top + ((b.Bottom - b.Top) / 2) + 5, b.Bottom + 5);
				if (bottom >= b.Bottom)
					bottom = b.Bottom - 1;

				Bounds = new Boundary(left + margin, top + margin, right - margin, bottom - margin);

				//Ensure the room is reasonably sized
				if (right - left >= minSize && bottom - top >= minSize)
					return;
			}
		}
	}

	//Just for bookkeeping.
	class RoomExit
	{
		public int Left { get; set; }
		public int Top { get; set; }
		public Direction Side { get; set; }
		public RoomExit(int l, int t, Direction side)
		{
			Left = l;
			Top = t;
			Side = side;
		}
	}

	//--------------------//

	abstract class BaseDungeonGenerator
	{
		public List<BSPNode> Nodes { get; set; }
		public BSPNode Root { get; set; }
		public List<Room> Rooms { get; set; }

		public void Create(int maxLevels = 3, int minDistance = 7, int minRoomSize = 4, int roomMargin = 0)
		{
			Nodes = new List<BSPNode>();
			Rooms = new List<Room>();

			var rand = Toolkit.Rand;
			Root = new BSPNode();
			Root.Bounds = new Boundary(0, 0, 79, 24);
			Root.Split(rand, 0, Nodes, maxLevels, minDistance);

			foreach (var node in Nodes)
			{
				var room = new Room(rand, node, minRoomSize, roomMargin);
				node.Room = room;
				Rooms.Add(room);
			}
		}

		private List<Room> CollectRooms(BSPNode node)
		{
			var rooms = new List<Room>();
			if (node.Left == null)
			{
				//It's a terminal, so we can just add and return the current node's room.
				rooms.Add(Rooms.First(x => x.Parent == node));
			}
			else
			{
				//It's a parent node, so we drill down.
				rooms.AddRange(CollectRooms(node.Left));
				rooms.AddRange(CollectRooms(node.Right));
			}
			return rooms;
		}

		public virtual void ToTilemap(ref Tile[,] map)
		{
			throw new NotImplementedException("BaseDungeon.ToTilemap() must be overridden in a subclass.");
		}

		public virtual void ToSectorMap(Dictionary<string, Rectangle> sectors)
		{
			throw new NotImplementedException("BaseDungeon.ToSectorMap() must be overridden in a subclass.");
		}
	}

	//Ye Olde Generic Dungeon
	/*
		################################################################################
		###..........###.........############...................###########...........##
		###..........###.........############...................###########...........##
		###..........###.........############...................###########...........##
		###..........###.........############...................###########...........##
		##..........##...........##...........#############..................###########
		##..........##...........##...........#############..................###########
		##..........##...........##...........#############..................###########
		##..........##...........##...........#############..................###########
		################################################################################
		##################################\%%%%%%%%%%%%%%%%%%%%%%%%%%/##################
		##........##..........############%..........................%#####...........##
		##........##..........############%..........................%#####...........##
		##........##..........############%..........................%#####...........##
		##........##..........############/%%%%%%%%%%%%%%%%%%%%%%%%%%\#####...........##
		################################################################################
		################################################################################
		################################################################################
		############################\%%%%%/#############################################
		##........#####.......######%.....%#########.......................#############
		##........#####.......######%.....%#########.......................#############
		##........#####.......######%.....%#########.......................#############
		##........#####.......######/%%%%%\#########.......................#############
		################################################################################
		################################################################################
	*/
	class StoneDungeonGenerator : BaseDungeonGenerator
	{
		private BiomeData biome;
		//corridor list here

		public void Create(BiomeData biome)
		{
			base.Create(3, 5, 4);
			//TODO: Add dungeon corridors
			this.biome = biome;
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			//TODO: make these biome-dependant (use this.biome)
			var floorStart = Color.FromArgb(65, 66, 87);
			var floorEnd = Color.FromArgb(88, 89, 122);
			var wallStart = Color.FromArgb(119, 120, 141);
			var wallEnd = Color.FromArgb(144, 144, 158);
			var wall = Color.FromArgb(71, 50, 33); 
			var floorCrud = new[] { ',', '\'', '`', '.', };


			//Base fill
			for (var row = 0; row < 25; row++)
				for (var col = 0; col < 80; col++)
					map[col, row] = new Tile() { Character = ' ', Solid = true, Background = Toolkit.Lerp(wallStart, wallEnd, Toolkit.Rand.NextDouble()) };

			foreach (var room in Rooms)
			{
				if (room.Bounds.Top == 0)
				{
					room.Bounds.Top++;
					room.Bounds.Bottom++;
				}
				if (room.Bounds.Left == 0)
				{
					room.Bounds.Left++;
					//room.Bounds.Right++;
				}

				//Room floors
				for (var row = room.Bounds.Top; row < room.Bounds.Bottom; row++)
					for (var col = room.Bounds.Left; col < room.Bounds.Right; col++)
						map[col, row] = new Tile() { Character = floorCrud[Toolkit.Rand.Next(floorCrud.Length)], Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()) };

				var width = room.Bounds.Right - room.Bounds.Left;
				var height = room.Bounds.Bottom - room.Bounds.Top;

				if (width < 5 || height < 5 || Toolkit.Rand.NextDouble() < 0.25)
					continue;

				//Vertical walls  |
				for (var row = room.Bounds.Top; row < room.Bounds.Bottom; row++)
				{
					map[room.Bounds.Left, row] = new Tile() { Character = (char)0x258C, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
					map[room.Bounds.Right - 1, row] = new Tile() { Character = (char)0x2590, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
				}
				//Horizontal walls  --
				for (var col = room.Bounds.Left; col < room.Bounds.Right; col++)
				{
					var bgColor = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
					map[col, room.Bounds.Top] = new Tile() { Character = (char)0x2580, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
					map[col, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0x2584, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
				}
				//Corners -- top left, top right, bottom left, bottom right
				map[room.Bounds.Left, room.Bounds.Top] = new Tile() { Character = (char)0x2588, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
				map[room.Bounds.Right - 1, room.Bounds.Top] = new Tile() { Character = (char)0x2588, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
				map[room.Bounds.Left, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0x2588, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };
				map[room.Bounds.Right - 1, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0x2588, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true, SpecialDescription = 2 };

			}
			
			//Prepare to fade out the walls
			var dijkstra = new int[80, 25];
			for (var col = 0; col < 80; col++)
				for (var row = 0; row < 25; row++)
					dijkstra[col, row] = (map[col, row].Solid && !map[col, row].CanBurn) ? 9000 : 0;

			//Get the data
			Dijkstra.JustDoIt(ref dijkstra);

			//Use it!
			for (var row = 0; row < 25; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					if (map[col, row].Solid && !map[col, row].CanBurn)
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

	//It's not even a dungeon -- it's a town.
	/*
		................................................................................
		....+----+............................+-----------------------------+...........
		....|    |............................|                             |...........
		....|    |............................|                             |...........
		....|    |............................|                             |...........
		....+-- -+............................+------------------------ ----+...........
		........................................................................+---+...
		........................................................................    |...
		........................................................................|   |...
		........+----- --+.....................+----------------------- -+......|   |...
		........|        |.....................|                         |......+---+...
		........|        |.....................|                         |..............
		........|        |.....................|                         |..............
		........+--------+.....................|                         |..............
		.......................................+-------------------------+..............
		................................................................................
		........................................................................+---+...
		.....+----------- -+....................................................|   |...
		.....|             |...+--------------------------- -----------------+..|   |...
		.....|             |...|                                             |..|   |...
		.....|             |...|                                             |..    |...
		.....+-------------+...|                                             |..+---+...
		.......................+---------------------------------------------+..........
		................................................................................
		................................................................................
	*/
	class TownGenerator : BaseDungeonGenerator
	{
		public Board Board { get; set; }
		public Culture Culture { get; set; }
		private int[,] map;
		private BiomeData biome;

		private class Marking
		{
			public string Type;
			public string[] Params;
			public int Owner;
		}

		private class Template
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
				Inhabitants = int.Parse(element.GetAttribute("inhabitants"));
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
					Markings.Add(c, new Marking() { Type = t, Params = p, Owner = o });
				}
			}
		}

		private static List<Template> templates = null;

		private struct Building
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
				if (template != null && culture != null)
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
				var areMarried = Toolkit.Rand.NextDouble() > 0.7;
				var firstPlan = "";
				count = 2;
				for (var i = 0; i < count; i++)
				{
					Character c;
					var plan = culture.Bodyplans[Toolkit.Rand.Next(culture.Bodyplans.Length)];
					if (i > 0 && Toolkit.Rand.NextDouble() > 0.7)
						plan = firstPlan;
					c = Character.Generate(plan, count == 1 ? Gender.Random : (i == 0 ? Gender.Male : Gender.Female));
					if (i == 0)
					{
						familyName = c.Name.Surname;
						firstPlan = plan;
					}
					if (i == 1)
					{
						var shipType = Toolkit.Rand.NextDouble() < culture.Marriage ? "spouse" : "friend";
						//if we chose spouse, handle the wife taking the surname of the husband.
						var ship = new Token() { Name = c.Name.ToString(true) };
						ship.Tokens.Add(new Token() { Name = shipType });
						r[0].Path("ships").Tokens.Add(ship);
						ship = new Token() { Name = r[0].Name.ToString(true) };
						ship.Tokens.Add(new Token() { Name = shipType });
						c.Path("ships").Tokens.Add(ship);
					}
					r.Add(c);
				}
				return r;
			}
		}

		private Building[,] plots;

		public void Create(BiomeData biome)
		{
			this.biome = biome;
			map = new int[80, 25];
			plots = new Building[8, 2];

			if (templates == null)
			{
				templates = new List<Template>();
				var xDoc = new XmlDocument();
				xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.buildings, "buildings.xml"));
				foreach (var t in xDoc.SelectNodes("//template").OfType<XmlElement>())
					templates.Add(new Template(t));
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
					if (Toolkit.Rand.NextDouble() < 0.2)
					{
						//justPlaced = false;
						continue;
					}
					var newTemplate = templates[Toolkit.Rand.Next(templates.Count)];
					//TODO: check if chosen template spills over and if so, if there's room. For now, assume all templates are <= 8
					//Each plot is 8x8. Given that and the template size, we can wiggle them around a bit from 0 to (8 - tSize).
					var sX = newTemplate.Width < 10 ? Toolkit.Rand.Next(1, 10 - newTemplate.Width) : 0;
					var sY = newTemplate.Height < 12 ? Toolkit.Rand.Next(1, 12 - newTemplate.Height) : 0;
					//Later on, we might be able to wiggle them out of their assigned plot a bit.
					//TODO: determine baseID from the first inhabitant's name.
					var newBuilding = new Building(string.Format("house{0}x{1}", col, row), newTemplate, sX, sY, Culture);
					plots[col, row] = newBuilding;
					//justPlaced = true;
				}
			}
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			var floorStart = Color.FromArgb(123, 92, 65);
			var floorEnd = Color.FromArgb(143, 114, 80); //Color.FromArgb(168, 141, 98);
			var wall = Color.FromArgb(71, 50, 33);

			var cornerJunctions = new List<Point>();

			for (var row = 0; row < 2; row++)
			{
				for (var col = 0; col < 8; col++)
				{
					if (plots[col, row].BaseID == null || plots[col, row].BaseID == "<spillover>")
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
							var fg = Color.Black;
							var bg = Color.Silver;
							var ch = '?';
							var s = false;
							switch (tc)
							{
								case '\'':
									continue;
								case '.':
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									ch = ' ';
									break;
								case '\\': //Exit -- can't be seen, coaxes walls into shape.
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									ch = '\xA0';
									break;
								case '+':
									fg = wall;
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									s = true;
									cornerJunctions.Add(new Point(sX + x, sY + y));
									break;
								case '-':
									fg = wall;
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									ch = '\x2550';
									s = true;
									break;
								case '|':
									fg = wall;
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									ch = '\x2551';
									s = true;
									break;
								case '~':
									fg = wall;
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									ch = '\x2500';
									s = true;
									break;
								case '¦':
									fg = wall;
									bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
									ch = '\x2502';
									s = true;
									break;
								default:
									if (template.Markings.ContainsKey(tc))
									{
										var m = template.Markings[tc];
										if (m.Type != "block" && m.Type != "floor")
										{
											//Keep a floor here. The entity fills in the blank.
											bg = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
											ch = ' ';
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
													BackgroundColor = bg,
													ID = building.BaseID + "_Bed_" + owner == null ? "Free" : owner.Name.FirstName,
													Description = owner == null ? "This is a free bed. Position yourself over it and press Enter to use it." : string.Format("This is {0}'s bed. If you want to use it, you should ask {0} for permission.", owner.Name.ToString(true), owner.HimHerIt()),
													ParentBoard = Board,
												};
												Board.Entities.Add(newBed);
											}
											if (m.Type == "container")
											{
												var chr = m.Params.Last()[0];
												var type = chr == '\x006C' ? "cabinet" : chr == '\x03C0' ? "chest" : "container";
												if (m.Params[0] == "clothes")
												{
													//if (type == "cabinet")
													type = "wardrobe";
												}
												var contents = RollContainer(owner, type);  //new List<Token>();
												var newContainer = new Container(owner == null ? type.Titlecase() : owner.Name.ToString(true) + "'s " + type, contents)
												{
													AsciiChar = m.Params.Last()[0],
													XPosition = sX + x,
													YPosition = sY + y,
													ForegroundColor = Color.Black,
													BackgroundColor = bg,
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
													BackgroundColor = bg,
													ParentBoard = Board,
													Blocking = m.Params.Contains("blocking"),
												};
												Board.Entities.Add(newClutter);
											}
										}
										else
										{
											fg = m.Params[0] == "floor" ? Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()) : m.Params[0] == "wall" ? wall : Toolkit.GetColor(m.Params[0]);
											bg = m.Params[1] == "floor" ? Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()) : m.Params[1] == "wall" ? wall : Toolkit.GetColor(m.Params[0]);
											ch = m.Params.Last()[0];
											s = m.Type == "block";
										}
									}
									break;
							}
							map[sX + x, sY + y] = new Tile() { Character = ch, Foreground = fg, Background = bg, Solid = s };
						}
					}

					for (var i = 0; i < building.Inhabitants.Count; i++)
					{
						var inhabitant = building.Inhabitants[i];
						//Find each inhabitant's bed so we can give them a starting place.
						//Alternatively, place them anywhere there's a ' ' within their sector.
						var bc = new BoardChar(inhabitant);
						var bedID = building.BaseID + "_Bed_" + inhabitant.Name.FirstName;
						var bed = Board.Entities.OfType<Clutter>().FirstOrDefault(b => b.ID == bedID);
						//if (bed != null)
						//{
						//	bc.XPosition = bed.XPosition;
						//	bc.YPosition = bed.YPosition;
						//}
						//else
						{
							//TODO: find random ' ' within sector
							var okay = false;
							var x = 0;
							var y = 0;
							while (!okay)
							{
								x = (col * 10) + Toolkit.Rand.Next(10);
								y = (row * 12) + Toolkit.Rand.Next(12);
								if (!map[x, y].Solid && map[x, y].Character == ' ' && Board.Entities.FirstOrDefault(e => e.XPosition == x && e.YPosition == y) == null)
									okay = true;
							}
							bc.XPosition = x;
							bc.YPosition = y;
							//bc.XPosition = (col * 10) + i;
							//bc.YPosition = row * 12;
						}
						bc.Movement = Motor.WanderSector;
						bc.ParentBoard = Board;
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
		}

		private static XmlDocument xDoc;

		private List<Token> RollContainer(Character owner, string type)
		{
			if (xDoc == null)
			{
				xDoc = new XmlDocument();
				xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Main, "noxico.xml"));
			}
			var ret = new List<Token>();
			var gender = owner == null ? Gender.Random : owner.Name.Female ? Gender.Female : Gender.Male;
			switch (type)
			{
				case "wardrobe":
					var costumes = xDoc.DocumentElement.SelectNodes("costumes/costume").OfType<XmlElement>().ToList();
					var amount = Toolkit.Rand.Next(2, 7);
					for (var i = 0; i < amount; i++)
					{
						XmlElement x = null;
						var carrier = new TokenCarrier();
						Token costume = null;
						var lives = 20;
						while (costume == null && lives > 0)
						{
							lives--;
							x = costumes[Toolkit.Rand.Next(costumes.Count)];
							carrier.Tokens = Token.Tokenize(x.InnerText);
							if (carrier.HasToken("rare") && Toolkit.Rand.NextDouble() > 0.5)
								continue;
							if (gender == Gender.Male && carrier.HasToken("male"))
								costume = carrier.GetToken("male");
							if (gender == Gender.Female && carrier.HasToken("female"))
								costume = carrier.GetToken("female");
						}
						if (carrier.HasToken("singleton"))
							costumes.Remove(x);
						if (costume == null)
							break;
						Toolkit.FoldCostumeRandoms(costume);
						Toolkit.FoldCostumeVariables(costume);
						foreach (var request in costume.Tokens)
						{
							var find = NoxicoGame.KnownItems.Find(item => item.ID == request.Name);
							if (find == null)
								continue;
							ret.Add(request);
						}
					}
					break;
				case "chest":
					break;
				case "cabinet":
					break;
			}
			return ret;
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
	/*
		################################################################################
		####.......###########....###################.....##############################
		###.........#...#####......#..##############.......#####..#################..###
		###......#.#......###..........###..###..###.......####...################....##
		###......##........###.........#.....#..............###...################....##
		##.........#........##..............................##.....###########........##
		##........#............................##.......##.........##########.........##
		###........#.............####..........####..##..#.........#########..........##
		####.......###.......#....####..........##.#..#............#########..........##
		####.......####.....###....#............#...#..............#..####............##
		####.........#.......#.....................#...................##.....##......##
		####.........................###........####...................#......###.....##
		###.........................#####........####..................#.......##....###
		###.........................####.........##......##...........#..........#######
		###.........................###.................####.........##...........######
		###.........................##..................#####.........##..........######
		###....#...................###..................####......................######
		###....##..................###.....................#.........................###
		###.........................#........................###......................##
		###...................##.........................###..##.....#...#......##....##
		####......##....###....##.....................#..###.........##.##.......##...##
		####.......#....####...#......#........#.....###..###...........#........#######
		#####...........####.........####.....###....###.........................#######
		#########......######......#######...#####..#####..........#........#...########
		################################################################################
	 */
	class CaveGenerator : BaseDungeonGenerator
	{
		private int[,] map;
		private BiomeData biome;

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
			var rand = Toolkit.Rand;
			for (var i = 0; i < 25; i++)
				for (var j = 0; j < 80; j++)
					if (rand.NextDouble() < 0.25)
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
						map[col, row] = new Tile() { Character = ' ', Solid = true, Background = Toolkit.Lerp(wallStart, wallEnd, Toolkit.Rand.NextDouble()) };
					}
					else
					{
						map[col, row] = new Tile() { Character = floorCrud[Toolkit.Rand.Next(floorCrud.Length)], Solid = false, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()) };
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
