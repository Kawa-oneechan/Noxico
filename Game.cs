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
		Left, Right, Up, Down, Rest, Activate, Items, Look, Aim, Chat, Fuck, Take, Drop, Travel,
		Accept, Back, Pause, Screenshot, LookAlt, TakeAlt, BackAlt, TabFocus, ScrollUp, ScrollDown
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
		private static string healthMessage;
		private static Color healthColor;
		private static int healthTimer;

		public static int StartingOWX = -1, StartingOWY;
		private DateTime lastUpdate;
		public string[] Potions;
		public static List<int> KnownTargets;
		public static Dictionary<int, string> TargetNames;
		public static NoxicanDate InGameTime;

		private static List<string> messageLog = new List<string>();
		public static int WorldVersion { get; private set; }

		public static Dictionary<int, Expectation> Expectations = new Dictionary<int, Expectation>();

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
				{ KeyBinding.Drop, GetIniKey("drop", Keys.D) },
				{ KeyBinding.Travel, GetIniKey("travel", Keys.T) },
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

			BiomeData.LoadBiomes();
			Ocean = Board.CreateBasicOverworldBoard(0, "Ocean", "The Ocean", "set://ocean");

			InGameTime = new NoxicanDate(740 + Toolkit.Rand.Next(0, 20), 6, 26, DateTime.Now.Hour, 0, 0);
			KnownTargets = new List<int>();
			TargetNames = new Dictionary<int, string>();

#if DEBUG
			//Towngen test
			//var towngenTest = Board.CreateBasicOverworldBoard(2, "TowngenTest", "Towngen Test", "set://debug");
			var towngenTest = Board.CreateBasicOverworldBoard(2, "DungeonTest", "DunGen Test", "set://debug");
			towngenTest.Type = BoardType.Town;
			CurrentBoard = towngenTest;
			var townGen = new StoneDungeonGenerator(); //new TownGenerator();
			townGen.Board = towngenTest;
			townGen.Culture = Culture.Cultures["human"];
			townGen.Create(BiomeData.Biomes[2]);
			townGen.ToTilemap(ref towngenTest.Tilemap);
			townGen.ToSectorMap(towngenTest.Sectors);
			towngenTest.DumpToHTML("final");
