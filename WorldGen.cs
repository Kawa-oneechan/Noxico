using System;
using System.Collections.Generic;
using System.Linq;

namespace Noxico
{
	public static class DungeonGenerator
	{
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
				var attempts = 0;
				var minSides = 1;
				while (true)
				{
					attempts++;
					if (attempts == 10)
						minSides = 0;

					eX = Random.Next(1, b.Width - 1);
					eY = Random.Next(1, b.Height - 1);

					//2013-03-07: prevent placing warps on same tile as clutter
					//<Ragath> Kawa, this is bad
					//<Ragath> that should be a .Any() call
					//if (b.Entities.FirstOrDefault(e => e.XPosition == eX && e.YPosition == eY) != null)
					if (b.Entities.Any(e => e.XPosition == eX && e.YPosition == eY))
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
					if (sides < 3 && sides >= minSides)
						break;
				}
				return new Warp() { XPosition = eX, YPosition = eY };
			};

			Warp originalExit = null;

			BiomeData.LoadBiomes();
			var biomeData = BiomeData.Biomes[DungeonGeneratorBiome];

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

			var boardWidths = new[] { 80, 80, 80, 40, 160, 120 };
			var boardHeights = new[] { 50, 50, 50, 25, 100, 75 };

			for (var i = 0; i < depth; i++)
			{
				levels.Add(new List<Board>());
				var length = Random.Next(2, 5);
				for (var j = 0; j < length; j++)
				{
					var board = new Board(boardWidths.PickOne(), boardHeights.PickOne());
					board.AllowTravel = false;
					board.Clear(DungeonGeneratorBiome);
					board.BoardNum = nox.Boards.Count;
					board.Coordinate = nox.Player.ParentBoard.Coordinate;
					if (i > 0)
						board.AddToken("dark");
					nox.Boards.Add(board);
					levels[i].Add(board);
				}
			}

			//Decide which boards are the exit and goal
			var entranceBoard = levels[0].PickOne();
			var goalBoard = levels[levels.Count - 1].PickOne();

