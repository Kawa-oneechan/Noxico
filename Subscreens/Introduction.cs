using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace Noxico
{
	public class Introduction
	{
		public static void Title()
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			NoxicoGame.Subscreen = Introduction.TitleHandler;
			NoxicoGame.Immediate = true;
			NoxicoGame.Sound.PlayMusic("set://Title");
		}

		public static void TitleHandler()
		{
			var host = NoxicoGame.HostForm;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				host.Clear();
				new UIPNGBackground(Mix.GetBitmap("title.png")).Draw();

				var i = new[] { "Debauchery", "Wickedness", "Sin", "Depravity", "Corruption", "Decadence", "Morality", "Iniquity", "Immorality", "Shamelessness" };
				var j = new[] { "Insanity", "Foolishness", "Irrationality", "Absurdity", "Folly", "Recklessness", "Stupidity", "Craziness", "Madness", "Lunacy" };
				var histories = "Histories of " + Toolkit.PickOne(i) + " and " + Toolkit.PickOne(j);
				host.Write(histories, Color.Teal, Color.Transparent, 10, 25 - histories.Length / 2);

				host.Write("\u2500\u2500\u2500\u2500\u2524 <cTeal>Press <cAqua>ENTER <cTeal>to begin <cGray>\u251C\u2500\u2500\u2500\u2500", Color.Gray, Color.Transparent, 12, 9);
				//host.SetCell(3, 48, (char)0x2122, Color.Silver, Color.Transparent);
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
					if (version < 15)
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
						return p + ", \"" + Path.GetFileName(s) + "\"";
					}
					return "Start over in \"" + Path.GetFileName(s) + "\"";
				}));
				options.Add("~", "Start new game...");
				MessageBox.List(saves.Count == 0 ? "Welcome to Noxico." : "There " + (saves.Count == 1 ? "is a saved game" : "are saved games") + " you can restore.", options,
					() =>
					{
						if ((string)MessageBox.Answer == "~")
						{
							MessageBox.Input("Enter a name for the new world:", NoxicoGame.WorldName,
								() =>
								{
									NoxicoGame.WorldName = (string)MessageBox.Answer;
									Directory.CreateDirectory(Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName));
									NoxicoGame.Mode = UserMode.Subscreen;
									NoxicoGame.Subscreen = Introduction.CharacterCreator;
									NoxicoGame.Immediate = true;
								});
						}
						else
						{
							NoxicoGame.WorldName = (string)MessageBox.Answer;
							host.Noxico.LoadGame();
							var playerFile = Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "player.bin");
							if (!File.Exists(playerFile))
							{
								NoxicoGame.Mode = UserMode.Subscreen;
								NoxicoGame.Subscreen = Introduction.CharacterCreator;
								//restarting = true;
								NoxicoGame.Immediate = true;
							}
							else
							{
								NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
								Subscreens.FirstDraw = true;
								NoxicoGame.Immediate = true;
								NoxicoGame.AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
								NoxicoGame.AddMessage("Remember, press <cBlack,Silver> " + Toolkit.TranslateKey(KeyBinding.Pause) + " <cSilver,Black> for help and options.");
								//TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
								NoxicoGame.Mode = UserMode.Walkabout;
							}
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
			public List<string> HairColors { get; set; }
			public List<string> SkinColors { get; set; }
			public List<string> EyeColors { get; set; }
			public bool GenderLocked { get; set; }
			public override string ToString()
			{
				return Name;
			}
		}

		private static List<PlayableRace> CollectPlayables()
		{
			var ret = new List<PlayableRace>();
			Program.WriteLine("Collecting playables...");
			var xDoc = Mix.GetXMLDocument("bodyplans.xml");
			var bodyPlans = xDoc.SelectNodes("//bodyplan");
			foreach (var bodyPlan in bodyPlans.OfType<XmlElement>())
			{
				var id = bodyPlan.GetAttribute("id");
				if (bodyPlan.ChildNodes[0].Value == null)
				{
					Program.WriteLine(" * Skipping {0} -- old format.", id);
					continue;
				}
				var plan = bodyPlan.ChildNodes[0].Value.Replace("\r\n", "\n");
				if (!plan.Contains("playable"))
					continue;
				Program.WriteLine(" * Parsing {0}...", id);

				var genlock = plan.Contains("only\n");

				var name = id;

				//TODO: write a support function that grabs everything for a specific token?
				//That is, given "terms \n \t generic ... \n tallness" it'd grab everything up to but not including tallness.
				//Use that to find subtokens like specific terms or colors.

				var hairs = new List<string>() { "<None>" };
				var hair = Toolkit.GrabToken(plan, "hair");
				if (hair != null)
				{
					var c = hair.Substring(hair.IndexOf("color: ") + 7).Trim();
					if (c.StartsWith("oneof"))
					{
						hairs.Clear();
						c = c.Substring(6);
						c = c.Remove(c.IndexOf('\n'));
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => hairs.Add(Color.NameColor(x.Trim()).Titlecase()));
					}
					else
					{
						hairs[0] = c.Remove(c.IndexOf('\n')).Titlecase();
					}
				}

				var eyes = new List<string>() { "Brown" };
				{
					var c = plan.Substring(plan.IndexOf("\neyes: ") + 7);
					if (c.StartsWith("oneof"))
					{
						eyes.Clear();
						c = c.Substring(6);
						c = c.Remove(c.IndexOf('\n'));
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => eyes.Add(Color.NameColor(x.Trim()).Titlecase()));
					}
					else
					{
						eyes[0] = c.Remove(c.IndexOf('\n')).Titlecase();
					}
				}

				var skins = new List<string>();
				var skinName = "skin";
				var s = Toolkit.GrabToken(plan, "skin");
				if (s != null)
				{
					if (s.Contains("type"))
					{
						skinName = s.Substring(s.IndexOf("type") + 5);
						skinName = skinName.Remove(skinName.IndexOf('\n')).Trim();
					}
					var c = s.Substring(s.IndexOf("color: ") + 7).Trim();
					if (c.StartsWith("oneof"))
					{
						skins.Clear();
						c = c.Substring(6);
						c = c.Remove(c.IndexOf('\n'));
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => skins.Add(Color.NameColor(x.Trim()).Titlecase()));
						//skinName = skin;
						//break;
					}
					else
					{
						skins.Add(c.Remove(c.IndexOf('\n')).Titlecase());
						//skinName = skin;
						//break;
					}
				}

				if (skins.Count > 0)
					skins = skins.Distinct().ToList();
				skins.Sort();

				ret.Add(new PlayableRace() { ID = id, Name = name, HairColors = hairs, SkinColors = skins, Skin = skinName, EyeColors = eyes, GenderLocked = genlock });

			}
			return ret;
		}
		private static List<PlayableRace> playables;

		private static Dictionary<string, UIElement> controls;
		private static List<UIElement>[] pages;
		private static Dictionary<string, string> controlHelps;

		private static int page = 0;
		private static Action<int> loadPage, loadColors;

		private static void CreateNox()
		{
			NoxicoGame.HostForm.Noxico.CreateRealm();
		}

		public static void CharacterCreator()
		{
			if (Subscreens.FirstDraw)
			{
				var traitsDoc = Mix.GetXMLDocument("bonustraits.xml");
				var traits = new List<string>();
				var traitHelps = new List<string>();
				foreach (var trait in traitsDoc.SelectNodes("//trait").OfType<XmlElement>())
				{
					traits.Add(trait.GetAttribute("name"));
					traitHelps.Add(trait.InnerText.Trim());
				}
				controlHelps = new Dictionary<string, string>()
				{
					{ "back", "Go back one page." },
					{ "next", "Go to the next page." },
					{ "play", Random.NextDouble() > 0.7 ? "FRUITY ANGELS MOLEST SHARKY" : "ENGAGE RIDLEY MOTHER FUCKER" },
					{ "name", "Enter the name for your character. Leave this blank to use a random name." },
					{ "species", "Select the (initial) species you'll play as." },
					{ "sex", "Are you a boy? Or are you a girl? Some species may not give you the choice." },
					{ "hair", "Select the color of your character's hair." },
					{ "body", "Select the color of your character's body. Some species may not give you a choice." },
					{ "eyes", "Select the color of your character's eyes. Again, you might not always have a choice." },
					{ "gift", traitHelps[0] },
				};

				controls = new Dictionary<string, UIElement>()
				{
					{ "backdrop", new UIPNGBackground(Mix.GetBitmap("chargen.png")) },
					{ "header", new UILabel("\u2500\u2500\u2524 Character Creation \u251C\u2500\u2500") { Left = 44, Top = 4, Foreground = Color.Black } },
					{ "back", new UIButton("< Back", null) { Left = 45, Top = 17, Width = 10 } },
					{ "next", new UIButton("Next >", null) { Left = 59, Top = 17, Width = 10 } },
					{ "play", new UIButton("PLAY >", null) { Left = 59, Top = 17, Width = 10 } },

					{ "nameLabel", new UILabel("Name") { Left = 44, Top = 7, Foreground = Color.Gray } },
					{ "name", new UITextBox(Environment.UserName) { Left = 45, Top = 8, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "nameRandom", new UILabel("[random]") { Left = 60, Top = 7, Hidden = true, Foreground = Color.Gray } },
					{ "speciesLabel", new UILabel("Species") { Left = 44, Top = 10, Foreground = Color.Gray } },
					{ "species", new UISingleList() { Left = 45, Top = 11, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "sexLabel", new UILabel("Sex") { Left = 44, Top = 13, Foreground = Color.Gray } },
					{ "sexNo", new UILabel("Not available") { Left = 50, Top = 14, Foreground = Color.Gray } },
					{ "sex", new UIBinary("Male", "Female") { Left = 45, Top = 14, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },

					{ "hairLabel", new UILabel("Hair color") { Left = 44, Top = 7, Foreground = Color.Gray } },
					{ "hair", new UIColorList() { Left = 45, Top = 8, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "bodyLabel", new UILabel("Body color") { Left = 44, Top = 10, Foreground = Color.Gray } },
					{ "bodyNo", new UILabel("Not available") { Left = 45, Top = 11, Foreground = Color.Gray } },
					{ "body", new UIColorList() { Left = 45, Top = 11, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },
					{ "eyesLabel", new UILabel("Eye color") { Left = 44, Top = 13, Foreground = Color.Gray } },
					{ "eyes", new UIColorList() { Left = 45, Top = 14, Width = 24, Foreground = Color.Black, Background = Color.Transparent } },

					{ "giftLabel", new UILabel("Bonus gift") { Left = 44, Top = 7, Foreground = Color.Gray } },
					{ "gift", new UIList("", null, traits) { Left = 45, Top = 8, Width = 24, Height = 8, Foreground = Color.Black, Background = Color.Transparent } },

					{ "controlHelp", new UILabel(traitHelps[0]) { Left = 1, Top = 8, Width = 40, Height = 4, Foreground = Color.White } },
					{ "topHeader", new UILabel("Starting a New Game") { Left = 1, Top = 0, Foreground = Color.Silver } },
					{ "helpLine", new UILabel(Toolkit.TranslateKey(KeyBinding.TabFocus) + " to switch controls, " + Toolkit.TranslateKey(KeyBinding.Up) + '/' + Toolkit.TranslateKey(KeyBinding.Down) + '/' + Toolkit.TranslateKey(KeyBinding.Left) + '/' + Toolkit.TranslateKey(KeyBinding.Right) + " and " + Toolkit.TranslateKey(KeyBinding.Accept, true) + " to choose. Or use a mouse.") { Left = 1, Top = 24, Foreground = Color.Silver } },
				};

				pages = new List<UIElement>[]
				{
					new List<UIElement>()
					{
						controls["backdrop"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["nameLabel"], controls["name"], controls["nameRandom"],
						controls["speciesLabel"], controls["species"],
						controls["sexLabel"], controls["sexNo"], controls["sex"],
						controls["controlHelp"], controls["next"],
					},
					new List<UIElement>()
					{
						controls["backdrop"], controls["header"], controls["topHeader"], controls["helpLine"],
						controls["hairLabel"], controls["hair"],
						controls["bodyLabel"], controls["bodyNo"], controls["body"],
						controls["eyesLabel"], controls["eyes"],
						controls["controlHelp"], controls["back"], controls["next"],
					},
					new List<UIElement>()
					{
						controls["backdrop"], controls["header"], controls["topHeader"], controls["helpLine"],
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
					controls["bodyLabel"].Text = species.Skin.Titlecase();
					((UISingleList)controls["hair"]).Items.Clear();
					((UISingleList)controls["body"]).Items.Clear();
					((UISingleList)controls["eyes"]).Items.Clear();
					((UISingleList)controls["hair"]).Items.AddRange(species.HairColors);
					((UISingleList)controls["body"]).Items.AddRange(species.SkinColors);
					((UISingleList)controls["eyes"]).Items.AddRange(species.EyeColors);
					((UISingleList)controls["hair"]).Index = 0;
					((UISingleList)controls["body"]).Index = 0;
					((UISingleList)controls["eyes"]).Index = 0;
				});

				controls["back"].Enter = (s, e) => { page--; loadPage(page); UIManager.Draw(); };
				controls["next"].Enter = (s, e) => { page++; loadPage(page); UIManager.Draw(); };
				controls["play"].Enter = (s, e) =>
				{
					var playerName = controls["name"].Text;
					var sex = ((UIBinary)controls["sex"]).Value;
					var species = ((UISingleList)controls["species"]).Index;
					var hair = ((UISingleList)controls["hair"]).Text;
					var body = ((UISingleList)controls["body"]).Text;
					var eyes = ((UISingleList)controls["eyes"]).Text;
					var bonus = ((UIList)controls["gift"]).Text;
					NoxicoGame.HostForm.Noxico.CreatePlayerCharacter(playerName, (Gender)(sex + 1), playables[species].ID, hair, body, eyes, bonus);
					NoxicoGame.HostForm.Noxico.CreateRealm();
					NoxicoGame.Sound.PlayMusic(NoxicoGame.HostForm.Noxico.CurrentBoard.Music);
					NoxicoGame.InGameTime.AddYears(Random.Next(0, 10));
					NoxicoGame.InGameTime.AddDays(Random.Next(20, 340));
					NoxicoGame.InGameTime.AddHours(Random.Next(10, 54));
					NoxicoGame.HostForm.Noxico.CurrentBoard.UpdateLightmap(NoxicoGame.HostForm.Noxico.Player, true);
					NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
					NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
					Subscreens.FirstDraw = true;
					NoxicoGame.Immediate = true;

					NoxicoGame.AddMessage("Welcome to Noxico, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
					NoxicoGame.AddMessage("Remember, press <cBlack,Silver> " + Toolkit.TranslateKey(KeyBinding.Pause) + " <cSilver,Black> for help and options.");
					TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);

					if (!IniFile.GetValue("misc", "skipintro", true))
					{
						var dream = new Character();
						dream.Name = new Name("Dream");
						dream.IsProperNamed = true;
						SceneSystem.Dreaming = true;
						SceneSystem.Engage(NoxicoGame.HostForm.Noxico.Player.Character, dream, "(new game start)", true);
					}
				};

				((UISingleList)controls["species"]).Items.Clear();
				playables.ForEach(x => ((UISingleList)controls["species"]).Items.Add(x.Name.Titlecase()));
				((UISingleList)controls["species"]).Index = 0;
				loadColors(0);
				controls["species"].Change = (s, e) =>
				{
					var speciesIndex = ((UISingleList)controls["species"]).Index;
					loadColors(speciesIndex);
					controls["sex"].Hidden = playables[speciesIndex].GenderLocked;
					controls["sexNo"].Hidden = !playables[speciesIndex].GenderLocked;
					UIManager.Draw();
				};
				controls["name"].Change = (s, e) =>
				{
					controls["nameRandom"].Hidden = !string.IsNullOrEmpty(controls["name"].Text);
					UIManager.Draw();
				};
				controls["gift"].Change = (s, e) =>
				{
					var giftIndex = ((UIList)controls["gift"]).Index;
					controls["controlHelp"].Text = traitHelps[giftIndex].Wordwrap(40);
					controls["controlHelp"].Top = controls["gift"].Top + giftIndex;
					UIManager.Draw();
				};

				UIManager.Initialize();
				UIManager.HighlightChanged = (s, e) =>
				{
					var c = controls.First(x => x.Value == UIManager.Highlight);
					if (controlHelps.ContainsKey(c.Key))
					{
						controls["controlHelp"].Text = controlHelps[c.Key].Wordwrap(40);
						controls["controlHelp"].Top = c.Value.Top;
					}
					else
						controls["controlHelp"].Text = "";
					UIManager.Draw();
				};
				loadPage(page);
				Subscreens.FirstDraw = false;
				Subscreens.Redraw = true;

				NoxicoGame.InGame = false;
			}

			if (Subscreens.Redraw)
			{
				UIManager.Draw();
				Subscreens.Redraw = false;
			}

			UIManager.CheckKeys();
		}
	}

}
