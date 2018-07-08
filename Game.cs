using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Noxico
{
	public enum KeyBinding
	{
		Left, Right, Up, Down, Rest, Activate, Items, Interact, Fly, Travel,
		Accept, Back, Pause, Screenshot, TabFocus, ScrollUp, ScrollDown
	}

	public class Message
	{
		public string Text { get; set; }
		public Color Color { get; set; }
		/// <summary>Should be increased by 1 every time the player gets to move.</summary>
		public int Turns { get; set; }
		public DateTime Time { get; private set; }

		public Message(string text)
		{
			Text = text;
			Color = Color.Silver;
			Turns = 0;
			Time = NoxicoGame.InGameTime;
		}

		public Message(string text, Color color)
		{
			Text = text;
			Color = color;
			Turns = 0;
			Time = NoxicoGame.InGameTime;
		}

		public override string ToString()
		{
			return Text;
		}
	}

	public class NoxicoGame
	{
		public static NoxicoGame Me
		{
			get
			{
				return NoxicoGame.HostForm.Noxico;
			}
		}

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

		public static SoundSystem Sound;
		public static char[] IngameToUnicode, IngameTo437;

		public static Dictionary<KeyBinding, Keys> KeyBindings { get; private set; }
		public static Dictionary<KeyBinding, string> RawBindings { get; private set; }

		public static List<InventoryItem> KnownItems { get; private set; }
		public List<Board> Boards { get; private set; }
		public Board CurrentBoard { get; set; }
		public static Board Limbo { get; private set; }
		public Player Player { get; set; }
		public static Dictionary<string, string[]> BookTitles { get; private set; }
		public static List<Message> Messages { get; private set; }
		public static List<Message> MessageLog { get; private set; }
		public static UserMode Mode { get; set; }
		public static Cursor Cursor { get; set; }
		public static SubscreenFunc Subscreen { get; set; }
		public static Dictionary<string, string> BodyplanHashes { get; private set; }
		public static string SavePath { get; private set; }
		public static bool InGame { get; set; }
		public static string ContextMessage { get; set; }

		public static int StartingOWX = -1, StartingOWY;
		private DateTime lastUpdate;
		//public string[] Potions;
		public static List<string> Identifications;
		public static Dictionary<int, string> TravelTargets;
		public static DateTime InGameTime; //public static NoxicanDate InGameTime;
		public static bool PlayerReady { get; set; }

		private static string lastMessage = string.Empty;
		public static string LookAt { get; set; }
		public static int WorldVersion { get; private set; }

		public static int CameraX, CameraY;

		private static int[][,] miniMap;

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

			Lua.Create();

			KeyBindings = new Dictionary<KeyBinding, Keys>();
			RawBindings = new Dictionary<KeyBinding, string>();
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
				if (SavePath.StartsWith('$'))
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
			Lua.Environment.HostForm = hostForm;
			KeyMap = new Dictionary<Keys, bool>();
			KeyTrg = new Dictionary<Keys, bool>();
			KeyRepeat = new Dictionary<Keys, DateTime>();
			for (var i = 0; i < 255; i++)
			{
				KeyMap.Add((Keys)i, false);
				KeyTrg.Add((Keys)i, false);
				KeyRepeat.Add((Keys)i, DateTime.Now);
			}
			Modifiers = new bool[3];
			Cursor = new Cursor();
			Messages = new List<Message>();
			MessageLog = new List<Message>();

			IngameToUnicode = new char[0x420];
			IngameTo437 = new char[0x420];
			for (var i = 0; i < 0x420; i++)
				IngameToUnicode[i] = IngameTo437[i] = '?';
			for (var i = 0x20; i < 0x80; i++)
				IngameToUnicode[i] = IngameTo437[i] = (char)i;
			var characterMap = Mix.GetString("lookup.txt");
			foreach (var l in characterMap.Split('\n'))
			{
				var line = l.Trim();
				if (line.IsBlank() || line[0] == '#')
					continue;
				var values = line.Split(new[] { ' ', '\t' }).Select(i => int.Parse(i, NumberStyles.HexNumber)).ToArray();
				IngameToUnicode[values[0]] = (char)values[1];
				IngameTo437[values[0]] = (char)values[2];
			}
			for (var i = 0; i < 0x1F; i++)
			{
				IngameTo437[i + 0x1E0] = IngameTo437[i];
				IngameToUnicode[i + 0x1E0] = IngameToUnicode[i];
			}

			Program.WriteLine("Seeing a man about some music...");
			Sound = new SoundSystem();

			Program.WriteLine("Loading bodyplans...");
			BodyplanHashes = new Dictionary<string, string>();
			Character.Bodyplans = Mix.GetTokenTree("bodyplans.tml");
			Character.Bodyplans.RemoveAll(t => t.Name != "bodyplan");
			foreach (var bodyPlan in Character.Bodyplans)
			{
				var id = bodyPlan.Text;
				var plan = bodyPlan.Tokens;
				Toolkit.VerifyBodyplan(bodyPlan, id);
				if (bodyPlan.HasToken("beast"))
					continue;
				BodyplanHashes.Add(id, Character.GetBodyComparisonHash(bodyPlan));
				Program.WriteLine("{0}\t{1}", id, BodyplanHashes[id]);
			}

#if DEBUG
			Program.WriteLine("----------------");
			foreach (var planRow in BodyplanHashes)
			{
				Program.Write(planRow.Key);
				foreach (var planCol in BodyplanHashes.Values)
				{
					Program.Write("\t{0}", planRow.Value.GetHammingDistance(planCol));
				}
				Program.WriteLine(string.Empty);
			}
			Program.WriteLine("----------------");
#endif

			Program.WriteLine("Loading special powers...");
			Character.Powers = new Dictionary<string, List<string>>();
			var powers = Mix.GetTokenTree("powers.tml");
			foreach (var power in new[] { "fullCopy", "sexCopy", "hover" })
			{
				Character.Powers[power] = new List<string>();
				var powerToken = powers.FirstOrDefault(t => t.Name == power);
				if (powerToken == null)
					continue;
				Character.Powers[power].AddRange(powerToken.Tokens.Select(t => t.Name));
			}

			Program.WriteLine("Loading items...");
			Identifications = new List<string>();

			var items = Mix.GetTokenTree("items.tml");
			KnownItems = new List<InventoryItem>();
			foreach (var item in items.Where(t => t.Name == "item" && !t.HasToken("disabled")))
				KnownItems.Add(InventoryItem.FromToken(item));

			/*
			Program.WriteLine("Randomizing potions and rings...");
			RollPotions();
			ApplyRandomPotions();
			*/

			Program.WriteLine("Preloading book info...");
			BookTitles = new Dictionary<string, string[]>();
			//Use GetFilesWithPattern to allow books in mission folders -- /missions/homestuck/books/legendbullshit.txt
			foreach (var book in Mix.GetFilesWithPattern("\\books\\*.txt"))
			{
				var bookFile = Mix.GetString(book, false).Split('\n');
				var bookID = Path.GetFileNameWithoutExtension(book);
				var bookName = bookFile[0].Substring(3).Trim();
				var bookAuthor = bookFile[1].StartsWith("## ") ? bookFile[1].Substring(3).Trim() : i18n.GetString("book_unknownauthor");
				BookTitles.Add(bookID, new[] { bookName, bookAuthor });
			}

			BiomeData.LoadBiomes();
			//Limbo = Board.CreateBasicOverworldBoard(BiomeData.ByName("nether"), "Limbo", "Limbo", "darkmere_deathtune.mod");
			//Limbo.BoardType = BoardType.Special;

			/*
			Random.Reseed(1);
			var test = Board.CreateBasicOverworldBoard(BiomeData.ByName("Grassland"), "test", "test", "test");
			test.Realm = Realms.Nox;
			test.MergeBitmap("missions\\playerbase\\toolshed.png", "missions\\playerbase\\lv0.txt");
			test.DumpToHtml();
			return;
			*/
			/*
			var weightedListTest = new Token();
			weightedListTest.Tokenize(@"
foo
	weight: 0.6
bar
	weight: 0.0
grill
quux
");
			for (var lol = 0; lol < 10; lol++)
			{
				var weightedChoice = weightedListTest.Pick();
			}
			*/
			/*
			Random.Reseed(1);
			var test = Board.CreateBasicOverworldBoard(BiomeData.ByName("Grassland"), "test", "test", "test");
			test.Realm = Realms.Nox;
			var gen = new TownGenerator();
			gen.Board = test;
			gen.Culture = Culture.DefaultCulture;
			gen.Create(BiomeData.Biomes[0]);
			gen.ToTilemap(ref test.Tilemap);
			test.DumpToHtml("test");
			Application.Exit();
			return;
			*/
			/*
			//New BoardDrawing methods :)
			Random.Reseed(1);
			var test = Board.CreateBasicOverworldBoard(BiomeData.ByName("Grassland"), "test", "test", "test");
			var env = Lua.Environment;
			env.testBoard = test;
			Lua.Run(@"
testBoard.Floodfill(1, 1, nil, ""nether"", true)

-- testBoard.Replace(201, 221); -- ints
-- testBoard.Replace(function(t,d,x,y) return t < 203 end, function(t,d,x,y) return t+1 end);

-- testBoard.Line(2, 2, 8, 16, 223) -- int: tile #
-- testBoard.Line(2, 2, 8, 16, ""taiga_rock"") -- string: tile name
-- testBoard.Line(2, 2, 8, 16, function(t, d, x, y) return 223 + (x % 2) end) -- a goddamn callback function!
-- t: tile #
-- d: t.Definition
-- x/y: coordinate
-- return: TileDefinition, tile name, or tile #.
");
			test.DumpToHtml();
			*/
			//var testChar = Character.Generate("felin", Gender.Male);
			//var test1 = "[t:He] [?:gesture-t-flirty].".Viewpoint(testChar);
			//var test = Lua.Run("return foo = 4 + \"foo\"");
			/*
			var testChar = Character.Generate("naga", Gender.Female);
			testChar.Culture = Culture.FindCultureByName("felin");
			var testLine = "[t:He] says, \"[?:Hello], my good [?:friend]! So much joy seeing you, it's great!\"".Viewpoint(testChar);
			var testLine2 = "g test: [hairlength]".Viewpoint(testChar);
			*/
			/*
			var testChar = Character.GetUnique("squeaky");
			var report = testChar.Mutate(20, 100, Mutations.Random);
			//report = testChar.Mutate(3, 1, Mutations.RemoveOddLegs);
			TextScroller.Plain(testChar.LookAt(null), testChar.GetKnownName(), false, true);
			return;
			*/
			
			InGameTime = new DateTime(740 + Random.Next(0, 20), 6, 26, DateTime.Now.Hour, 0, 0);
			TravelTargets = new Dictionary<int, string>();

			CurrentBoard = new Board();
			this.Player = new Player();
			Introduction.Title();
		}

		public void SaveGame(bool noPlayer = false, bool force = false, bool clear = true)
		{
			if (!InGame && !force)
				return;

			if (clear)
				HostForm.Clear();
			HostForm.Write(i18n.GetString("loadsave_saveheader") /* " -- Saving... -- " */, Color.White, Color.Black);
			//HostForm.Draw();

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
				/*
				Program.WriteLine("Potion check...");
				if (Potions[0] == null)
					RollPotions();
				*/
				b = new BinaryWriter(new CryptStream(f));
				Program.WriteLine("Player data...");
				Toolkit.SaveExpectation(b, "PLAY");
				b.Write(CurrentBoard.BoardNum);
				b.Write(Boards.Count);
				/*
				Program.WriteLine("Potions...");
				Toolkit.SaveExpectation(b, "POTI");
				for (var i = 0; i < 256; i++)
					b.Write(Potions[i] ?? "...");
				*/
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
				Program.WriteLine("Minimap...");
				Toolkit.SaveExpectation(b, "MMAP");
				b.Write((Int16)miniMap.Length);
				for (var i = 0; i < miniMap.Length; i++)
				{
					b.Write((Int16)miniMap[i].GetLength(0));
					b.Write((Int16)miniMap[i].GetLength(1));
					for (var y = 0; y < miniMap[i].GetLength(0); y++)
						for (var x = 0; x < miniMap[i].GetLength(1); x++)
							b.Write((byte)miniMap[i][y, x]);
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
			File.WriteAllText(verCheck, "20");
			Program.WriteLine("Done.");
			Program.WriteLine("--------------------------");
		}

		public void LoadGame()
		{
			var verCheck = Path.Combine(SavePath, WorldName, "version");
			if (!File.Exists(verCheck))
				throw new Exception("Tried to open an old worldsave.");
			WorldVersion = int.Parse(File.ReadAllText(verCheck));
			if (WorldVersion < 20)
				throw new Exception("Tried to open an old worldsave.");

			HostForm.Clear();
			HostForm.Write(i18n.GetString("loadsave_loadheader"), Color.White, Color.Black);
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

			//TODO: remove entirely after bumping version #
			var poti = bin.ReadChars(4);
			if (new string(poti) == "POTI")
			{
				Program.WriteLine("Ignoring POTI data...");
				for (var i = 0; i < 256; i++)
					bin.ReadString();
			}
			else
			{
				bin.BaseStream.Seek(-4, SeekOrigin.Current);
			}
			/*
			Toolkit.ExpectFromFile(bin, "POTI", "potion and ring");
			Potions = new string[256];
			for (var i = 0; i < 256; i++)
				Potions[i] = bin.ReadString();
			*/

			Toolkit.ExpectFromFile(bin, "ITID", "item identification");
			var numIDs = bin.ReadInt32();
			Identifications.Clear();
			for (var i = 0; i < numIDs; i++)
				Identifications.Add(bin.ReadString());
			Toolkit.ExpectFromFile(bin, "UNIQ", "unique item tracking");
			var numUniques = bin.ReadInt32();
			Toolkit.ExpectFromFile(bin, "TIME", "ingame time");
			InGameTime = new DateTime(bin.ReadInt64());
			Toolkit.ExpectFromFile(bin, "TARG", "known targets list");
			var numTargets = bin.ReadInt32();
			TravelTargets = new Dictionary<int, string>();
			for (var i = 0; i < numTargets; i++)
				TravelTargets.Add(bin.ReadInt32(), bin.ReadString());
			Toolkit.ExpectFromFile(bin, "MMAP", "minimap");
			miniMap = new int[bin.ReadInt16()][,];
			for (var i = 0; i < miniMap.Length; i++)
			{
				miniMap[i] = new int[bin.ReadInt16(), bin.ReadInt16()];
				for (var y = 0; y < miniMap[i].GetLength(0); y++)
					for (var x = 0; x < miniMap[i].GetLength(1); x++)
						miniMap[i][y, x] = bin.ReadByte();
			}

			//ApplyRandomPotions();
			ApplyRandomPotionsAndRings();
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
				CurrentBoard.PlayMusic();
				CurrentBoard.Redraw();
				CurrentBoard.AimCamera(Player.XPosition, Player.YPosition);

				if (!Player.Character.HasToken("player"))
					Player.Character.AddToken("player", (int)DateTime.Now.Ticks);
				Player.Character.RecalculateStatBonuses();
				Player.Character.CheckHasteSlow();

				//this solves SO MANY things holy shit
				if (Player.Character.BoardChar != Player)
					Player.Character.BoardChar = Player;
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

		public static void AgeMessages()
		{
			var maxLines = 5;
			var maxAge = 10;
			if (Messages.Count > maxLines)
				Messages.RemoveAll(m => m.Turns > 1);
			Messages.ForEach(m => m.Turns++);
			Messages.RemoveAll(m => m.Turns > maxAge);
		}

		public static void DrawMessages()
		{
			//assume top left
			//Me.CurrentBoard.Redraw();
			var max = 5;
			var line = 0;
			var from = Math.Max(0, Messages.Count - max);
			for (var i = from; i < Messages.Count; i++, line++)
				HostForm.Write(Messages[i].Text, Messages[i].Color, Color.Transparent, line, 0, true);
		}
	
		public static void ClearMessages()
		{
			Messages.Clear();
			//DrawMessages();
		}

		public static void AddMessage(object messageOrMore, object color = null)
		{
			if (messageOrMore is Neo.IronLua.LuaTable)
				messageOrMore = ((Neo.IronLua.LuaTable)messageOrMore).ArrayList.ToArray();

			while (messageOrMore is object[])
			{
				var options = (object[])messageOrMore;
				messageOrMore = options.PickOne();
				if (messageOrMore is Neo.IronLua.LuaTable)
					messageOrMore = ((Neo.IronLua.LuaTable)messageOrMore).ArrayList.ToArray();
			}

			var message = messageOrMore.ToString();

			/*
			if (message.Contains("[t:") || message.Contains("[b:"))
				message = message.Viewpoint((Character)Lua.Environment.top, (Character)Lua.Environment.bottom);
			*/

			//Do not accept black -- this would imply the parameter was left out.
			var clr = Color.Silver;
			if (color != null)
			{
				if (color is string)
					clr = Color.FromName((string)color);
				else if (color is Color)
					clr = (Color)color;
				if (string.IsNullOrEmpty(clr.Name))
					throw new Exception(string.Format("Message colors must be named, sorry. The color 0x{0:X} has no name.", clr.ArgbValue));
			}

			if (lastMessage != message)
			{
				lastMessage = message;
				var m = new Message(message, clr);
				Messages.Add(m);
				MessageLog.Add(m);
			}
			DrawMessages();
		}

		public static void ShowMessageLog()
		{
			if (MessageLog.Count == 0)
				MessageBox.Notice(i18n.GetString("nomoremessages"), true);
			else
				TextScroller.Plain(string.Join("\n", MessageLog.Where(m => !m.Text.StartsWith('\uE2FD')).Select(m => string.Format("{0} -- <c{1}>{2}<c>", m.Time.ToShortTimeString(), m.Color.Name, m.Text))));
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
					//if (HostForm.Cursor.X >= 0)
					//	HostForm.Cursor = new Point(-1, -1);
					HostForm.Cursor = new Point(Player.XPosition - CameraX, Player.YPosition - CameraY);

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
				else if (Mode == UserMode.Walkabout && PlayerReady)
				{
					if (Player.Character.HasToken("tutorial"))
						CheckForTutorialStuff();
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

		private static void SetStatus(string text, int progress, int maxProgress)
		{
			if (progress + maxProgress > 0)
			{
				text = string.Format("{0} - {1}/{2}", text, progress, maxProgress);
			}
			if (UIManager.Elements.Count >= 3 && UIManager.Elements[3] is UILabel)
			{
				UIManager.Elements[3].Text = text.PadRight(90);
				UIManager.Elements[3].Draw();
			}
			else
				HostForm.Write(text, Color.White, UIColors.LightBackground, 0, 0);
		}

		public void CreateRealm()
		{
			Action<string, int, int> setStatus = SetStatus;

			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();
			HostForm.Clear();

			var generator = new WorldMapGenerator();
			var townBoards = new List<Board>();
			miniMap = new int[Enum.GetValues(typeof(Realms)).Length][,];
			for (var i = 0; i < Enum.GetValues(typeof(Realms)).Length; i++)
			{
#if DEBUG
				Random.Reseed("pandora".GetHashCode());
#endif
				var realm = (Realms)i;
				generator.GenerateWorldMap(realm, setStatus);
				Program.WriteLine("{0} -- Generated basic geography and town locations.", stopwatch.Elapsed);
				Boardificate(generator, setStatus, realm);
				Program.WriteLine("{0} -- Made individual boards.", stopwatch.Elapsed);
				PlaceTowns(generator, setStatus, ref townBoards);
				Program.WriteLine("{0} -- Placed towns.", stopwatch.Elapsed);
				PlaceDungeons(generator, setStatus);
				Program.WriteLine("{0} -- Placed dungeon entrances.", stopwatch.Elapsed);
				ApplyMissions(generator, setStatus, realm);
				Program.WriteLine("{0} -- Applied missions. Basic world generation is now DONE.", stopwatch.Elapsed);

				miniMap[i] = generator.RoughBiomeMap;

#if DEBUG
				var png = new System.Drawing.Bitmap((generator.MapSizeX - 1) * 80, (generator.MapSizeY - 1) * 50);
				var gfx = System.Drawing.Graphics.FromImage(png);
				var font = new System.Drawing.Font("Silkscreen", 7);
				for (var y = 0; y < generator.MapSizeY - 1; y++)
				{
					setStatus("Drawing actual bitmap...", y, generator.MapSizeY);
					for (var x = 0; x < generator.MapSizeX - 1; x++)
					{
						var thisBoard = generator.BoardMap[y, x];
						if (thisBoard == null)
							continue; //draw empty spot?
						for (var ty = 0; ty < 50; ty++)
						{
							for (var tx = 0; tx < 80; tx++)
							{
								var tile = thisBoard.Tilemap[tx, ty];
								png.SetPixel((x * 80) + tx, (y * 50) + ty, tile.Definition.Background);
							}
						}
						gfx.DrawString(thisBoard.GetToken("biome").Value.ToString(), font, System.Drawing.Brushes.Red, (x * 80) + 1, (y * 50) + 1);
					}
				}
				png.Save("world_" + realm.ToString() + ".png");
#endif
			}

			Program.WriteLine("Generated all boards and contents in {0}.", stopwatch.Elapsed.ToString());

			//Wait for the character creator to finish.
			setStatus(i18n.GetString("worldgen_waitingcharacter"), 0, 0);
			Introduction.WorldGenFinished = true;
			while (this.Player.Character == null)
			{
				System.Threading.Thread.Sleep(50);
			}

			var homeBase = Boards.FirstOrDefault(b => b != null && b.ID == "home");
			if (homeBase == null)
			{
				while (true)
				{
					this.CurrentBoard = townBoards.PickOne();
					if (this.CurrentBoard.Realm == Realms.Nox) //don't spawn in the demon world!
						break;
				}
				if (!TravelTargets.ContainsKey(this.CurrentBoard.BoardNum))
					TravelTargets.Add(this.CurrentBoard.BoardNum, this.CurrentBoard.Name);
				this.Player.ParentBoard = this.CurrentBoard;
				this.CurrentBoard.Entities.Add(Player);
				this.Player.Reposition();
			}
			else
			{
				this.CurrentBoard = homeBase;
				this.Player.Character.AddToken("homeboard", homeBase.BoardNum);
				this.Player.Character.AddToken("homeboardlevel", 0);
				this.Player.Lives = 2;
				this.Player.Respawn();
				this.Player.Character.Health = this.Player.Character.MaximumHealth;
			}

			Directory.CreateDirectory(Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName));
			stopwatch.Stop();
			SaveGame(false, true, false);
			Program.WriteLine("Did all that and saved in {0}.", stopwatch.Elapsed.ToString());

			ApplyRandomPotionsAndRings();

			//setStatus(i18n.GetString("worldgen_ready"), 0, 0);
			ClearKeys();
			Immediate = true;
			Mode = UserMode.Walkabout;
			Me.CurrentBoard.Redraw();
			Me.CurrentBoard.Draw(true);
			Subscreens.FirstDraw = true;
			TextScroller.LookAt(NoxicoGame.Me.Player); // start by showing player details

			InGame = true;
			System.Threading.Thread.CurrentThread.Abort();
		}

		private void Boardificate(WorldMapGenerator generator, Action<string, int, int> setStatus, Realms realm)
		{
			for (var y = 0; y < generator.MapSizeY - 1; y++)
			{
				setStatus(i18n.GetString("worldgen_createboards"), y, generator.MapSizeY);
				for (var x = 0; x < generator.MapSizeX - 1; x++)
				{
					if (generator.RoughBiomeMap[y, x] == -1)
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
					newBoard.Realm = realm;
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
		}

		private void PlaceTowns(WorldMapGenerator generator, Action<string, int, int> setStatus, ref List<Board> townBoards)
		{
			var vendorTypes = new List<string>();
			var lootData = Mix.GetTokenTree("loot.tml", true);
			foreach (var filter in lootData.Where(t => t.Path("filter/vendorclass") != null).Select(t => t.Path("filter/vendorclass")))
				if (!vendorTypes.Contains(filter.Text))
					vendorTypes.Add(filter.Text);

			for (var i = 0; i < 8; i++)
			{
				setStatus(i18n.GetString("worldgen_towns"), i, 8);
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
							var cultureName = biome.Cultures.PickOne();
							townGen.Culture = Culture.Cultures[cultureName];
							townGen.Create(biome);
							townGen.ToTilemap(ref thisBoard.Tilemap);
							townGen.ToSectorMap(thisBoard.Sectors);
							thisBoard.Music = biome.Realm == Realms.Nox ? "set://Town" : "set://Dungeon";
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
							thisBoard.ID = string.Format("{0}x{1}-{2}", x, y, thisBoard.Name.ToID());

							var citizens = thisBoard.Entities.OfType<BoardChar>().Where(e => e.Character.Path("role/vendor") == null).ToList();
							foreach (var vendorType in vendorTypes)
							{
								if (Random.Flip())
									continue;
								if (citizens.Count == 0) //Shouldn't happen, but who knows.
									break;
								var chosenCitizen = citizens.PickOne();
								citizens.Remove(chosenCitizen);
								var spouse = chosenCitizen.Character.Spouse;
								if (spouse != null)
									citizens.Remove(spouse.BoardChar);
								var newVendor = chosenCitizen.Character;
								var vendorStock = newVendor.GetToken("items");
								newVendor.RemoveAll("role");
								newVendor.AddToken("role").AddToken("vendor").AddToken("class", 0, vendorType);
								newVendor.GetToken("money").Value = 1000 + (Random.Next(0, 20) * 50);
								Program.WriteLine("*** {0} of {2} is now a {1} ***", newVendor.Name.ToString(true), vendorType, thisBoard.Name);
								chosenCitizen.RestockVendor();
							}

							townBoards.Add(thisBoard);
						}
					}
				}
			}
		}

		private void PlaceDungeons(WorldMapGenerator generator, Action<string, int, int> setStatus)
		{
			var dungeonEntrances = 0;
			while (dungeonEntrances < 25)
			{
				setStatus(i18n.GetString("worldgen_dungeons"), dungeonEntrances, 25);
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
						if (thisBoard.IsSolid(eY - 1, eX, SolidityCheck.DryWalker))
							sides++;
						if (thisBoard.IsSolid(eY + 1, eX, SolidityCheck.DryWalker))
							sides++;
						if (thisBoard.IsSolid(eY, eX - 1, SolidityCheck.DryWalker))
							sides++;
						if (thisBoard.IsSolid(eY, eX + 1, SolidityCheck.DryWalker))
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
						thisBoard.SetTile(eY, eX, "dungeonEntrance");

						dungeonEntrances++;
					}
				}
			}
		}

		public void ApplyMissions(WorldMapGenerator generator, Action<string, int, int> setStatus, Realms realm)
		{
			setStatus(i18n.Format("worldgen_missions", realm), 0, 0);

			Func<string, Board> findBoardByID = (id) =>
			{
				var board = Boards.FirstOrDefault(b => b != null && b.ID == id);
				return board;
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
				var cultureName = culture.IsBlank() ? biome.Cultures.PickOne() : culture;
				townGen.Culture = Culture.Cultures[cultureName];
				townGen.Create(biome);
				townGen.ToTilemap(ref board.Tilemap);
				townGen.ToSectorMap(board.Sectors);
				board.GetToken("culture").Text = cultureName;
				while (true)
				{
					var newName = Culture.GetName(cultureName, Culture.NameType.Town);
					if (Boards.Find(b => b != null && b.Name == newName) == null)
					{
						board.Name = newName;
						break;
					}
				}
				return 0;
			};

			dynamic env = Lua.IronLua.CreateEnvironment();
			Lua.Ascertain(env);
			env.Realm = realm.ToString();
			env.GetBoard = new Func<int, Board>(x => GetBoard(x));
			env.FindBoardByID = findBoardByID;
			env.GetBiomeByName = new Func<string, int>(BiomeData.ByName);
			env.MakeTown = makeTown;
			env.print = new Action<string>(x =>
			{
				Program.WriteLine(x);
			});
			//Board.DrawEnv = env;

			var metadata = Mix.GetTokenTree("metadata.tml");
			foreach (var meta in metadata)
			{
				var name = meta.HasToken("name") ? meta.GetToken("name").Text : meta.Name.Replace('_', ' ').Titlecase();
				var author = meta.HasToken("author") ? meta.GetToken("author").Text : "Anonymous";
				var scriptFile = meta.HasToken("script") ? meta.GetToken("script").Text : ("mission-" + meta.Name + ".lua");
				Console.WriteLine("Applying \"{0}\" by {1}...", name, author);
				Lua.RunFile(scriptFile, env);
			}
		}

		public void CreatePlayerCharacter(string name, Gender bioGender, Gender idGender, int preference, string bodyplan, Dictionary<string, string> colorMap, string bonusTrait)
		{
			Board.HackishBoardTypeThing = "wild";
			var pc = Character.Generate(bodyplan, bioGender, idGender, Realms.Nox);
			var pref = pc.GetToken("sexpreference");
			if (pref == null)
				pref = pc.AddToken("sexpreference");
			pref.Value = preference;
			this.Player = new Player(pc);

			foreach (var item in pc.GetToken("items").Tokens)
				item.RemoveToken("owner");

			pc.IsProperNamed = true;
			if (!name.IsBlank())
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

			foreach (var color in colorMap)
			{
				var colorToken = pc.Path(color.Key);
				if (colorToken != null)
					colorToken.Text = color.Value;
				if (color.Value.StartsWith('['))
					colorToken.Text = string.Empty;
			}
			Action<TokenCarrier> removeBlanks = null;
			removeBlanks= new Action<TokenCarrier>(t =>
			{
				foreach (var token in t.Tokens)
				{
					if (token.HasToken("removeifblank") && token.Text.IsBlank())
					{
						t.RemoveToken(token);
						return;
					}
					else
					{
						removeBlanks(token);
					}
				}
			});
			removeBlanks(pc);

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
							if (!valueToken.Text.IsBlank())
							{
								if (valueToken.Text.EndsWith('%'))
								{
									var percentage = int.Parse(valueToken.Text.Remove(valueToken.Text.Length - 1));
									aspect.Value = (percentage / 100.0f) * oldVal;
								}
								else if (valueToken.Text.StartsWith('+'))
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
		}

		public static string RollWorldName()
		{
			var result = Lua.Environment.GetWorldName();
			return result.ToString();
		}

		public void ApplyRandomPotionsAndRings()
		{
			var seed = Random.ExtractSeed();
			Random.Reseed(WorldName);
			var done = new List<string>();
			var colors = i18n.GetArray("potion_colors");
			var potmods = i18n.GetArray("potion_mods");
			var ringmods = i18n.GetArray("potion_ringmods");
			foreach (var item in KnownItems.Where(ki => ki.HasToken("randomized")))
			{
				var isRing = (item.Path("equipable/ring") != null);
				var mods = isRing ? ringmods : potmods;
				var roll = string.Empty;
				var color = string.Empty;
				var mod = string.Empty;
				while (true)
				{
					color = colors.PickOne();
					mod = mods[Random.NextDouble() > 0.6 ? Random.Next(1, mods.Length) : 0];
					roll = i18n.Format(isRing ? "potion_ringname" : "potion_name", mod, color);
					if (!done.Contains(roll)) break;
				}
				item.UnknownName = roll; //item.AddToken("_u", roll);
				item.GetToken("ascii").AddToken("color", color);
				done.Add(roll);
			}
			Random.Reseed(seed);
		}

		/*
		public void RollPotions()
		{
			this.Potions = new string[256];
			for (var i = 0; i < 256; i++)
				Potions[i] = string.Empty;

			var colors = i18n.GetArray("potion_colors");
			var mods = i18n.GetArray("potion_mods");

			for (var i = 0; i < 128; i++)
			{
				string roll = string.Empty;
				while (Potions.Contains(roll))
				{
					var color = colors.PickOne();
					var mod = mods[Random.NextDouble() > 0.6 ? Random.Next(1, mods.Length) : 0];
					roll = i18n.Format("potion_name", mod, color) + '\0' + color;
				}
				Potions[i] = roll;
			}
			mods = i18n.GetArray("potion_ringmods");
			for (var i = 128; i < 192; i++)
			{
				string roll = string.Empty;
				while (Potions.Contains(roll))
				{
					var color = colors.PickOne();
					var mod = mods[Random.NextDouble() > 0.6 ? Random.Next(1, mods.Length) : 0];
					roll = i18n.Format("potion_ringname", mod, color) + '\0' + color;
				}
				Potions[i] = roll;
			}
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
					Program.WriteLine("Potions[{0}] = \"{1}\"", rid, Potions[rid]);
					var rdesc = Potions[rid].Remove(Potions[rid].IndexOf('\0'));

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
					var color = Color.NameColor(Potions[rid].Substring(Potions[rid].IndexOf('\0') + 1));
					var fore = item.Path("ascii/fore");
					if (fore == null)
						fore = item.GetToken("ascii").AddToken("fore");
					fore.Text = color;
				}
			}
		}
		*/

		public static void DrawSidebar()
		{
			var player = HostForm.Noxico.Player;
			if (NoxicoGame.Subscreen == Introduction.CharacterCreator)
				return;
			if (player == null || player.Character == null)
				return;
			var character = player.Character;

			Me.CurrentBoard.Redraw();
			if (Mode == UserMode.Walkabout)
				DrawMessages();

			Lua.Environment.player = player;
			Lua.Environment.Is437 = HostForm.Is437;
			Lua.Environment.DrawStatus();

			if (!LookAt.IsBlank())
				HostForm.Write(LookAt, Color.Silver, Color.Transparent, 0, 0, true);

			if (!ContextMessage.IsBlank())
				HostForm.Write(' ' + ContextMessage + ' ', Color.Silver, Color.Transparent, 0, 80 - ContextMessage.Length() - 2, true);
		}

		public static void CheckForTutorialStuff()
		{
			//We can assume this is only invoked when we -have- a tutorial token.
			var player = NoxicoGame.Me.Player.Character;
			var tutorial = player.GetToken("tutorial");
			if (tutorial.HasToken("dointeractmode"))
			{
				tutorial.AddToken("interactmode");
				MessageBox.Notice(i18n.GetString("tutorial_interactmode"), true, i18n.GetString("tutorial_title")); //, "tutorichel.png");
			}
			else if (!tutorial.HasToken("firstmoves") && tutorial.Value > 5)
			{
				tutorial.Value = 0;
				tutorial.AddToken("firstmoves");
				MessageBox.Notice(i18n.GetString("tutorial_firstmoves"), true, i18n.GetString("tutorial_title")); //, "tutorichel.png");
			}
			else if (!tutorial.HasToken("flying") && player.HasToken("wings") && !player.GetToken("wings").HasToken("small"))
			{
				tutorial.AddToken("flying");
				MessageBox.Notice(i18n.GetString("tutorial_flying"), true, i18n.GetString("tutorial_title")); //, "tutorichel.png");
			}
		}
	}
}
