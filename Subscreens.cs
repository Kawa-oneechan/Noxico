using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;

namespace Noxico
{
	public class Subscreens
	{
		public static Stack<SubscreenFunc> PreviousScreen = new Stack<SubscreenFunc>();

		public static bool FirstDraw = true;
		public static bool Redraw = true;

		public static bool UsingMouse = false;
		public static bool Mouse = false;
		public static int MouseX = -1;
		public static int MouseY = -1;
	}

	public class Pause
	{
		//TODO: Rewrite to use UIManager
		private static int page = 0;
		private static Dictionary<string, string> pages = new Dictionary<string,string>()
		{
			{ "Character stats", "dynamic page" },
			{ "Skill levels", "dynamic page" },
			{ "Important keys", "l, / - Look\ni - Inventory\n, - Pick up\nc - Chat\n< - Go down stairs, enter door\n> Go up stairs, enter door\n<g1B><g18><g19><g1A> - Move\n. - Rest" },
			{ "Other keys", "..." },
			{ "Credits", "..." },
			{ "Memory stats", "..." },
#if DEBUG
			{ "Debug cheats", "..." },
#endif
			{ "Save and exit", "Press Enter to save." },
		};

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;

			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				Toolkit.DrawWindow(5, 4, 21, 1 + pages.Count, "PAUSED", Color.Maroon, Color.Black, Color.Red);
				Subscreens.Redraw = true;
				keys[(int)Keys.Escape] = false;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				Toolkit.DrawWindow(28, 4, 41, 16, null, Color.Blue, Color.Black, Color.Blue);
				var titles = pages.Keys.ToArray();
				for (var i = 0; i < pages.Count; i++)
					host.Write((' ' + titles[i]).PadRight(20), i == page ? Color.Black : Color.Silver, i == page ? Color.Silver : Color.Black, 6, 5 + i);
				var text = pages.Values.ElementAt(page).Split('\n');
				for (var i = 0; i < text.Length; i++)
					host.Write(text[i], Color.Silver, Color.Black, 30, 5 + i);
			}

			if (keys[(int)Keys.Escape])
			{
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}

			if (trig[(int)Keys.Up])
			{
				if (page == 0)
					page = pages.Count;
				page--;
				Subscreens.Redraw = true;
			}
			else if (trig[(int)Keys.Down])
			{
				page++;
				if (page == pages.Count)
					page = 0;
				Subscreens.Redraw = true;
			}

