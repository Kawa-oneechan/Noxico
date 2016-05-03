using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Drawing;

namespace Noxico
{
	public class Introduction
	{
		private static System.Threading.Thread worldgen;
		//private static int twirlingBaton = 0;
		//private static char[] twirlingBatons = "-\\|/".ToCharArray();
		private static UIPNGBackground titleBack;
		private static UILabel titleCaption, titlePressEnter;

		public static void KillWorldgen()
		{
			if (worldgen != null && worldgen.ThreadState == System.Threading.ThreadState.Running)
				worldgen.Abort();
		}
		
		public static void Title()
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = Introduction.TitleHandler;
			NoxicoGame.Immediate = true;
		}

		public static void TitleHandler()
		{
			var host = NoxicoGame.HostForm;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				host.Clear();
				var background = new Bitmap(100, 60, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				var logo = Mix.GetBitmap("logo.png");
				var titleOptions = Mix.GetFilesWithPattern("titles\\*.png");
				var chosen = Mix.GetBitmap(titleOptions[Random.Next(titleOptions.Length)]);
				using (var gfx = Graphics.FromImage(background))
				{
					gfx.Clear(Color.Black);
					gfx.DrawImage(chosen, 0, 0, 100, 60);
					gfx.DrawImage(logo, 0, 0, logo.Width, logo.Height);
				}
				UIManager.Initialize();
				titleBack = new UIPNGBackground(background);
				//new UIPNGBackground(Mix.GetBitmap("title.png")).Draw();

				var subtitle = i18n.GetString("ts_subtitle");
				var pressEnter = "\xC4\xC4\xC4\xC4\xB4 " + i18n.GetString("ts_pressentertobegin") + " <cGray>\xC4\xC4\xC4\xC4\xC4";
				titleCaption = new UILabel(subtitle) { Top = 20, Left = 25 - subtitle.Length() / 2, Foreground = Color.Teal };
				titlePressEnter = new UILabel(pressEnter) { Top = 22, Left = 25 - pressEnter.Length() / 2, Foreground = Color.Gray };
				UIManager.Elements.Add(titleBack);
				UIManager.Elements.Add(titleCaption);
				UIManager.Elements.Add(titlePressEnter);
				UIManager.Draw();
				//host.Write(subtitle, Color.Teal, Color.Transparent, 20, 25 - subtitle.Length() / 2);
				//host.Write(pressEnter, Color.Gray, Color.Transparent, 22, 25 - pressEnter.Length() / 2);
			}
			if (NoxicoGame.IsKeyDown(KeyBinding.Accept) || Subscreens.Mouse || Vista.Triggers != 0)
			{
				if (Subscreens.Mouse)
					Subscreens.UsingMouse = true;
				Subscreens.Mouse = false;
				Subscreens.FirstDraw = true;
				var rawSaves = Directory.GetDirectories(NoxicoGame.SavePath);
				var saves = new List<string>();
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
				var options = saves.ToDictionary(new Func<string, object>(s => Path.GetFileName(s)), new Func<string, string>(s =>
				{
					string p;
					var playerFile = Path.Combine(s, "player.bin");
					if (File.Exists(playerFile))
					{
						using (var f = new BinaryReader(File.OpenRead(playerFile)))
						{
							//p = f.ReadString();
							p = Player.LoadFromFile(f).Character.Name.ToString(true);
						}
						return i18n.Format("ts_loadgame", p, Path.GetFileName(s));
					}
					return i18n.Format("ts_startoverinx", Path.GetFileName(s));
				}));
				options.Add("~", i18n.GetString("ts_startnewgame"));
				MessageBox.List(saves.Count == 0 ? i18n.GetString("ts_welcometonoxico") : i18n.GetString(saves.Count == 1 ? "ts_thereisasave" : "ts_therearesaves"), options,
					() =>
					{
						if ((string)MessageBox.Answer == "~")
						{
							//NoxicoGame.Mode = UserMode.Subscreen;
							//NoxicoGame.Subscreen = Introduction.CharacterCreator;
							//NoxicoGame.Immediate = true;
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
						else
						{
							NoxicoGame.WorldName = (string)MessageBox.Answer;
							host.Noxico.LoadGame();
							NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
							Subscreens.FirstDraw = true;
							NoxicoGame.Immediate = true;
							NoxicoGame.AddMessage(i18n.GetString("welcomeback"), Color.Yellow);
							NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
							//TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
							NoxicoGame.Mode = UserMode.Walkabout;
						}
					}
				);
			}
		}


		private class PlayableRace
		{
			public string ID { get; set; }
			public string Name { get; set; }
			public string Skin { get; set; }
			/*
			public List<string> HairColors { get; set; }
			public List<string> SkinColors { get; set; }
			public List<string> EyeColors { get; set; }
			*/
			public Dictionary<string, UIElement> ColorItems { get; set; }
			public bool[] SexLocks { get; set; }
			public string Bestiary { get; set; }
			public override string ToString()
			{
				return Name;
			}
		}

		private static List<PlayableRace> CollectPlayables()
		{
			var ret = new List<PlayableRace>();
			Program.WriteLine("Collecting playables...");
			TokenCarrier.NoRolls = true; //Bit of a hack, I know. It resets to false when Tokenize() is finished.
			var plans = Mix.GetTokenTree("bodyplans.tml");
			foreach (var bodyPlan in plans.Where(t => t.Name == "bodyplan"))
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

				var name = id.Replace('_', ' ').Titlecase();
				if (!string.IsNullOrWhiteSpace(bodyPlan.GetToken("playable").Text))
					name = bodyPlan.GetToken("playable").Text;

				var bestiary = bodyPlan.HasToken("bestiary") ? bodyPlan.GetToken("bestiary").Text : string.Empty;

				var colorItems = new Dictionary<string, UIElement>();
				var editables = "skin/color|Skin color, hair/color|Hair color";
				if (bodyPlan.HasToken("editable"))
					editables = bodyPlan.GetToken("editable").Text;
				var top = 10;
				foreach (var aspect in editables.Split(','))
				{
					if (string.IsNullOrWhiteSpace(aspect))
						continue;
					var a = aspect.Trim().Split('|');
					var path = a[0];
					var label = a[1];
					var t = bodyPlan.Path(path).Text;
					if (t.StartsWith("oneof"))
						t = t.Substring(6);
					var oneof = t.Split(',').ToList();
					var items = new List<string>();
					colorItems.Add("lbl-" + path, new UILabel(label) { Left = 56, Top = top, Foreground = Color.Gray });
					foreach (var i in oneof)
					{
						var iT = i.Trim().Titlecase();
						if (string.IsNullOrWhiteSpace(iT))
							iT = i18n.GetString("[none]", false);
						if (!items.Contains(iT))
							items.Add(iT);
					}
					if (label.EndsWith("color", StringComparison.InvariantCultureIgnoreCase))
					{
						for (var i = 0; i < items.Count; i++)
							items[i] = Color.NameColor(items[i]).Titlecase();
						colorItems.Add(path, new UIColorList() { Items = items, Left = 58, Top = top + 1, Foreground = Color.Black, Background = Color.Transparent, Index = 0 });
					}
					else
						colorItems.Add(path, new UISingleList() { Items = items, Left = 58, Top = top + 1, Foreground = Color.Black, Background = Color.Transparent, Index = 0 });
					top += 4;
				}

				ret.Add(new PlayableRace() { ID = id, Name = name, Bestiary = bestiary, ColorItems = colorItems, SexLocks = sexlocks });
			}
			return ret;
		}
		private static List<PlayableRace> playables;

