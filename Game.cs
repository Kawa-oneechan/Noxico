using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;


namespace Noxico
{
	public enum KeyBinding
	{
		Left, Right, Up, Down, Rest, Activate, Items, Interact, Fly, Travel,
		Accept, Back, Pause, Screenshot, TabFocus, ScrollUp, ScrollDown
	}

	public class NoxicoGame
	{
		public int Speed { get; set; }
		public static bool Immediate { get; set; }

		public static string WorldName { get; set; }
		public static IGameHost HostForm { get; private set; }
		public static Dictionary<Keys, bool> KeyMap { get; set; }
		public static Dictionary<Keys, bool> KeyTrg { get; set; }
		public static bool[] Modifiers { get; set; }
		public static Dictionary<Keys, DateTime> KeyRepeat { get; set; }
		public static char LastPress { get; set; }
		public static bool ScrollWheeled { get; set; }
		public static bool Mono { get; set; }

		public static Dictionary<KeyBinding, Keys> KeyBindings { get; private set; }
		public static Dictionary<KeyBinding, string> RawBindings { get; private set; }

		public static List<InventoryItem> KnownItems { get; private set; }
		public List<Board> Boards { get; private set; }
		public Board CurrentBoard { get; set; }
		public static Board Limbo { get; private set; }
		public Player Player { get; set; }
		public static List<string> BookTitles { get; private set; }
		public static List<string> BookAuthors { get; private set; }
		public static List<string> Messages { get; private set; }
		public static UserMode Mode { get; set; }
		public static Cursor Cursor { get; set; }
		public static SubscreenFunc Subscreen { get; set; }
		public static string[] TileDescriptions { get; private set; }
		public static Dictionary<string, string> BodyplanHashes { get; private set; }
		public static string SavePath { get; private set; }
		public static bool InGame { get; set; }
		public static string ContextMessage { get; set; }

		public static int StartingOWX = -1, StartingOWY;
		private DateTime lastUpdate;
		public string[] Potions;
		public static List<string> Identifications;
		public static Dictionary<int, string> TravelTargets;
		public static NoxicanDate InGameTime;
		public static bool PlayerReady { get; set; }

		private static List<string> messageLog = new List<string>();
		private static string lastMessage = "";
		public static int WorldVersion { get; private set; }

		public static int Updates = 0;

		public static bool IsKeyDown(KeyBinding binding)
		{
			if (KeyBindings[binding] == 0)
				return false;
			return (NoxicoGame.KeyMap[KeyBindings[binding]]);
		}

		public void Initialize(IGameHost hostForm)
		{
			Program.WriteLine("IT BEGINS...");

			Random.Reseed();

			KeyBindings = new Dictionary<KeyBinding, Keys>();
			RawBindings = new Dictionary<KeyBinding,string>();
			var keyNames = Enum.GetNames(typeof(Keys)).Select(x => x.ToUpperInvariant());
			//Keep this array in synch with KeyBinding.
			var defaults = new[]
			{
				Keys.Left, Keys.Right, Keys.Up, Keys.Down,
				Keys.OemPeriod, Keys.Enter, Keys.OemQuotes, Keys.OemQuestion,
				Keys.Oemcomma, Keys.OemSemicolon, Keys.Enter, Keys.Escape,
				Keys.F1, Keys.F12, Keys.Tab,
				Keys.Up, Keys.Down
			};
			for (var i = 0; i < defaults.Length; i++)
			{
				var iniKey = ((KeyBinding)i).ToString().ToLowerInvariant();
				var iniValue = IniFile.GetValue("keymap", iniKey, Enum.GetName(typeof(Keys), defaults[i])).ToUpperInvariant();
				if (keyNames.Contains(iniValue))
				{
					KeyBindings[(KeyBinding)i] = (Keys)Enum.Parse(typeof(Keys), iniValue, true);
				}
				else
				{
					//Try unfriendly name
					iniValue = "OEM" + iniValue;
					if (keyNames.Contains(iniValue))
						KeyBindings[(KeyBinding)i] = (Keys)Enum.Parse(typeof(Keys), iniValue, true);
					else
					{
						//Give up, use default
						KeyBindings[(KeyBinding)i] = defaults[i];
						iniValue = defaults[i].ToString().ToUpperInvariant();
					}
				}
				RawBindings[(KeyBinding)i] = iniValue;
			}

			SavePath = Vista.GetInterestingPath(Vista.SavedGames);
			if (IniFile.GetValue("misc", "vistasaves", true) && SavePath != null)
				SavePath = Path.Combine(SavePath, "Noxico"); //Add a Noxico directory to V/7's Saved Games
			else
			{
				SavePath = IniFile.GetValue("misc", "savepath", @"$/Noxico"); //"saves"; //Use <startup>\saves instead
				if (SavePath.StartsWith("$"))
					SavePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + SavePath.Substring(1);
				SavePath = Path.GetFullPath(SavePath);
			}

			WorldName = RollWorldName();
			if (!Directory.Exists(SavePath))
				Directory.CreateDirectory(SavePath);

			lastUpdate = DateTime.Now;
			Speed = 60;
			this.Boards = new List<Board>();
			HostForm = hostForm;
			KeyMap = new Dictionary<Keys, bool>();
			KeyTrg = new Dictionary<Keys, bool>();
			KeyRepeat = new Dictionary<Keys,DateTime>();
			for (var i = 0; i < 255; i++)
			{
				KeyMap.Add((Keys)i, false);
				KeyTrg.Add((Keys)i, false);
				KeyRepeat.Add((Keys)i, DateTime.Now);
			}
			Modifiers = new bool[3];
			Cursor = new Cursor();
			Messages = new List<string>(); //new List<StatusMessage>();

			Program.WriteLine("Loading bodyplans...");
			BodyplanHashes = new Dictionary<string, string>();
			var plans = Mix.GetTokenTree("bodyplans.tml");
			foreach (var bodyPlan in plans.Where(t => t.Name == "bodyplan"))
			{
				var id = bodyPlan.Text;
				var plan = bodyPlan.Tokens;
				Toolkit.VerifyBodyplan(bodyPlan, id);
				if (bodyPlan.HasToken("beast"))
					continue;
				BodyplanHashes.Add(id, Toolkit.GetBodyComparisonHash(bodyPlan));
			}

			Program.WriteLine("Loading items...");
			Identifications = new List<string>();

			var items = Mix.GetTokenTree("items.tml");
			KnownItems = new List<InventoryItem>();
			foreach (var item in items.Where(t => t.Name == "item"))
				KnownItems.Add(InventoryItem.FromToken(item));

			Program.WriteLine("Randomizing potions and rings...");
			RollPotions();
			ApplyRandomPotions();

			TileDescriptions = Mix.GetString("TileSpecialDescriptions.txt").Split('\n');

			Program.WriteLine("Loading books...");
			BookTitles = new List<string>();
			BookAuthors = new List<string>();
			BookTitles.Add("[null]");
			BookAuthors.Add("[null]");
			var xDoc = Mix.GetXmlDocument("books.xml");
			var books = xDoc.SelectNodes("//book");
			foreach (var b in books.OfType<XmlElement>())
			{
				BookTitles.Add(b.GetAttribute("title"));
				BookAuthors.Add(b.HasAttribute("author") ? b.GetAttribute("author") : "an unknown author");
			}

			//ScriptVariables.Add("consumed", 0);
			JavaScript.MainMachine = JavaScript.Create();

			BiomeData.LoadBiomes();
			Limbo = Board.CreateBasicOverworldBoard(BiomeData.ByName("nether"), "Limbo", "Limbo");
			Limbo.BoardType = BoardType.Special;

			InGameTime = new NoxicanDate(740 + Random.Next(0, 20), 6, 26, DateTime.Now.Hour, 0, 0);
			TravelTargets = new Dictionary<int, string>();

			CurrentBoard = new Board();
			this.Player = new Player();
			Introduction.Title();

			/*
			var dungen = new StoneDungeonGenerator();
			var board = new Board();
			dungen.Board = board;
			dungen.Create(BiomeData.Biomes[2]);
			dungen.ToTilemap(ref board.Tilemap);
			board.DumpToHtml("lol");
			Application.Exit();
			*/

			/*
			var pervA = new BoardChar(Character.Generate("human", Gender.Male));
			var pervB = new BoardChar(Character.Generate("goblin", Gender.Male));
			SexManager.Engage(pervA, pervB);
			var results = SexManager.GetPossibilities(pervA, pervB);
			var result = SexManager.GetResult("pin_down", pervA, pervB);
			SexManager.Apply(result, pervA, pervB, new Action<string>(x => Console.WriteLine("--> {0}", x)));
			result = SexManager.GetResult("struggle", pervB, pervA);
			SexManager.Apply(result, pervB, pervA, new Action<string>(x => Console.WriteLine("--> {0}", x)));
			*/
		}