			//Generate content for each board
			for (var i = 0; i < levels.Count; i++)
			{
				for (var j = 0; j < levels[i].Count; j++)
				{
					var board = levels[i][j];

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
					if (!name.IsBlank())
						board.Name = string.Format("{0}, level {1}-{2}", name, i + 1, (char)('A' + j));
					board.ID = string.Format("Dng_{0}_{1}{2}", DungeonGeneratorEntranceBoardNum, i + 1, (char)('A' + j));
					board.BoardType = BoardType.Dungeon;
					board.Music = "set://Dungeon";
					var encounters = board.GetToken("encounters");
					foreach (var e in biomeData.Encounters)
						encounters.AddToken(e);
					encounters.Value = biomeData.MaxEncounters;
					encounters.GetToken("stock").Value = encounters.Value * Random.Next(3, 5);
					board.RespawnEncounters();

					//If this is the entrance board, add a warp back to the Overworld.
					if (board == entranceBoard)
					{
						var exit = findWarpSpot(board);
						originalExit = exit;
						exit.ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_Exit";
						board.Warps.Add(exit);
						board.SetTile(exit.YPosition, exit.XPosition, "dungeonExit"); //board.SetTile(exit.YPosition, exit.XPosition, '<', Color.Silver, Color.Black);
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
				boardHere.SetTile(here.YPosition, here.XPosition, up ? "dungeonUpstairs" : "dungeonDownstairs"); //boardHere.SetTile(here.YPosition, here.XPosition, up ? '<' : '>', Color.Gray, Color.Black);
				boardThere.SetTile(there.YPosition, there.XPosition, up ? "dungeonDownstairs" : "dungeonUpstairs"); //boardThere.SetTile(there.YPosition, there.XPosition, !up ? '<' : '>', Color.Gray, Color.Black);

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
					boardHere.SetTile(here.YPosition, here.XPosition, "dungeonSideexit"); //boardHere.SetTile(here.YPosition, here.XPosition, '\x2261', Color.Gray, Color.Black);
					boardThere.SetTile(there.YPosition, there.XPosition, "dungeonSideexit"); //boardThere.SetTile(there.YPosition, there.XPosition, '\x2261', Color.Gray, Color.Black);

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
				treasureX = Random.Next(1, goalBoard.Width - 1);
				treasureY = Random.Next(1, goalBoard.Height - 1);

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
			var treasure = DungeonGenerator.GetRandomLoot("container", "dungeon_chest", new Dictionary<string, string>()
			{
				{ "biome", BiomeData.Biomes[DungeonGenerator.DungeonGeneratorBiome].Name.ToLowerInvariant() },
			});
			var treasureChest = new Container(i18n.GetString("treasurechest"), treasure)
			{
				Glyph = 0x14A,
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
			entranceBoard.UpdateLightmap(nox.Player, true);
			entranceBoard.Redraw();
			entranceBoard.PlayMusic();
			NoxicoGame.Immediate = true;
			NoxicoGame.Mode = UserMode.Walkabout;
			NoxicoGame.Me.SaveGame();

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

				//if we're looking for lootsets applying to a particular character, toss any non-character-specific potentials.
				if (setsFilters != null && setsFilters.HasToken("id"))
					lootsets.RemoveAll(set => !set.GetToken("filter").HasToken("id"));

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
					var co = cols.PickOne();
					cols.Remove(co);
					pal.Add(co);
				}
				return pal;
			});

			var colors = getPal("black,gray,white,red,blue,green,navy,maroon,pink,yellow");
			var color = -1;

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
							possibilities.Add(new Token(items.PickOne().ID));
					}
					else if (option.Contains('-') || option.Contains('+'))
					{
						//complicated token check
						var fuckery = option.Replace("+", ",").Replace("-", ",-").Split(',');
						foreach (var knownItem in NoxicoGame.KnownItems.Where(i => !i.HasToken("unique")))
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
									newPoss.AddToken("color", 0, colors[color == -1 ? Random.Next(colors.Count) : color]);
								ApplyBonusMaybe(newPoss, knownItem);
								possibilities.Add(newPoss);
							}
						}
					}
					else
					{
						//simple token check
						var items = NoxicoGame.KnownItems.Where(i => i.HasToken(option) && !i.HasToken("unique")).ToList();
						if (items.Count > 0)
						{
							var knownItem = items.PickOne();
							var newPoss = new Token(knownItem.ID);
							if (knownItem.HasToken("colored"))
								newPoss.AddToken("color", 0, colors[color == -1 ? Random.Next(colors.Count) : color]);
							if (knownItem.ID == "book")
								newPoss.AddToken("id", NoxicoGame.BookTitles.Keys.ToArray().PickOne());
							possibilities.Add(newPoss);
						}
					}
					if (possibilities.Count > 0)
						return possibilities.PickOne();
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
							newPoss.AddToken("color", 0, colors[color == -1 ? Random.Next(colors.Count) : color]);
						return newPoss;
					}
				}
				return null;
			});

			var loot = new List<Token>();
			var lootsets = GetLoots(target, type, filters);
			if (lootsets.Count == 0)
				return loot;
			var lootset = lootsets.PickOne();
			if (!lootset.HasToken("someof") && !lootset.HasToken("oneof") && !lootset.HasToken("oneofeach"))
				return loot;
			foreach (var of in lootset.Tokens)
			{
				var options = new List<string>();
				var min = 1;
				var max = 1;
				color = -1;
				if (of.Name == "colors")
				{
					colors = getPal(string.Join(",", of.Tokens.Select(x => x.Name).ToList()));
					continue;
				}
				else if (of.Name == "oneof")
				{
					options = of.Tokens.Select(x => x.Name).Where(x => x[0] != '$').ToList();
					color = of.HasToken("$color") ? (int)of.GetToken("$color").Value - 1 : -1;
				}
				else if (of.Name == "someof")
				{
					options = of.Tokens.Select(x => x.Name).Where(x => x[0] != '$').ToList();
					color = of.HasToken("$color") ? (int)of.GetToken("$color").Value - 1 : -1;
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
					var option = options.PickOne();
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

	public class BiomeData
	{
		public static List<BiomeData> Biomes;
		public static List<int> WaterLevels;
		public static List<Fluids> WaterTypes;

		public static void LoadBiomes()
		{
			Biomes = new List<BiomeData>();
			WaterLevels = new List<int>();
			WaterTypes = new List<Fluids>();
			var biomeData = Mix.GetTokenTree("biomes.tml");
			var i = 0;
			foreach (var realm in biomeData.Where(x => x.Name == "realm"))
			{
				var waterLevel = (int)realm.GetToken("waterlevel").Value;
				WaterLevels.Add(waterLevel);
				var waterType = (Fluids)Enum.Parse(typeof(Fluids), realm.GetToken("watertype").Text, true);
				WaterTypes.Add(waterType);
				foreach (var biome in realm.Tokens.Where(x => x.Name == "biome"))
					Biomes.Add(BiomeData.FromToken(biome, i));
				i++;
			}
		}

		public Realms Realm { get; private set; }
		public string Name { get; private set; }
		public int GroundTile { get; private set; }
		public int MaxEncounters { get; private set; }
		public string Music { get; private set; }
		public string[] Encounters { get; private set; }
		public string[] Cultures { get; private set; }
		public System.Drawing.Rectangle Rect { get; private set; }

		public static BiomeData FromToken(Token t, int realmNum)
		{
			var n = new BiomeData();
			n.Realm = (Realms)realmNum;
			n.Name = t.Text;

			var cvars = t.GetToken("rect").Text.Split(' ').Select(i => int.Parse(i)).ToArray();
			n.Rect = new System.Drawing.Rectangle(cvars[0], cvars[1], cvars[2] - cvars[0], cvars[3] - cvars[1]);

			n.GroundTile = (int)t.GetToken("ground").Value;
			if (n.GroundTile == 0)
				n.GroundTile = TileDefinition.Find(t.GetToken("ground").Text, true).Index;

			var encounters = t.GetToken("encounters");
			if (encounters == null)
				n.Encounters = new string[0];
			else
			{
				n.MaxEncounters = 10;
				if (encounters.Value > 0)
					n.MaxEncounters = (int)encounters.Value;
				n.Encounters = encounters.Tokens.Select(x => x.Name).ToArray();
			}

			var cultures = t.GetToken("cultures");
			if (cultures == null)
				n.Cultures = new[] { "human" };
			else
				n.Cultures = cultures.Tokens.Select(x => x.Name).Where(e => Culture.Cultures.ContainsKey(e)).ToArray();

			if (t.HasToken("music"))
				n.Music = t.GetToken("music").Text;

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
			if (!initialized)
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
			noise = new float[width, height];
			for (int x = 0; x < width; ++x)
				for (int y = 0; y < height; ++y)
					noise[x, y] = ((float)(Random.NextDouble()) - 0.5f) * 2.0f;
			initialized = true;
		}
	}

	public class WorldMapGenerator
	{
		public const int TileWidth = 80, TileHeight = 50;

		public int[,] RoughBiomeMap, TownMap, DetailedMap;
		public byte[,] WaterMap;
		public Board[,] BoardMap;
		public int MapSizeX, MapSizeY, TownMarkers, WaterBiome;
		public Realms Realm;

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

					v = v.Clamp(0, 1);
					var b = (byte)(v * 255);

					map[row, col] = b;
				}
			}
			return map;
		}

		private byte[,] CreateBiomeMap(int reach, byte[,] height, byte[,] precip, byte[,] temp)
		{
			var cols = (int)Math.Floor(reach / (float)TileWidth) * TileWidth;
			var rows = (int)Math.Floor(reach / (float)TileHeight) * TileHeight;
			var map = new byte[rows, cols];
			for (var row = 0; row < rows; row++)
			{
				for (var col = 0; col < cols; col++)
				{
					var h = height[row * 1, col];
					var p = precip[row * 1, col];
					var t = temp[row * 1, col];
					for (var i = 0; i < BiomeData.Biomes.Count; i++)
					{
						var b = BiomeData.Biomes[i];
						if (b.Realm == Realm && t >= b.Rect.Left && t <= b.Rect.Right && p >= b.Rect.Top && p <= b.Rect.Bottom)
						{
							map[row, col] = (byte)i;
							continue;
						}
					}
				}
			}
			return map;
		}

		private byte[,] CreateWaterMap(int reach, byte[,] height)
		{
			var waterLevel = BiomeData.WaterLevels[(int)Realm];
			var cols = (int)Math.Floor(reach / (float)TileWidth) * TileWidth;
			var rows = (int)Math.Floor(reach / (float)TileHeight) * TileHeight;
			var map = new byte[rows, cols];
			for (var row = 0; row < rows; row++)
			{
				for (var col = 0; col < cols; col++)
				{
					var h = height[row, col];
					if (h < waterLevel)
						map[row, col] = 1;
				}
			}
			return map;
		}

		public void GenerateWorldMap(Realms realm, Action<string, int, int> setStatus)
		{
			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			Program.WriteLine("{0} -- Start worldgen.", stopwatch.Elapsed);
			var demon = realm == Realms.Seradevari;

			Realm = realm;
			var reach = 1200;

			setStatus(i18n.Format("worldgen_heightmap", realm), 0, 0); //"Creating heightmap..."
			var height = CreateHeightMap(reach);
			Program.WriteLine("{0} -- Create heightmap.", stopwatch.Elapsed);
			setStatus(i18n.Format("worldgen_rainmap", realm), 0, 0); //"Creating precipitation map..."
			var precip = CreateClouds(reach, 0.010f, 0.3, false);
			Program.WriteLine("{0} -- Create precipitation map.", stopwatch.Elapsed);
			setStatus(i18n.Format("worldgen_tempmap", realm), 0, 0); //"Creating temperature map..."
			var temp = CreateClouds(reach, 0.005f, 0.5, true);
			Program.WriteLine("{0} -- Create temperature map.", stopwatch.Elapsed);
			setStatus(i18n.Format("worldgen_watermap", realm), 0, 0); //"Creating water map..."
			var water = CreateWaterMap(reach, height);
			Program.WriteLine("{0} -- Create water map.", stopwatch.Elapsed);
			setStatus(i18n.Format("worldgen_biomemap", realm), 0, 0); //"Creating biome map..."
			var biome = CreateBiomeMap(reach, height, precip, temp);
			Program.WriteLine("{0} -- Create biome map.", stopwatch.Elapsed);

			MapSizeX = (int)Math.Floor(reach / (float)TileWidth);
			MapSizeY = (int)Math.Floor(reach / (float)TileHeight);

			var bmpWidth = MapSizeX * TileWidth;
			var bmpHeight = MapSizeY * TileHeight; //reach / 1;
			var bmp = new int[bmpHeight + 1, bmpWidth + 1];
			for (var row = 0; row < bmpHeight; row++)
			{
				setStatus(i18n.Format("worldgen_bitmap", realm), row, bmpHeight); //"Drawing board bitmap..."
				for (var col = 0; col < bmpWidth; col++)
					bmp[row, col] = biome[row, col];
			}
			DetailedMap = bmp;
			Program.WriteLine("{0} -- Draw board bitmap ({1} by {2} px/tiles).", stopwatch.Elapsed, bmpWidth, bmpHeight);

			RoughBiomeMap = new int[MapSizeY, MapSizeX]; //maps to usual biome list
			var oceans = 0;
			//var water = BiomeData.Biomes.IndexOf(BiomeData.Biomes.First(x => x.IsWater && x.Realm == Realm));
			for (var bRow = 0; bRow < MapSizeY; bRow++)
			{
				setStatus(i18n.Format("worldgen_determinebiomes", realm), bRow, MapSizeY); //"Determining biomes..."
				for (var bCol = 0; bCol < MapSizeX; bCol++)
				{
					var counts = new int[256];
					var oceanTreshold = (TileWidth * TileHeight) - 32;
					var waterCount = 0;
					//Count the colors, 1 2 and 3. Everything goes, coming up OOO!
					for (var pRow = 0; pRow < TileHeight; pRow++)
					{
						for (var pCol = 0; pCol < TileWidth; pCol++)
						{
							var b = biome[(bRow * TileHeight) + pRow, (bCol * TileWidth) + pCol];
							var h = water[(bRow * TileHeight) + pRow, (bCol * TileWidth) + pCol];
							if (h > 0)
								waterCount++;
							counts[b]++;
						}
					}
					//Special rule for Oceans
					if (waterCount >= oceanTreshold)
					{
						RoughBiomeMap[bRow, bCol] = -1;
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
			Program.WriteLine("{0} -- Determine biomes ({1} by {2} boards).", stopwatch.Elapsed, MapSizeX, MapSizeY);

			var towns = 0;
			var townBoards = 0;
			var wateringHoles = 0;
			TownMap = new int[MapSizeY, MapSizeX]; //0 - none, -1 - watering hole (town can go nearby), >0 - town
			for (var bRow = 0; bRow < MapSizeY; bRow++)
			{
				setStatus(i18n.Format("worldgen_wateringholes", realm), bRow, MapSizeY); //"Finding watering holes..."
				for (var bCol = 0; bCol < MapSizeX; bCol++)
				{
					//Find a board with a reasonable amount of water
					if (RoughBiomeMap[bRow, bCol] == -1)
						continue;
					var waterAmount = 0;
					var waterMin = (TileWidth * TileHeight) / 4; //1500;
					var waterMax = (TileWidth * TileHeight) / 2; //2500;
					for (var pRow = 0; pRow < TileHeight; pRow++)
						for (var pCol = 0; pCol < TileWidth; pCol++)
							if (water[(bRow * TileHeight) + pRow, (bCol * TileWidth) + pCol] == 1)
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
			Program.WriteLine("{0} -- Build town location map.", stopwatch.Elapsed);

			for (var bRow = 1; bRow < MapSizeY - 1; bRow++)
			{
				setStatus(i18n.Format("worldgen_markingtowns", realm), bRow, MapSizeY); //"Marking possible town locations..."
				for (var bCol = 1; bCol < MapSizeX - 1; bCol++)
				{
					if (TownMap[bRow, bCol] != 0)
					{
						var added = 0;
						for (var row = bRow - 1; row < bRow + 1; row++)
						{
							for (var col = bCol - 1; col < bCol + 1; col++)
							{
								if (TownMap[row, col] != 0)
									continue;
								var waterAmount = 0;
								var waterMax = (TileWidth * TileHeight) / 16; //500;
								for (var pRow = 0; pRow < TileHeight; pRow++)
									for (var pCol = 0; pCol < TileWidth; pCol++)
										if (water[(row * TileHeight) + pRow, (col * TileWidth) + pCol] == 1)
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
			Program.WriteLine("{0} -- Determine actual town locations.", stopwatch.Elapsed);

			//Ensure there's at least one town marker
			if (towns == 0)
			{
				var tx = MapSizeX / 2;
				var ty = MapSizeY / 2;
				if (TownMap[tx, ty] == -1 && RoughBiomeMap[tx, ty] != 0)
					TownMap[tx, ty] = towns;
				else
				{
					while (true)
					{
						tx = Random.Next(MapSizeX - 1);
						ty = Random.Next(MapSizeY - 1);
						if (TownMap[ty, tx] == -1 && RoughBiomeMap[ty, tx] < 255)
						{
							TownMap[ty, tx] = towns;
							break;
						}
					}
				}
				for (var pRow = 0; pRow < TileHeight; pRow++)
					for (var pCol = 0; pCol < TileWidth; pCol++)
						DetailedMap[(ty * TileHeight) + pRow, (tx * TileWidth) + pCol] = RoughBiomeMap[ty, tx];
			}

			TownMarkers = towns;
			//WaterBiome = water;
			WaterMap = water;
			BoardMap = new Board[MapSizeY, MapSizeX];
			Program.WriteLine("{0} -- Done.", stopwatch.Elapsed);
		}
	}
}