		private static Dictionary<string, UIElement> controls;
		private static List<UIElement>[] pages;
		private static Dictionary<string, string> controlHelps;
		private static Dictionary<string, Bitmap> portraits;

		private static int page = 0;
		private static Action<int> loadPage, loadColors, redrawBackdrop;
		private static Bitmap backdrop, backWithPortrait;

		public static void CharacterCreator()
		{
			if (Subscreens.FirstDraw)
			{
				//Start creating the world as we work...
				worldgen = new System.Threading.Thread(NoxicoGame.HostForm.Noxico.CreateRealm);
				worldgen.Start();
				//host.Noxico.CreateTheWorld();

				var traits = new List<string>();
				var traitHelps = new List<string>();
				var traitsDoc = Mix.GetTokenTree("bonustraits.tml");
				foreach (var trait in traitsDoc.Where(t => t.Name == "trait"))
				{
					traits.Add(trait.GetToken("display").Text);
					traitHelps.Add(trait.GetToken("description").Text);
				}
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
					//{ "hair", i18n.GetString("cchelp_hair") },
					//{ "body", i18n.GetString("cchelp_body") },
					//{ "eyes", i18n.GetString("cchelp_eyes") },
					{ "gift", traitHelps[0] },
				};
				backdrop = Mix.GetBitmap("chargen.png");
				backWithPortrait = new Bitmap(backdrop.Width, backdrop.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				using (var g = Graphics.FromImage(backWithPortrait))
				{
					g.DrawImage(backdrop, 0, 0, backdrop.Width, backdrop.Height);
				}
				var title = "\xB4 " + i18n.GetString("cc_title") + " \xC3";
				var bar = new string('\xC4', 33);
				string[] sexoptions = {i18n.GetString("Male"), i18n.GetString("Female"), i18n.GetString("Herm"), i18n.GetString("Neuter")};
				string[] prefoptions = { i18n.GetString("Male"), i18n.GetString("Female"), i18n.GetString("Either") };
				controls = new Dictionary<string, UIElement>()
				{
					{ "backdrop", new UIPNGBackground(backWithPortrait) },
					{ "headerline", new UILabel(bar) { Left = 56, Top = 8, Foreground = Color.Black } },
					{ "header", new UILabel(title) { Left = 73 - (title.Length() / 2), Top = 8, Width = title.Length(), Foreground = Color.Black } },
					{ "back", new UIButton(i18n.GetString("cc_back"), null) { Left = 58, Top = 46, Width = 10, Height = 3 } },
					{ "next", new UIButton(i18n.GetString("cc_next"), null) { Left = 78, Top = 46, Width = 10, Height = 3 } },
					{ "play", new UIButton(i18n.GetString("cc_play"), null) { Left = 78, Top = 46, Width = 10, Height = 3 } },

					//{ "worldLabel", new UILabel(i18n.GetString("cc_world")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					//{ "world", new UITextBox(NoxicoGame.RollWorldName()) { Left = 58, Top = 11, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "nameLabel", new UILabel(i18n.GetString("cc_name")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					{ "name", new UITextBox(string.Empty) { Left = 58, Top = 11, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "nameRandom", new UILabel(i18n.GetString("cc_random")) { Left = 60, Top = 11, Foreground = Color.Gray } },
					{ "speciesLabel", new UILabel(i18n.GetString("cc_species")) { Left = 56, Top = 14, Foreground = Color.Gray } },
					{ "species", new UISingleList() { Left = 58, Top = 15, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					{ "sexLabel", new UILabel(i18n.GetString("cc_sex")) { Left = 56, Top = 18, Foreground = Color.Gray } },
					{ "sex", new UIRadioList(sexoptions) { Left = 58, Top = 19, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "gidLabel", new UILabel(i18n.GetString("cc_gid")) { Left = 56, Top = 24, Foreground = Color.Gray } },
					{ "gid", new UIRadioList(sexoptions) { Left = 58, Top = 25, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "prefLabel", new UILabel(i18n.GetString("cc_pref")) { Left = 56, Top = 30, Foreground = Color.Gray } },
					{ "pref", new UIRadioList(prefoptions) { Left = 58, Top = 31, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "tutorial", new UIToggle(i18n.GetString("cc_tutorial")) { Left = 58, Top = 40, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "easy", new UIToggle(i18n.GetString("cc_easy")) { Left = 58, Top = 42, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },

					/*
					{ "hairLabel", new UILabel(i18n.GetString("cc_hair")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					{ "hair", new UIColorList() { Left = 58, Top = 11, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					{ "bodyLabel", new UILabel(i18n.GetString("cc_body")) { Left = 56, Top = 14, Foreground = Color.Gray } },
					{ "bodyNo", new UILabel(i18n.GetString("cc_no")) { Left = 60, Top = 15, Foreground = Color.Gray } },
					{ "body", new UIColorList() { Left = 58, Top = 15, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					{ "eyesLabel", new UILabel(i18n.GetString("cc_eyes")) { Left = 56, Top = 18, Foreground = Color.Gray } },
					{ "eyes", new UIColorList() { Left = 58, Top = 19, Width = 30, Foreground = Color.Black, Background = Color.Transparent } },
					*/

					{ "giftLabel", new UILabel(i18n.GetString("cc_gift")) { Left = 56, Top = 10, Foreground = Color.Gray } },
					{ "gift", new UIList("", null, traits) { Left = 58, Top = 12, Width = 30, Height = 32, Foreground = Color.Black, Background = Color.Transparent } },

					{ "controlHelp", new UILabel(traitHelps[0]) { Left = 1, Top = 8, Width = 50, Height = 4, Foreground = Color.White } },
					{ "topHeader", new UILabel(i18n.GetString("cc_header")) { Left = 1, Top = 0, Foreground = Color.Silver } },
					{ "helpLine", new UILabel(i18n.GetString("cc_footer")) { Left = 1, Top = 59, Foreground = Color.Silver } },
				};

				pages = new List<UIElement>[]
				{
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						//controls["worldLabel"], controls["world"],
						controls["nameLabel"], controls["name"], controls["nameRandom"],
						controls["speciesLabel"], controls["species"],
						controls["sexLabel"], controls["sex"], controls["gidLabel"], controls["gid"], controls["prefLabel"], controls["pref"],
						controls["tutorial"], controls["easy"],
						controls["controlHelp"], controls["next"],
					},
					new List<UIElement>(), //Placeholder
					/*
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["hairLabel"], controls["hair"],
						controls["bodyLabel"], controls["bodyNo"], controls["body"],
						controls["eyesLabel"], controls["eyes"],
						controls["controlHelp"], controls["back"], controls["next"],
					}, */
					new List<UIElement>()
					{
						controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["giftLabel"], controls["gift"],
						controls["controlHelp"], controls["back"], /* controls["playNo"], */ controls["play"],
					},
				};

				playables = CollectPlayables();

				loadPage = new Action<int>(p =>
				{
					UIManager.Elements.Clear();
					UIManager.Elements.AddRange(pages[page]);
					UIManager.Highlight = UIManager.Elements[5];
				});

				loadColors = new Action<int>(i =>
				{
					var species = playables[i];
					controlHelps["species"] = species.Bestiary;
					/* controls["bodyLabel"].Text = species.Skin.Titlecase();
					((UISingleList)controls["hair"]).Items.Clear();
					((UISingleList)controls["body"]).Items.Clear();
					((UISingleList)controls["eyes"]).Items.Clear();
					((UISingleList)controls["hair"]).Items.AddRange(species.HairColors);
					((UISingleList)controls["body"]).Items.AddRange(species.SkinColors);
					((UISingleList)controls["eyes"]).Items.AddRange(species.EyeColors);
					((UISingleList)controls["hair"]).Index = 0;
					((UISingleList)controls["body"]).Index = 0;
					((UISingleList)controls["eyes"]).Index = 0; */
					pages[1].Clear();
					pages[1].AddRange(new[] { controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"] });
					pages[1].AddRange(species.ColorItems.Values);
					pages[1].AddRange(new[] { controls["controlHelp"], controls["back"], controls["next"] });
				});

				redrawBackdrop = new Action<int>(i =>
				{
					var playable = playables[((UISingleList)controls["species"]).Index];
					var portrait = "chargen\\" + playable.ID + "_" + "mfhn"[((UIRadioList)controls["sex"]).Value] + ".png";
					if (!Mix.FileExists(portrait))
					{
						portrait = "chargen\\" + playable.ID + ".png";
						if (!Mix.FileExists(portrait))
						{
							portrait = "chargen\\_.png";
						}
					}
					if (portraits == null)
						portraits = new Dictionary<string, Bitmap>();
					if (!portraits.ContainsKey(portrait))
						portraits.Add(portrait, Mix.GetBitmap(portrait));
					var p = portraits[portrait];
					for (var row = 0; row < 58; row++)
					{
						for (var col = 0; col < 54; col++)
						{
							var a = p.GetPixel(col, row).R / 255f;
							var c = backdrop.GetPixel(col, row + 1);
							var r = c.R / 255f;
							var g = c.G / 255f;
							var b = c.B / 255f;
							r = 1 - (1 - r) * (1 - a);
							g = 1 - (1 - g) * (1 - a);
							b = 1 - (1 - b) * (1 - a);
							r = r * 255f;
							g = g * 255f;
							b = b * 255f;
							if (r > 255) r = 255;
							if (g > 255) g = 255;
							if (b > 255) b = 255;
							if (r < 0) r = 0;
							if (g < 0) g = 0;
							if (b < 0) b = 0;
							backWithPortrait.SetPixel(col, row + 1, Color.FromArgb((int)r, (int)g, (int)b));
						}
					}
					((UIPNGBackground)controls["backdrop"]).Bitmap = backWithPortrait;
				});

				controls["back"].Enter = (s, e) => { page--; loadPage(page); UIManager.Draw(); };
				controls["next"].Enter = (s, e) => { page++; loadPage(page); UIManager.Draw(); };
				controls["play"].Enter = (s, e) =>
				{
					//NoxicoGame.WorldName = controls["world"].Text.Replace(':', '_').Replace('\\', '_').Replace('/', '_').Replace('"', '_');
					var playerName = controls["name"].Text;
					var sex = ((UIRadioList)controls["sex"]).Value;
					var gid = ((UIRadioList)controls["gid"]).Value;
					var pref = ((UIRadioList)controls["pref"]).Value;
					var species = ((UISingleList)controls["species"]).Index;
					var tutorial = ((UIToggle)controls["tutorial"]).Checked;
					var easy = ((UIToggle)controls["easy"]).Checked;
					/* var hair = ((UISingleList)controls["hair"]).Text;
					var body = ((UISingleList)controls["body"]).Text;
					var eyes = ((UISingleList)controls["eyes"]).Text; */
					var bonus = ((UIList)controls["gift"]).Text;
					//NoxicoGame.HostForm.Noxico.CreatePlayerCharacter(playerName.Trim(), (Gender)(sex + 1), (Gender)(gid + 1), pref, playables[species].ID, hair, body, eyes, bonus);
					var colorMap = new Dictionary<string, string>();
					foreach (var editable in playables[species].ColorItems)
					{
						if (editable.Key.StartsWith("lbl"))
							continue;
						var path = editable.Key;
						var value = ((UISingleList)editable.Value).Text;
						colorMap.Add(path, value);
					}
					NoxicoGame.HostForm.Noxico.CreatePlayerCharacter(playerName.Trim(), (Gender)(sex + 1), (Gender)(gid + 1), pref, playables[species].ID, colorMap, bonus);
					if (tutorial)
						NoxicoGame.HostForm.Noxico.Player.Character.AddToken("tutorial");
					if (easy)
						NoxicoGame.HostForm.Noxico.Player.Character.AddToken("easymode");
					//NoxicoGame.HostForm.Noxico.CreateRealm();
					NoxicoGame.InGameTime.AddYears(Random.Next(0, 10));
					NoxicoGame.InGameTime.AddDays(Random.Next(20, 340));
					NoxicoGame.InGameTime.AddHours(Random.Next(10, 54));
					NoxicoGame.HostForm.Noxico.CurrentBoard.UpdateLightmap(NoxicoGame.HostForm.Noxico.Player, true);
					Subscreens.FirstDraw = true;
					NoxicoGame.Immediate = true;

					NoxicoGame.AddMessage(i18n.GetString("welcometonoxico"), Color.Yellow);
					NoxicoGame.AddMessage(i18n.GetString("rememberhelp"));
					if (worldgen.ThreadState == System.Threading.ThreadState.Running)
					{
						Story();
					}

					/*
					if (!IniFile.GetValue("misc", "skipintro", true))
					{
						var dream = new Character();
						dream.Name = new Name("Dream");
						dream.AddToken("special");
						NoxicoGame.HostForm.Noxico.Player.Character.AddToken("introdream");
						SceneSystem.Engage(NoxicoGame.HostForm.Noxico.Player.Character, dream, "(new game start)");
					}
					*/
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
					//controls["sex"].Hidden = playable.GenderLocked;
					//controls["sexNo"].Hidden = !playable.GenderLocked;
					sexList.ItemsEnabled = playable.SexLocks;
					if (!sexList.ItemsEnabled[sexList.Value])
					{
						for (var i = 0; i < sexList.ItemsEnabled.Length; i++)
						{
							if (sexList.ItemsEnabled[i])
							{
								sexList.Value = i;
								break;
							}
						}
					}
					redrawBackdrop(0);
					UIManager.Draw();
				};
				controls["sex"].Change = (s, e) =>
				{
					redrawBackdrop(0);
					UIManager.Draw();
				};
				/* controls["world"].Change = (s, e) =>
				{
					controls["next"].Hidden = string.IsNullOrWhiteSpace(controls["world"].Text);
					UIManager.Draw();
				}; */
				controls["name"].Change = (s, e) =>
				{
					controls["nameRandom"].Hidden = !string.IsNullOrWhiteSpace(controls["name"].Text);
					UIManager.Draw();
				};
				controls["gift"].Change = (s, e) =>
				{
					var giftIndex = ((UIList)controls["gift"]).Index;
					controls["controlHelp"].Text = traitHelps[giftIndex].Wordwrap(50);
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
						controls["controlHelp"].Text = "";
					UIManager.Draw();
				};
				loadPage(page);
				redrawBackdrop(0);
				Subscreens.FirstDraw = false;
				Subscreens.Redraw = true;
				UIManager.HighlightChanged(null, null);

				NoxicoGame.InGame = false;
			}

			if (Subscreens.Redraw)
			{
				UIManager.Draw();
				Subscreens.Redraw = false;
			}

			UIManager.CheckKeys();

			/*
			if (worldgen.ThreadState == System.Threading.ThreadState.Running)
			{
				NoxicoGame.HostForm.SetCell(19, 50, twirlingBatons[twirlingBaton / 10], Random.Next(15), 15);
				NoxicoGame.HostForm.SetCell(19, 60, twirlingBatons[twirlingBaton / 10], Random.Next(15), 15);
				twirlingBaton++;
				if (twirlingBaton == 40)
					twirlingBaton = 0;
			}
			*/
		}

		private static string story;
		private static int storyCursor;
		private static int storyDelay;

		public static void Story()
		{
			NoxicoGame.Subscreen = Introduction.StoryHandler;
			NoxicoGame.Mode = UserMode.Subscreen;
			//controls["backdrop"], controls["headerline"], controls["header"], controls["topHeader"], controls["helpLine"],
			UIManager.Elements[0] = new UIPNGBackground(Mix.GetBitmap("story.png"));
			UIManager.Elements[1] = new UILabel(string.Empty) { Left = 10, Top = 10, Width = 60, Foreground = Color.White };
			UIManager.Elements[2] = new UILabel(i18n.GetString("worldgen_loading")) { Left = 1, Top = 0 };
			UIManager.Elements[3] = new UILabel(string.Empty) { Left = 1, Top = 1 };
			UIManager.Elements.RemoveRange(4, UIManager.Elements.Count - 4);
			//Remove the rest of all this.
			UIManager.Draw();
			story = Mix.GetString("story.txt", false);	
			storyCursor = 0;
			storyDelay = 2;
		}
		public static void StoryHandler()
		{
			if (worldgen.ThreadState == System.Threading.ThreadState.Running)
			{
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
						storyDelay = 2;
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
			else
			{
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
				TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
			}
		}
	}

}