		public void SaveGame(bool noPlayer = false, bool force = false, bool clear = true)
		{
			if (!InGame && !force)
				return;

			if (clear)
				HostForm.Clear();
			HostForm.Write(" -- Saving... -- ", Color.White, Color.Black);
			HostForm.Draw();

			if (!noPlayer && !Player.Character.HasToken("gameover"))
			{
				Program.WriteLine("Saving player...");
				var pfile = File.Open(Path.Combine(SavePath, WorldName, "player.bin"), FileMode.Create);
				var pbin = new BinaryWriter(pfile);
				Player.SaveToFile(pbin);
				pbin.Flush();
				pfile.Flush();
				pfile.Close();
			}

			Program.WriteLine("--------------------------");
			Program.WriteLine("Saving globals...");
			var global = Path.Combine(SavePath, WorldName, "global.bin");
			Program.WriteLine("Location will be {0}", global);
			using (var f = File.Open(global, FileMode.Create))
			{
				var b = new BinaryWriter(f);
				Program.WriteLine("Header...");
				b.Write(Encoding.UTF8.GetBytes("NOXiCO"));
				Program.WriteLine("Potion check...");
				if (Potions[0] == null)
					RollPotions();
				b = new BinaryWriter(new CryptStream(f));
				Program.WriteLine("Player data...");
				Toolkit.SaveExpectation(b, "PLAY");
				b.Write(CurrentBoard.BoardNum);
				b.Write(Boards.Count);
				Program.WriteLine("Potions...");
				Toolkit.SaveExpectation(b, "POTI");
				for (var i = 0; i < 256; i++)
					b.Write(Potions[i] ?? "...");
				Program.WriteLine("Item identification states...");
				Toolkit.SaveExpectation(b, "ITID");
				b.Write(Identifications.Count);
				Identifications.ForEach(x => b.Write(x));
				Program.WriteLine("Unique Items counter lol...");
				Toolkit.SaveExpectation(b, "UNIQ");
				b.Write(0);
				Toolkit.SaveExpectation(b, "TIME");
				b.Write(InGameTime.ToBinary());
				Toolkit.SaveExpectation(b, "TARG");
				b.Write(TravelTargets.Count);
				foreach (var target in TravelTargets)
				{
					b.Write(target.Key);
					b.Write(target.Value);
				}
			}

			Program.WriteLine("--------------------------");
			Program.WriteLine("Saving World...");

			for (var i = 0; i < Boards.Count; i++)
			{
				if (Boards[i] != null)
				{
					Boards[i].SaveToFile(i);
					Boards[i] = null;
				}
			}
			if (!string.IsNullOrEmpty(CurrentBoard.Name))
				CurrentBoard.SaveToFile(CurrentBoard.BoardNum);

			var verCheck = Path.Combine(SavePath, WorldName, "version");
			File.WriteAllText(verCheck, "18");
			Program.WriteLine("Done.");
			Program.WriteLine("--------------------------");
		}

