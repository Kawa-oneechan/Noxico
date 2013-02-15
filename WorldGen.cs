using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Noxico
{
	public static class WorldGen
	{
		private static TownGenerator townGen;
		private static List<Func<InventoryItem, bool>> vendorTypeList;

		public static Board CreateTown(int biomeID, string cultureName, string name, bool withSurroundings)
		{
			if (townGen == null)
				townGen = new TownGenerator();

			if (biomeID < 0)
				biomeID = Random.Next(2, 5);

			var boards = NoxicoGame.HostForm.Noxico.Boards;
			var thisMap = new Board();
			var biome = BiomeData.Biomes[biomeID];

			var vendorChance = 0.5;
			if (vendorTypeList == null)
			{
				vendorTypeList = new List<Func<InventoryItem, bool>>()
				{
					new Func<InventoryItem, bool>(x => x.Path("equipable/male") != null && !(x.Path("equipable/underpants") != null || x.Path("equipable/undershirt") != null)), //mens clothing
					new Func<InventoryItem, bool>(x => x.Path("equipable/female") != null && !(x.Path("equipable/underpants") != null || x.Path("equipable/undershirt") != null)), //womens clothing
					new Func<InventoryItem, bool>(x => x.Path("equipable/female") == null && !(x.Path("equipable/underpants") != null || x.Path("equipable/undershirt") != null)), //other clothing
					new Func<InventoryItem, bool>(x => x.Path("equipable/male") != null && (x.Path("equipable/underpants") != null || x.Path("equipable/undershirt") != null)), //mens underwear
					new Func<InventoryItem, bool>(x => x.Path("equipable/female") != null && (x.Path("equipable/underpants") != null || x.Path("equipable/undershirt") != null)), //lingerie
					new Func<InventoryItem, bool>(x => x.Path("statbonus/health") != null && x.Path("singleuse") == null), //foodstuffs
					new Func<InventoryItem, bool>(x => x.Path("weapon") != null && x.Path("weapon/range") == null), //short-range weapons

					new Func<InventoryItem, bool>(x => x.Path("statbonus/health") != null && x.Path("singleuse") == null), //repeat for bias
					new Func<InventoryItem, bool>(x => x.Path("weapon") != null && x.Path("weapon/range") == null),
				};
			}
			var vendorTypes = new List<Func<InventoryItem, bool>>();

			if (string.IsNullOrEmpty(cultureName))
				cultureName = biome.Cultures[Random.Next(biome.Cultures.Length)];

			thisMap.Clear(biomeID);
			thisMap.BoardType = BoardType.Town;
			thisMap.Music = "set://Town";
			thisMap.AddToken("culture", 0, cultureName);

			townGen.Board = thisMap;
			townGen.Culture = Culture.Cultures[cultureName];
			townGen.Create(biome);
			townGen.ToTilemap(ref thisMap.Tilemap);
			townGen.ToSectorMap(thisMap.Sectors);

			if (Random.NextDouble() < vendorChance)
				if (AddVendor(thisMap, vendorTypes))
					vendorChance *= 0.75;

			if (string.IsNullOrEmpty(name))
			{
				while (true)
				{
					name = Culture.GetName(townGen.Culture.TownName, Culture.NameType.Town);
					if (boards.Find(b => b != null && b.Name == name) == null)
						break;
				}
			}
			thisMap.Name = name;

			thisMap.BoardNum = boards.Count;
			thisMap.ID = thisMap.Name.ToID() + thisMap.BoardNum;

			boards.Add(thisMap);

			if (!withSurroundings)
				return thisMap;

			//Generate surroundings
			var north = new Board();
			var south = new Board();
			var east = new Board();
			var west = new Board();
			var northWest = new Board();
			var northEast = new Board();
			var southWest = new Board();
			var southEast = new Board();
			foreach (var lol in new[] { northWest, north, northEast, east, southEast, south, southWest, west })
			{
				lol.Clear(biomeID);
				lol.BoardType = BoardType.Wild;
				lol.Music = "set://Town";
				lol.BoardNum = boards.Count;
				lol.Name = thisMap.Name + " Outskirts";
				lol.ID = lol.Name.ToID() + lol.BoardNum;
				if (Random.NextDouble() > 0.5 && biome.Encounters.Length > 0)
				{
					var encounters = lol.GetToken("encounters");
					encounters.Value = biome.MaxEncounters;
					foreach (var e in biome.Encounters)
						encounters.AddToken(e);

					//Possibility of linked dungeons
					if (Random.NextDouble() < 0.4)
					{
						var eX = Random.Next(2, 78);
						var eY = Random.Next(1, 23);

						if (lol.IsSolid(eY, eX))
							continue;
						var sides = 0;
						if (lol.IsSolid(eY - 1, eX))
							sides++;
						if (lol.IsSolid(eY + 1, eX))
							sides++;
						if (lol.IsSolid(eY, eX - 1))
							sides++;
						if (lol.IsSolid(eY, eX + 1))
							sides++;
						if (sides > 3)
							continue;

						var newWarp = new Warp()
						{
							TargetBoard = -1, //mark as ungenerated dungeon
							ID = lol.ID + "_Dungeon",
							XPosition = eX,
							YPosition = eY,
						};
						lol.Warps.Add(newWarp);
						lol.SetTile(eY, eX, '>', Color.Silver, Color.Black);
					}
				}
				else if (Random.NextDouble() > 0.8)
				{
					lol.BoardType = BoardType.Town;
					lol.Name = thisMap.Name;
					lol.ID = lol.Name.ToID() + lol.BoardNum;
					townGen.Board = lol;
					townGen.Create(biome);
					townGen.ToTilemap(ref lol.Tilemap);
					townGen.ToSectorMap(lol.Sectors);
					if (Random.NextDouble() < vendorChance)
						if (AddVendor(lol, vendorTypes))
							vendorChance *= 0.75;
				}
				boards.Add(lol);
			}
			thisMap.Connect(Direction.North, north);
			thisMap.Connect(Direction.South, south);
			thisMap.Connect(Direction.East, east);
			thisMap.Connect(Direction.West, west);
			north.Connect(Direction.West, northWest);
			north.Connect(Direction.East, northEast);
			south.Connect(Direction.West, southWest);
			south.Connect(Direction.East, southEast);
			east.Connect(Direction.North, northEast);
			east.Connect(Direction.South, southEast);
			west.Connect(Direction.North, northWest);
			west.Connect(Direction.South, southWest);

			return thisMap;
		}

		public static Board CreateTown()
		{
			return CreateTown(-1, null, null, true);
		}

		public static bool AddVendor(Board board, List<Func<InventoryItem, bool>> typeList)
		{
			var unexpected = board.Entities.OfType<BoardChar>().Where(e => !e.Character.HasToken("expectation") && e.Character.Path("role/vendor") == null).ToList();
			if (unexpected.Count == 0)
				return false;
			var vendor = unexpected[0].Character;
			var stock = vendor.GetToken("items");
			var count = Random.Next(10, 20);
			if (typeList.Count == 0)
				typeList.AddRange(vendorTypeList);
			var criterion = typeList[Random.Next(typeList.Count)];
			typeList.Remove(criterion);
			var sellable = NoxicoGame.KnownItems.FindAll(x => x.HasToken("price") && criterion(x)).ToList();
			while (sellable.Count == 0)
			{
				criterion = typeList[Random.Next(typeList.Count)];
				typeList.Remove(criterion);
				if (typeList.Count == 0)
				{
					Console.WriteLine("*** Gave up trying to make {0} a vendor ***", vendor.Name.ToString(true));
					return false;
				}
			}
			while (count > 0)
			{
				if (sellable.Count == 0)
					break;
				var item = sellable[Random.Next(sellable.Count)];
				stock.AddToken(item.ID);
				if (Random.NextDouble() < 0.8)
					sellable.Remove(item);
			}
			vendor.RemoveAll("role");
			vendor.AddToken("role").AddToken("vendor");
			vendor.GetToken("money").Value = 1000 + (Random.Next(0, 20) * 50);
			Console.WriteLine("*** {0} is now a vendor ***", vendor.Name.ToString(true));
			return true;
		}



		public static int DungeonGeneratorEntranceBoardNum;
		public static string DungeonGeneratorEntranceWarpID;
		public static int DungeonGeneratorBiome;

		public static void CreateDungeon()
		{
			CreateDungeon(false, null);
		}

		public static Board CreateDungeon(int biomeID, string cultureName, string name)
		{
			DungeonGeneratorBiome = biomeID < 0 ? biomeID = Random.Next(2, 5) : biomeID;
			return CreateDungeon(true, name);
		}

		public static Board CreateDungeon(bool forTravel, string name)
		{
			var host = NoxicoGame.HostForm;
			var nox = host.Noxico;

			//host.LoadBitmap(Mix.GetBitmap("makecave.png"));
			host.Write("Generating dungeon. Please wait.", Color.Silver, Color.Transparent, 1, 2);
			host.Draw();

			var dunGen = new StoneDungeonGenerator();
			var caveGen = new CaveGenerator();

			Func<Board, Warp> findWarpSpot = (b) =>
			{
				var eX = 0;
				var eY = 0;
				while (true)
				{
					eX = Random.Next(1, 79);
					eY = Random.Next(1, 24);

					var sides = 0;
					if (b.IsSolid(eY - 1, eX))
						sides++;
					if (b.IsSolid(eY + 1, eX))
						sides++;
					if (b.IsSolid(eY, eX - 1))
						sides++;
					if (b.IsSolid(eY, eX + 1))
						sides++;
					if (sides < 3 && sides > 1)
						break;
				}
				return new Warp() { XPosition = eX, YPosition = eY };
			};

			Warp originalExit = null;

			BiomeData.LoadBiomes();
			var biomeData = BiomeData.Biomes[DungeonGeneratorBiome]; //TODO: replace 3 with DungeonGeneratorBiome -- this is for testing.

			/* Step 1 - Randomize jagged array, make boards for each entry.
			 * ------------------------------------------------------------
			 * ("goal" board is boss/treasure room, picked at random from bottom floor set.)
			 * [EXIT] [ 01 ] [ 02 ]
			 * [ 03 ] [ 04 ]
			 * [ 05 ] [ 06 ] [ 07 ] [ 08 ]
			 * [ 09 ] [ 10 ] [ 11 ]
			 * [GOAL] [ 13 ]
			*/
			var levels = new List<List<Board>>();
			var depth = Random.Next(3, 6);
			for (var i = 0; i < depth; i++)
			{
				levels.Add(new List<Board>());
				var length = Random.Next(2, 5);
				for (var j = 0; j < length; j++)
				{
					var board = new Board();
					board.BoardNum = nox.Boards.Count;
					if (i > 0)
						board.AddToken("dark");
					nox.Boards.Add(board);
					levels[i].Add(board);
				}
			}

			//Decide which boards are the exit and goal
			var entranceBoard = levels[0][Random.Next(levels[0].Count)];
			var goalBoard = levels[levels.Count - 1][Random.Next(levels[levels.Count - 1].Count)];

			//Generate content for each board
			for (var i = 0; i < levels.Count; i++)
			{
				for (var j = 0; j < levels[i].Count; j++)
				{
					var board = levels[i][j];

					//TODO: uncomment this decision when the dungeon generator gets pathways.
					if (Random.NextDouble() > 0.7 || board == entranceBoard)
					{
						caveGen.Board = board;
						caveGen.Create(biomeData);
						caveGen.ToTilemap(ref board.Tilemap);
					}
					else
					{
						dunGen.Board = board;
						dunGen.Create(biomeData);
						dunGen.ToTilemap(ref board.Tilemap);
					}

					board.Name = string.Format("Level {0}-{1}", i + 1, (char)('A' + j));
					if (!string.IsNullOrWhiteSpace(name))
						board.Name = string.Format("{0}, level {1}-{2}", name, i + 1, (char)('A' + j));
					board.ID = string.Format("Dng_{0}_{1}{2}", DungeonGeneratorEntranceBoardNum, i + 1, (char)('A' + j));
					board.Music = "set://Dungeon";
					board.BoardType = BoardType.Dungeon;
					var encounters = board.GetToken("encounters");
					foreach (var e in biomeData.Encounters)
						encounters.AddToken(e);
					encounters.Value = biomeData.MaxEncounters;
					board.RespawnEncounters();

					//If this is the entrance board, add a warp back to the Overworld.
					if (board == entranceBoard)
					{
						var exit = findWarpSpot(board);
						originalExit = exit;
						exit.ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_Exit";
						board.Warps.Add(exit);
						board.SetTile(exit.YPosition, exit.XPosition, '<', Color.Silver, Color.Black);
					}
				}
			}

			/* Step 2 - Randomly add up/down links
			 * -----------------------------------
			 * (imagine for the moment that each board can have more than one exit and that this goes for both directions.)
			 * [EXIT] [ 01 ] [ 02 ]
			 *    |
			 * [ 03 ] [ 04 ]
			 * 	         |
			 * [ 05 ] [ 06 ] [ 07 ] [ 08 ]
			 *    |             |
			 * [ 09 ] [ 10 ] [ 11 ]
			 * 	                |
			 * 	      [GOAL] [ 13 ]
			 */
			var connected = new List<Board>();
			for (var i = 0; i < levels.Count; i++)
			{
				var j = Random.Next(0, levels[i].Count);
				//while (connected.Contains(levels[i][j]))
				//	j = Randomizer.Next(0, levels[i].Count);

				var up = false;
				var destLevel = i + 1;
				if (destLevel == levels.Count)
				{
					up = true;
					destLevel = i - 1;
				}
				var dest = Random.Next(0, levels[destLevel].Count);

				var boardHere = levels[i][j];
				var boardThere = levels[destLevel][dest];

				var here = findWarpSpot(boardHere);
				var there = findWarpSpot(boardThere);
				boardHere.Warps.Add(here);
				boardThere.Warps.Add(there);
				here.ID = boardHere.ID + boardHere.Warps.Count;
				there.ID = boardThere.ID + boardThere.Warps.Count;
				here.TargetBoard = boardThere.BoardNum;
				there.TargetBoard = boardHere.BoardNum;
				here.TargetWarpID = there.ID;
				there.TargetWarpID = here.ID;
				boardHere.SetTile(here.YPosition, here.XPosition, up ? '<' : '>', Color.Gray, Color.Black);
				boardThere.SetTile(there.YPosition, there.XPosition, !up ? '<' : '>', Color.Gray, Color.Black);

				Console.WriteLine("Connected {0} || {1}.", boardHere.ID, boardThere.ID);

				connected.Add(boardHere);
				connected.Add(boardThere);
			}

			/* Step 3 - Connect the Unconnected
			 * --------------------------------
			 * [EXIT]=[ 01 ]=[ 02 ]
			 * 	|
			 * [ 03 ]=[ 04 ]
			 *           |
			 * [ 05 ]=[ 06 ] [ 07 ]=[ 08 ]
			 *    |             |
			 * [ 09 ]=[ 10 ]=[ 11 ]
			 *                  |
			 *        [GOAL]=[ 13 ]
			 */

			for (var i = 0; i < levels.Count; i++)
			{
				for (var j = 0; j < levels[i].Count - 1; j++)
				{
					//Don't connect if this board AND the right-hand neighbor are already connected.
					//if (connected.Contains(levels[i][j]) && connected.Contains(levels[i][j + 1]))
					//	continue;

					var boardHere = levels[i][j];
					var boardThere = levels[i][j + 1];

					var here = findWarpSpot(boardHere);
					var there = findWarpSpot(boardThere);
					boardHere.Warps.Add(here);
					boardThere.Warps.Add(there);
					here.ID = boardHere.ID + boardHere.Warps.Count;
					there.ID = boardThere.ID + boardThere.Warps.Count;
					here.TargetBoard = boardThere.BoardNum;
					there.TargetBoard = boardHere.BoardNum;
					here.TargetWarpID = there.ID;
					there.TargetWarpID = here.ID;
					boardHere.SetTile(here.YPosition, here.XPosition, '\x2261', Color.Gray, Color.Black);
					boardThere.SetTile(there.YPosition, there.XPosition, '\x2261', Color.Gray, Color.Black);

					Console.WriteLine("Connected {0} -- {1}.", boardHere.ID, boardThere.ID);

					connected.Add(boardHere);
					connected.Add(boardThere);
				}
			}

			// Step 4 - place sick lewt in goalBoard
			var treasureX = 0;
			var treasureY = 0;
			while (true)
			{
				treasureX = Random.Next(1, 79);
				treasureY = Random.Next(1, 24);

				var sides = 0;
				if (goalBoard.IsSolid(treasureY - 1, treasureX))
					sides++;
				if (goalBoard.IsSolid(treasureY + 1, treasureX))
					sides++;
				if (goalBoard.IsSolid(treasureY, treasureX - 1))
					sides++;
				if (goalBoard.IsSolid(treasureY, treasureX + 1))
					sides++;
				if (sides < 3 && sides > 1 && goalBoard.Warps.FirstOrDefault(w => w.XPosition == treasureX && w.YPosition == treasureY) == null)
					break;
			}
			var treasure = InventoryItem.RollContainer(null, "dungeontreasure");
			var treasureChest = new Container("Treasure chest", treasure)
			{
				AsciiChar = (char)0x00C6,
				XPosition = treasureX,
				YPosition = treasureY,
				ForegroundColor = Color.FromName("SaddleBrown"),
				BackgroundColor = Color.Black,
				ParentBoard = goalBoard,
				Blocking = false,
			};
			goalBoard.Entities.Add(treasureChest);

			if (forTravel)
			{
				originalExit.TargetBoard = -2; //causes Travel menu to appear on use.
				return entranceBoard;
			}

			var entrance = nox.CurrentBoard.Warps.Find(w => w.ID == DungeonGeneratorEntranceWarpID);
			entrance.TargetBoard = entranceBoard.BoardNum; //should be this one.
			entrance.TargetWarpID = originalExit.ID;
			originalExit.TargetBoard = nox.CurrentBoard.BoardNum;
			originalExit.TargetWarpID = entrance.ID;

			nox.CurrentBoard.EntitiesToRemove.Add(nox.Player);
			nox.CurrentBoard = entranceBoard;
			nox.Player.ParentBoard = entranceBoard;
			entranceBoard.Entities.Add(nox.Player);
			nox.Player.XPosition = originalExit.XPosition;
			nox.Player.YPosition = originalExit.YPosition;
			entranceBoard.Redraw();
			NoxicoGame.Sound.PlayMusic(entranceBoard.Music);
			NoxicoGame.Immediate = true;
			NoxicoGame.Mode = UserMode.Walkabout;
			NoxicoGame.HostForm.Noxico.SaveGame();

			return entranceBoard;
		}
	}

	public class BiomeData
	{
		public static List<BiomeData> Biomes;

		public static void LoadBiomes()
		{
			Biomes = new List<BiomeData>();
			var x = Mix.GetXMLDocument("biomes.xml");
			var realm = x.SelectSingleNode("//realm") as XmlElement;
			foreach (var b in realm.SelectNodes("biome").OfType<XmlElement>())
				Biomes.Add(BiomeData.FromXML(b));
		}

		public string Name { get; private set; }
		public Color Color { get; private set; }
		public System.Drawing.Rectangle Rect { get; private set; }
		public string Music { get; private set; }
		public bool IsWater { get; private set; }
		public bool CanBurn { get; private set; }
		public char[] GroundGlyphs { get; private set; }
		public double DarkenPlus { get; private set; }
		public double DarkenDiv { get; private set; }
		public int MaxEncounters { get; private set; }
		public string[] Encounters { get; private set; }
		public string[] Cultures { get; private set; }

		public static BiomeData FromXML(XmlElement x)
		{
			var n = new BiomeData();
			n.Name = x.GetAttribute("name");
			var cvars = x.GetAttribute("rect").Split(' ').Select(i => int.Parse(i)).ToArray();
			n.Rect = new System.Drawing.Rectangle(cvars[0], cvars[1], cvars[2] - cvars[0], cvars[3] - cvars[1]);
			cvars = x.GetAttribute("color").Split(' ').Select(i => int.Parse(i)).ToArray();
			n.Color = System.Drawing.Color.FromArgb(cvars[0], cvars[1], cvars[2]);
			n.Music = x.GetAttribute("music");
			n.IsWater = x.HasAttribute("isWater");
			n.CanBurn = x.HasAttribute("canBurn");

			var groundGlyphs = x.SelectSingleNode("groundGlyphs");
			if (groundGlyphs == null)
				n.GroundGlyphs = new[] { ',', '\'', '`', '.', };
			else
				n.GroundGlyphs = ((XmlElement)groundGlyphs).InnerText.ToCharArray();
			var darken = x.SelectSingleNode("darken");
			if (darken == null)
			{
				n.DarkenPlus = 2;
				n.DarkenDiv = 2;
			}
			else
			{
				n.DarkenPlus = double.Parse(((XmlElement)darken).GetAttribute("plus"), System.Globalization.CultureInfo.InvariantCulture);
				n.DarkenDiv = double.Parse(((XmlElement)darken).GetAttribute("div"), System.Globalization.CultureInfo.InvariantCulture);
			}

			var encounters = x.SelectSingleNode("encounters");
			if (encounters == null)
				n.Encounters = new string[0];
			else
			{
				n.MaxEncounters = 10;
				if (((XmlElement)encounters).HasAttribute("max"))
					n.MaxEncounters = int.Parse(((XmlElement)encounters).GetAttribute("max"));
				n.Encounters = encounters.InnerText.Split(',').Select(e => e.Trim()).ToArray();
			}

			var cultures = x.SelectSingleNode("cultures");
			if (cultures == null)
				n.Cultures = new[] { "human" };
			else
			{
				n.Cultures = cultures.InnerText.Split(',').Select(e => e.Trim()).Where(e => Culture.Cultures.ContainsKey(e)).ToArray();
			}

			return n;
		}
	}
}
