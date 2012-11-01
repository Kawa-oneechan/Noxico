using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;


namespace Noxico
{
	public enum KeyBinding
	{
		Left, Right, Up, Down, Rest, Activate, Items, Look, Aim, Chat, Fuck, Take, Accept, Back,
		Pause, Screenshot, LookAlt, TakeAlt, BackAlt, TabFocus, ScrollUp, ScrollDown
	}

	public class NoxicoGame
	{
		public static SoundSystem Sound;

		public int Speed { get; set; }
		public static bool Immediate { get; set; }

		public static string WorldName { get; set; }
		public static MainForm HostForm { get; private set; }
		public static bool[] KeyMap { get; set; }
		public static bool[] KeyTrg { get; set; }
		public static bool[] Modifiers { get; set; }
		public static DateTime[] KeyRepeat { get; set; }
		public static char LastPress { get; set; }
		public static bool ScrollWheeled { get; set; }
		public static int AutoRestTimer { get; set; }
		public static int AutoRestSpeed { get; set; }
		public static bool Mono { get; set; }

		public static Dictionary<KeyBinding, int> KeyBindings { get; set; }

		public static List<InventoryItem> KnownItems { get; private set; }
		public List<Board> Boards { get; set; }
		public Board CurrentBoard { get; set; }
		public static Board Ocean { get; set; }
		public int[,] Overworld { get; set; }
		public int OverworldBarrier { get; private set; }
		public Player Player { get; set; }
		public static List<string> BookTitles { get; private set; }
		public static List<string> BookAuthors { get; private set; }
		public static List<StatusMessage> Messages { get; set; }
		public static UserMode Mode { get; set; }
		public static Cursor Cursor { get; set; }
		public static SubscreenFunc Subscreen { get; set; }
		public static Dictionary<string, char> Views { get; set; }
		public static string[] TileDescriptions { get; private set; }
		public static Dictionary<string, string> BodyplanLevs { get; set; }
		public static string SavePath { get; private set; }
		public static bool InGame { get; set; }
#if CONTEXT_SENSITIVE
		public static string ContextMessage { get; set; }
		private static bool hadContextMessage;
#endif

		public static int StartingOWX = -1, StartingOWY;
		private DateTime lastUpdate;
		public string[] Potions;
		public static NoxicanDate InGameTime;

		public static bool IsKeyDown(KeyBinding binding)
		{
			if (KeyBindings[binding] == 0)
				return false;
			return (NoxicoGame.KeyMap[(int)KeyBindings[binding]]);
		}