		public void LoadGame()
		{
			var verCheck = Path.Combine(SavePath, WorldName, "version");
			if (!File.Exists(verCheck))
				throw new Exception("Tried to open an old worldsave.");
			WorldVersion = int.Parse(File.ReadAllText(verCheck));
			if (WorldVersion < 18)
				throw new Exception("Tried to open an old worldsave.");

			HostForm.Clear();
			HostForm.Write(" -- Loading... -- ", Color.White, Color.Black);
			HostForm.Draw();

			var playerFile = Path.Combine(SavePath, WorldName, "player.bin");
			if (File.Exists(playerFile))
			{
				var pfile = File.Open(playerFile, FileMode.Open);
				var pbin = new BinaryReader(pfile);
				Player = Player.LoadFromFile(pbin);
				Player.AdjustView();
				pfile.Close();
			}

			var global = Path.Combine(SavePath, WorldName, "global.bin");
			var file = File.Open(global, FileMode.Open);
			var bin = new BinaryReader(file);
			var header = bin.ReadBytes(6);
			if (Encoding.UTF8.GetString(header) != "NOXiCO")
			{
				MessageBox.Notice("Invalid world header.");
				return;
			}
			var crypt = new CryptStream(file);
			bin = new BinaryReader(crypt);
			Toolkit.ExpectFromFile(bin, "PLAY", "player position");
			var currentIndex = bin.ReadInt32();
			var boardCount = bin.ReadInt32();
			Toolkit.ExpectFromFile(bin, "POTI", "potion and ring");
			Potions = new string[256];
			for (var i = 0; i < 256; i++)
				Potions[i] = bin.ReadString();
			Toolkit.ExpectFromFile(bin, "ITID", "item identification");
			var numIDs = bin.ReadInt32();
			Identifications.Clear();
			for (var i = 0; i < numIDs; i++)
				Identifications.Add(bin.ReadString());
			Toolkit.ExpectFromFile(bin, "UNIQ", "unique item tracking");
			var numUniques = bin.ReadInt32();
			Toolkit.ExpectFromFile(bin, "TIME", "ingame time");
			InGameTime = new NoxicanDate(bin.ReadInt64());
			Toolkit.ExpectFromFile(bin, "TARG", "known targets list");
			var numTargets = bin.ReadInt32();
			TravelTargets = new Dictionary<int, string>();
			for (var i = 0; i < numTargets; i++)
				TravelTargets.Add(bin.ReadInt32(), bin.ReadString());
			ApplyRandomPotions();
			file.Close();

			Boards = new List<Board>(boardCount);
			for (int i = 0; i < boardCount; i++)
				Boards.Add(null);

			file.Close();

			InGame = true;

			if (File.Exists(playerFile))
			{
				GetBoard(currentIndex);
				CurrentBoard = Boards[currentIndex];
				CurrentBoard.Entities.Add(Player);
				Player.ParentBoard = CurrentBoard;
				CurrentBoard.LoadSurroundings();
				CurrentBoard.CheckCombatStart();
				CurrentBoard.Redraw();

				if (!Player.Character.HasToken("player"))
					Player.Character.AddToken("player", (int)DateTime.Now.Ticks);
				Player.Character.RecalculateStatBonuses();
				Player.Character.CheckHasteSlow();
			}
		}

		public Board GetBoard(int index)
		{
			if (index == -1 || index >= Boards.Count)
				return NoxicoGame.Limbo;

			if (Boards[index] == null)
			{
				Program.WriteLine("Requested board #{0}. Loading...", index);
				Boards[index] = Board.LoadFromFile(index);
				Boards[index].BoardNum = index;
			}
			return Boards[index];
		}

		public static void DrawMessages()
		{
			for (var i = 50; i < 60; i++)
				for (var col = 0; col < 80; col++)
					HostForm.SetCell(i, col, ' ', Color.Silver, Color.Black);

			if (Messages.Count == 0)
				return;
			var row = 59;
			for (var i = 0; i < 5 && i < Messages.Count; i++)
			{
				var m = Messages.Count - 1 - i;
				//var c = Messages[m].Color;
				//if (c.Lightness < 0.2)
				//	c = Toolkit.Lerp(c, Color.White, 0.5);
				HostForm.Write(Messages[m], Color.Silver, Color.Black, row, 1);
				row--;
			}
		}
		public static void ClearMessages()
		{
			Messages.Clear();
		}
		public static void AddMessage(string message, Color color)
		{
			if (lastMessage != message)
			{
				lastMessage = message;
				if (color.Lightness < 0.2)
					color = Color.Gray;
				var lastLine = Messages.LastOrDefault();
				if (lastLine == null)
					lastLine = "";
				else
					Messages.Remove(lastLine);
				var newLines = (lastLine + "  <c" + color.Name + ">" + message).Wordwrap(78).Trim().Split('\n');
				if (newLines.Length > 1)
				{
					for (var i = 1; i < newLines.Length; i++)
						newLines[i] = "<c" + color.Name + ">" + newLines[i];
				}
				Messages.AddRange(newLines);
				//Messages.Add(new StatusMessage() { Message = message, Color = color });
				if (Mode == UserMode.Walkabout)
					messageLog.Add(InGameTime.ToShortTimeString() + " -- " + message);
			}
			DrawMessages();
		}
		public static void AddMessage(string message)
		{
			AddMessage(message, Color.Silver);
		}

		public static void ShowMessageLog()
		{
			if (messageLog.Count == 0)
				MessageBox.Notice("There are no messages to display.", true);
			else
				TextScroller.Plain(string.Join("\n", messageLog.Where(m => !m.StartsWith("\uE2FD"))));
		}

		public void FlushDungeons()
		{
			Program.WriteLine("Flushing dungeons...");
			for (var i = 0; i < Boards.Count; i++)
			{
				var board = Boards[i];
				if (board == null)
					continue;
				if (board == CurrentBoard)
					continue; //Shouldn't have to happen.
				if (board.GetToken("type").Value == (float)BoardType.Dungeon)
					board.Flush();
			}
		}

		public void Update()
		{
			if (Mode != UserMode.Subscreen)
			{
				if (Mode == UserMode.Walkabout)
				{
					Subscreens.PreviousScreen.Clear();
					if (HostForm.Cursor.X >= 0)
						HostForm.Cursor = new Point(-1, -1);
					var timeNow = DateTime.Now;
					//while ((DateTime.Now - timeNow).Milliseconds < (Immediate ? 1 : Speed)) ;
					//if ((timeNow - lastUpdate).Milliseconds >= Speed)
					{
						lastUpdate = timeNow;
						CurrentBoard.Update();

						for (var i = 0; i < Boards.Count; i++)
						{
							var board = Boards[i];
							if (board == null)
								continue;
							if (board.HasToken("dungeon"))
								continue; //Don't autoflush dungeons. Use FlushDungeons() for that.
							board.Lifetime++;
							if (board.Lifetime == 1000)
								board.Flush();
						}
					}
				}
				CurrentBoard.Draw();
				//UpdateMessages();
				DrawMessages();
				DrawSidebar();

				if (Mode == UserMode.Aiming)
				{
					var timeNow = DateTime.Now;
					//while ((DateTime.Now - timeNow).Milliseconds < (Immediate ? 1 : Speed)) ;
					if ((timeNow - lastUpdate).Milliseconds >= Speed)
					{
						lastUpdate = timeNow;
						Cursor.Update();
					}
					Cursor.Draw();
				}
			}
			else
			{
				if (Subscreen != null)
					Subscreen();
				else
					Mode = UserMode.Walkabout;
			}

			HostForm.Draw();
			Immediate = false;
			for (int i = 0; i < 255; i++)
				KeyTrg[(Keys)i] = false;
			if (ScrollWheeled)
			{
				KeyMap[KeyBindings[KeyBinding.ScrollUp]] = false;
				KeyMap[KeyBindings[KeyBinding.ScrollDown]] = false;
				ScrollWheeled = false;
			}
			Vista.UpdateGamepad();
			Updates++;
		}

		public static void ClearKeys()
		{
			for (var i = 0; i < 255; i++)
			{
				KeyMap[(Keys)i] = false;
				KeyTrg[(Keys)i] = false;
				KeyRepeat[(Keys)i] = DateTime.Now;
			}
			Vista.ReleaseTriggers();
		}

