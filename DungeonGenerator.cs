using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Noxico
{
	enum SplitDirection
	{
		Horizontal, Vertical
	}

	//The poor man's Rectangle.
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
		public RoomExit(int l, int t)
		{
			Left = l;
			Top = t;
		}
	}

	//--------------------//

	abstract class BaseDungeonGenerator
	{
		public List<BSPNode> Nodes { get; set; }
		public BSPNode Root { get; set; }
		public List<Room> Rooms { get; set; }

		public virtual void Create(int maxLevels = 3, int minDistance = 7, int minRoomSize = 4, int roomMargin = 0)
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

		public virtual string ToAscii()
		{
			throw new NotImplementedException("BaseDungeon.ToAscii() must be overridden in a subclass.");
		}

		public virtual void ToTilemap(ref Tile[,] map)
		{
			throw new NotImplementedException("BaseDungeon.ToTilemap() must be overridden in a subclass.");
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
		//corridor list here

		public void Create(Biome biome)
		{
			base.Create(3, 5, 4);
			//TODO: Add dungeon corridors
		}

		public override string ToAscii()
		{
			var map = new char[80, 25];

			//Base fill
			for (var row = 0; row < 25; row++)
				for (var col = 0; col < 80; col++)
					map[col, row] = '#';

			var rand = Toolkit.Rand;

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
						map[col, row] = '.';

				var width = room.Bounds.Right - room.Bounds.Left;
				var height = room.Bounds.Bottom - room.Bounds.Top;

				if (width < 5 || height < 5)
					continue;

				//Vertical walls  |
				for (var row = room.Bounds.Top; row < room.Bounds.Bottom; row++)
				{
					map[room.Bounds.Left, row] = '%';
					map[room.Bounds.Right - 1, row] = '%';
				}
				//Horizontal walls  --
				for (var col = room.Bounds.Left; col < room.Bounds.Right; col++)
				{
					map[col, room.Bounds.Top] = '%';
					map[col, room.Bounds.Bottom - 1] = '%';
				}
				//Corners -- top left, top right, bottom left, bottom right
				map[room.Bounds.Left, room.Bounds.Top] = '\\';
				map[room.Bounds.Right - 1, room.Bounds.Top] = '/';
				map[room.Bounds.Left, room.Bounds.Bottom - 1] = '/';
				map[room.Bounds.Right - 1, room.Bounds.Bottom - 1] = '\\';
			}

			//Convert map to strings
			var ascii = new StringBuilder();
			var thisRow = new StringBuilder();
			for (var row = 0; row < 25; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					thisRow.Append(map[col, row]);
				}
				ascii.AppendLine(thisRow.ToString().TrimEnd());
				thisRow.Clear();
			}
			return ascii.ToString().TrimEnd();
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			//Base fill
			for (var row = 0; row < 25; row++)
				for (var col = 0; col < 80; col++)
					map[col, row] = new Tile() { Character = ' ', Solid = true, Background = Color.Gray };

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
						map[col, row] = new Tile() { Character = ' ', Background = Color.Black };

				var width = room.Bounds.Right - room.Bounds.Left;
				var height = room.Bounds.Bottom - room.Bounds.Top;

				if (width < 5 || height < 5)
					continue;

				//Vertical walls  |
				for (var row = room.Bounds.Top; row < room.Bounds.Bottom; row++)
				{
					map[room.Bounds.Left, row] = new Tile() { Character = (char)0xBA, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
					map[room.Bounds.Right - 1, row] = new Tile() { Character = (char)0xBA, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
				}
				//Horizontal walls  --
				for (var col = room.Bounds.Left; col < room.Bounds.Right; col++)
				{
					map[col, room.Bounds.Top] = new Tile() { Character = (char)0xCD, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
					map[col, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0xCD, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
				}
				//Corners -- top left, top right, bottom left, bottom right
				map[room.Bounds.Left, room.Bounds.Top] = new Tile() { Character = (char)0xC9, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
				map[room.Bounds.Right - 1, room.Bounds.Top] = new Tile() { Character = (char)0xBB, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
				map[room.Bounds.Left, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0xC8, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
				map[room.Bounds.Right - 1, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0xBC, Background = Color.Black, Foreground = Color.Brown, CanBurn = true, Solid = true };
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
		public List<RoomExit> Exits { get; private set; }

		public void Create(Biome biome)
		{
			base.Create(3, 5, 7, 1);

			var rand = Toolkit.Rand;

			//TODO: add special buildings
			//To do that, check for a terminal BSPNode of appropriate dimensions and remove the associated room.

			Exits = new List<RoomExit>();
			foreach (var room in Rooms)
			{
				var width = room.Bounds.Right - room.Bounds.Left;
				var height = room.Bounds.Bottom - room.Bounds.Top;

				if (width < 5)
				{
					width = 5;
					room.Bounds.Right = room.Bounds.Left + 5;
					if (room.Bounds.Right > 79)
					{
						room.Bounds.Right = 79;
						room.Bounds.Left = 74;
					}
				}
				if (width > 10)
				{
					width = 10;
					room.Bounds.Right = room.Bounds.Left + 10;
				}
				if (height < 4)
				{
					height = 4;
					room.Bounds.Bottom = room.Bounds.Top + 4;
					if (room.Bounds.Bottom > 24)
					{
						room.Bounds.Bottom = 24;
						room.Bounds.Top = 20;
					}
				}

				var onVertical = false;
				if (width > height)
					onVertical = false;
				else if (width < height)
					onVertical = true;
				else
					onVertical = rand.NextDouble() < 0.5;

				var left = 0;
				var top = 0;
				if (onVertical)
				{
					top = rand.Next(room.Bounds.Top + 1, room.Bounds.Bottom - 1);
					left = (room.Bounds.Right < 40) ? room.Bounds.Right - 1 : room.Bounds.Left;
				}
				else
				{
					top = (room.Bounds.Bottom < 12) ? room.Bounds.Bottom - 1 : room.Bounds.Top;
					left = rand.Next(room.Bounds.Left + 1, room.Bounds.Right - 1);
				}

				Exits.Add(new RoomExit(left, top));
			}

			//TODO: trace paths through the map?
		}

		public override void ToTilemap(ref Tile[,] map)
		{
			var floorStart = Color.FromArgb(123, 92, 65);
			var floorEnd = Color.FromArgb(143, 114, 80); //Color.FromArgb(168, 141, 98);
			var wall = Color.FromArgb(71, 50, 33);

			foreach (var room in Rooms)
			{
				//Room floors
				for (var row = room.Bounds.Top; row < room.Bounds.Bottom; row++)
					for (var col = room.Bounds.Left; col < room.Bounds.Right; col++)
						map[col, row] = new Tile() { Character = ' ', Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()) };

				//Vertical walls  |
				for (var row = room.Bounds.Top; row < room.Bounds.Bottom; row++)
				{
					map[room.Bounds.Left, row] = new Tile() { Character = (char)0xDD, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
					map[room.Bounds.Right - 1, row] = new Tile() { Character = (char)0xDE, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
				}
				//Horizontal walls  --
				for (var col = room.Bounds.Left; col < room.Bounds.Right; col++)
				{
					var bgColor = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble());
					map[col, room.Bounds.Top] = new Tile() { Character = (char)0xDF, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
					map[col, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0xDC, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
				}
				//Corners -- top left, top right, bottom left, bottom right
				map[room.Bounds.Left, room.Bounds.Top] = new Tile() { Character = (char)0xDB, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
				map[room.Bounds.Right - 1, room.Bounds.Top] = new Tile() { Character = (char)0xDB, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
				map[room.Bounds.Left, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0xDB, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
				map[room.Bounds.Right - 1, room.Bounds.Bottom - 1] = new Tile() { Character = (char)0xDB, Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()), Foreground = wall, CanBurn = true, Solid = true };
			}

			foreach (var exit in Exits)
			{
				map[exit.Left, exit.Top] = new Tile() { Character = ' ', Background = Toolkit.Lerp(floorStart, floorEnd, Toolkit.Rand.NextDouble()) };
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

		public void Create(Biome biome)
		{
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
		}

		public override string ToAscii()
		{
			//Convert map to strings
			var ascii = new StringBuilder();
			var thisRow = new StringBuilder();
			for (var row = 0; row < 25; row++)
			{
				for (var col = 0; col < 80; col++)
					thisRow.Append(map[col, row] == 1 ? '#' : '.');

				ascii.AppendLine(thisRow.ToString().TrimEnd());
				thisRow.Clear();
			}
			return ascii.ToString().TrimEnd();
		}
	}
}