		public NoxicoGame(MainForm hostForm)
		{
			Console.WriteLine("IT BEGINS...");

			Func<string, Keys, int> GetIniKey = (s, d) =>
			{
				var keyNames = Enum.GetNames(typeof(Keys)).Select(x => x.ToLowerInvariant());
				var keyValue = IniFile.GetString("keymap", s, d.ToString()).ToLowerInvariant();
				if (keyNames.Contains(keyValue))
					return (int)(Keys)Enum.Parse(typeof(Keys), keyValue, true);
				keyValue = "oem" + keyValue; //try unfriendly name
				if (keyNames.Contains(keyValue))
					return (int)(Keys)Enum.Parse(typeof(Keys), keyValue, true);
				//give up and return default
				return (int)d;
			};
			KeyBindings = new Dictionary<KeyBinding, int>()
			{
				{ KeyBinding.Left, GetIniKey("left", Keys.Left) },
				{ KeyBinding.Right, GetIniKey("right", Keys.Right) },
				{ KeyBinding.Up, GetIniKey("up", Keys.Up) },
				{ KeyBinding.Down, GetIniKey("down", Keys.Down) },
				{ KeyBinding.Rest, GetIniKey("rest", Keys.OemPeriod) },
				{ KeyBinding.Activate, GetIniKey("activate", Keys.Enter) },
				{ KeyBinding.Items, GetIniKey("items", Keys.I) },
				{ KeyBinding.Look, GetIniKey("look", Keys.L) },
				{ KeyBinding.Aim, GetIniKey("aim", Keys.A) },
				{ KeyBinding.Chat, GetIniKey("chat", Keys.C) },
				{ KeyBinding.Fuck, GetIniKey("fuck", Keys.F) },
				{ KeyBinding.Take, GetIniKey("take", Keys.P) },
				{ KeyBinding.Accept, GetIniKey("accept", Keys.Enter) },
				{ KeyBinding.Back, GetIniKey("back", Keys.Escape) },
				{ KeyBinding.Pause, GetIniKey("pause", Keys.F1) },
				{ KeyBinding.Screenshot, GetIniKey("screenshot", Keys.F12) },
				{ KeyBinding.LookAlt, GetIniKey("lookalt", Keys.OemQuestion) },
				{ KeyBinding.TakeAlt, GetIniKey("takealt", Keys.Oemcomma) },
				{ KeyBinding.BackAlt, GetIniKey("backalt", Keys.Back) },
				{ KeyBinding.TabFocus, GetIniKey("tabfocus", Keys.Tab) },
				{ KeyBinding.ScrollUp, GetIniKey("scrollup", Keys.Up) },
				{ KeyBinding.ScrollDown, GetIniKey("scrolldown", Keys.Down) },
			};

			SavePath = Vista.GetInterestingPath(Vista.SavedGames);
			if (IniFile.GetBool("misc", "vistasaves", true) && SavePath != null)
				SavePath = Path.Combine(SavePath, "Noxico"); //Add a Noxico directory to V/7's Saved Games
			else
			{
				SavePath = IniFile.GetString("misc", "savepath", @"$/Noxico"); //"saves"; //Use <startup>\saves instead
				if (SavePath.StartsWith("$"))
					SavePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + SavePath.Substring(1);
				SavePath = Path.GetFullPath(SavePath);
			}

			WorldName = RollWorldName();
			if (!Directory.Exists(SavePath))
				Directory.CreateDirectory(SavePath);

			lastUpdate = DateTime.Now;
			Speed = 60;
			this.Boards = new List<Board>();
			HostForm = hostForm;
			KeyMap = new bool[256];
			KeyTrg = new bool[256];
			KeyRepeat = new DateTime[256];
			Modifiers = new bool[3];
			AutoRestSpeed = IniFile.GetInt("misc", "autorest", 100); //100;
			if (AutoRestSpeed < 5)
				AutoRestSpeed = 5;
			Cursor = new Cursor();
			Messages = new List<StatusMessage>();
			Sound = new SoundSystem();

			//var xDoc = new XmlDocument();
			Console.WriteLine("Loading items...");
			var xDoc = Mix.GetXMLDocument("items.xml");
			//xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Items, "items.xml"));
			KnownItems = new List<InventoryItem>();
			foreach (var item in xDoc.SelectNodes("//item").OfType<XmlElement>())
				KnownItems.Add(InventoryItem.FromXML(item));
			Console.WriteLine("Randomizing potions and rings...");
			RollPotions();
			ApplyRandomPotions();
			Console.WriteLine("Loading bodyplans...");
			xDoc = Mix.GetXMLDocument("bodyplans.xml");
			//xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.BodyPlans, "bodyplans.xml"));
			Views = new Dictionary<string, char>();
			var ohboy = new TokenCarrier();
			BodyplanLevs = new Dictionary<string, string>();
			foreach (var bodyPlan in xDoc.SelectNodes("//bodyplan").OfType<XmlElement>())
			{
				var id = bodyPlan.GetAttribute("id");
				Console.WriteLine("Loading {0}...", id);
				var plan = bodyPlan.ChildNodes[0].Value.Replace("\r\n", "\n");
				var ascii = Toolkit.GrabToken(plan, "ascii");
				if (ascii != null)
				{
					var ch = ascii.IndexOf("\tchar: ");
					if (ch != -1)
					{
						var part = ascii.Substring(ascii.IndexOf("U+") + 2);
						part = part.Remove(part.IndexOf('\n'));
						var c = int.Parse(part, System.Globalization.NumberStyles.HexNumber);
						Views.Add(id, (char)c);
					}
				}
				ohboy.Tokens = Token.Tokenize(plan);
				var lev = Toolkit.GetLevenshteinString(ohboy);
				BodyplanLevs.Add(id, lev);
			}

			//Tile descriptions
			TileDescriptions = Mix.GetString("TileSpecialDescriptions.txt").Split('\n');

			Console.WriteLine("Loading books...");
			BookTitles = new List<string>();
			BookAuthors = new List<string>();
			BookTitles.Add("[null]");
			BookAuthors.Add("[null]");
			xDoc = Mix.GetXMLDocument("books.xml");
			//xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Library, "books.xml"));
			var books = xDoc.SelectNodes("//book");
			foreach (var b in books.OfType<XmlElement>())
			{
				BookTitles.Add(b.GetAttribute("title"));
				BookAuthors.Add(b.HasAttribute("author") ? b.GetAttribute("author") : "an unknown author");
			}

			//ScriptVariables.Add("consumed", 0);
			HostForm.Noxico = this;
			Javascript.MainMachine = Javascript.Create();

			WorldGen.LoadBiomes();
			Ocean = Board.CreateBasicOverworldBoard(0, "Ocean", "The Ocean", "set://ocean");

			InGameTime = new NoxicanDate(740 + Toolkit.Rand.Next(0, 20), 6, 26, DateTime.Now.Hour, 0, 0);

#if DEBUG
			//Towngen test
			//var towngenTest = Board.CreateBasicOverworldBoard(2, "TowngenTest", "Towngen Test", "set://debug");
			var towngenTest = Board.CreateBasicOverworldBoard(2, "DungeonTest", "DunGen Test", "set://debug");
			towngenTest.Type = BoardType.Town;
			CurrentBoard = towngenTest;
			var townGen = new StoneDungeonGenerator(); //new TownGenerator();
			townGen.Board = towngenTest;
			townGen.Culture = Culture.Cultures["human"];
			townGen.Create(WorldGen.Biomes[2]);
			townGen.ToTilemap(ref towngenTest.Tilemap);
			townGen.ToSectorMap(towngenTest.Sectors);
			towngenTest.DumpToHTML("final");
#endif

			CurrentBoard = new Board();
			this.Player = new Player()
			{
				CurrentRealm = "Nox",
			};
			/*
			if (IniFile.GetBool("misc", "skiptitle", false) && Directory.Exists(WorldName)) //File.Exists("world.bin"))
			{
				LoadGame();
				HostForm.Noxico.CurrentBoard.Draw();
				Subscreens.FirstDraw = true;
				Immediate = true;
				AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
				AddMessage("Remember, press F1 for help and options.");
				Mode = UserMode.Walkabout;
			}
			else
			*/
			{
				Introduction.Title();
			}
		}