			if (keys[(int)Keys.Enter])
			{
				if (page == pages.Count - 1) //can't use absolute index because Debug might be missing.
				{
					host.Noxico.SaveGame();
					host.Close();
				}
			}
		}

		private static int CountTokens(Token t)
		{
			var r = t.Tokens.Count;
			if (r > 0)
				foreach (var t2 in t.Tokens)
					r += CountTokens(t2);
			return r;
		}

		public static void Open()
		{
			var host = NoxicoGame.HostForm;
			var nox = host.Noxico;
			var player = nox.Player.Character;

			var sb = new StringBuilder();
			sb.AppendLine("Name                " + player.Name);
			sb.AppendLine("Health              " + player.GetToken("health").Value + " / " + player.GetMaximumHealth());
			sb.AppendLine("Money               " + player.GetToken("money").Value + " Z");
			sb.AppendLine("Play time           " + "lol");
			sb.AppendLine("Charisma            " + player.GetToken("charisma").Value);
			sb.AppendLine("Climax              " + player.GetToken("climax").Value);
			sb.AppendLine("Cunning             " + player.GetToken("cunning").Value);
			sb.AppendLine("Carnality           " + player.GetToken("carnality").Value);
			sb.AppendLine("Stimulation         " + player.GetToken("stimulation").Value);
			sb.AppendLine("Sensitivity         " + player.GetToken("sensitivity").Value);
			sb.AppendLine("Speed               " + player.GetToken("speed").Value);
			sb.AppendLine("Strength            " + player.GetToken("strength").Value);
			pages["Character stats"] = sb.ToString();

			sb.Clear();
			foreach (var skill in player.GetToken("skills").Tokens)
				sb.AppendLine(skill.Name.Replace('_', ' ').Titlecase().PadRight(20) + ((int)skill.Value + 1).ToString());
			pages["Skill levels"] = sb.ToString();

			var entities = 0;
			var tokens = 0;
			nox.Boards.ForEach(x =>
			{
				entities += x.Entities.Count;
				foreach(var c in x.Entities.OfType<BoardChar>())
				{
					tokens += c.Character.Tokens.Count;
					foreach(var t in c.Character.Tokens)
						tokens += CountTokens(t);
				}
			});
			NoxicoGame.KnownItems.ForEach(x =>
			{
				tokens += x.Tokens.Count;
				foreach (var t in x.Tokens)
					tokens += CountTokens(t);
			});
			sb.Clear();
			sb.AppendLine("Number of boards            " + nox.Boards.Count.ToString("G"));
			sb.AppendLine("Number of known items       " + NoxicoGame.KnownItems.Count.ToString("G"));
			sb.AppendLine("Number of entities          " + entities.ToString("G"));
			sb.AppendLine("Total set of tokens         " + tokens.ToString("G"));
			pages["Memory stats"] = sb.ToString();

			if (!IniFile.GetBool("misc", "rememberpause", true))
				page = 0;
			
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.ClearKeys();
		}
	}

	public class MessageBox
	{
		private static string[] text = { };
		private static bool isQuestion;
		private static Action onYes, onNo;


		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var rows = text.Length - 2;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				Toolkit.DrawWindow(5, 5, 69, rows + 2, isQuestion ? "Question" : string.Empty, Color.Gray, Color.Black, Color.White);
				for (int i = 0; i < text.Length; i++)
					host.Write(text[i], Color.Silver, Color.Black, 7, 6 + i);
				if (isQuestion)
					host.Write("<gB5,126> Y/N <gC6,127>", Color.Gray, Color.Black, 66, 7 + rows);
				else
					host.Write("<gB5,126><g19><gC6,127>", Color.Gray, Color.Black, 70, 7 + rows);
			}
			if (keys[(int)Keys.Escape] || keys[(int)Keys.Enter] || (isQuestion && (keys[(int)Keys.Y] || keys[(int)Keys.N])))
			{
				if (Subscreens.PreviousScreen.Count == 0)
				{
					NoxicoGame.Mode = UserMode.Walkabout;
					host.Noxico.CurrentBoard.Redraw();
				}
				else
				{
					NoxicoGame.Subscreen = Subscreens.PreviousScreen.Pop();
					host.Noxico.CurrentBoard.Redraw();
					host.Noxico.CurrentBoard.Draw();
					Subscreens.FirstDraw = true;
				}
				if (isQuestion)
				{
					if ((keys[(int)Keys.Enter] || keys[(int)Keys.Y]) && onYes != null)
					{
						NoxicoGame.ClearKeys();
						onYes();
					}
					else if ((keys[(int)Keys.Escape] || keys[(int)Keys.N]) && onNo != null)
					{
						NoxicoGame.ClearKeys();
						onNo();
					}
				}
				else
				{
					isQuestion = false;
					NoxicoGame.ClearKeys();
				}
			}
		}

		public static void Ask(string question, Action yes, Action no, bool dontPush = false)
		{
			if (!dontPush && NoxicoGame.Subscreen != null)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			isQuestion = true;
			text = Toolkit.Wordwrap(question.Trim(), 68).Split('\n');
			onYes = yes;
			onNo = no;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Message(string message, bool dontPush = false)
		{
			if (!dontPush)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			isQuestion = false;
			text = Toolkit.Wordwrap(message.Trim(), 68).Split('\n');
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}
	}

	public class TextScroller
	{
		private static string[] text = { };
		private static int scroll = 0;
		private static DateTime slow = DateTime.Now;

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			if (Subscreens.FirstDraw)
			{
				scroll = 1;
				Subscreens.FirstDraw = false;

				Toolkit.DrawWindow(5, 3, 69, 18, text[0], Color.Navy, Color.Black, Color.Yellow);
#if USE_EXTENDED_TILES
				host.SetCell(21, 19, (char)0x126, Color.Navy, Color.Black);
				host.SetCell(21, 60, (char)0x127, Color.Navy, Color.Black);
#else
				host.SetCell(21, 19, (char)0xB5, Color.Navy, Color.Black);
				host.SetCell(21, 60, (char)0xC6, Color.Navy, Color.Black);
#endif
				host.Write(" Press <b><g18><b> and <b><g19><b> to scroll, <b>Esc<b> to return ", Color.Cyan, Color.Black, 20, 21);
				for (int i = scroll; i < text.Length && i - scroll < 17; i++)
				{
					if (i < 1)
						continue;
					host.Write(text[i], Color.Silver, Color.Black, 7, 3 + i);
				}
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				if (text.Length > 17)
				{
					for (int i = 4; i < 21; i++)
#if !USE_EXTENDED_TILES
						host.SetCell(i, 74, (char)0xBA, Color.Navy, Color.Black, true);
#else
						host.SetCell(i, 74, (char)0x125, Color.Navy, Color.Black, true);
#endif
					float pct = (float)(scroll - 1) / (float)((text.Length - 16 < 0) ? 1 : text.Length - 16);
					int tp = (int)(pct * 17) + 4;
#if !USE_EXTENDED_TILES
					host.SetCell(tp, 74, (char)0xF0, Color.Black, Color.Silver, true);
#else
					host.SetCell(tp, 74, (char)0x117, Color.Black, Color.Silver, true);
#endif
				}
				Subscreens.Redraw = false;
			}

			if (Subscreens.Mouse)
			{
				Subscreens.Mouse = false;
				Subscreens.UsingMouse = true;
				if (Subscreens.MouseY == 24 && Subscreens.MouseX >= 71 && Subscreens.MouseX <= 77)
				{
					NoxicoGame.Immediate = true;
					NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
					NoxicoGame.Mode = UserMode.Walkabout;
					Subscreens.FirstDraw = true;
				}
			}

			if (keys[(int)Keys.S])
			{
				File.WriteAllText("current.txt", string.Join("\n", text));
			}

			if (keys[(int)Keys.Escape])
			{
				keys[(int)Keys.Escape] = false;
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}

			if (keys[(int)Keys.Up] && (DateTime.Now - slow).Milliseconds >= 100)
			{
				slow = DateTime.Now;
				scroll--;
				if (scroll < 1)
					scroll = 1;
				else
				{
					host.ScrollDown(4, 21, 6, 73);
					var i = scroll;
					host.Write(text[i].PadRight(60), Color.Silver, Color.Black, 7, 4);
					Subscreens.Redraw = true;
				}
			}
			if (keys[(int)Keys.Down] && (DateTime.Now - slow).Milliseconds >= 100)
			{
				slow = DateTime.Now;
				scroll++;
				if (scroll > text.Length - 17)
					scroll = text.Length - 17;
				else if (scroll < 1)
					scroll = 1;
				else
				{
					host.ScrollUp(3, 21, 6, 73);
					var i = scroll + 16;
					if (i >= text.Length)
						host.Write(new string(' ', 60), Color.Black, Color.Black, 4, 20);
					else
						host.Write(text[i].PadRight(60), Color.Silver, Color.Black, 7, 20);
					Subscreens.Redraw = true;
				}
			}
		}

		public static void LookAt(Entity target)
		{
			//var host = NoxicoGame.HostForm;
			var pa = target;
			//var keys = NoxicoGame.KeyMap;
			var sb = new StringBuilder();

			if (pa is BoardChar)
			{
				var chr = ((BoardChar)pa).Character;
				sb.Append(chr.GetName());

				sb.AppendLine();
				sb.Append(chr.LookAt(pa));
			}
			else if (pa is Dressing)
			{
				var drs = (Dressing)pa;
				sb.AppendFormat("{0}", drs.Name);
				sb.AppendLine();
				sb.AppendLine();
				sb.AppendFormat("{0}", drs.Description);
				sb.AppendLine();
			}
			/*
			else if (pa is Sexytimes)
			{
				var sex = (Sexytimes)pa;
				sb.AppendLine(sex.GetParticipants(true));
				sb.AppendLine();
				foreach (var line in sex.Log)
					sb.AppendLine(line);
			}
			*/
			text = Toolkit.Wordwrap(sb.ToString(), 68).Split('\n');
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
		}

		public static void ReadBook(int bookNum)
		{
			/*
			var filename = Path.Combine("books", "BOK" + bookNum.ToString("00000") + ".TXT");
			if (File.Exists(filename))
				text = File.ReadAllText(filename).Wordwrap(68).Split('\n');
			else
				text = new[] { "Can't find the content for this book." };
			*/
			var xDoc = new XmlDocument();
			xDoc.Load(new CryptStream(new System.IO.Compression.GZipStream(File.OpenRead("books.dat"), System.IO.Compression.CompressionMode.Decompress)));
			var books = xDoc.SelectNodes("//book");
			XmlElement book = null;
			foreach (var b in books.OfType<XmlElement>())
			{
				if (b.GetAttribute("id") == bookNum.ToString())
				{
					book = b;
					break;
				}
			}
			if (book == null)
				text = new[] { "Can't find the content for this book." };
			else
			{
				var t = book.GetAttribute("title") + '\n' + book.Noxicize();
				text = t.Wordwrap(68).Split('\n');
			}

			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Noxicobotic(Entity source, string message)
		{
			var header = "";
			if (source is BoardChar)
			{
				header = ((BoardChar)source).Character.GetName();
				message = message.Viewpoint((BoardChar)source);
			}
			text = (header + "\n" + message.Wordwrap(68)).Split('\n');
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
		}
	}

	/*
	public class Introduction
	{
		private static System.Threading.Thread worldgen;

		public static void KillWorldgen()
		{
			if (worldgen != null && worldgen.ThreadState == System.Threading.ThreadState.Running)
				worldgen.Abort();
		}

		private static List<PlayableRace> playables = CollectPlayables();
		private static List<string> hairColors = new List<string>();
		private static List<string> races = new List<string>();
		private static int sel = 0;
		private static int cursorBlink = 0;
		private static char[] name = Environment.UserName.PadRight(22).ToCharArray();
		private static int nameCursor = Environment.UserName.Length;
		private static int sex = 0;
		private static int hair = 0;
		private static int race = 0;
		private static char[] typables = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 012345679,.".ToCharArray();
		private static string[] helpText = new[]
		{
			" Enter a name \xB3 Enter, Tab: to next field \xB3 Shift-Tab: to last field".PadRight(80),
			" \x1D: decide \xB3 Enter, Tab: to next field \xB3 Shift-Tab: to previous field".PadRight(80),
			" \x1D: decide \xB3 Enter, Tab: to next field \xB3 Shift-Tab: to previous field".PadRight(80),
			" \x1D: decide \xB3 Enter: PLAY \xB3 Tab: to first field \xB3 Shift-Tab: to previous field".PadRight(80),
			"",
		};
		
		private class PlayableRace
		{
			public string ID { get; set; }
			public string Name { get; set; }
			public List<string> HairColors { get; set; }
			public List<string> GenderNames { get; set; }
			public bool GenderLocked { get; set; }
			public override string ToString()
			{
				return Name;
			}
		}

		private static List<PlayableRace> CollectPlayables()
		{
			var ti = CultureInfo.InvariantCulture.TextInfo;
			var ret = new List<PlayableRace>();
			var xDoc = new XmlDocument();
			xDoc.Load("Noxico.xml");
			var playables = xDoc.SelectNodes("//playable").OfType<XmlElement>();
			foreach (var playable in playables)
			{
				var bodyPlan = playable.ParentNode as XmlElement;
				var id = bodyPlan.GetAttribute("id");

				var genlock = false;
				if (bodyPlan.SelectSingleNode("maleonly") != null)
					genlock = true;
				if (bodyPlan.SelectSingleNode("femaleonly") != null)
					genlock = true;
				if (bodyPlan.SelectSingleNode("hermonly") != null)
					genlock = true;
				if (bodyPlan.SelectSingleNode("neuteronly") != null)
					genlock = true;

				var name = id;
				var n = bodyPlan.SelectSingleNode("terms/generic");
				if (n != null)
					name = n.InnerText;
				var male = name;
				var female = name;
				n = bodyPlan.SelectSingleNode("terms/male");
				if (n != null)
					male = n.InnerText;
				n = bodyPlan.SelectSingleNode("terms/female");
				if (n != null)
					female = n.InnerText;
				male = ti.ToTitleCase(male);
				female = ti.ToTitleCase(female);
				var genders = new List<string>() { male, female };

				var hairs = new List<string>() { "<None>" };
				n = bodyPlan.SelectSingleNode("hair/color");
				if (n != null)
				{
					foreach (var hn in n.ChildNodes.OfType<XmlElement>())
					{
						if (hn.Name == "oneof")
						{
							hairs.Clear();
							var oneof = ((XmlElement)hn).InnerText.Trim().Split(',').ToList();
							oneof.ForEach(x => hairs.Add(ti.ToTitleCase(x)));
							break;
						}
						else
						{
							hairs[0] = ti.ToTitleCase(hn.Name);
							break;
						}
					}
				}

				ret.Add(new PlayableRace() { ID = id, Name = name, GenderNames = genders, HairColors = hairs, GenderLocked = genlock });
			}
			return ret;
		}

		public static void CharacterCreator()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;
			if (Subscreens.FirstDraw)
			{
				NoxicoGame.LastPress = (char)255;
				Subscreens.FirstDraw = false;
				host.Clear();
				//host.LoadBin(global::Noxico.Properties.Resources.CharacterGenerator);
				host.LoadBitmap(global::Noxico.Properties.Resources.CharacterGenerator);
				Subscreens.Redraw = true;

				host.Write("Starting a New Game", Color.Silver, Color.Transparent, 1, 0);
				host.Write("\xC4\xC4\xB4 Character Creation \xC3\xC4\xC4", Color.Black, Color.Transparent, 44, 4);

				races.Clear();
				playables.ForEach(x => races.Add(x.Name));
				hairColors.Clear();
				hairColors.AddRange(playables[race].HairColors);

				//Start creating the world as we work...
				worldgen = new System.Threading.Thread(host.Noxico.CreateTheWorld);
				worldgen.Start();
				//host.Noxico.CreateTheWorld();
			}

			if (Subscreens.Mouse)
			{
				Subscreens.UsingMouse = true;
				Subscreens.Mouse = false;
				if (Subscreens.MouseX >= 45 && Subscreens.MouseX <= 69)
				{
					if (Subscreens.MouseY == 8)
					{
						sel = 0;
						Subscreens.Redraw = true;
					}
					else if (Subscreens.MouseY == 10)
					{
						sel = 1;
						trig[(int)Keys.Left] = true;
#if !USE_EXTENDED_TILES
						if (Subscreens.MouseX == 69)
#else
						if (Subscreens.MouseX == 68)
#endif
							trig[(int)Keys.Right] = true;
						Subscreens.Redraw = true;
					}
					else if (Subscreens.MouseY == 13)
					{
						sel = 2;
						if (Subscreens.MouseX == 67)
						if (Subscreens.MouseX == 67)
							trig[(int)Keys.Left] = true;
#if !USE_EXTENDED_TILES
						if (Subscreens.MouseX == 69)
#else
						if (Subscreens.MouseX == 68)
#endif
							trig[(int)Keys.Right] = true;
						Subscreens.Redraw = true;
					}
					else if (Subscreens.MouseY == 16)
					{
						sel = 3;
						if (Subscreens.MouseX >= 45 && Subscreens.MouseX <= 52)
							sex = 0;
						else if (Subscreens.MouseX >= 59 && Subscreens.MouseX <= 69)
							sex = 1;
						Subscreens.Redraw = true;
					}
					else if (Subscreens.MouseY == 18)
					{
						if (Subscreens.MouseX >= 53 && Subscreens.MouseX <= 57)
							sel = 4;
					}
				}
			}

			//host.Write(worldgen == null ? "<null>" : worldgen.ThreadState.ToString() + "       ", 7, 0, 0, 0);
			if (worldgen == null || worldgen.ThreadState == System.Threading.ThreadState.Stopped)
				Subscreens.Redraw = true;

			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;

				host.Write(helpText[sel], Color.Silver, Color.Transparent, 0, 24);

				host.Write("Name", sel == 0 ? Color.Red : Color.Gray, Color.Transparent, 44, 6);
				host.Write("Species", sel == 1 ? Color.Red : Color.Gray, Color.Transparent, 44, 9);
				host.Write("Hair", sel == 2 ? Color.Red : Color.Gray, Color.Transparent, 44, 12);
				host.Write("Sex", sel == 3 ? Color.Red : Color.Gray, Color.Transparent, 44, 15);

#if !USE_EXTENDED_TILES
				host.Write("\x1D", sel == 1 ? Color.Black : Color.Gray, Color.Transparent, 68, 10);
				host.Write("\x1D", sel == 2 ? Color.Black : Color.Gray, Color.Transparent, 68, 13);
#else
				host.Write("\x11A\x11B", sel == 1 ? Color.Black : Color.Gray, Color.Transparent, 67, 10);
				host.Write("\x11A\x11B", sel == 2 ? Color.Black : Color.Gray, Color.Transparent, 67, 13);
#endif

				var n = string.Concat(name);
				if (string.IsNullOrWhiteSpace(n))
					host.Write("        (random)        ", Color.Gray, Color.Transparent, 45, 7);
				else
					host.Write(n.PadRight(24), sel == 0 ? Color.Black : Color.Gray, Color.Transparent, 45, 7);

				host.Write(races[race].PadRight(20), sel == 1 ? Color.Black : Color.Gray, Color.Transparent, 45, 10);

				if (!playables[race].GenderLocked)
				{
#if !USE_EXTENDED_TILES
					host.Write("[ ] Male       [ ] Female", Color.Gray, Color.Transparent, 44, 16);
					host.SetCell(16, 45, sex == 0 ? (char)0xFB : ' ', sel == 1 ? Color.Red : Color.Gray, Color.Transparent);
					host.SetCell(16, 60, sex == 1 ? (char)0xFB : ' ', sel == 1 ? Color.Red : Color.Gray, Color.Transparent);
#else
					host.Write("\x113\x114  Male       \x113\x114  Female", Color.Gray, Color.Transparent, 44, 16);
					host.SetCell(16, 45, sex == 0 ? (char)0x115 : (char)0x114, Color.Gray, Color.Transparent);
					host.SetCell(16, 60, sex == 1 ? (char)0x115 : (char)0x114, Color.Gray, Color.Transparent);
#endif
				}
				else
					host.Write("      Cannot choose      ", Color.Silver, Color.Transparent, 44, 16);


				host.Write("\xDB\xDD", Toolkit.GetColor(hairColors[hair]), Color.Transparent, 45, 13);
				host.Write(Toolkit.NameColor(hairColors[hair]).PadRight(20), sel == 2 ? Color.Black : Color.Gray, Color.Transparent, 47, 13);

				if (worldgen.ThreadState == System.Threading.ThreadState.Running)
					host.Write("Working", Color.Gray, Color.Transparent, 52, 18);
				else if (Subscreens.UsingMouse)
				{
					host.Write("            ", Color.White, Color.Transparent, 50, 18);
#if !USE_EXTENDED_TILES
					host.Write("\xDD GO! \xDE", Color.White, Color.Gray, 53, 19); 
#else
					host.Write("  GO!  ", Color.White, Color.Gray, 53, 18);
#endif
				}
				else
					host.Write("   Ready.   ", Color.Black, Color.Transparent, 50, 18);
			}

			if (sel == 0) //Name
			{
				cursorBlink++;
				if (cursorBlink < 5)
					host.SetCell(7, 45 + nameCursor, (char)0xDB, Color.Gray, Color.Transparent);
				else if (cursorBlink < 10)
					host.SetCell(7, 45 + nameCursor, name[nameCursor], Color.Black, Color.Transparent);
				else if (cursorBlink < 20)
					cursorBlink = 0;


				if (NoxicoGame.LastPress != 255)
				{
					if (typables.Contains(NoxicoGame.LastPress))
					{
						name[nameCursor] = NoxicoGame.LastPress;
						if (nameCursor < 21)
							nameCursor++;
						Subscreens.Redraw = true;
					}
					else if (NoxicoGame.LastPress == '\b')
					{
						if (nameCursor > 0)
							nameCursor--;
						for (int i = nameCursor; i < 21; i++)
							name[i] = name[i + 1];
						name[21] = ' ';
						Subscreens.Redraw = true;
					}
					else if (NoxicoGame.LastPress == '\r' || (NoxicoGame.LastPress == '\t' && !NoxicoGame.Modifiers[0]))
					{
						NoxicoGame.LastPress = (char)255;
						sel++;
						Subscreens.Redraw = true;
					}
					else if (NoxicoGame.LastPress == '\t' && NoxicoGame.Modifiers[0])
					{
						NoxicoGame.LastPress = (char)255;
						sel = 3;
						Subscreens.Redraw = true;
					}
					NoxicoGame.LastPress = (char)255;
				}
				else
				{
					if (trig[(int)Keys.Left])
					{
						if (nameCursor > 0)
							nameCursor--;
						Subscreens.Redraw = true;
					}
					else if (trig[(int)Keys.Right])
					{
						if (nameCursor < 22)
							nameCursor++;
						Subscreens.Redraw = true;
					}
				}
			}
			else if (sel == 1) //Race
			{
				if (NoxicoGame.LastPress == '\r')
				{
					NoxicoGame.LastPress = (char)255;
					sel++;
					Subscreens.Redraw = true;
				}
				else if (NoxicoGame.LastPress == '\t' && !NoxicoGame.Modifiers[0])
				{
					NoxicoGame.LastPress = (char)255;
					sel = 2;
					Subscreens.Redraw = true;
				}
				else if (NoxicoGame.LastPress == '\t' && NoxicoGame.Modifiers[0])
				{
					NoxicoGame.LastPress = (char)255;
					sel--;
					Subscreens.Redraw = true;
				}
				else if (trig[(int)Keys.Left])
				{
					if (race == 0)
						race = races.Count;
					race--;
					Subscreens.Redraw = true;

					hairColors.Clear();
					hairColors.AddRange(playables[race].HairColors);
					if (hair >= hairColors.Count)
						hair = 0;
				}
				else if (trig[(int)Keys.Right])
				{
					race = (race + 1) % races.Count;
					Subscreens.Redraw = true;

					hairColors.Clear();
					hairColors.AddRange(playables[race].HairColors);
					if (hair >= hairColors.Count)
						hair = 0;
				}
			}
			else if (sel == 2) //Hair
			{
				if (NoxicoGame.LastPress == '\r' || (NoxicoGame.LastPress == '\t' && !NoxicoGame.Modifiers[0]))
				{
					NoxicoGame.LastPress = (char)255;
					sel++;
					if (playables[race].GenderLocked)
						sel++;
					Subscreens.Redraw = true;
				}
				else if (NoxicoGame.LastPress == '\t' && NoxicoGame.Modifiers[0])
				{
					NoxicoGame.LastPress = (char)255;
					sel--;
					if (playables[race].GenderLocked)
						sel--;
					Subscreens.Redraw = true;
				}
				else if (trig[(int)Keys.Left])
				{
					if (hair == 0)
						hair = hairColors.Count;
					hair--;
					Subscreens.Redraw = true;
				}
				else if (trig[(int)Keys.Right])
				{
					hair = (hair + 1) % hairColors.Count;
					Subscreens.Redraw = true;
				}
			}
			else if (sel == 3) //Sex
			{
				if (NoxicoGame.LastPress == '\r')
				{
					NoxicoGame.LastPress = (char)255;
					sel++;
					Subscreens.Redraw = true;
				}
				else if (NoxicoGame.LastPress == '\r' || (NoxicoGame.LastPress == '\t' && !NoxicoGame.Modifiers[0]))
				{
					NoxicoGame.LastPress = (char)255;
					sel = 0;
					Subscreens.Redraw = true;
				}
				else if (NoxicoGame.LastPress == '\t' && NoxicoGame.Modifiers[0])
				{
					NoxicoGame.LastPress = (char)255;
					sel--;
					Subscreens.Redraw = true;
				}
				else if (keys[(int)Keys.Left])
				{
					sex = 0;
					Subscreens.Redraw = true;
				}
				else if (keys[(int)Keys.Right])
				{
					sex = 1;
					Subscreens.Redraw = true;
				}
			}
			else if (sel == 4) //DONE!
			{
				if (worldgen != null && worldgen.ThreadState == System.Threading.ThreadState.Running)
				{
					sel = 0;
					Subscreens.Redraw = true;
				}
				else
				{
					//NoxicoGame.HostForm.Noxico.CreateTheWorld();
					var playerName = string.Concat(name).Trim();
					//host.Noxico.CreatePlayerCharacter(playerName, (Gender)(sex + 1), playables[race].ID, hairColors[hair].ToLowerInvariant());
					NoxicoGame.Sound.PlayMusic(host.Noxico.CurrentBoard.Music);
					//NoxicoGame.HostForm.Noxico.SaveGame();
					host.Noxico.CurrentBoard.Redraw();
					host.Noxico.CurrentBoard.Draw();
					Subscreens.FirstDraw = true;
					NoxicoGame.Immediate = true;
					NoxicoGame.AddMessage("Welcome to Noxico, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".");
					TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
				}
			}
		}
	}
*/

	public class Inventory
	{
		//TODO: Rewrite to use UIManager
		private static int selection = 0;
		private static Dictionary<Token, InventoryItem> inventory = new Dictionary<Token, InventoryItem>();

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (!player.Character.HasToken("items") || player.Character.GetToken("items").Tokens.Count == 0)
			{
				MessageBox.Message("You are carrying nothing.", true);
				return;
			}
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				for (var i = 0; i < 255; i++)
					keys[i] = trig[i] = false;
				inventory.Clear();
				foreach (var carriedItem in player.Character.GetToken("items").Tokens)
				{
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					inventory.Add(carriedItem, find);
				}
				Toolkit.DrawWindow(1, 1, 40, 1 + inventory.Count, "Inventory", Color.Purple, Color.Black, Color.Magenta);
				host.Write("<gB5,126> <g18> <g19> <gC6,127>", Color.Magenta, Color.Black, 33, 2 + inventory.Count);
				if (selection >= inventory.Count)
					selection = 0;
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				for (var i = 0; i < 20 && i < inventory.Count; i++)
				{
					var item = inventory.ElementAt(i).Value; //inventory[i];
					var sigil = "";
					var carried = inventory.ElementAt(i).Key; //player.Character.GetToken("items").GetToken(item.ID);
					if (item.HasToken("equipable"))
						sigil += "<c" + (carried.HasToken("equipped") ? "Navy" : "Gray") + ">W";
					if (carried.HasToken("unidentified"))
						sigil += "<cSilver>?";
#if DEBUG
					if (carried.HasToken("cursed"))
						sigil += "<c" + (carried.GetToken("cursed").HasToken("known") ? "Magenta" : "Purple") + ">C";
#else
					if (carried.HasToken("cursed") && carried.GetToken("cursed").HasToken("known"))
						sigil += "<c13>C";
#endif
					host.Write(item.ToString(carried).PadRight(32) + "<c0,0> " + sigil, (i == selection) ? Color.White : Color.Silver, (i == selection) ? Color.Gray : Color.Black, 3, 2 + i);
				}
			}

			if (keys[(int)Keys.Escape] || keys[(int)Keys.I])
			{
				Subscreens.PreviousScreen.Clear();
				NoxicoGame.Mode = UserMode.Walkabout;
				host.Noxico.CurrentBoard.Redraw();
				Subscreens.FirstDraw = true;
				for (var i = 0; i < 255; i++)
					keys[i] = trig[i] = false;
			}

			if (trig[(int)Keys.Up])
			{
				if (selection == 0)
					selection = inventory.Count;
				selection--;
				Subscreens.Redraw = true;
			}
			else if (trig[(int)Keys.Down])
			{
				selection++;
				if (selection == inventory.Count)
					selection = 0;
				Subscreens.Redraw = true;
			}

			if (keys[(int)Keys.L])
			{
				keys[(int)Keys.L] = false;
				var item = inventory.ElementAt(selection).Value;
				var token = inventory.ElementAt(selection).Key;
				var text = item.HasToken("description") && !token.HasToken("unidentified") ? item.GetToken("description").Text : "This is " + item.ToString() + ".";
				MessageBox.Message(text);
			}

			if (keys[(int)Keys.Enter])
			{
				keys[(int)Keys.Enter] = false;
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
				var chosen = inventory.ElementAt(selection).Value;
				var token = inventory.ElementAt(selection).Key;
				chosen.Use(player.Character, token);
			}

		}
	}

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
			var keys = NoxicoGame.KeyMap;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				host.Clear();
				//host.LoadBin(global::Noxico.Properties.Resources.TitleScreen);
				host.LoadBitmap(global::Noxico.Properties.Resources.TitleScreen);
				host.Write("\xFA\xC4\xC4\xC4\xB4 <cTeal>Press <cAqua>ENTER <cTeal>to begin <cGray>\xC3\xC4\xC4\xC4\xFA", Color.Gray, Color.Transparent, 8, 12);
				host.Write("<cSilver><g1>   <cYellow,Red><g2,102><c>    <cAqua,Navy><g2><c>    <cYellow,Navy><g2,10B><c>   <cWhite,Gray><g2><c>", Color.Silver, Color.Transparent, 14, 10);
