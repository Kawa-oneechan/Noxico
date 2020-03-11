using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing;

namespace Noxico
{
	/// <summary>
	/// Displays the title and character creator subscreens.
	/// </summary>
	public class Introduction
	{
		private static System.Threading.Thread worldgen;
		public static bool WorldGenFinished;
		//private static int twirlingBaton = 0;
		//private static char[] twirlingBatons = "-\\|/".ToCharArray();
		private static UIPNGBackground titleBack;
		private static UILabel titleCaption, titlePressEnter;

		/// <summary>
		/// Should we be running world generation (be in the Character Creator) and exiting...
		/// </summary>
		public static void KillWorldgen()
		{
			if (worldgen != null && worldgen.ThreadState == System.Threading.ThreadState.Running)
				worldgen.Abort();
		}
		
		/// <summary>
		/// Sets up the title screen.
		/// </summary>
		public static void Title()
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = Introduction.TitleHandler;
			NoxicoGame.Immediate = true;
			NoxicoGame.Sound.PlayMusic("set://Title");
		}

		/// <summary>
		/// Generic Subscreen handler.
		/// </summary>
		public static void TitleHandler()
		{
			var host = NoxicoGame.HostForm;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				host.Clear();
				var xScale = Program.Cols / 80f;
				var yScale = Program.Rows / 25f;

				var background = new Bitmap(Program.Cols, Program.Rows * 2, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				var logo = Mix.GetBitmap("logo.png");
				var titleOptions = Mix.GetFilesWithPattern("titles\\*.png");
				var chosen = Mix.GetBitmap(titleOptions.PickOne());
				//Given our random backdrop and fixed logo, draw them both onto background
				//because we can't -just- display alpha-blended PNGs.
				using (var gfx = Graphics.FromImage(background))
				{
					gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
					gfx.Clear(Color.Black);
					gfx.DrawImage(chosen, 0, 0, Program.Cols, Program.Rows * 2);
					gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
					gfx.DrawImage(logo, 0, 0, logo.Width * xScale, logo.Height * yScale);
				}
				UIManager.Initialize();
				titleBack = new UIPNGBackground(background);

				var subtitle = i18n.GetString("ts_subtitle");
				var pressEnter = "\xC4\xC4\xC4\xC4\xB4 " + i18n.GetString("ts_pressentertobegin") + " <cGray>\xC3\xC4\xC4\xC4\xC4";
				titleCaption = new UILabel(subtitle) { Top = (int)(10 * xScale), Left = (int)(25 * yScale) - subtitle.Length() / 2, Foreground = Color.Teal, Darken = true };
				titlePressEnter = new UILabel(pressEnter) { Top = (int)(10 * xScale) + 2, Left = (int)(25 * yScale) - pressEnter.Length() / 2, Foreground = Color.Gray, Darken = true };
				UIManager.Elements.Add(titleBack);
				UIManager.Elements.Add(titleCaption);
				UIManager.Elements.Add(titlePressEnter);
				//UIManager.Elements.Add(new UILabel("\u015c") { Top = 6, Left = 50, Foreground = Color.Gray });
				UIManager.Draw();
			}
			if (NoxicoGame.IsKeyDown(KeyBinding.Accept) || Subscreens.Mouse || Vista.Triggers != 0)
			{
				if (Subscreens.Mouse)
					Subscreens.UsingMouse = true;
				Subscreens.Mouse = false;
				Subscreens.FirstDraw = true;
				var rawSaves = Directory.GetDirectories(NoxicoGame.SavePath);
				var saves = new List<string>();
				//Check each possible save's version.
				foreach (var s in rawSaves)
				{
					var verCheck = Path.Combine(s, "version");
					if (!File.Exists(verCheck))
						continue;
					var version = int.Parse(File.ReadAllText(verCheck));
					if (version < 20)
						continue;
					if (File.Exists(Path.Combine(s, "global.bin")))
						saves.Add(s);
				}
				NoxicoGame.ClearKeys();
				Subscreens.Mouse = false;
				//Linq up a set of options for each save game. This returns the game's names as the keys.
				var options = saves.ToDictionary(new Func<string, object>(s => Path.GetFileName(s)), new Func<string, string>(s =>
				{
					string p;
					var playerFile = Path.Combine(s, "player.bin");
					if (File.Exists(playerFile))
					{
						using (var f = new BinaryReader(File.OpenRead(playerFile)))
						{
							p = Player.LoadFromFile(f).Character.Name.ToString(true);
						}
						return i18n.Format("ts_loadgame", p, Path.GetFileName(s));
					}
					return i18n.Format("ts_startoverinx", Path.GetFileName(s));
				}));
				options.Add("~", i18n.GetString("ts_startnewgame"));
				options.Add("~~", i18n.GetString("ts_testingarena"));
				//Display our list of saves.
				MessageBox.List(saves.Count == 0 ? i18n.GetString("ts_welcometonoxico") : i18n.GetString(saves.Count == 1 ? "ts_thereisasave" : "ts_therearesaves"), options,
					() =>
					{
						if ((string)MessageBox.Answer == "~")
						{
							//Restore our title screen backdrop, since the MessageBox subscreen purged it.
							UIManager.Elements.Add(titleBack);
							UIManager.Elements.Add(titleCaption);
							UIManager.Elements.Add(titlePressEnter);
							UIManager.Draw();
							MessageBox.Input("What name would you like for your new world?",
								NoxicoGame.RollWorldName(),
								() =>
								{
									NoxicoGame.WorldName = (string)MessageBox.Answer;
									NoxicoGame.Mode = UserMode.Subscreen;
									NoxicoGame.Subscreen = Introduction.CharacterCreator;
									NoxicoGame.Immediate = true;
								}
							);
						}
						else if ((string)MessageBox.Answer == "~~")
						{
							NoxicoGame.WorldName = "<Testing Arena>";
							var env = Lua.Environment;
							Lua.RunFile("testarena.lua");
							var testBoard = new Board(env.TestArena.ArenaWidth, env.TestArena.ArenaHeight);
							var me = NoxicoGame.Me;
							me.Boards.Add(testBoard);
							me.CurrentBoard = testBoard;
							me.CreatePlayerCharacter(env.TestArena.Name, env.TestArena.BioGender, env.TestArena.IdentifyAs, env.TestArena.Preference, env.TestArena.Bodyplan, new Dictionary<string, string>(), env.TestArena.BonusTrait);
							env.BuildTestArena(testBoard);
							me.Player.ParentBoard = testBoard;
							testBoard.EntitiesToAdd.Add(me.Player);
							NoxicoGame.InGameTime = new DateTime(740 + Random.Next(0, 20), 6, 26, 12, 0, 0);
							testBoard.UpdateLightmap(null, true);
							testBoard.AimCamera();
							testBoard.Redraw();
							testBoard.Draw();
							Subscreens.FirstDraw = true;
							NoxicoGame.Immediate = true;
							NoxicoGame.AddMessage(i18n.GetString("welcometest"), Color.Yellow);
							NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
							NoxicoGame.Mode = UserMode.Walkabout;
						}
						else
						{
							NoxicoGame.WorldName = (string)MessageBox.Answer;
							host.Noxico.LoadGame();
							NoxicoGame.Me.CurrentBoard.Draw();
							Subscreens.FirstDraw = true;
							NoxicoGame.Immediate = true;
							NoxicoGame.AddMessage(i18n.GetString("welcomeback"), Color.Yellow);
							NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
							//TextScroller.LookAt(NoxicoGame.Me.Player);
							NoxicoGame.Mode = UserMode.Walkabout;
						}
					}
				);
			}
		}