		public void SaveGame(bool noPlayer = false, bool force = false)
		{
			if (!InGame && !force)
				return;

			var header = Encoding.UTF8.GetBytes("NOXiCO");

			if (!noPlayer && !Player.Character.HasToken("gameover"))
			{
				Console.WriteLine("Saving player...");
				var pfile = File.Open(Path.Combine(SavePath, WorldName, "player.bin"), FileMode.Create);
				var pbin = new BinaryWriter(pfile);
				Player.SaveToFile(pbin);
				pbin.Flush();
				pfile.Flush();
				pfile.Close();
			}

			Console.WriteLine("--------------------------");
			Console.WriteLine("Saving globals...");
			var global = Path.Combine(SavePath, WorldName, "global.bin");
			Console.WriteLine("Location will be {0}", global);
			using (var f = File.Open(global, FileMode.Create))
			{
				var b = new BinaryWriter(f);
				Console.WriteLine("Header...");
				b.Write(Encoding.UTF8.GetBytes("NOXiCO"));
				Console.WriteLine("Potion check...");
				if (Potions[0] == null)
					RollPotions();
				Console.WriteLine("Potions...");
				b = new BinaryWriter(new CryptStream(f));
				for (var i = 0; i < 256; i++)
					b.Write(Potions[i] ?? "...");
				Console.WriteLine("Unique Items counter lol...");
				b.Write(0);
				b.Write(InGameTime.ToBinary());
			}

			Console.WriteLine("--------------------------");
			Console.WriteLine("Saving World...");

			var realm = Path.Combine(SavePath, WorldName, Player.CurrentRealm);
			if (!Directory.Exists(realm))
				Directory.CreateDirectory(realm);

			var file = File.Open(Path.Combine(SavePath, WorldName, Player.CurrentRealm, "world.bin"), FileMode.Create);
			var bin = new BinaryWriter(file);
			bin.Write(header);

			bin.Write(Overworld.GetLength(0));
			bin.Write(Overworld.GetLength(1));
			for (var y = 0; y < Overworld.GetLength(1); y++)
				for (var x = 0; x < Overworld.GetLength(0); x++)
					bin.Write(Overworld[x, y]);


			bin.Write(CurrentBoard.BoardNum);
			bin.Write(Boards.Count);
			//foreach (var b in Boards)
			//	b.SaveToFile(bin);
			for (var i = 0; i < Boards.Count; i++)
			{
				if (Boards[i] != null)
				{
					Boards[i].SaveToFile(i);
					Boards[i] = null;
				}
			}
			CurrentBoard.SaveToFile(CurrentBoard.BoardNum);

			bin.Write(StartingOWX);
			bin.Write(StartingOWY);

			bin.Flush();

			file.Flush();
			file.Close();
			Console.WriteLine("Done.");
			Console.WriteLine("--------------------------");
		}

