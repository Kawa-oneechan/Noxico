using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Noxico
{
	public static class WorldGen
	{
		/*
		private static TownGenerator townGen;
		private static List<string> vendorTypeList;

		public static Board CreateTown(int biomeID, string cultureName, string name, bool withSurroundings)
		{
			if (townGen == null)
				townGen = new TownGenerator();

			if (biomeID < 0)
				biomeID = Random.Next(2, 5);

			var boards = NoxicoGame.HostForm.Noxico.Boards;
			var thisMap = new Board();
			var biome = BiomeData.Biomes[biomeID];

			var vendorChance = 0.50;
			if (vendorTypeList == null)
			{
				vendorTypeList = new List<string>();
				//var lootData = Mix.GetXmlDocument("loot.xml");
				//var filters = lootData.SelectNodes("//filter[@key=\"vendorclass\"]");
				//foreach (var filter in filters.OfType<XmlElement>())
				var lootData = Mix.GetTokenTree("loot.tml", true);
				foreach (var filter in lootData.Where(t => t.Path("filter/vendorclass") != null).Select(t => t.Path("filter/vendorclass")))
				{
					//var v = filter.GetAttribute("value");
					var v = filter.Text;
					if (!vendorTypeList.Contains(v))
						vendorTypeList.Add(v);
				}
			}
			var vendorTypes = new List<string>();

			if (string.IsNullOrEmpty(cultureName))
				cultureName = biome.Cultures[Random.Next(biome.Cultures.Length)];

			thisMap.Clear(biomeID);
			thisMap.BoardType = BoardType.Town;
			Board.HackishBoardTypeThing = "town";
			thisMap.AddToken("culture", 0, cultureName);

			townGen.Board = thisMap;
			townGen.Culture = Culture.Cultures[cultureName];
			townGen.Create(biome);
			townGen.ToTilemap(ref thisMap.Tilemap);
			townGen.ToSectorMap(thisMap.Sectors);

			if (Random.NextDouble() < vendorChance)
				if (AddVendor(thisMap, vendorTypes))
					vendorChance *= 0.9;

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
				lol.GetToken("biome").Value = biomeID;
				lol.Clear(biomeID);
				lol.BoardType = BoardType.Wild;
				Board.HackishBoardTypeThing = "wild";
				lol.BoardNum = boards.Count;
				lol.Name = thisMap.Name + " Outskirts";
				lol.ID = lol.Name.ToID() + lol.BoardNum;
				if (Random.NextDouble() > 0.5 && biome.Encounters.Length > 0)
				{
					var encounters = lol.GetToken("encounters");
					encounters.Value = biome.MaxEncounters;
					encounters.GetToken("stock").Value = encounters.Value * Random.Next(3, 5);
					foreach (var e in biome.Encounters)
						encounters.AddToken(e);

					lol.AddClutter();

					//Possibility of linked dungeons
					if (Random.NextDouble() < 0.6)
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
					Board.HackishBoardTypeThing = "town";
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

		public static bool AddVendor(Board board, List<string> typeList)
		{
			var unexpected = board.Entities.OfType<BoardChar>().Where(e => !e.Character.HasToken("expectation") && e.Character.Path("role/vendor") == null).ToList();
			if (unexpected.Count == 0)
				return false;
			var vendor = unexpected[0].Character;
			var stock = vendor.GetToken("items");
			if (typeList.Count == 0)
				typeList.AddRange(vendorTypeList);
			var type = typeList[Random.Next(typeList.Count)];
			typeList.Remove(type);
			vendor.RemoveAll("role");
			vendor.AddToken("role").AddToken("vendor").AddToken("class", 0, type);
			vendor.GetToken("money").Value = 1000 + (Random.Next(0, 20) * 50);
			Program.WriteLine("*** {0} is now a vendor ***", vendor.Name.ToString(true));
			unexpected[0].RestockVendor();
			return true;
		}
		*/

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

			new UIPNGBackground(Mix.GetBitmap("makecave.png")).Draw();
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

					//2013-03-07: prevent placing warps on same tile as clutter
					if (b.Entities.FirstOrDefault(e => e.XPosition == eX && e.YPosition == eY) != null)
					{
						Program.WriteLine("Tried to place a warp below an entity -- rerolling...");
						continue;
					}
					
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
					board.AllowTravel = false;
					board.Clear(DungeonGeneratorBiome);
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

				Program.WriteLine("Connected {0} || {1}.", boardHere.ID, boardThere.ID);

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

					Program.WriteLine("Connected {0} -- {1}.", boardHere.ID, boardThere.ID);

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

				//2013-03-07: prevent treasure from spawning inside a wall
				if (goalBoard.IsSolid(treasureY, treasureX))
					continue;
				//2013-03-07: prevent placing warps on same tile as clutter
				if (goalBoard.Entities.FirstOrDefault(e => e.XPosition == treasureX && e.YPosition == treasureY) != null)
				{
					Program.WriteLine("Tried to place cave treasure below an entity -- rerolling...");
					continue;
				}

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
			var treasure = WorldGen.GetRandomLoot("container", "dungeon_chest"); //InventoryItem.RollContainer(null, "dungeontreasure");
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
			NoxicoGame.Immediate = true;
			NoxicoGame.Mode = UserMode.Walkabout;
			NoxicoGame.HostForm.Noxico.SaveGame();

			return entranceBoard;
		}

		private static List<Token> lootDoc;
		public static List<Token> GetLoots(string target, string type, Dictionary<string, string> filters = null)
		{
			if (lootDoc == null)
				lootDoc = Mix.GetTokenTree("loot.tml");
			var lootsets = new List<Token>();
			if (filters == null)
				filters = new Dictionary<string, string>();
			foreach (var potentialSet in lootDoc.Where(t => t.Name == "lootset" && t.GetToken("target").Text == target && t.GetToken("type").Text == type))
			{
				//var setsFilters = potentialSet.SelectNodes("filter").OfType<XmlElement>().ToList();
				var setsFilters = potentialSet.GetToken("filter");
				if (setsFilters != null && setsFilters.Tokens.Count > 0)
				{
					var isOkay = true;
					foreach (var f in setsFilters.Tokens)
					{
						var key = f.Name;
						var value = f.Text;
						if (filters.ContainsKey(key) && ((value[0] != '!' && filters[key] != value) || (value[0] == '!' && filters[key] == value.Substring(1))))
						{
							isOkay = false;
							break;
						}
					}
					if (!isOkay)
						continue;
				}
				lootsets.Add(potentialSet);
				if (potentialSet.HasToken("final"))
					break;
			}
			return lootsets;
		}
		public static List<Token> GetRandomLoot(string target, string type, Dictionary<string, string> filters = null)
		{
			Func<string, List<string>> getPal = new Func<string, List<string>>(c =>
			{
				var pal = new List<string>();
				var cols = new List<string>();
				for (var i = 0; i < 4; i++)
				{
					if (cols.Count == 0)
						cols.AddRange(c.Split(',').Select(x => x.Trim()).ToList());
					var co = cols[Random.Next(cols.Count)];
					cols.Remove(co);
					pal.Add(co);
				}
				return pal;
			});

			var colors = getPal("black,gray,white,red,blue,green,navy,maroon,pink,yellow");
			var color = 0;

			Func<string, Token> parseOption = new Func<string, Token>(option =>
			{
				if (option[0] == '@')
				{
					var possibilities = new List<Token>();
					option = option.Substring(1);
					if (option[0] == '-')
					{
						//simple negatory token check
						var items = NoxicoGame.KnownItems.Where(i => (!i.HasToken(option))).ToList();
						if (items.Count > 0)
							possibilities.Add(new Token(items[Random.Next(items.Count)].ID));
					}
					else if (option.Contains('-') || option.Contains('+'))
					{
						//complicated token check
						var fuckery = option.Replace("+", ",").Replace("-", ",-").Split(',');
						foreach (var knownItem in NoxicoGame.KnownItems)
						{
							var includeThis = false;
							foreach (var fucking in fuckery)
							{
								if (fucking[0] != '-' && knownItem.HasToken(fucking))
									includeThis = true;
								else if (knownItem.HasToken(fucking.Substring(1)))
								{
									includeThis = false;
									break;
								}
							}

							if (includeThis)
							{
								var newPoss = new Token(knownItem.ID);
								if (knownItem.HasToken("colored"))
									newPoss.AddToken("color", 0, colors[color]);
								ApplyBonusMaybe(newPoss, knownItem);
								possibilities.Add(newPoss);
							}
						}
					}
					else
					{
						//simple token check
						var items = NoxicoGame.KnownItems.Where(i => i.HasToken(option)).ToList();
						if (items.Count > 0)
							possibilities.Add(new Token(items[Random.Next(items.Count)].ID));
					}
					if (possibilities.Count > 0)
						return possibilities[Random.Next(possibilities.Count)];
				}
				else
				{
					//direct item ID
					//ascertain existance first, and maybe add a color or BUC state.
					var item = NoxicoGame.KnownItems.FirstOrDefault(i => i.ID == option);
					if (item != null)
					{
						var newPoss = new Token(option);
						if (item.HasToken("colored"))
							newPoss.AddToken("color", 0, colors[color]);
						return newPoss;
					}
				}
				return null;
			});

			var loot = new List<Token>();
			var lootsets = GetLoots(target, type, filters);
			if (lootsets.Count == 0)
				return loot;
			var lootset = lootsets[Random.Next(lootsets.Count)];
			if (!lootset.HasToken("someof") && !lootset.HasToken("oneof") && !lootset.HasToken("oneofeach"))
				return loot;
			foreach (var of in lootset.Tokens)
			{
				var options = new List<string>();
				var min = 1;
				var max = 1;
				color = 0;
				if (of.Name == "colors")
				{
					colors = getPal(string.Join(",", of.Tokens.Select(x => x.Name).ToList()));
					continue;
				}
				else if (of.Name == "oneof")
				{
					options = of.Tokens.Select(x => x.Name).Where(x => x[0] != '$').ToList();
					color = of.HasToken("$color") ? (int)of.GetToken("$color").Value - 1 : 0;
				}
				else if (of.Name == "someof")
				{
					options = of.Tokens.Select(x => x.Name).Where(x => x[0] != '$').ToList();
					if (of.Text != null && of.Text.Contains('-'))
					{
						var minmax = of.Text.Split('-');
						min = int.Parse(minmax[0]);
						max = int.Parse(minmax[1]);
					}
					else
						min = max = (int)of.Value;
				}
				else if (of.Name == "oneofeach")
				{
					foreach (var item in of.Tokens)
					{
						if (item.Name == "$color")
						{
							color = (int)of.GetToken("$color").Value - 1;
							continue;
						}
						var thing = parseOption(item.Name);
						if (thing != null)
							loot.Add(thing);
					}
					continue;
				}
				else
					continue;
				var amount = Random.Next(min, max);
				while (amount > 0)
				{
					var option = options[Random.Next(options.Count)];
					var toAdd = parseOption(option);
					if (toAdd != null)
						loot.Add(toAdd);
					amount--;
				}
			}
			return loot;
		}

		private static void ApplyBonusMaybe(Token possibleItem, InventoryItem knownItem)
		{
			if (knownItem.HasToken("unique"))
				return;
			if (Random.NextDouble() < 0.3)
				return;
			if (knownItem.HasToken("weapon"))
			{
				var bonus = Random.Next(1, 6);
				possibleItem.AddToken("bonus", bonus);
			}
		}
	}

	public class ClutterDefinition
	{
		public Color ForegroundColor { get; private set; }
		public Color BackgroundColor { get; private set; }
		public char Character { get; private set; }
		public int Description { get; private set; }
		public bool CanBurn { get; private set; }
		public bool Fence { get; private set; }
		public bool Wall { get; private set; }
		public bool Noisy { get; private set; }
		public double Chance { get; private set; }

		public static ClutterDefinition FromXml(XmlElement x)
		{
			var n = new ClutterDefinition();
			n.Character = x.GetAttribute("char")[0];
			n.ForegroundColor = Color.FromName(x.GetAttribute("color"));
			n.BackgroundColor = x.HasAttribute("background") ? Color.FromName(x.GetAttribute("background")) : Color.Transparent;
			n.Description = x.HasAttribute("description") ? int.Parse(x.GetAttribute("description")) : 0;
			n.CanBurn = x.HasAttribute("canBurn");
			n.Fence = x.GetAttribute("solid") != "no";
			n.Wall = x.GetAttribute("solid") == "wall";
			n.Noisy = true;
			n.Chance = x.HasAttribute("chance") ? double.Parse(x.GetAttribute("chance")) : 0.02;
			return n;
		}
	}

	public class BiomeData
	{
		public static List<BiomeData> Biomes;
		public static Dictionary<string, int> WaterLevels;

		public static void LoadBiomes()
		{
			Biomes = new List<BiomeData>();
			WaterLevels = new Dictionary<string, int>();
			var x = Mix.GetXmlDocument("biomes.xml");
			foreach (var realm in x.SelectNodes("//realm").OfType<XmlElement>())
			{
				var realmID = realm.GetAttribute("id");
				var waterLevel = int.Parse(realm.GetAttribute("waterLevel"));
				WaterLevels[realmID] = waterLevel;
				foreach (var b in realm.SelectNodes("biome").OfType<XmlElement>())
					Biomes.Add(BiomeData.FromXml(b, realmID));
			}
		}

		public string RealmID { get; private set; }
		public string Name { get; private set; }
		public Color Color { get; private set; }
		public bool IsWater { get; private set; }
		public bool CanBurn { get; private set; }
		public char[] GroundGlyphs { get; private set; }
		public double DarkenPlus { get; private set; }
		public double DarkenDiv { get; private set; }
		public int MaxEncounters { get; private set; }
		public string[] Encounters { get; private set; }
		public string[] Cultures { get; private set; }
		public List<ClutterDefinition> Clutter { get; private set; }
		public System.Drawing.Rectangle Rect { get; private set; }

		public static BiomeData FromXml(XmlElement x, string realmID)
		{
			var n = new BiomeData();
			n.RealmID = realmID;
			n.Name = x.GetAttribute("name");
			n.Color = Color.FromName(x.GetAttribute("color"));
			n.IsWater = x.HasAttribute("isWater");
			n.CanBurn = x.HasAttribute("canBurn");

			var cvars = x.GetAttribute("rect").Split(' ').Select(i => int.Parse(i)).ToArray();
			n.Rect = new System.Drawing.Rectangle(cvars[0], cvars[1], cvars[2] - cvars[0], cvars[3] - cvars[1]);

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

			var clutters = x.SelectSingleNode("clutterdefs");
			if (clutters != null)
			{
				n.Clutter = new List<ClutterDefinition>();
				foreach (var clutter in clutters.ChildNodes.OfType<XmlElement>().Where(c => c.Name == "clutter"))
					n.Clutter.Add(ClutterDefinition.FromXml(clutter));
			}

			return n;
		}

		public static int ByName(string name)
		{
			var b = BiomeData.Biomes.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (b == null)
				return 1;
			return BiomeData.Biomes.IndexOf(b);
		}
	}

	//Stolen from... somewhere.
	public class PerlinNoise
	{
		private int width = 256;
		private int height = 256;
		private float[,] noise;
		private bool initialized = false;

		public PerlinNoise(int width, int height)
		{
			this.width = width;
			this.height = height;
		}

		public float GetRandomHeight(float x, float y, float maxHeight, float frequency, float amplitude, float persistance, int octaves)
		{
			GenerateNoise();
			float FinalValue = 0.0f;
			for (int i = 0; i < octaves; ++i)
			{
				FinalValue += GetSmoothNoise(x * frequency, y * frequency) * amplitude;
				frequency *= 2.0f;
				amplitude *= persistance;
			}
			if (FinalValue < -1.0f)
			{
				FinalValue = -1.0f;
			}
			else if (FinalValue > 1.0f)
			{
				FinalValue = 1.0f;
			}
			return FinalValue * maxHeight;
		}

		private float GetSmoothNoise(float x, float y)
		{
			float fracX = x - (int)x;
			float fracY = y - (int)y;
			int x1 = ((int)x + width) % width;
			int y1 = ((int)y + height) % height;
			//for cool art deco looking images, do +1 for X2 and Y2 instead of -1...
			int x2 = ((int)x + width - 1) % width;
			int y2 = ((int)y + height - 1) % height;
			float FinalValue = 0.0f;
			FinalValue += fracX * fracY * noise[x1, y1];
			FinalValue += fracX * (1 - fracY) * noise[x1, y2];
			FinalValue += (1 - fracX) * fracY * noise[x2, y1];
			FinalValue += (1 - fracX) * (1 - fracY) * noise[x2, y2];
			return FinalValue;
		}

		private void GenerateNoise()
		{
			if (initialized) 
				return;
			noise = new float[width, height];
			for (int x = 0; x < width; ++x)
				for (int y = 0; y < height; ++y)
					noise[x, y] = ((float)(Random.NextDouble()) - 0.5f) * 2.0f;
			initialized = true;
		}
	}

	public class WorldMapGenerator
	{
		public int[,] RoughBiomeMap, TownMap, DetailedMap;
		public Board[,] BoardMap;
		public int MapSizeX, MapSizeY;
		public string Realm;

		private byte[,] CreateHeightMap(int reach)
		{
			var map = new byte[reach, reach];
			var noise = new PerlinNoise(reach, reach);
			var wDiv = 1 / (double)reach;
			var hDiv = 1 / (double)reach;
			var dist = reach / 3;
			var distMod = 1 / (float)dist;

			var pct = reach / 100f;

			for (var row = 0; row < reach; row++)
			{
				for (var col = 0; col < reach; col++)
				{
					var overall = noise.GetRandomHeight(col, row, 1f, 0.02f, 0.65f, 0.4f, 4) + 0.3;
					var rough = noise.GetRandomHeight(col, row, 1f, 0.05f, 0.65f, 0.5f, 8);
					//var extra = noise.GetRandomHeight(col, row, 0.05f, 1f, 1f, 1f, 8);
					//var rough = 0f;
					var extra = 0f;
					var v = (overall + (rough * 0.75) + extra) + 0.3; // + 0.5;
					//var v = noise.GetRandomHeight(col, row, 1f, 0.01f, 0.45f, 0.4f, 4) + 0.5;

					if (row < dist) v *= distMod * row;
					if (col < dist) v *= distMod * col;
					if (row > reach - dist) v *= distMod * (reach - row);
					if (col > reach - dist) v *= distMod * (reach - col);

					if (v < 0) v = 0;
					if (v > 1) v = 1;
					var b = (byte)(v * 255);

					map[row, col] = b;
				}
			}
			return map;
		}

		private byte[,] CreateClouds(int reach, float freq, double offset, bool poles = false)
		{
			var map = new byte[reach, reach];
			var noise = new PerlinNoise(reach, reach);
			var wDiv = 1 / (double)reach;
			var hDiv = 1 / (double)reach;
			var dist = reach / 5;
			var distMod = 1 / (float)dist;

			var pct = reach / 100f;

			for (var row = 0; row < reach; row++)
			{
				for (var col = 0; col < reach; col++)
				{
					var overall = noise.GetRandomHeight(col, row, 1f, freq, 0.45f, 0.8f, 2) + offset;
					var v = overall;

					if (poles)
 					{
						v += 0.04;
						if (row < dist) v -= 2 - ((distMod * row) * 2);
						if (row > reach - dist) v -= 2 - ((distMod * (reach - row)) * 2);
 					}
 
					if (v < 0) v = 0;
					if (v > 1) v = 1;
					var b = (byte)(v * 255);
 
					map[row, col] = b;
				}
			}
			return map;
		}

		private byte[,] CreateBiomeMap(int reach, byte[,] height, byte[,] precip, byte[,] temp)
		{
			var waterLevel = BiomeData.WaterLevels[Realm];
			var water = BiomeData.Biomes.IndexOf(BiomeData.Biomes.First(x => x.IsWater));
			var map = new byte[(reach / 2) + 25, reach + 80];
			for (var row = 0; row < reach / 2; row++)
			{
				for (var col = 0; col < reach; col++)
				{
					var h = height[row * 2, col];
					var p = precip[row * 2, col];
					var t = temp[row * 2, col];
					if (h < waterLevel)
 					{
						map[row, col] = (byte)water;
						continue;
					}
					for (var i = 0; i < BiomeData.Biomes.Count; i++)
					{
						var b = BiomeData.Biomes[i];
						if (b.RealmID == Realm && t >= b.Rect.Left && t <= b.Rect.Right && p >= b.Rect.Top && p <= b.Rect.Bottom)
						{
							map[row, col] = (byte)i;
							continue;
						}
 					}
 				}
 			}
			return map;
		}

		public void GenerateWorldMap(string realm, Action<string> setStatus, string randSeed = "")
		{
			Realm = realm;
			var seed = 0xF00D;
			var reach = 2000;
			if (!string.IsNullOrWhiteSpace(randSeed))
			{
				if (randSeed.StartsWith("0x"))
				{
					randSeed = randSeed.Substring(2);
					if (!int.TryParse(randSeed, System.Globalization.NumberStyles.HexNumber, null, out seed))
					{
						seed = randSeed.GetHashCode();
						setStatus(string.Format("Using hash seed -- \"{0}\" -> 0x{1:X}", randSeed, seed));
					}
					else
						setStatus(string.Format("Using seed 0x{0:X}.", seed));
				}
				else
				{
					if (!int.TryParse(randSeed, out seed))
					{
						seed = randSeed.GetHashCode();
						setStatus(string.Format("Using hash seed -- \"{0}\" -> 0x{1:X}", randSeed, seed));
					}
					else
						setStatus(string.Format("Using seed 0x{0:X}.", seed));
				}
			}

			setStatus("Creating heightmap...");
			var height = CreateHeightMap(reach);
			setStatus("Creating precipitation map...");
			var precip = CreateClouds(reach, 0.010f, 0.3, false);
			setStatus("Creating temperature map...");
			var temp = CreateClouds(reach, 0.005f, 0.5, true);
			setStatus("Creating biome map...");
			var biome = CreateBiomeMap(reach, height, precip, temp);

			setStatus("Drawing board bitmap...");
			var bmpWidth = (int)Math.Floor(reach / 80.0) * 80;
			var bmpHeight = reach / 2;
			var bmp = new int[bmpHeight + 1, bmpWidth + 1];
			for (var row = 0; row < bmpHeight; row++)
				for (var col = 0; col < bmpWidth; col++)
					bmp[row, col] = biome[row, col];
			DetailedMap = bmp;

#if DEBUG
			setStatus("Drawing actual bitmap...");
			var png = new System.Drawing.Bitmap(bmpWidth, bmpHeight);
			for (var row = 0; row < bmpHeight; row++)
				for (var col = 0; col < bmpWidth; col++)
					png.SetPixel(col, row, BiomeData.Biomes[biome[row, col]].Color);
			png.Save("world.png");
#endif

			MapSizeX = reach / 80; //1000 -> 12
			MapSizeY = (reach / 2) / 25; //1000 -> 20
			RoughBiomeMap = new int[MapSizeY, MapSizeX]; //maps to usual biome list
			var oceans = 0;
			setStatus("Determining biomes...");
			for (var bRow = 0; bRow < MapSizeY; bRow++)
			{
				for (var bCol = 0; bCol < MapSizeX; bCol++)
				{
					var counts = new int[255];
					var oceanTreshold = 2000 - 4;
					//Count the colors, 1 2 and 3. Everything goes, coming up OOO!
					for (var pRow = 0; pRow < 25; pRow++)
					{
						for (var pCol = 0; pCol < 80; pCol++)
						{
							var b = biome[(bRow * 25) + pRow, (bCol * 80) + pCol];
							counts[b]++;
						}
					}
					//Special rule for Oceans
					if (counts[0] >= oceanTreshold)
					{
						RoughBiomeMap[bRow, bCol] = 0;
						oceans++;
						continue;
					}
					//Determine most significant non-Ocean biome
					var highestNumber = 0;
					var biggestBiome = 0;
					for (var i = 1; i < counts.Length; i++)
					{
						if (counts[i] > highestNumber)
						{
							highestNumber = counts[i];
							biggestBiome = i;
						}
					}
					RoughBiomeMap[bRow, bCol] = biggestBiome;
				}
			}

			setStatus("Finding watering holes...");
			var towns = 0;
			var townBoards = 0;
			var wateringHoles = 0;
			TownMap = new int[MapSizeY, MapSizeX]; //0 - none, -1 - watering hole (town can go nearby), >0 - town
			for (var bRow = 0; bRow < MapSizeY; bRow++)
			{
				for (var bCol = 0; bCol < MapSizeX; bCol++)
				{
					//Find a board with a reasonable amount of water
					if (RoughBiomeMap[bRow, bCol] == 0)
						continue;
					var waterAmount = 0;
					var waterMin = 128;
					var waterMax = 256;
					for (var pRow = 0; pRow < 25; pRow++)
						for (var pCol = 0; pCol < 80; pCol++)
							if (biome[(bRow * 25) + pRow, (bCol * 80) + pCol] == 0)
								waterAmount++;
					if (waterAmount >= waterMin && waterAmount <= waterMax)
					{
						//Randomly DON'T mark
						if (Random.Flip())
							continue;
						//Seems like a nice place. Mark off.
						TownMap[bRow, bCol] = -1;
						wateringHoles++;
					}
				}
			}

			setStatus("Marking possible town locations...");
			for (var bRow = 1; bRow < MapSizeY - 1; bRow++)
			{
				for (var bCol = 1; bCol < MapSizeX - 1; bCol++)
				{
					if (TownMap[bRow, bCol] == -1)
					{
						var added = 0;
						for (var row = bRow - 1; row < bRow + 1; row++)
						{
							for (var col = bCol - 1; col < bCol + 1; col++)
							{
								if (TownMap[row, col] != 0)
									continue;
								var waterAmount = 0;
								var waterMax = 128;
								for (var pRow = 0; pRow < 25; pRow++)
									for (var pCol = 0; pCol < 80; pCol++)
										if (biome[(row * 25) + pRow, (col * 80) + pCol] == 0)
											waterAmount++;
								if (waterAmount < waterMax)
								{
									TownMap[row, col] = towns + 1;
									townBoards++;
									added++;
								}
							}
						}
						if (added > 0)
							towns++;
					}
				}
			}

			BoardMap = new Board[MapSizeY, MapSizeX];
		}
	}
}