		public void CreateRealm()
		{
			var line = 0;
			var setStatus = new Action<string>(s =>
			{
				Program.WriteLine(s);
				HostForm.Write(s, Color.White, Color.Black, line, 0);
				HostForm.Draw();
				line++;
			});

			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			HostForm.Clear();

			var generator = new WorldMapGenerator();
			generator.GenerateWorldMap("Nox", setStatus, "pandora");
			setStatus("Creating boards...");
			for (var y = 0; y < generator.MapSizeY - 1; y++)
			{
				for (var x = 0; x < generator.MapSizeX - 1; x++)
				{
					if (generator.RoughBiomeMap[y, x] == generator.WaterBiome)
						continue;
					var newBoard = new Board();
					newBoard.Coordinate = new Point(x, y);
					newBoard.BoardNum = this.Boards.Count;
					generator.BoardMap[y, x] = newBoard;
					if (x > 0)
						newBoard.Connect(Direction.West, generator.BoardMap[y, x - 1]);
					if (x < generator.MapSizeX - 1)
						newBoard.Connect(Direction.East, generator.BoardMap[y, x + 1]);
					if (y > 0)
						newBoard.Connect(Direction.North, generator.BoardMap[y - 1, x]);
					if (y < generator.MapSizeY - 1)
						newBoard.Connect(Direction.South, generator.BoardMap[y + 1, x]);
					newBoard.ClearToWorld(generator);
					newBoard.AddClutter();
					var biome = BiomeData.Biomes[generator.RoughBiomeMap[y, x]];
					if (biome.Encounters.Length > 0)
					{
						var encounters = newBoard.GetToken("encounters");
						encounters.Value = biome.MaxEncounters;
						encounters.GetToken("stock").Value = encounters.Value * Random.Next(3, 5);
						foreach (var e in biome.Encounters)
							encounters.AddToken(e);
					}
					this.Boards.Add(newBoard);
				}
			}

			setStatus("Placing towns...");
			var townBoards = new List<Board>();
			for (var i = 0; i < 8; i++)
			{
				for (var y = 0; y < generator.MapSizeY - 1; y++)
				{
					for (var x = 0; x < generator.MapSizeX - 1; x++)
					{
						if (generator.TownMap[y, x] > 0)
						{
							var townGen = new TownGenerator();
							var thisBoard = generator.BoardMap[y, x];
							if (thisBoard.BoardType == BoardType.Town)
								continue;
							thisBoard.BoardType = BoardType.Town;
							thisBoard.ClearToWorld(generator);
							thisBoard.GetToken("encounters").Value = 0;
							thisBoard.GetToken("encounters").Tokens.Clear();
							townGen.Board = thisBoard;
							var biome = BiomeData.Biomes[(int)thisBoard.GetToken("biome").Value];
							var cultureName = biome.Cultures[Random.Next(biome.Cultures.Length)];
							townGen.Culture = Culture.Cultures[cultureName];
							townGen.Create(biome);
							townGen.ToTilemap(ref thisBoard.Tilemap);
							townGen.ToSectorMap(thisBoard.Sectors);
							thisBoard.AddToken("culture", 0, cultureName);
							while (true)
							{
								var newName = Culture.GetName(townGen.Culture.TownName, Culture.NameType.Town);
								if (Boards.Find(b => b != null && b.Name == newName) == null)
								{
									thisBoard.Name = newName;
									break;
								}
							}
							//if (!townGen.Culture.Demonic)
								townBoards.Add(thisBoard);
						}
					}
				}
			}

			setStatus("Scattering dungeon entrances...");
			var dungeonEntrances = 0;
			while (dungeonEntrances < 25)
			{
				for (var y = 0; y < generator.MapSizeY - 1; y++)
				{
					for (var x = 0; x < generator.MapSizeX - 1; x++)
					{
						if (generator.TownMap[y, x] > 0)
							continue;

						//Don't always place one, to prevent north-west clumping
						if (Random.Flip())
							continue;

						//And don't place one where there already is one.
						if (generator.TownMap[y, x] == -2)
							continue;

						var thisBoard = generator.BoardMap[y, x];
						if (thisBoard == null)
							continue;

						var eX = Random.Next(2, 78);
						var eY = Random.Next(1, 23);

						if (thisBoard.IsSolid(eY, eX))
							continue;
						var sides = 0;
						if (thisBoard.IsSolid(eY - 1, eX))
							sides++;
						if (thisBoard.IsSolid(eY + 1, eX))
							sides++;
						if (thisBoard.IsSolid(eY, eX - 1))
							sides++;
						if (thisBoard.IsSolid(eY, eX + 1))
							sides++;
						if (sides > 3)
							continue;
						generator.TownMap[y, x] = -2;

						var newWarp = new Warp()
						{
							TargetBoard = -1, //mark as ungenerated dungeon
							ID = thisBoard.ID + "_Dungeon",
							XPosition = eX,
							YPosition = eY,
						};
						thisBoard.Warps.Add(newWarp);
						thisBoard.SetTile(eY, eX, '>', Color.Silver, Color.Black);

						dungeonEntrances++;
					}
				}
			}

			setStatus("Applying missions...");
			ApplyMissions(generator);

#if DEBUG
			setStatus("Drawing actual bitmap...");
			var png = new System.Drawing.Bitmap(generator.MapSizeX * 80, generator.MapSizeY * 25);
			for (var y = 0; y < generator.MapSizeY - 1; y++)
			{
				for (var x = 0; x < generator.MapSizeX - 1; x++)
				{
					var thisBoard = generator.BoardMap[y, x];
					if (thisBoard == null)
						continue; //draw empty spot?
					for (var ty = 0; ty < 25; ty++)
					{
						for (var tx = 0; tx < 80; tx++)
						{
							var tile = thisBoard.Tilemap[tx, ty];
							png.SetPixel((x * 80) + tx, (y * 25) + ty, tile.Background);
						}
					}
				}
			}
			png.Save("world.png");
#endif

			Program.WriteLine("Generated all boards and contents in {0}.", stopwatch.Elapsed.ToString());

			//TODO: give the player a proper home.
			this.CurrentBoard = townBoards[Random.Next(townBoards.Count)]; //GetBoard(KnownTargets[0]);
			if (!TravelTargets.ContainsKey(this.CurrentBoard.BoardNum))
				TravelTargets.Add(this.CurrentBoard.BoardNum, this.CurrentBoard.Name);
			this.Player.ParentBoard = this.CurrentBoard;
			this.CurrentBoard.Entities.Add(Player);
			this.Player.Reposition();

			setStatus("Saving overworld boards...");
			Directory.CreateDirectory(Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName));
			for (var i = 0; i < this.Boards.Count; i++)
			{
				if (this.Boards[i] == null)
					continue;
				this.Boards[i].SaveToFile(i);
				//if (i > 0)
				//	this.Boards[i] = null;
			}
			stopwatch.Stop();
			SaveGame(false, true, false);
			Program.WriteLine("Did all that and saved in {0}.", stopwatch.Elapsed.ToString());