		public void LoadGame()
		{
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
			if (!File.Exists(global))
			{
				using (var f = File.Open(global, FileMode.Create))
				{
					var b = new BinaryWriter(f);
					b.Write(Encoding.UTF8.GetBytes("NOXiCO"));
					b = new BinaryWriter(new CryptStream(f));
					RollPotions();
					for (var i = 0; i < 256; i++)
						b.Write(Potions[i]);
					b.Write(0);
				}
			}
			var file = File.Open(global, FileMode.Open);
			var bin = new BinaryReader(file);
			var header = bin.ReadBytes(6);
			if (Encoding.UTF8.GetString(header) != "NOXiCO")
			{
				MessageBox.Message("Invalid world header.");
				return;
			}
			var crypt = new CryptStream(file);
			bin = new BinaryReader(crypt);
			Potions = new string[256];
			for (var i = 0; i < 256; i++)
				Potions[i] = bin.ReadString();
			var numUniques = bin.ReadInt32();
			try
			{
				InGameTime = new NoxicanDate(bin.ReadInt64());
			}
			catch (EndOfStreamException)
			{
				//Old save game, change is... fixable.
				InGameTime = new NoxicanDate(40, 6, 26, DateTime.Now.Hour, 0, 0);
			}
			ApplyRandomPotions();
			file.Close();

			var realm = Path.Combine(SavePath, WorldName, Player.CurrentRealm);

			file = File.Open(Path.Combine(realm, "world.bin"), FileMode.Open);
			bin = new BinaryReader(file);
			header = bin.ReadBytes(6);
			if (Encoding.UTF8.GetString(header) != "NOXiCO")
			{
				MessageBox.Message("Invalid world header.");
				return;
			}

			var owX = bin.ReadInt32();
			var owY = bin.ReadInt32();
			Overworld = new int[owX, owY];
			OverworldBarrier = owX * owY;
			for (var y = 0; y < owY; y++)
			{
				for (var x = 0; x < owX; x++)
				{
					Overworld[x, y] = bin.ReadInt32();
				}
			}

			var currentIndex = bin.ReadInt32();
			var boardCount = bin.ReadInt32();
			Boards = new List<Board>(boardCount);
			for (int i = 0; i < boardCount; i++)
				Boards.Add(null);
			//for (int i = 0; i < boardCount; i++)
			//	Boards.Add(Board.LoadFromFile(bin));

			StartingOWX = bin.ReadInt32();
			StartingOWY = bin.ReadInt32();

			file.Close();

			InGame = true;

			if (File.Exists(playerFile))
			{
				GetBoard(currentIndex);

				CurrentBoard = Boards[currentIndex];
				CurrentBoard.Entities.Add(Player);
				Player.ParentBoard = CurrentBoard;
				CurrentBoard.UpdateLightmap(Player, true);
				CurrentBoard.Redraw();
				Sound.PlayMusic(CurrentBoard.Music);

				if (!Player.Character.HasToken("player"))
					Player.Character.Tokens.Add(new Token() { Name = "player", Value = (int)DateTime.Now.Ticks });
				Player.Character.RecalculateStatBonuses();
				Player.Character.CheckHasteSlow();
				SaveGame();

				Achievements.StartingTime = DateTime.Now;
			}
		}

		public Board GetBoard(int index)
		{
			if (index == -1 || index >= Boards.Count)
				return NoxicoGame.Ocean;

			if (Boards[index] == null)
			{
				Console.WriteLine("Requested board #{0}. Loading...", index);
				Boards[index] = Board.LoadFromFile(index);
				Boards[index].BoardNum = index;
			}
			return Boards[index];
		}