#if USE_EXTENDED_TILES
				host.SetCell(3, 48, (char)0x10E, Color.Silver, Color.Transparent);
#endif
			}
			if (keys[(int)Keys.Enter] || Subscreens.Mouse)
			{
				if (Subscreens.Mouse)
					Subscreens.UsingMouse = true;
				Subscreens.Mouse = false;
				Subscreens.FirstDraw = true;
				if (File.Exists("world.bin"))
				{
					keys[(int)Keys.Enter] = false;
					Subscreens.Mouse = false;
					host.Clear();
					MessageBox.Ask("There is a saved game you could restore. Would you like to do so?",
						() =>
						{
							host.Noxico.LoadGame();
							NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
							Subscreens.FirstDraw = true;
							NoxicoGame.Immediate = true;
							NoxicoGame.AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".");
							//TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
							NoxicoGame.Mode = UserMode.Walkabout;
						},
						() =>
						{
#if CAREFUL_ABOUT_OVERWRITING
							MessageBox.Ask("This will <b>overwrite<b> your old saved game. Are you sure?",
								() =>
								{
									NoxicoGame.Mode = UserMode.Subscreen;
									NoxicoGame.Subscreen = Subscreens.CharacterCreator;
									NoxicoGame.Immediate = true;
								},
								() =>
								{
									host.Noxico.LoadGame();
									NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
									firstDraw = true;
									NoxicoGame.Immediate = true;
									NoxicoGame.AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".");
									Subscreens.LookAt(NoxicoGame.HostForm.Noxico.CurrentBoard.Entities.OfType<Player>().First());
								}
							);
#else
							NoxicoGame.Mode = UserMode.Subscreen;
							NoxicoGame.Subscreen = Introduction.CharacterCreator;
							NoxicoGame.Immediate = true;
#endif
						}
					);
				}
				else
				{
					NoxicoGame.Mode = UserMode.Subscreen;
					NoxicoGame.Subscreen = Introduction.CharacterCreator;
					NoxicoGame.Immediate = true;
				}
			}
		}


		private static System.Threading.Thread worldgen;

		public static void KillWorldgen()
		{
			if (worldgen != null && worldgen.ThreadState == System.Threading.ThreadState.Running)
				worldgen.Abort();
		}

		private class PlayableRace
		{
			public string ID { get; set; }
			public string Name { get; set; }
			public string Skin { get; set; }
			public List<string> HairColors { get; set; }
			public List<string> SkinColors { get; set; }
			public List<string> EyeColors { get; set; }
			public List<string> GenderNames { get; set; }
			public bool GenderLocked { get; set; }
			public override string ToString()
			{
				return Name;
			}
		}

		private static List<PlayableRace> CollectPlayables()
		{
			var ti = CultureInfo.InvariantCulture.TextInfo;
			var ret = new List<PlayableRace>();
			var xDoc = new XmlDocument();
			xDoc.Load("Noxico.xml");
			var playables = xDoc.SelectNodes("//playable").OfType<XmlElement>();
			foreach (var playable in playables)
			{
				var bodyPlan = playable.ParentNode as XmlElement;
				var id = bodyPlan.GetAttribute("id");

				var genlock = false;
				if (bodyPlan.SelectSingleNode("maleonly") != null)
					genlock = true;
				if (bodyPlan.SelectSingleNode("femaleonly") != null)
					genlock = true;
				if (bodyPlan.SelectSingleNode("hermonly") != null)
					genlock = true;
				if (bodyPlan.SelectSingleNode("neuteronly") != null)
					genlock = true;

				var name = id;
				var n = bodyPlan.SelectSingleNode("terms/generic");
				if (n != null)
					name = n.InnerText;
				var male = name;
				var female = name;
				n = bodyPlan.SelectSingleNode("terms/male");
				if (n != null)
					male = n.InnerText;
				n = bodyPlan.SelectSingleNode("terms/female");
				if (n != null)
					female = n.InnerText;
				male = ti.ToTitleCase(male);
				female = ti.ToTitleCase(female);
				var genders = new List<string>() { male, female };

				var hairs = new List<string>() { "<None>" };
				n = bodyPlan.SelectSingleNode("hair/color");
				if (n != null)
				{
					foreach (var hn in n.ChildNodes.OfType<XmlElement>())
					{
						if (hn.Name == "oneof")
						{
							hairs.Clear();
							var oneof = ((XmlElement)hn).InnerText.Trim().Split(',').ToList();
							oneof.ForEach(x => hairs.Add(ti.ToTitleCase(x)));
							break;
						}
						else
						{
							hairs[0] = ti.ToTitleCase(hn.Name);
							break;
						}
					}
				}

				var eyes = new List<string>() { "Brown" };
				n = bodyPlan.SelectSingleNode("eyes");
				if (n != null)
				{
					foreach (var en in n.ChildNodes.OfType<XmlElement>())
					{
						if (en.Name == "oneof")
						{
							eyes.Clear();
							var oneof = ((XmlElement)en).InnerText.Trim().Split(',').ToList();
							oneof.ForEach(x => eyes.Add(ti.ToTitleCase(x)));
							break;
						}
						else
						{
							eyes[0] = ti.ToTitleCase(en.Name);
							break;
						}
					}
				} 
				
				var skinTypes = new[] { "skin", "fur", "scales", "slime", "rubber" };
				var skins = new List<string>();
				var skinName = skinTypes[0];
				foreach (var skin in skinTypes)
				{
					n = bodyPlan.SelectSingleNode(skin);
					if (n != null)
					{
						foreach (var sn in n.ChildNodes.OfType<XmlElement>())
						{
							if (sn.Name == "oneof")
							{
								skins.Clear();
								var oneof = ((XmlElement)sn).InnerText.Trim().Split(',').ToList();
								oneof.ForEach(x => skins.Add(ti.ToTitleCase(x)));
								skinName = skin;
								break;
							}
							else
							{
								skins.Add(ti.ToTitleCase(sn.Name));
								skinName = skin;
								break;
							}
						}
					}
				}
				if (skins.Count > 0)
					skins = skins.Distinct().ToList();
				skins.Sort();

				ret.Add(new PlayableRace() { ID = id, Name = name, GenderNames = genders, HairColors = hairs, SkinColors = skins, Skin = skinName, EyeColors = eyes, GenderLocked = genlock });
			}
			return ret;
		}
		private static List<PlayableRace> playables = CollectPlayables();

		private static Dictionary<string, UIElement> controls = new Dictionary<string, UIElement>()
		{
			{ "backdrop", new UIPNGBackground(global::Noxico.Properties.Resources.CharacterGenerator) },
			{ "header", new UILabel("\xC4\xC4\xB4 Character Creation \xC3\xC4\xC4") { Left = 44, Top = 4, Foreground = Color.Black } },
			{ "back", new UIButton("< Back", null) { Left = 45, Top = 17, Width = 10 } },
			{ "next", new UIButton("Next >", null) { Left = 59, Top = 17, Width = 10 } },
			{ "playNo", new UILabel("Wait...") { Left = 60, Top = 17, Foreground = Color.Gray } },
			{ "play", new UIButton("PLAY >", null) { Left = 59, Top = 17, Width = 10, Hidden = true } },

			{ "nameLabel", new UILabel("Name") { Left = 44, Top = 7 } },
			{ "name", new UITextBox(Environment.UserName) { Left = 45, Top = 8, Width = 24 } },
			{ "nameRandom", new UILabel("[random]") { Left = 60, Top = 7, Hidden = true } },
			{ "speciesLabel", new UILabel("Species") { Left = 44, Top = 10 } },
			{ "species", new UISingleList() { Left = 45, Top = 11, Width = 24 } },
			{ "sexLabel", new UILabel("Sex") { Left = 44, Top = 13 } },
			{ "sexNo", new UILabel("Not available") { Left = 50, Top = 14 } },
			{ "sex", new UIBinary("Male", "Female") { Left = 45, Top = 14, Width = 24 } },

			{ "hairLabel", new UILabel("Hair color") { Left = 44, Top = 7 } },
			{ "hair", new UIColorList() { Left = 45, Top = 8, Width = 24 } },
			{ "bodyLabel", new UILabel("Body color") { Left = 44, Top = 10 } },
			{ "bodyNo", new UILabel("Not available") { Left = 45, Top = 11 } },
			{ "body", new UIColorList() { Left = 45, Top = 11, Width = 24 } },
			{ "eyesLabel", new UILabel("Eye color") { Left = 44, Top = 13 } },
			{ "eyes", new UIColorList() { Left = 45, Top = 14, Width = 24 } },

			{ "giftLabel", new UILabel("Bonus gift") { Left = 44, Top = 7 } },
			{ "gift", new UIList("", null, new[] { "Strength", "Toughness", "Speed", "Smarts", "Libido", "Touch", "Big Cock", "Lots of Jizz" }) { Left = 45, Top = 8, Width = 24, Height = 8 } },

			{ "topHeader", new UILabel("Starting a New Game") { Left = 1, Top = 0, Foreground = Color.Silver } },
			{ "helpLine", new UILabel("") { Tag = "worldGen", Left = 1, Top = 24, Foreground = Color.Silver } },
		};
		private static List<UIElement>[] pages = new List<UIElement>[]
		{
			new List<UIElement>()
			{
				controls["backdrop"], controls["header"], controls["topHeader"], controls["helpLine"],
				controls["nameLabel"], controls["name"], controls["nameRandom"],
				controls["speciesLabel"], controls["species"],
				controls["sexLabel"], controls["sexNo"], controls["sex"],
				controls["next"],
			},
			new List<UIElement>()
			{
				controls["backdrop"], controls["header"], controls["topHeader"], controls["helpLine"],
				controls["hairLabel"], controls["hair"],
				controls["bodyLabel"], controls["bodyNo"], controls["body"],
				controls["eyesLabel"], controls["eyes"],
				controls["back"], controls["next"],
			},
			new List<UIElement>()
			{
				controls["backdrop"], controls["header"], controls["topHeader"], controls["helpLine"],
				controls["giftLabel"], controls["gift"],
				controls["back"], controls["playNo"], controls["play"],
			},
		};

		private static int page = 0;

		public static void CharacterCreator()
		{
			var loadPage = new Action<int>(p =>
			{
				UIManager.Elements.Clear();
				UIManager.Elements.AddRange(pages[page]);
				UIManager.Highlight = UIManager.Elements[0];
			});

			var loadColors = new Action<int>(i =>
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
				NoxicoGame.HostForm.Noxico.CreatePlayerCharacter(playerName, (Gender)(sex + 1), playables[species].ID, hair, body, eyes);
				NoxicoGame.Sound.PlayMusic(NoxicoGame.HostForm.Noxico.CurrentBoard.Music);
				//NoxicoGame.HostForm.Noxico.SaveGame();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
				Subscreens.FirstDraw = true;
				NoxicoGame.Immediate = true;
				NoxicoGame.AddMessage("Welcome to Noxico, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".");
				TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
			};

			if (Subscreens.FirstDraw)
			{
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
					controls["nameRandom"].Hidden = (controls["name"].Text != "");
					UIManager.Draw();
				};

				UIManager.Initialize();
				loadPage(page); 
				Subscreens.FirstDraw = false;
				Subscreens.Redraw = true;

				//Start creating the world as we work...
				worldgen = new System.Threading.Thread(NoxicoGame.HostForm.Noxico.CreateTheWorld);
				worldgen.Start();

			}
			if (Subscreens.Redraw)
			{
				UIManager.Draw();
				Subscreens.Redraw = false;
			}

			if (worldgen.ThreadState != System.Threading.ThreadState.Running && controls["play"].Hidden)
			{
				controls["play"].Hidden = false;
				if (page == 2)
					controls["play"].Draw();
			}

			UIManager.CheckKeys();

		}
	}
}