			//this.CurrentBoard = GetBoard(townID); //this.Boards[townID];
			//NoxicoGame.HostForm.Write("The World is Ready...         ", Color.Silver, Color.Transparent, 50, 0);
			setStatus("The World is Ready.");
			InGame = true;
			//this.CurrentBoard.Redraw();
		}

		public void CreatePlayerCharacter(string name, Gender bioGender, Gender idGender, string bodyplan, string hairColor, string bodyColor, string eyeColor, string bonusTrait)
		{
			Board.HackishBoardTypeThing = "wild";
			var pc = Character.Generate(bodyplan, bioGender, idGender);
			this.Player = new Player(pc);

			pc.IsProperNamed = true;
			if (!string.IsNullOrWhiteSpace(name))
			{
				pc.Name = new Name(name);
				if (idGender == Gender.Female)
					pc.Name.Female = true;
				//else if (bioGender == Gender.Herm || bioGender == Gender.Neuter)
				//	pc.Name.Female = Random.NextDouble() > 0.5;
			}
			else
			{
				pc.Name.NameGen = pc.GetToken("namegen").Text;
				pc.Name.Regenerate();

				if (pc.Name.Surname.StartsWith("#patronym"))
				{
					var parentName = new Name() { NameGen = pc.Name.NameGen };
					parentName.Regenerate();
					pc.Name.ResolvePatronym(parentName, parentName);
				}
			}

			pc.Path("skin/color").Text = bodyColor;
			if (pc.Path("skin/type").Text != "slime" && pc.Path("hair/color") != null)
				pc.Path("hair/color").Text = hairColor;
			if (pc.HasToken("eyes"))
				pc.GetToken("eyes").Text = eyeColor;

			pc.AddToken("player", (int)DateTime.Now.Ticks);

			var playerShip = new Token(Environment.UserName);
			playerShip.AddToken("player");
			pc.GetToken("ships").Tokens.Add(playerShip);

			var traitsDoc = Mix.GetTokenTree("bonustraits.tml");
			var trait = traitsDoc.FirstOrDefault(t => t.Name == "trait" && t.GetToken("display").Text == bonusTrait);
			if (trait != null)
			{
				foreach (var bonus in trait.Tokens)
				{
					if ((bonus.HasToken("men_only") && bioGender != Gender.Male) || (bonus.HasToken("women_only") && bioGender != Gender.Female))
						continue;
					if (bonus.HasToken("requires") && pc.Path(bonus.GetToken("requires").Text) == null)
						continue;

					switch (bonus.Name)
					{
						case "stat":
							var increase = 20.0f;
							var percent = true;
							if (bonus.HasToken("percent"))
							{
								increase = bonus.GetToken("percent").Value;
							}
							else if (bonus.HasToken("increase"))
							{
								increase = bonus.GetToken("increase").Value;
								percent = false;
							}
							var stat = pc.GetToken(bonus.Text);
							var oldVal = stat.Value;
							var newVal = oldVal + increase;
							if (percent)
								newVal = oldVal + ((increase / 100.0f) * oldVal);
							stat.Value = newVal;
							break;
						case "skill":
							var skill = bonus.Text.Replace(' ', '_').ToLowerInvariant();
							var by = bonus.HasToken("level") ? bonus.GetToken("level").Value : 1.0f;
							var skillToken = pc.Path("skills/" + skill);
							if (skillToken == null)
								skillToken = pc.GetToken("skills").AddToken(skill);
							skillToken.Value += by;
							break;
						case "rating":
							var aspect = pc.Path(bonus.Text);
							if (aspect == null)
								continue;
							var valueToken = bonus.GetToken("value");
							oldVal = aspect.Value;
							if (!string.IsNullOrWhiteSpace(valueToken.Text))
							{
								if (valueToken.Text.EndsWith("%"))
								{
									var percentage = int.Parse(valueToken.Text.Remove(valueToken.Text.Length - 1));
									aspect.Value = (percentage / 100.0f) * oldVal;
								}
								else if (valueToken.Text.StartsWith("+"))
								{
									increase = int.Parse(valueToken.Text.Substring(1));
									aspect.Value = oldVal + increase;
								}
							}
							else
								aspect.Value = Math.Max(oldVal, valueToken.Value);
							break;
						case "token":
							Token token;
							if (bonus.Tokens.Count > 0)
							{
								token = pc.Path(bonus.Tokens[0].Name);
								if (token == null)
									token = pc.AddToken(bonus.Tokens[0].Name);
								token.Value = bonus.Tokens[0].Value;
								token.Text = bonus.Tokens[0].Text;
							}
							break;
					}
				}
			}

			Player.Character.RecalculateStatBonuses();
			Player.Character.CheckHasteSlow();
			Player.Character.UpdateTitle();
			Player.AdjustView();

			//InGame = true;
			//SaveGame();
		}

		public static string RollWorldName()
		{
			var x = Mix.GetString("Homestuck.txt").Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var a = Toolkit.PickOne(x);
			var b = Toolkit.PickOne(x);
			while(b == a)
				b = Toolkit.PickOne(x);
			return "Land of " + a + " and " + b;
		}

		public void RollPotions()
		{
			this.Potions = new string[256];
			var colors = new[] { "black", "blue", "green", "red", "yellow", "mauve", "brown", "white", "silver", "purple", "chocolate", "orange", "gray" };
			var mods = new[] { "", "bubbly ", "fizzy ", "viscious ", "translucent ", "smoky ", "smelly ", "fragrant ", "sparkly ", "tar-like " };
			for (var i = 0; i < 128; i++)
			{
				string roll = null;
				while (Potions.Contains(roll))
				{
					var color = colors[Random.Next(colors.Length)];
					var mod = mods[Random.NextDouble() > 0.6 ? Random.Next(1, mods.Length) : 0];
					roll = mod + color + " potion";
				}
				Potions[i] = roll;
			}
			mods = new[] { "", "shiny ", "sparking ", "warm ", "cold ", "translucent ", "glistening " };
			for (var i = 128; i < 192; i++)
			{
				string roll = null;
				while (Potions.Contains(roll))
				{
					var color = colors[Random.Next(colors.Length)];
					var mod = mods[Random.NextDouble() > 0.6 ? Random.Next(1, mods.Length) : 0];
					roll = mod + color + " ring";
				}
				Potions[i] = roll;
			}
			for (var i = 192; i < 256; i++)
				Potions[i] = "";
		}