		public static void DrawMessages()
		{
#if CONTEXT_SENSITIVE
			if (!string.IsNullOrWhiteSpace(ContextMessage))
			{
				hadContextMessage = true;
				HostForm.Write(' ' + ContextMessage + ' ', Color.Silver, Color.Black, 80 - ContextMessage.Length - 2, 0);
			}
			else if (hadContextMessage)
			{
				HostForm.Noxico.CurrentBoard.Redraw();
				hadContextMessage = false;
			}
#endif
		
			if (Messages.Count == 0)
				return;
			var row = 24;
			for (var i = 0; i < 4 && i < Messages.Count; i++)
			{
				var m = Messages.Count - 1 - i;
				var c = Messages[m].Color;
				if (c.GetBrightness() < 0.2)
					c = Toolkit.Lerp(c, Color.White, 0.5);
				HostForm.Write(' ' + Messages[m].Message + ' ', c, Color.Black, 0, row);
				row--;
			}
		}
		public static void ClearMessages()
		{
			Messages.Clear();
		}
		public static void UpdateMessages()
		{
			if (Messages.Count == 0)
				return;
			if (Messages[0].New)
			{
				Messages[0].New = false;
				return;
			}
			Messages.RemoveAt(0);
			HostForm.Noxico.CurrentBoard.Redraw();
		}
		public static void AddMessage(string message, Color color)
		{
			Messages.Add(new StatusMessage() { Message = message, Color = color, New = true });
		}
		public static void AddMessage(string message)
		{
			Messages.Add(new StatusMessage() { Message = message, Color = Color.Silver, New = true });
		}