		/// <summary>
		/// Holds a bunch of info for the character creator.
		/// </summary>
		private class PlayableRace
		{
			/// <summary>
			/// Internal name of this race
			/// </summary>
			public string ID { get; set; }
			/// <summary>
			/// Display name
			/// </summary>
			public string Name { get; set; }
			/// <summary>
			/// Maps token paths to UIElements -- presumably UIColorLists and UISingleLists, and their UILabels.
			/// </summary>
			public Dictionary<string, UIElement> ColorItems { get; set; }
			/// <summary>
			/// Determines which genders can't be picked. Maps directly to UIRadioList.ItemsEnabled.
			/// </summary>
			public bool[] SexLocks { get; set; }
			/// <summary>
			/// Description, if any.
			/// </summary>
			public string Bestiary { get; set; }
			public override string ToString()
			{
				return Name;
			}
		}

		/// <summary>
		/// Goes through bodyplans.tml to get a list of PlayableRaces.
		/// </summary>
		private static void CollectPlayables()
		{
			playables = new List<PlayableRace>();
			Program.WriteLine("Collecting playables...");
			foreach (var bodyPlan in Character.Bodyplans.Where(t => t.Name == "bodyplan"))
			{
				var id = bodyPlan.Text;
				var plan = bodyPlan.Tokens;
				if (!bodyPlan.HasToken("playable"))
					continue;
				Program.WriteLine(" * Parsing {0}...", id);

				var sexlocks = new[] { true, true, true, false };
				if (bodyPlan.HasToken("normalgenders"))
					sexlocks = new[] { true, true, false, false };
				else if (bodyPlan.HasToken("maleonly"))
					sexlocks = new[] { true, false, false, false };
				else if (bodyPlan.HasToken("femaleonly"))
					sexlocks = new[] { false, true, false, false };
				else if (bodyPlan.HasToken("hermonly"))
					sexlocks = new[] { false, false, true, false };
				else if (bodyPlan.HasToken("neuteronly"))
					sexlocks = new[] { false, false, false, true };
				if (bodyPlan.HasToken("allowneuter"))
					sexlocks[3] = true;

				//Use the ID ("bodyplan: example") as the name, unless there's a "playable: proper name".
				var name = id.Replace('_', ' ').Titlecase();
				if (!bodyPlan.GetToken("playable").Text.IsBlank())
					name = bodyPlan.GetToken("playable").Text;

				var bestiary = bodyPlan.HasToken("bestiary") ? bodyPlan.GetToken("bestiary").Text : string.Empty;

				//Figure out what to put on page two.
				//By default, we assume skin and hair colors can be edited.
				//Most bodyplans will be different, though.
				var colorItems = new Dictionary<string, UIElement>();
				var editables = "skin/color|Skin color, hair/color|Hair color"; //path|Label, path|Label...
				if (bodyPlan.HasToken("editable"))
					editables = bodyPlan.GetToken("editable").Text;

				var xScale = Program.Cols / 80f;
				var yScale = Program.Rows / 25f;
				var ccpage = Mix.GetBitmap("ccpage.png");
				var pageLeft = Program.Cols - ccpage.Width - (int)(2 * yScale);
				var pageTop = Program.Rows - (ccpage.Height / 2);

				var labelL = pageLeft + 5;
				var cntrlL = labelL + 2;
				var top = (pageTop / 2) + 5;
				var width = 26;

				foreach (var aspect in editables.Split(','))
				{
					if (aspect.IsBlank())
						continue;
					var a = aspect.Trim().Split('|');
					var path = a[0];
					var label = a[1];
					var t = bodyPlan.Path(path).Text;
					if (t.StartsWith("oneof"))
						t = t.Substring(6);
					var oneof = t.Split(',').ToList();
					var items = new List<string>();
					colorItems.Add("lbl-" + path, new UILabel(label) { Left = labelL, Top = top, Foreground = Color.Gray });
					foreach (var i in oneof)
					{
						var iT = i.Trim();
						iT = iT.IsBlank(i18n.GetString("[none]", false), iT.Titlecase());
						if (!items.Contains(iT))
							items.Add(iT);
					}
					if (label.EndsWith("color", StringComparison.InvariantCultureIgnoreCase))
					{
						for (var i = 0; i < items.Count; i++)
							items[i] = Color.NameColor(items[i]).Titlecase();
						colorItems.Add(path, new UIColorList() { Items = items, Left = cntrlL, Top = top + 1, Width = width, Foreground = Color.Black, Background = Color.Transparent, Index = 0 });
					}
					else
						colorItems.Add(path, new UISingleList() { Items = items, Left = cntrlL, Top = top + 1, Width = width, Foreground = Color.Black, Background = Color.Transparent, Index = 0 });
					top += 2;
				}

				playables.Add(new PlayableRace() { ID = id, Name = name, Bestiary = bestiary, ColorItems = colorItems, SexLocks = sexlocks });
			}
		}
		/// <summary>
		/// Information on all bodyplans with a playable token, built by CollectPlayables.
		/// </summary>
		private static List<PlayableRace> playables;