		public void ApplyRandomPotions()
		{
			foreach (var item in KnownItems)
			{
				if (item.HasToken("randomized"))
				{
					var rid = (int)item.GetToken("randomized").Value;
					if (item.Path("equipable/ring") != null && rid < 128)
						rid += 128;
					var rdesc = Potions[rid];

					if (rdesc == null)
					{
						Program.WriteLine("Fuckup in applying to {0}.", item.ToString());
						continue;
					}
					if (rdesc == "...")
						continue;
					if (rdesc.Contains('!'))
					{
						//Item has been identified.
						rdesc = rdesc.Substring(1);
						item.UnknownName = null;
					}
					else
					{
						item.UnknownName = rdesc;
					}
					//No matter if it's identified or not, we'll want to change the color.
					var color = Color.NameColor(rdesc.Remove(rdesc.IndexOf(' ')));
					var fore = item.Path("ascii/fore");
					if (fore == null)
						fore = item.GetToken("ascii").AddToken("fore");
					fore.Tokens.Clear();
					fore.AddToken(color);
				}
			}
		}

		public void ApplyMissions(WorldMapGenerator generator)
		{
			var makeBoardTarget = new Action<Board>(board =>
			{
				if (string.IsNullOrWhiteSpace(board.Name))
					throw new Exception("Board must have a name before it can be added to the target list.");
				if (TravelTargets.ContainsKey(board.BoardNum))
					TravelTargets[board.BoardNum] = board.Name;
				else
					TravelTargets.Add(board.BoardNum, board.Name);
			});

			Func<BoardType, int, int, Board> pickBoard = (boardType, biome, maxWater) =>
			{
				var options = new List<Board>();
				foreach (var board in Boards)
				{
					if (board == null)
						continue;
					if (board.BoardType != boardType)
						continue;
					if (biome > 0 && board.GetToken("biome").Value != biome)
						continue;
					if (maxWater != -1)
					{
						var water = 0;
						for (var y = 0; y < 50; y++)
							for (var x = 0; x < 80; x++)
								if (board.Tilemap[x, y].Water)
									water++;
						if (water > maxWater)
							continue;
					}
					options.Add(board);
				}
				if (options.Count == 0)
					return null;
				var choice = options[Random.Next(options.Count)];
				return choice;
			};

			Func<Board, string, int> makeTown = (board, culture) =>
			{
				var townGen = new TownGenerator();
				board.BoardType = BoardType.Town;
				board.GetToken("encounters").Value = 0;
				board.GetToken("encounters").Tokens.Clear();
				board.Sectors.Clear();
				townGen.Board = board;
				var biome = BiomeData.Biomes[(int)board.GetToken("biome").Value];
				var cultureName = string.IsNullOrWhiteSpace(culture) ? biome.Cultures[Random.Next(biome.Cultures.Length)] : culture;
				townGen.Culture = Culture.Cultures[cultureName];
				townGen.Create(biome);
				townGen.ToTilemap(ref board.Tilemap);
				townGen.ToSectorMap(board.Sectors);
				board.GetToken("culture").Text = cultureName;
				while (true)
				{
					var newName = Culture.GetName(townGen.Culture.TownName, Culture.NameType.Town);
					if (Boards.Find(b => b != null && b.Name == newName) == null)
					{
						board.Name = newName;
						break;
					}
				}
				return 0;
			};

			var js = JavaScript.Create();
			JavaScript.Ascertain(js);
			js.SetParameter("Random", typeof(Random));
			js.SetParameter("BoardType", typeof(BoardType));
			js.SetParameter("Character", typeof(Character));
			js.SetParameter("BoardChar", typeof(BoardChar));
			js.SetParameter("InventoryItem", typeof(InventoryItem));
			js.SetParameter("Tile", typeof(Tile));
			js.SetParameter("Color", typeof(Color));
			js.SetFunction("MakeBoardTarget", makeBoardTarget);
			js.SetFunction("GetBoard", new Func<int, Board>(x => GetBoard(x)));
			js.SetFunction("PickBoard", pickBoard);
			js.SetFunction("GetBiomeByName", new Func<string, int>(BiomeData.ByName));
			js.SetFunction("MakeTown", makeTown);
			js.SetFunction("print", new Action<string>(x => Program.WriteLine(x)));
#if DEBUG
			js.SetDebugMode(true);
			js.Step += (s, di) =>
			{
				Program.Write("JINT: {0}", di.CurrentStatement.Source.Code.ToString());
			};
#endif
			Board.DrawJS = js;

			var missionDirs = Mix.GetFilesInPath("missions");
			foreach (var missionDir in missionDirs.Where(x => x.EndsWith("\\manifest.txt")))
			{
				var manifest = Mix.GetString(missionDir).Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				var path = Path.GetDirectoryName(missionDir);
				var jsFile = Path.Combine(path, "mission.js");
				if (!Mix.FileExists(jsFile))
					continue;
				var okay = true;
				for (var i = 2; i < manifest.Length; i++)
				{
					if (!Mix.FileExists(Path.Combine(path, manifest[i])))
						okay = false;
				}
				if (!okay)
				{
					Program.WriteLine("Mission \"{0}\" by {1} is missing files.", manifest[0], manifest[1]);
					continue;
				}
				Program.WriteLine("Applying mission \"{0}\" by {1}...", manifest[0], manifest[1]);
				var jsCode = Mix.GetString(jsFile);
				js.Run(jsCode);
			}
		}