		public void FlushDungeons()
		{
			Console.WriteLine("Flushing dungeons...");
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
					var timeNow = DateTime.Now;
					//while ((DateTime.Now - timeNow).Milliseconds < (Immediate ? 1 : Speed)) ;
					if ((timeNow - lastUpdate).Milliseconds >= Speed)
					{
						lastUpdate = timeNow;
						AutoRestTimer--;
						if (AutoRestTimer <= 0)
						{
							Sound.PlaySound("Open Gate");
							AutoRestTimer = AutoRestSpeed;
							KeyMap[KeyBindings[KeyBinding.Rest]] = true;
						}
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
				DrawMessages();

				if (Mode == UserMode.LookAt)
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

			Sound.Update();
			HostForm.Draw();
			Immediate = false;
			for (int i = 0; i < KeyTrg.Length; i++)
				KeyTrg[i] = false;
			if (ScrollWheeled)
			{
				KeyMap[KeyBindings[KeyBinding.ScrollUp]] = false;
				KeyMap[KeyBindings[KeyBinding.ScrollDown]] = false;
				ScrollWheeled = false;
			}
		}

		public static void ClearKeys()
		{
			for (var i = 0; i < 255; i++)
			{
				KeyMap[i] = false;
				KeyTrg[i] = false;
				KeyRepeat[i] = DateTime.Now;
			}
		}

		public void CreateRealm(string realm = "Nox")
		{
			Console.WriteLine("Creating realm \"{0}\"...", realm);

			var setStatus = new Action<string>(s =>
			{
				var line = UIManager.Elements.Find(x => x.Tag == "worldGen");
				if (line == null)
					return;
				line.Text = s.PadRight(70);
				line.Draw();
				//Console.WriteLine(s);
			});

			var host = NoxicoGame.HostForm;
			this.Boards.Clear();

			setStatus("Generating world map...");
			var worldGen = new WorldGen();
			worldGen.Generate(setStatus /*, "pandora" */);

			setStatus("Generating boards...");
			Overworld = new int[worldGen.MapSizeX, worldGen.MapSizeY];
			for (var y = 0; y < worldGen.MapSizeY - 1; y++)
				for (var x = 0; x < worldGen.MapSizeX - 1; x++)
					Overworld[x, y] = -1;
			OverworldBarrier = worldGen.MapSizeX * worldGen.MapSizeY;
			for (var y = 0; y < worldGen.MapSizeY - 1; y++)
			{
				for (var x = 0; x < worldGen.MapSizeX - 1; x++)
				{
					//if (WorldGen.Biomes[worldGen.BiomeMap[y, x]].IsWater)
					if (worldGen.BiomeMap[y, x] == 0)
					{
						Boards.Add(null);
						continue;
					}
					var newBoard = Board.CreateFromBitmap(worldGen.BiomeBitmap, worldGen.BiomeMap[y,x], x, y);
					this.Boards.Add(newBoard);
					Overworld[x, y] = Boards.Count - 1;
				}
			}

			setStatus("Placing towns...");
			var townGen = new TownGenerator();
			for (int y = 0; y < worldGen.MapSizeY - 1; y++)
			{
				for (int x = 0; x < worldGen.MapSizeX - 1; x++)
				{
					if (worldGen.TownMap[y, x] > 0)
					{
						if (Overworld[x, y] == 1)
							continue;
						if (StartingOWX == -1)
						{
							StartingOWX = x;
							StartingOWY = y;
						}

						var thisMap = Boards[Overworld[x, y]];
						thisMap.Type = BoardType.Town;
						townGen.Board = thisMap;
						var biome = WorldGen.Biomes[worldGen.BiomeMap[y, x]];
						var cultureName = biome.Cultures[Toolkit.Rand.Next(biome.Cultures.Length)];
						townGen.Culture = Culture.Cultures[cultureName]; //Culture.Cultures["human"];
						townGen.Create(biome);
						townGen.ToTilemap(ref thisMap.Tilemap);
						townGen.ToSectorMap(thisMap.Sectors);
						thisMap.GetToken("music").Text = "set://Town";
						thisMap.Tokens.Add(new Token() { Name = "culture", Text = cultureName });
						while (true)
						{
							var newName = Culture.GetName(townGen.Culture.TownName, Culture.NameType.Town);
							if (Boards.Find(b => b != null && b.Name == newName) == null)
							{
								thisMap.Name = newName;
								break;
							}
						}
					}
				}
			}

			var dungeonEntrances = 0;
			while (dungeonEntrances < 25)
			{
				setStatus("Placing dungeon entrances... (" + dungeonEntrances + ")");
				for (int y = 0; y < worldGen.MapSizeY - 1; y++)
				{
					for (int x = 0; x < worldGen.MapSizeX - 1; x++)
					{
						//Don't place one in oceans
						if (worldGen.BiomeMap[y, x] == 0)
							continue;
						//Don't place one in towns either
						if (worldGen.TownMap[y, x] > 0)
							continue;

						//Don't always place one, to prevent north-west clumping
						if (Toolkit.Rand.NextDouble() > 0.4)
							continue;

						//And don't place one where there already is one.
						if (worldGen.TownMap[y, x] == -2)
							continue;

						var thisMap = Boards[Overworld[x, y]];
						var eX = Toolkit.Rand.Next(2, 78);
						var eY = Toolkit.Rand.Next(1, 23);

						if (thisMap.IsSolid(eY, eX))
							continue;
						var sides = 0;
						if (thisMap.IsSolid(eY - 1, eX))
							sides++;
						if (thisMap.IsSolid(eY + 1, eX))
							sides++;
						if (thisMap.IsSolid(eY, eX - 1))
							sides++;
						if (thisMap.IsSolid(eY, eX + 1))
							sides++;
						if (sides > 3)
							continue;
						worldGen.TownMap[y, x] = -2;

						var newWarp = new Warp()
						{
							TargetBoard = -1, //mark as ungenerated dungeon
							ID = thisMap.ID + "_Dungeon",
							XPosition = eX,
							YPosition = eY,
						};
						thisMap.Warps.Add(newWarp);
						thisMap.SetTile(eY, eX, '>', Color.Silver, Color.Black);

						dungeonEntrances++;
					}
				}
			}

			setStatus("Applying missions...");
			Board.WorldGen = worldGen;
			ApplyMissions();

			setStatus("Saving chunks... (lol)");
			for (var i = 0; i < this.Boards.Count; i++)
			{
				if (this.Boards[i] == null)
					continue;
				this.Boards[i].SaveToFile(i);
				if (i > 0)
					this.Boards[i] = null;
			}
			SaveGame(true, true);

			//this.CurrentBoard = GetBoard(townID); //this.Boards[townID];
			//NoxicoGame.HostForm.Write("The World is Ready...         ", Color.Silver, Color.Transparent, 50, 0);
			setStatus("The World is Ready.");
			//Sound.PlayMusic(this.CurrentBoard.Music);
			//this.CurrentBoard.Redraw();
		}

		public void CreatePlayerCharacter(string name, Gender gender, string bodyplan, string hairColor, string bodyColor, string eyeColor, string BonusTrait)
		{
			var pc = Character.Generate(bodyplan, gender);

			pc.IsProperNamed = true;
			if (!string.IsNullOrWhiteSpace(name))
			{
				pc.Name = new Name(name);
				if (gender == Gender.Female)
					pc.Name.Female = true;
				else if (gender == Gender.Herm || gender == Gender.Neuter)
					pc.Name.Female = Toolkit.Rand.NextDouble() > 0.5;
			}
			else
			{
				pc.Name.NameGen = pc.GetToken("namegen").Text;
				pc.Name.Regenerate();
			
				if (pc.Name.Surname.StartsWith("#patronym"))
				{
					var parentName = new Name() { NameGen = pc.Name.NameGen };
					if (gender == Gender.Female)
						pc.Name.Female = true;
					parentName.Regenerate();
					pc.Name.ResolvePatronym(parentName, parentName);
				}
			}

			if (pc.Path("skin/type").Text != "slime")
				pc.Path("skin/color").Text = bodyColor;
			if (pc.Path("hair/color") != null)
				pc.Path("hair/color").Text = hairColor;
			if (pc.HasToken("eyes"))
				pc.GetToken("eyes").Text = eyeColor;

			pc.Tokens.Add(new Token() { Name = "player", Value = (int)DateTime.Now.Ticks });

			var playerShip = new Token() { Name = Environment.UserName };
			playerShip.Tokens.Add(new Token() { Name = "player" });
			pc.GetToken("ships").Tokens.Add(playerShip);

			var traitsDoc = Mix.GetXMLDocument("bonustraits.xml");
			//var traitsDoc = new XmlDocument();
			//traitsDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.BonusTraits, "bonustraits.xml"));
			var trait = traitsDoc.SelectSingleNode("//trait[@name=\"" + BonusTrait + "\"]");
			if (trait != null)
			{
				foreach (var bonus in trait.ChildNodes.OfType<XmlElement>())
				{
					switch (bonus.Name)
					{
						case "stat":
							var increase = 20;
							var percent = true;
							if (bonus.HasAttribute("value"))
							{
								var x = bonus.GetAttribute("value");
								if (x.EndsWith("%"))
								{
									percent = true;
									x = x.Remove(x.Length - 1);
								}
								else
									percent = false;
								increase = int.Parse(x);
							}
							var stat = pc.GetToken(bonus.GetAttribute("id"));
							var oldVal = stat.Value;
							var newVal = oldVal + increase;
							if (percent)
								newVal = oldVal + ((increase / 100.0f) * oldVal);
							stat.Value = newVal;
							break;
						case "skill":
							var skill = bonus.GetAttribute("name").Replace(' ', '_').ToLowerInvariant();
							var by = bonus.HasAttribute("level") ? float.Parse(bonus.GetAttribute("level"), NumberStyles.Float, CultureInfo.InvariantCulture) : 1.0f;
							var skillToken = pc.Path("skills/" + skill);
							if (skillToken == null)
								skillToken = pc.GetToken("skills").AddToken(skill);
							skillToken.Value += by;
							break;
						case "rating":
							//TODO: implement the rating bonus trait effect.
							var path = bonus.GetAttribute("id");
							var v = bonus.GetAttribute("value");
							var g = bonus.GetAttribute("gender");
							if ((g == "female" && gender != Gender.Female) || (g == "male" && gender != Gender.Male))
								continue;
							var plus = false;
							percent = false;
							if (v.EndsWith("%"))
							{
								percent = true;
								v = v.Remove(v.Length - 1);
							}
							else if (v.StartsWith("+"))
							{
								plus = true;
								v = v.Substring(1);
							}
							increase = int.Parse(v);
							var aspect = pc.Path(path);
							if (aspect == null)
								continue;
							oldVal = aspect.Value;
							if (plus)
								newVal = oldVal + increase;
							else if (percent)
								newVal = oldVal + ((increase / 100.0f) * oldVal);
							else
								newVal = Math.Max(increase, oldVal);
							aspect.Value = newVal;
							break;
						case "token":
							path = bonus.GetAttribute("id");
							v = bonus.GetAttribute("value");
							g = bonus.GetAttribute("gender");
							if ((g == "female" && gender != Gender.Female) || (g == "male" && gender != Gender.Male))
								continue;
							var token = pc.Path(path);
							if (token == null)
							{
								if (path.Contains('/'))
									continue;
								token = pc.AddToken(path);
							}
							token.Value = float.Parse(v);
							break;
					}
				}
			}

			this.CurrentBoard = GetBoard(Overworld[StartingOWX, StartingOWY]);
			this.Player = new Player(pc)
			{
				XPosition = 40,
				YPosition = 12,
				ParentBoard = this.CurrentBoard,
				OverworldX = StartingOWX,
				OverworldY = StartingOWY,
				OnOverworld = true,
				CurrentRealm = "Nox",
			};
			this.CurrentBoard.Entities.Add(Player);

			Player.Character.RecalculateStatBonuses();
			Player.Character.CheckHasteSlow();

			/*
			var pregTest = Player.Character.AddToken("pregnancy");
			pregTest.AddToken("gestation").AddToken("max", 10, "");
			pregTest.AddToken("father", 0, "Ulfric Stormcloak");
			*/
			Player.Character.GetToken("items").AddToken("catmorph");

			InGame = true;
			SaveGame();
		}

		public static int GetOverworldIndex(Board board)
		{
			var n = HostForm.Noxico;
			for (var i = 0; i < n.Boards.Count && i < HostForm.Noxico.OverworldBarrier; i++)
				if (n.GetBoard(i).ID == board.ID)
					return i;
			return -1;
		}

		//TODO: have the game ask for a name through UITextBox. Use this for when 
		public string RollWorldName()
		{
			//var x = new[] { "The Magnificent", "Under", "The Hungry", "The Realm of", "Over", "The Isle of", "The Kingdom of" };
			//var y = new[] { "Boundary", "Earth", "Marrow", "Picking", "Farnsworth", Environment.UserName, "Kipperlings" };
			//var ret = Toolkit.PickOne(x) + ' ' + Toolkit.PickOne(y);
			//return ret;
			var x = Mix.GetString("Homestuck.txt").Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var a = Toolkit.PickOne(x);
			var b = Toolkit.PickOne(x);
			while(b == a)
				b = Toolkit.PickOne(x);
			return "Land of " + a + " and " + b;
		}

		public void RollPotions()
		{
			Potions = new string[256];
			var colors = new[] { "black", "blue", "green", "red", "yellow", "mauve", "brown", "white", "silver", "purple", "chocolate", "orange", "gray" };
			var mods = new[] { "", "bubbly ", "fizzy ", "viscious ", "translucent ", "smoky ", "smelly ", "fragrant ", "sparkly ", "tar-like " };
			for (var i = 0; i < 128; i++)
			{
				string roll = null;
				while (Potions.Contains(roll))
				{
					var color = colors[Toolkit.Rand.Next(colors.Length)];
					var mod = mods[Toolkit.Rand.NextDouble() > 0.6 ? Toolkit.Rand.Next(1, mods.Length) : 0];
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
					var color = colors[Toolkit.Rand.Next(colors.Length)];
					var mod = mods[Toolkit.Rand.NextDouble() > 0.6 ? Toolkit.Rand.Next(1, mods.Length) : 0];
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
						Console.WriteLine("Fuckup in applying to {0}.", item.ToString());
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
					var color = Toolkit.NameColor(rdesc.Remove(rdesc.IndexOf(' ')));
					var fore = item.Path("ascii/fore");
					if (fore == null)
						fore = item.GetToken("ascii").AddToken("fore");
					fore.Tokens.Clear();
					fore.AddToken(color);
				}
			}
		}

		public void ApplyMissions()
		{
			//TODO: add board drawing functions. This is JUST enough to implement Pettancow.

			Func<BoardType, int, int, Board> pickBoard = (boardType, biome, maxWater) =>
			{
				var options = new List<Board>();
				foreach (var board in Boards)
				{
					if (board == null)
						continue;
					if (board.Type != boardType)
						continue;
					if (biome > 0 && board.GetToken("biome").Value != biome)
						continue;
					if (maxWater != -1)
					{
						var water = 0;
						for (var y = 0; y < 25; y++)
							for (var x = 0; x < 80; x++)
								if (board.Tilemap[x, y].IsWater)
									water++;
						if (water > maxWater)
							continue;
					}
					options.Add(board);
				}
				if (options.Count == 0)
					return null;
				var choice = options[Toolkit.Rand.Next(options.Count)];
				return choice;
			};

			var js = Javascript.Create();
			Javascript.Ascertain(js);
			js.SetParameter("BoardType", typeof(BoardType));
			js.SetParameter("Character", typeof(Character));
			js.SetParameter("InventoryItem", typeof(InventoryItem));
			js.SetParameter("Tile", typeof(Tile));
			js.SetParameter("Color", typeof(System.Drawing.Color));
			js.SetFunction("GetBoard", new Func<int, Board>(x => GetBoard(x)));
			js.SetFunction("PickBoard", pickBoard);
			js.SetFunction("print", new Action<string>(x => Console.WriteLine(x)));
#if DEBUG
			js.SetDebugMode(true);
			//js.Step += (s, di) =>
			//{
			//	Console.Write("JINT: {0}", di.CurrentStatement.Source.Code.ToString());
			//};
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
					Console.WriteLine("Mission \"{0}\" by {1} is missing files.", manifest[0], manifest[1]);
					continue;
				}
				Console.WriteLine("Applying mission \"{0}\" by {1}...", manifest[0], manifest[1]);
				var jsCode = Mix.GetString(jsFile);
				js.Run(jsCode);
			}
		}
	}
}