#endif

			CurrentBoard = new Board();
			this.Player = new Player();
				Introduction.Title();
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
				b = new BinaryWriter(new CryptStream(f));
				Console.WriteLine("Player data...");
				Toolkit.SaveExpectation(b, "PLAY");
				b.Write(CurrentBoard.BoardNum);
				b.Write(Boards.Count);
				Console.WriteLine("Potions...");
				Toolkit.SaveExpectation(b, "POTI");
				for (var i = 0; i < 256; i++)
					b.Write(Potions[i] ?? "...");
				Console.WriteLine("Unique Items counter lol...");
				Toolkit.SaveExpectation(b, "UNIQ");
				b.Write(0);
				Toolkit.SaveExpectation(b, "TIME");
				b.Write(InGameTime.ToBinary());
				Toolkit.SaveExpectation(b, "TARG");
				b.Write(KnownTargets.Count);
				KnownTargets.ForEach(x => b.Write(x));
				Toolkit.SaveExpectation(b, "TARN");
				b.Write(TargetNames.Count);
				foreach (var target in TargetNames)
				{
					b.Write(target.Key);
					b.Write(target.Value);
				}
				Toolkit.SaveExpectation(b, "EXPL");
				b.Write(Expectations.Count);
				foreach (var expectation in Expectations)
				{
					b.Write(expectation.Key);
					expectation.Value.SaveToFile(b);
				}
			}

			Console.WriteLine("--------------------------");
			Console.WriteLine("Saving World...");

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
			File.WriteAllText(verCheck, "15");
			Console.WriteLine("Done.");
			Console.WriteLine("--------------------------");
		}

		public void LoadGame()
		{
			var verCheck = Path.Combine(SavePath, WorldName, "version");
			if (!File.Exists(verCheck))
				throw new Exception("Tried to open an old worldsave.");
			WorldVersion = int.Parse(File.ReadAllText(verCheck));
			if (WorldVersion < 15)
				throw new Exception("Tried to open an old worldsave.");

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
				MessageBox.Message("Invalid world header.");
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
			Toolkit.ExpectFromFile(bin, "UNIQ", "unique item tracking");
			var numUniques = bin.ReadInt32();
			Toolkit.ExpectFromFile(bin, "TIME", "ingame time");
			InGameTime = new NoxicanDate(bin.ReadInt64());
			Toolkit.ExpectFromFile(bin, "TARG", "known targets list");
			var numTargets = bin.ReadInt32();
			KnownTargets = new List<int>();
			for (var i = 0; i < numTargets; i++)
				KnownTargets.Add(bin.ReadInt32());
			Toolkit.ExpectFromFile(bin, "TARN", "target names list");
			numTargets = bin.ReadInt32();
			TargetNames = new Dictionary<int, string>();
			for (var i = 0; i < numTargets; i++)
				TargetNames.Add(bin.ReadInt32(), bin.ReadString());
			Toolkit.ExpectFromFile(bin, "EXPL", "expectation list");
			var numExpectations = bin.ReadInt32();
			Expectations = new Dictionary<int, Expectation>();
			for (var i = 0; i < numExpectations; i++)
				Expectations.Add(bin.ReadInt32(), Expectation.LoadFromFile(bin));
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
			if (healthTimer > 0)
				HostForm.Write(healthMessage, healthColor, Color.Black, 0, 0);

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
			if (healthTimer > 0)
				healthTimer--;

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
			if (Messages.Count > 0 && Messages.Last().Message == message)
				Messages.Last().New = true;
			else
			{
				Messages.Add(new StatusMessage() { Message = message, Color = color, New = true });
				if (Mode == UserMode.Walkabout)
					messageLog.Add(InGameTime.ToShortTimeString() + " -- " + message);
			}
		}
		public static void AddMessage(string message)
		{
			AddMessage(message, Color.Silver);
		}

		public static void ShowMessageLog()
		{
			if (messageLog.Count == 0)
				MessageBox.Message("There are no messages to display.", true);
			else
				TextScroller.Plain(string.Join("\n", messageLog));
		}

		public static void HealthMessage()
		{
			healthTimer = 5;
			var pc = HostForm.Noxico.Player.Character;
			var hp = pc.GetStat(Stat.Health);
			var max = pc.GetMaximumHealth();
			healthColor = Color.Lime;
			if (hp <= max / 4)
				healthColor = Color.Red;
			else if (hp <= max / 2)
				healthColor = Color.Yellow;
			healthMessage = string.Format("{0}/{1}", hp, max);
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

		public void CreateRealm()
		{
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

			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();

			setStatus("Generating handful of towns...");
			var townGen = new TownGenerator();
			for (var i = 0; i < 2; i++)
			{
				var thisMap = WorldGen.CreateTown(-1, null, null, true);
				KnownTargets.Add(thisMap.BoardNum);
				TargetNames.Add(thisMap.BoardNum, thisMap.Name);
			}

			/*
			KnownTargets.Add(-10);
			TargetNames.Add(-10, "OndemandVille");
			Expectations.Add(-10, new Expectation() { Biome = 9 });
			KnownTargets.Add(-12);
			TargetNames.Add(-12, "Dreadmor Caverns");
			Expectations.Add(-12, new Expectation() { Dungeon = true });
			*/


			setStatus("Placing dungeon entrances...");
			foreach (var board in Boards)
			{
				if (board.Type == BoardType.Town)
					continue;

				//Don't always place one, to prevent clumping
				if (Toolkit.Rand.NextDouble() > 0.4)
					continue;

				var eX = Toolkit.Rand.Next(2, 78);
				var eY = Toolkit.Rand.Next(1, 23);

				if (board.IsSolid(eY, eX))
					continue;
				var sides = 0;
				if (board.IsSolid(eY - 1, eX))
					sides++;
				if (board.IsSolid(eY + 1, eX))
					sides++;
				if (board.IsSolid(eY, eX - 1))
					sides++;
				if (board.IsSolid(eY, eX + 1))
					sides++;
				if (sides > 3)
					continue;

				var newWarp = new Warp()
				{
					TargetBoard = -1, //mark as ungenerated dungeon
					ID = board.ID + "_Dungeon",
					XPosition = eX,
					YPosition = eY,
				};
				board.Warps.Add(newWarp);
				board.SetTile(eY, eX, '>', Color.Silver, Color.Black);
			}
		
			setStatus("Applying missions...");
			//Board.WorldGen = worldGen;
			//ApplyMissions();

			Console.WriteLine("Generated all boards and contents in {0}.", stopwatch.Elapsed.ToString());

			setStatus("Saving chunks... (lol)");
			for (var i = 0; i < this.Boards.Count; i++)
			{
				if (this.Boards[i] == null)
					continue;
				this.Boards[i].SaveToFile(i);
				if (i > 0)
					this.Boards[i] = null;
			}
			stopwatch.Stop();
			Console.WriteLine("Did all that and saved in {0}.", stopwatch.Elapsed.ToString());
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
							var f = 0f;
							if (float.TryParse(v, out f))
								token.Value = f;
							break;
					}
				}
			}

			this.CurrentBoard = GetBoard(KnownTargets[0]);
			this.Player = new Player(pc)
			{
				XPosition = 40,
				YPosition = 12,
				ParentBoard = this.CurrentBoard,
			};
			this.CurrentBoard.Entities.Add(Player);

			Player.Character.RecalculateStatBonuses();
			Player.Character.CheckHasteSlow();

			/*
			var pregTest = Player.Character.AddToken("pregnancy");
			pregTest.AddToken("gestation").AddToken("max", 10, "");
			pregTest.AddToken("father", 0, "Ulfric Stormcloak");
			*/
			//Player.Character.GetToken("items").AddToken("catmorph");

			InGame = true;
			SaveGame();
		}

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

	public class Expectation
	{
		public bool Dungeon { get; set; }
		public int Biome { get; set; }
		public string Culture { get; set; }
		public List<string> Roles { get; set; }
		public List<string> Species { get; set; }
		
		public Expectation()
		{
			Dungeon = false;
			Biome = -1;
			Culture = string.Empty;
			Roles = new List<string>();
			Species = new List<string>();
		}
		
		public static Expectation LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "EXPT", "location expectation");
			var exp = new Expectation();
			exp.Dungeon = stream.ReadBoolean();
			exp.Biome = (int)stream.ReadInt16();
			exp.Culture = stream.ReadString();
			var numRoles = stream.ReadInt16();
			var numSpecies = stream.ReadInt16();
			for (var i = 0; i < numRoles; i++)
				exp.Roles.Add(stream.ReadString());
			for (var i = 0; i < numSpecies; i++)
				exp.Species.Add(stream.ReadString());
			return exp;
		}
		
		public void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "EXPT");
			stream.Write(Dungeon);
			stream.Write((Int16)Biome);
			stream.Write(Culture ?? string.Empty);
			stream.Write((Int16)Roles.Count);
			stream.Write((Int16)Species.Count);
			foreach (var role in Roles)
				stream.Write(role);
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
			var boards = NoxicoGame.HostForm.Noxico.Boards;
			if (biomeID < 0)
				biomeID = Toolkit.Rand.Next(2, 5);
			var biome = BiomeData.Biomes[biomeID];
			var cultureName = biome.Cultures[Toolkit.Rand.Next(biome.Cultures.Length)];
			var culture = Noxico.Culture.Cultures[cultureName];

			if (string.IsNullOrEmpty(name))
			{
				while (true)
				{
					name = Noxico.Culture.GetName(culture.TownName, Noxico.Culture.NameType.Town);
					if (boards.Find(b => b != null && b.Name == name) == null)
						break;
				}
			}
			var id = -10;
			if (NoxicoGame.Expectations.Count > 0)
				id = NoxicoGame.Expectations.Last().Key;
			id--;
			NoxicoGame.Expectations.Add(id, new Expectation() { Biome = biomeID, Culture = cultureName });
			NoxicoGame.TargetNames.Add(id, name);
			NoxicoGame.KnownTargets.Add(id);
			return NoxicoGame.Expectations[id];
		}
	}
}