		public static void DrawSidebar()
		{
			var player = HostForm.Noxico.Player;
			if (player == null || player.Character == null)
				return;

			for (var row = 0; row < 60; row++)
				for (var col = 80; col < 100; col++)
					HostForm.SetCell(row, col, ' ', Color.Silver, Color.Black);

			var character = player.Character;
			HostForm.SetCell(1, 81, player.AsciiChar, player.ForegroundColor, player.BackgroundColor);
			HostForm.Write(character.Name.ToString(false), Color.White, Color.Transparent, 1, 83);
			switch (character.Gender)
			{
				case Gender.Male:
					HostForm.SetCell(2, 81, '\x0B', Color.FromArgb(30, 54, 90), Color.Transparent);
					break;
				case Gender.Female:
					HostForm.SetCell(2, 81, '\x0C', Color.FromArgb(90, 30, 30), Color.Transparent);
					break;
				case Gender.Herm:
					HostForm.SetCell(2, 81, '\x15D', Color.FromArgb(84, 30, 90), Color.Transparent);
					break;
			}
			HostForm.Write(character.GetToken("money").Value.ToString("C").PadLeft(17), Color.White, Color.Transparent, 2, 82);

			var hpNow = character.Health;
			var hpMax = character.MaximumHealth;
			var hpBarLength = (int)Math.Ceiling((hpNow / hpMax) * 18);
			HostForm.Write(new string(' ', 18), Color.White, Color.FromArgb(9, 21, 39), 3, 81);
			HostForm.Write(new string(' ', hpBarLength), Color.White, Color.FromArgb(30, 54, 90), 3, 81);
			HostForm.Write(hpNow + " / " + hpMax, Color.White, Color.Transparent, 3, 81);

			var statNames = Enum.GetNames(typeof(Stat));
			var statRow = 5;
			foreach (var stat in statNames)
			{
				if (stat == "Health")
					continue;
				var bonus = "";
				var statBonus = character.GetToken(stat.ToLowerInvariant() + "bonus").Value;
				var statBase = character.GetToken(stat.ToLowerInvariant()).Value;
				var total = statBase + statBonus;
				if (statBonus > 0)
					bonus = "<cGray> (" + statBase + "+" + statBonus + ")<cSilver>";
				else if (statBonus < 0)
					bonus = "<cMaroon> (" + statBase + "-" + (-statBonus) + ")<cSilver>";
				HostForm.Write(i18n.GetString("shortstat_" + stat) + "  <cWhite>" + total + bonus, Color.Silver, Color.Transparent, statRow, 81);
				statRow++;
			}
			var sb = new StringBuilder();
			if (character.HasToken("haste"))
				sb.Append(i18n.GetString("mod_haste"));
			if (character.HasToken("slow"))
				sb.Append(i18n.GetString("mod_slow"));
			if (character.HasToken("flying"))
				sb.Append(i18n.Format("mod_flying", Math.Floor((character.GetToken("flying").Value / 100) * 100)));
			if (character.HasToken("swimming"))
			{
				if (character.GetToken("swimming").Value == -1)
					sb.Append(i18n.GetString("mod_swimmingunl"));
				else
					sb.Append(i18n.Format("mod_swimming", Math.Floor((character.GetToken("swimming").Value / 20) * 100)));
			}

			HostForm.Write(sb.ToString().Wordwrap(18), Color.Silver, Color.Transparent, statRow, 81);

			var renegadeLight = (int)Math.Ceiling((character.GetToken("renegade").Value / 100) * 8);
			var paragonLight = (int)Math.Ceiling((character.GetToken("paragon").Value / 100) * 8);
			var renegadeDark = 8 - renegadeLight;
			var paragonDark = 8 - paragonLight;
			HostForm.SetCell(16, 81, '\x03', Color.FromArgb(116, 48, 48), Color.Transparent);
			HostForm.SetCell(16, 98, '\x06', Color.FromArgb(128, 128, 128), Color.Transparent);
			HostForm.Write(new string(' ', paragonDark), Color.Black, Color.FromArgb(38, 10, 10), 16, 82);
			HostForm.Write(new string(' ', paragonLight), Color.Black, Color.FromArgb(90, 30, 30), 16, 82 + paragonDark);
			HostForm.Write(new string(' ', renegadeLight), Color.Black, Color.FromArgb(30, 54, 90), 16, 82 + 8);
			HostForm.Write(new string(' ', renegadeDark), Color.Black, Color.FromArgb(9, 21, 39), 16, 82 + 8 + renegadeLight);
			HostForm.Write(InGameTime.ToShortTimeString(), Color.Silver, Color.Transparent, 17, 81);
			HostForm.Write(InGameTime.ToShortDateString(), Color.Silver, Color.Transparent, 18, 81);

			if (Mode == UserMode.Aiming && Cursor.PointingAt is BoardChar && !(Cursor.PointingAt is Player))
			{
				var boardChar = Cursor.PointingAt as BoardChar;
				character = boardChar.Character;
				HostForm.SetCell(20, 81, boardChar.AsciiChar, boardChar.ForegroundColor, boardChar.BackgroundColor);
				HostForm.Write(character.GetKnownName(), Color.White, Color.Transparent, 20, 83);

				switch (character.Gender)
				{
					case Gender.Male:
						HostForm.SetCell(21, 81, '\x0B', Color.FromArgb(30, 54, 90), Color.Transparent);
						break;
					case Gender.Female:
						HostForm.SetCell(21, 81, '\x0C', Color.FromArgb(90, 30, 30), Color.Transparent);
						break;
					case Gender.Herm:
						HostForm.SetCell(21, 81, '\x15D', Color.FromArgb(84, 30, 90), Color.Transparent);
						break;
				}

				if (!character.HasToken("beast"))
					HostForm.Write(character.Title, Color.Silver, Color.Transparent, 21, 83);

				hpNow = character.Health;
				hpMax = character.MaximumHealth;
				hpBarLength = (int)Math.Ceiling((hpNow / hpMax) * 18);
				HostForm.Write(new string(' ', 18), Color.White, Color.FromArgb(9, 22, 39), 22, 81);
				HostForm.Write(new string(' ', hpBarLength), Color.White, Color.FromArgb(30, 54, 90), 22, 81);
				sb.Clear();
				if (character.Path("role/vendor") != null)
					sb.Append(i18n.GetString("vendor_" + character.Path("role/vendor/class").Text) + ' ');
				if (character.HasToken("hostile"))
					sb.Append(i18n.GetString("mod_hostile"));
				if (character.HasToken("helpless"))
					sb.Append(i18n.GetString("mod_helpless"));
				HostForm.Write(sb.ToString().Wordwrap(18), Color.Silver, Color.Transparent, 23, 81);
			}

			if (!string.IsNullOrWhiteSpace(ContextMessage))
				HostForm.Write(' ' + ContextMessage + ' ', Color.Silver, Color.Black, 0, 100 - ContextMessage.Length() - 2);
#if DEBUG
			HostForm.Write(player.Energy.ToString(), PlayerReady ? Color.Yellow : Color.Red, Color.Black, 49, 81);
#endif
		}
	}

	/*
	public class Expectation
	{
		public BoardType Type { get; set; }
		public int Biome { get; set; }
		public string Culture { get; set; }
		public string BuildingSet { get; set; }
		public List<string> Characters { get; private set; }
		public List<string> Species { get; private set; }
		
		public Expectation()
		{
			Type = BoardType.Town;
			Biome = -1;
			Culture = string.Empty;
			Characters = new List<string>();
			Species = new List<string>();
		}
		
		public static Expectation LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "EXPT", "location expectation");
			var exp = new Expectation();
			exp.Type = (BoardType)stream.ReadInt16();
			exp.Biome = (int)stream.ReadInt16();
			exp.Culture = stream.ReadString();
			var numChars = stream.ReadInt16();
			var numSpecies = stream.ReadInt16();
			for (var i = 0; i < numChars; i++)
				exp.Characters.Add(stream.ReadString());
			for (var i = 0; i < numSpecies; i++)
				exp.Species.Add(stream.ReadString());
			return exp;
		}
		
		public void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "EXPT");
			stream.Write((Int16)Type);
			stream.Write((Int16)Biome);
			stream.Write(Culture ?? string.Empty);
			stream.Write((Int16)Characters.Count);
			stream.Write((Int16)Species.Count);
			foreach (var character in Characters)
				stream.Write(character);
			foreach (var species in Species)
				stream.Write(species);
		}

		public int ID
		{
			get
			{
				return NoxicoGame.Expectations.First(e => e.Value == this).Key;
			}
		}
		public string Name
		{
			get
			{
				return NoxicoGame.TargetNames[ID];
			}
		}

		public static Expectation ExpectTown(string name, int biomeID)
		{
			if (biomeID < 0)
				biomeID = Random.Next(2, 8);
			var biome = BiomeData.Biomes[biomeID];
			var cultureName = biome.Cultures[Random.Next(biome.Cultures.Length)];
			var culture = Noxico.Culture.Cultures[cultureName];

			if (string.IsNullOrEmpty(name))
			{
				while (true)
				{
					name = Noxico.Culture.GetName(culture.TownName, Noxico.Culture.NameType.Town);
					if (!NoxicoGame.TargetNames.ContainsValue(name))
						break;
				}
			}
			var id = -10;
			if (NoxicoGame.Expectations.Count > 0)
				id = NoxicoGame.Expectations.Last().Key;
			id--;
			NoxicoGame.Expectations.Add(id, new Expectation() { Biome = biomeID, Culture = cultureName, BuildingSet = "town" });
			NoxicoGame.TargetNames.Add(id, name);
			NoxicoGame.KnownTargets.Add(id);
			return NoxicoGame.Expectations[id];
		}

		public static Expectation ExpectTown(string name, string biomeName)
		{
			return ExpectTown(name, BiomeData.ByName(biomeName));
		}

		//TODO: probaby better to use while still creating the board, re spouses
		public static void AddCharacters(Board board, List<string> characters)
		{
			var culture = Noxico.Culture.DefaultCulture;
			if (board.HasToken("culture") && !string.IsNullOrWhiteSpace(board.GetToken("culture").Text))
				culture = Noxico.Culture.Cultures[board.GetToken("culture").Text];
			var tokens = new List<string>();

			var unexpected = board.Entities.OfType<BoardChar>().Where(e => !e.Character.HasToken("expectation")).ToList();
			Character character = null;

			Board.HackishBoardTypeThing = board.BoardType.ToString().ToLowerInvariant();

			foreach (var expectedChar in characters)
			{
				var replacement = unexpected[Random.Next(unexpected.Count)];
				var fullReplace = true;
				var schedule = "villager";

				var bodyplan = Toolkit.PickOne(culture.Bodyplans);
				var gender = Gender.Random;
				var firstName = "";
				var surName = "";
				tokens.Clear();

				if (expectedChar.StartsWith("unique="))
				{
					var unique = expectedChar.Split('=')[1];
					character = Character.GetUnique(unique);
					if (character.HasToken("schedule"))
					{
						schedule = character.GetToken("schedule").Text;
						character.RemoveToken("schedule");
					}
					Scheduler.AddSchedule(schedule, character);

					//See if there's a character on the board with this gender
					var cg = character.Gender;
					foreach (var person in unexpected)
					{
						if (cg == person.Character.Gender)
						{
							replacement = person;
							fullReplace = false;
							break;
						}
					}
				}
				else
				{
					foreach (var item in expectedChar.Split(';'))
					{
						var stuff = item.Split('=');
						switch (stuff[0])
						{
							case "bodyplan":
								bodyplan = stuff[1];
								break;
							case "gender":
								gender = (Gender)Enum.Parse(typeof(Gender), stuff[1], true);
								break;
							case "firstname":
								firstName = stuff[1];
								break;
							case "surname":
								surName = stuff[1];
								break;
							case "token":
								tokens.Add(stuff[1]);
								break;
							case "schedule":
								schedule = stuff[1];
								break;
						}
					}

					//See if there's a character on the board with this bodyplan and gender already
					foreach (var person in unexpected)
					{
						var primaryHash = Toolkit.GetBodyComparisonHash(person.Character);
						var distance = Toolkit.GetHammingDistance(primaryHash, NoxicoGame.BodyplanHashes[bodyplan]);
						if (distance == 0) //?
						{
							var pg = person.Character.Gender;
							if (gender != pg)
								continue;
							replacement = person;
							fullReplace = false;
							break;
						}
					}

					character = fullReplace ? Character.Generate(bodyplan, gender) : replacement.Character;

					if (!string.IsNullOrEmpty(firstName))
						character.Name.FirstName = firstName;
					if (!string.IsNullOrEmpty(surName))
						character.Name.Surname = surName;

					foreach (var tokenEntry in tokens)
					{
						var token = (tokenEntry + ' ').TrimEnd();
						Token t = null;
						var tVal = "";
						if (token.StartsWith("!"))
						{
							token = token.Substring(1);
							t = character.Path(token);
							if (t != null)
							{
								token = token.Remove(token.LastIndexOf('/'));
								character.Path(token).RemoveToken(t);
							}
							continue;
						}
						if (token.Contains('('))
						{
							tVal = token.Substring(token.IndexOf('(') + 1);
							tVal = tVal.Remove(tVal.Length - 1);
							token = token.Remove(token.IndexOf('('));
						}
						if (!token.Contains('/'))
						{
							if (character.HasToken(token))
								t = character.GetToken(token);
							else
							{
								t = new Token(token);
								character.AddToken(t);
							}
						}
						else
						{
							TokenCarrier o = character;
							foreach (var part in token.Split('/'))
							{
								if (o.HasToken(part))
									o = o.GetToken(part);
								else
								{
									t = new Token(part);
									o.AddToken(t);
									o = t;
								}
							}
						}
						if (!string.IsNullOrEmpty(tVal))
						{
							var fVal = 0.0f;
							var isNumeric = float.TryParse(tVal, out fVal);
							if (isNumeric)
								t.Value = fVal;
							else
								t.Text = tVal;
						}
					}

					Scheduler.AddSchedule(schedule, character);
					character.AddToken("expectation");
				}

				unexpected.Remove(replacement);
				replacement.Character = character;
				replacement.ID = character.Name.ToID();
				replacement.AdjustView();
			}
		}

		public static Expectation FindUnknownExpectation(BoardType type)
		{
			var result = NoxicoGame.Expectations.Values.FirstOrDefault(e => e.Type == type);
			return result;
		}
	}
	*/
}