		private static Dictionary<string, UIElement> controls;
		private static List<UIElement>[] pages;
		private static Dictionary<string, string> controlHelps;
		//private static Dictionary<string, Bitmap> portraits;

		private static int page = 0;
		private static Action<int> loadPage, loadColors; //, redrawBackdrop;
		private static Bitmap backdrop; //, backWithPortrait;

		/// <summary>
		/// Don't see a Subscreen with multiple handlers often...
		/// </summary>
		public static void CharacterCreator()
		{
			if (Subscreens.FirstDraw)
			{
				//Start creating the world as we work...
				if (worldgen == null) //Conditional added by Mat.
				{
					worldgen = new System.Threading.Thread(NoxicoGame.Me.CreateRealm);
					worldgen.Start();
				}

				//Load all bonus traits.
				var traits = new List<string>();
				var traitHelps = new List<string>();
				var traitsDoc = Mix.GetTokenTree("bonustraits.tml");
				foreach (var trait in traitsDoc.Where(t => t.Name == "trait"))
				{
					traits.Add(trait.GetToken("display").Text);
					traitHelps.Add(trait.GetToken("description").Text);
				}
				//Define the help texts for the standard controls.
				controlHelps = new Dictionary<string, string>()
				{
					{ "back", i18n.GetString("cchelp_back") },
					{ "next", i18n.GetString("cchelp_next") },
					{ "play", Random.NextDouble() > 0.7 ? "FRUITY ANGELS MOLEST SHARKY" : "ENGAGE RIDLEY MOTHER FUCKER" },
					{ "name", i18n.GetString("cchelp_name") },
					{ "species", string.Empty },
					{ "sex", i18n.GetString("cchelp_sex") },
					{ "gid", i18n.GetString("cchelp_gid") },
					{ "pref", i18n.GetString("cchelp_pref") },
					{ "tutorial", i18n.GetString("cchelp_tutorial") },
					{ "easy", i18n.GetString("cchelp_easy") },
					{ "gift", traitHelps[0] },
				};

				var xScale = Program.Cols / 80f;
				var yScale = Program.Rows / 25f;

				//backdrop = Mix.GetBitmap("chargen.png");
				backdrop = new Bitmap(Program.Cols, Program.Rows * 2, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				var ccback = Mix.GetBitmap("ccback.png");
				var ccpage = Mix.GetBitmap("ccpage.png");
				var ccbars = Mix.GetBitmap("ccbars.png");
				var pageLeft = Program.Cols - ccpage.Width - (int)(2 * yScale);
				var pageTop = Program.Rows - (ccpage.Height / 2);
				using (var gfx = Graphics.FromImage(backdrop))
				{
					gfx.Clear(Color.Black);
					gfx.DrawImage(ccback, 0, 0, Program.Cols, Program.Rows * 2);
					gfx.DrawImage(ccpage, pageLeft, pageTop, ccpage.Width, ccpage.Height);
					var barsSrc = new System.Drawing.Rectangle(0, 3, ccbars.Width, 7);
					var barsDst = new System.Drawing.Rectangle(0, 0, Program.Cols, 7);
					gfx.DrawImage(ccbars, barsDst, barsSrc, GraphicsUnit.Pixel);
					barsSrc = new System.Drawing.Rectangle(0, 0, ccbars.Width, 5);
					barsDst = new System.Drawing.Rectangle(0, (Program.Rows * 2) - 5, Program.Cols, 5);
					gfx.DrawImage(ccbars, barsDst, barsSrc, GraphicsUnit.Pixel);
				}

				//Build the interface.
				var title = "\xB4 " + i18n.GetString("cc_title") + " \xC3";
				var bar = new string('\xC4', 33);
				string[] sexoptions = {i18n.GetString("Male"), i18n.GetString("Female"), i18n.GetString("Herm"), i18n.GetString("Neuter")};
				string[] prefoptions = { i18n.GetString("Male"), i18n.GetString("Female"), i18n.GetString("Either") };

				var labelL = pageLeft + 5;
				var cntrlL = labelL + 2;
				var header = (pageTop / 2) + 3;
				var top = header + 2;
				var buttons = (pageTop / 2) + 20;

				controls = new Dictionary<string, UIElement>()
				{
					{ "backdrop", new UIPNGBackground(backdrop) }, //backWithPortrait) },
					{ "headerline", new UILabel(bar) { Left = labelL, Top = header, Foreground = Color.Black } },
					{ "header", new UILabel(title) { Left = labelL + 17 - (title.Length() / 2), Top = header, Width = title.Length(), Foreground = Color.Black } },
					{ "back", new UIButton(i18n.GetString("cc_back"), null) { Left = labelL, Top = buttons, Width = 10, Height = 1 } },
					{ "next", new UIButton(i18n.GetString("cc_next"), null) { Left = labelL + 23, Top = buttons, Width = 10, Height = 1 } },
					{ "wait", new UILabel(i18n.GetString("cc_wait")) { Left = labelL + 23, Top = buttons, Foreground = Color.Silver } },
					{ "play", new UIButton(i18n.GetString("cc_play"), null) { Left = labelL + 23, Top = buttons, Width = 10, Height = 1, Hidden = true } },

					{ "nameLabel", new UILabel(i18n.GetString("cc_name")) { Left = labelL, Top = top, Foreground = Color.Gray } },
					{ "name", new UITextBox(string.Empty) { Left = cntrlL, Top = top + 1, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "nameRandom", new UILabel(i18n.GetString("cc_random")) { Left = cntrlL + 2, Top = top + 1, Foreground = Color.Gray } },
					{ "speciesLabel", new UILabel(i18n.GetString("cc_species")) { Left = labelL, Top = top + 3, Foreground = Color.Gray } },
					{ "species", new UISingleList() { Left = cntrlL, Top = top + 4, Width = 26, Foreground = Color.Black, Background = Color.Transparent } },
					{ "tutorial", new UIToggle(i18n.GetString("cc_tutorial")) { Left = labelL, Top = top + 6, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "easy", new UIToggle(i18n.GetString("cc_easy")) { Left = labelL, Top = top + 7, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },

					{ "sexLabel", new UILabel(i18n.GetString("cc_sex")) { Left = labelL, Top = top, Foreground = Color.Gray } },
					{ "sex", new UIRadioList(sexoptions) { Left = cntrlL, Top = top + 1, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "gidLabel", new UILabel(i18n.GetString("cc_gid")) { Left = labelL, Top = top + 6, Foreground = Color.Gray } },
					{ "gid", new UISingleList(string.Empty, null, sexoptions.ToList(), 0) { Left = cntrlL, Top = top + 7, Width = 26, Foreground = Color.Black, Background = Color.Transparent } },
					{ "prefLabel", new UILabel(i18n.GetString("cc_pref")) { Left = labelL, Top = top + 9, Foreground = Color.Gray } },
					{ "pref", new UISingleList(string.Empty, null, prefoptions.ToList(), 1) { Left = cntrlL, Top = top + 10, Width = 26, Foreground = Color.Black, Background = Color.Transparent } },

					{ "giftLabel", new UILabel(i18n.GetString("cc_gift")) { Left = labelL, Top = top, Foreground = Color.Gray } },
					{ "gift", new UIList(string.Empty, null, traits) { Left = cntrlL, Top = top + 1, Width = 30, Height = 24, Foreground = Color.Black, Background = Color.Transparent } },

					{ "controlHelp", new UILabel(traitHelps[0]) { Left = 1, Top = 1, Width = pageLeft - 2, Height = 4, Foreground = Color.White, Darken = true } },
					{ "topHeader", new UILabel(i18n.GetString("cc_header")) { Left = 1, Top = 0, Foreground = Color.Silver } },
					{ "helpLine", new UILabel(i18n.GetString("cc_footer")) { Left = 1, Top = Program.Rows - 1, Foreground = Color.Silver } },
				};
				//Map the controls to pages.
				pages = new List<UIElement>[]
				{
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["nameLabel"], controls["name"], controls["nameRandom"],
						controls["speciesLabel"], controls["species"],
						controls["tutorial"], controls["easy"],
						controls["controlHelp"], controls["next"],
					},
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["sexLabel"], controls["sex"], controls["gidLabel"], controls["gid"], controls["prefLabel"], controls["pref"],
						controls["controlHelp"], controls["back"], controls["next"],
					},
					new List<UIElement>(), //Placeholder, filled in on-demand from PlayableRace.ColorItems.
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["giftLabel"], controls["gift"],
						controls["controlHelp"], controls["back"], controls["wait"], controls["play"],
					},
				};

				CollectPlayables();

				loadPage = new Action<int>(p =>
				{
					UIManager.Elements.Clear();
					UIManager.Elements.AddRange(pages[page]);
					UIManager.Highlight = UIManager.Elements[5]; //select whatever comes after helpLine.
				});

				//Called when changing species.
				loadColors = new Action<int>(i =>
				{
					var species = playables[i];
					controlHelps["species"] = species.Bestiary;
					pages[2].Clear();
					pages[2].AddRange(new[] { controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"] });
					pages[2].AddRange(species.ColorItems.Values);
					pages[2].AddRange(new[] { controls["controlHelp"], controls["back"], controls["next"] });
				});

				controls["back"].Enter = (s, e) => { page--; loadPage(page); UIManager.Draw(); };
				controls["next"].Enter = (s, e) => { page++; loadPage(page); UIManager.Draw(); };
				controls["play"].Enter = (s, e) =>
				{
					var playerName = controls["name"].Text;
					var sex = ((UIRadioList)controls["sex"]).Value;
					var gid = ((UISingleList)controls["gid"]).Index;
					var pref = ((UISingleList)controls["pref"]).Index;
					var species = ((UISingleList)controls["species"]).Index;
					var tutorial = ((UIToggle)controls["tutorial"]).Checked;
					var easy = ((UIToggle)controls["easy"]).Checked;
					var bonus = ((UIList)controls["gift"]).Text;
					var colorMap = new Dictionary<string, string>();
					foreach (var editable in playables[species].ColorItems)
					{
						if (editable.Key.StartsWith("lbl"))
							continue;
						var path = editable.Key;
						var value = ((UISingleList)editable.Value).Text;
						colorMap.Add(path, value);
					}
					NoxicoGame.Me.CreatePlayerCharacter(playerName.Trim(), (Gender)(sex + 1), (Gender)(gid + 1), pref, playables[species].ID, colorMap, bonus);
					if (tutorial)
						NoxicoGame.Me.Player.Character.AddToken("tutorial");
					if (easy)
						NoxicoGame.Me.Player.Character.AddToken("easymode");
					NoxicoGame.InGameTime.AddYears(Random.Next(0, 10));
					NoxicoGame.InGameTime.AddDays(Random.Next(20, 340));
					NoxicoGame.InGameTime.AddHours(Random.Next(10, 54));
					NoxicoGame.Me.CurrentBoard.UpdateLightmap(NoxicoGame.Me.Player, true);
					Subscreens.FirstDraw = true;
					NoxicoGame.Immediate = true;
					/*
#if DEBUG
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("orgasm_denial_ring");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("baseballbat");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("really_kinky_panties");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("timertest");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("clit_regenring");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("gExtractor");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("strapon_ovi");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("strapon");
#if FREETESTPOTIONS
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("foxite"); // ok
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("odd_nip"); // ok
					//NoxicoGame.Me.Player.Character.GetToken("items").AddToken("dogmorph"); no bodyplan
					//NoxicoGame.Me.Player.Character.GetToken("items").AddToken("bunnymorph"); no bodyplan
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("chaos_potion"); // ok
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("enhanced_chaos_potion"); // ok
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("demonite_potion");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("tentacle_potion");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("cock_potion");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("corrupted_cock_potion");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("neutralizer_potion");
					NoxicoGame.Me.Player.Character.GetToken("items").AddToken("spidermorph");
#endif
#endif
					*/
					NoxicoGame.AddMessage(i18n.GetString("welcometonoxico"), Color.Yellow);
					NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
					
					//Story();
				};

				((UISingleList)controls["species"]).Items.Clear();
				playables.ForEach(x => ((UISingleList)controls["species"]).Items.Add(x.Name.Titlecase()));
				((UISingleList)controls["species"]).Index = 0;
				loadColors(0);
				((UIRadioList)controls["sex"]).ItemsEnabled = playables[0].SexLocks;
				controls["species"].Change = (s, e) =>
				{
					var speciesIndex = ((UISingleList)controls["species"]).Index;
					loadColors(speciesIndex);
					var playable = playables[speciesIndex];
					controlHelps["species"] = playable.Bestiary;
					controls["controlHelp"].Text = playable.Bestiary.Wordwrap(controls["controlHelp"].Width);
					var sexList = (UIRadioList)controls["sex"];
					sexList.ItemsEnabled = playable.SexLocks;
					if (!sexList.ItemsEnabled[sexList.Value])
					{
						//Uh-oh. Select the first non-disabled item.
						for (var i = 0; i < sexList.ItemsEnabled.Length; i++)
						{
							if (sexList.ItemsEnabled[i])
							{
								sexList.Value = i;
								break;
							}
						}
					}
					//redrawBackdrop(0);
					UIManager.Draw();
				};
				controls["sex"].Change = (s, e) =>
				{
					//redrawBackdrop(0);
					UIManager.Draw();
				};
				controls["name"].Change = (s, e) =>
				{
					controls["nameRandom"].Hidden = !controls["name"].Text.IsBlank();
					UIManager.Draw();
				};
				controls["gift"].Change = (s, e) =>
				{
					var giftIndex = ((UIList)controls["gift"]).Index;
					controls["controlHelp"].Text = traitHelps[giftIndex].Wordwrap(controls["controlHelp"].Width);
					controls["controlHelp"].Top = controls["gift"].Top + giftIndex;
					UIManager.Draw();
				};

				((UIToggle)controls["tutorial"]).Checked = IniFile.GetValue("misc", "tutorial", true);
				((UIToggle)controls["easy"]).Checked = IniFile.GetValue("misc", "easymode", false);

				UIManager.Initialize();
				UIManager.HighlightChanged = (s, e) =>
				{
					var c = controls.FirstOrDefault(x => x.Value == UIManager.Highlight);
					if (c.Key != null && controlHelps.ContainsKey(c.Key))
					{
						controls["controlHelp"].Text = controlHelps[c.Key].Wordwrap(controls["controlHelp"].Width);
						controls["controlHelp"].Top = c.Value.Top;
					}
					else
						controls["controlHelp"].Text = string.Empty;
					UIManager.Draw();
				};
				loadPage(page);
				//redrawBackdrop(0);
				Subscreens.FirstDraw = false;
				Subscreens.Redraw = true;
				UIManager.HighlightChanged(null, null);
				NoxicoGame.Sound.PlayMusic("set://Character Creation", false);

				NoxicoGame.InGame = false;
			}

			if (Subscreens.Redraw)
			{
				UIManager.Draw();
				Subscreens.Redraw = false;
			}

			if (WorldGenFinished)
			{
				WorldGenFinished = false;
				controls["play"].Hidden = false;
				UIManager.Draw();
			}

			UIManager.CheckKeys();
		}

		/*
		private static string story;
		private static int storyCursor;
		private static int storyDelay;

		/// <summary>
		/// In case the world generator isn't done yet...
		/// </summary>
		public static void Story()
		{
			NoxicoGame.Subscreen = Introduction.StoryHandler;
			NoxicoGame.Mode = UserMode.Subscreen;

			UIManager.Elements[0] = new UIPNGBackground(Mix.GetBitmap("story.png"));
			UIManager.Elements[1] = new UILabel(string.Empty) { Left = 10, Top = 10, Width = 60, Foreground = Color.White };
			UIManager.Elements[2] = new UILabel(string.Empty) { Left = 1, Top = 0 };
			UIManager.Elements[3] = new UILabel(string.Empty) { Left = 1, Top = 1 }; // reserved for setStatus

			if (WorldGenFinished())
			{
				// if gen already finished we lost the status line
				UIManager.Elements[3] = new UILabel(i18n.GetString("worldgen_ready")) { Left = 1, Top = 1 };
			}

			//Remove everything but these first few UIElements.
			UIManager.Elements.RemoveRange(4, UIManager.Elements.Count - 4);
			UIManager.Draw();
			story = Mix.GetString("story.txt", false);
			storyCursor = 0;
			storyDelay = 2;
		}
		/// <summary>
		/// Types out the game's backstory, unimportant as it is, while waiting for the world generator to finish.
		/// </summary>
		public static void StoryHandler()
		{
			if (WorldGenFinished())
			{
				if (NoxicoGame.IsKeyDown(KeyBinding.Accept) || Subscreens.Mouse || Vista.Triggers != 0)
				{
					NoxicoGame.ClearKeys();
					NoxicoGame.Immediate = true;
					NoxicoGame.Mode = UserMode.Walkabout;
					NoxicoGame.Me.CurrentBoard.Redraw();
					NoxicoGame.Me.CurrentBoard.Draw(true);
					Subscreens.FirstDraw = true;
					TextScroller.LookAt(NoxicoGame.Me.Player); // start by showing player details
				}
			}
			
			if (storyDelay > 0)
			{
				storyDelay--;
			}
			else
			{
				if (storyCursor < story.Length - 1)
				{
					UIManager.Elements[1].Text += story[storyCursor];
					storyCursor++;
					storyDelay = 1;
					if (story[storyCursor] == '¦')
					{
						storyCursor++;
						storyDelay = 50;
					}
				}
				else
				{
					storyDelay = 10000;
				}
			}
			UIManager.Draw();
		}
		*/
	}
}
