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
			{ "Important keys",
@"l, / - Look
i    - Inventory
p, , - Pick up
c    - Chat
<    - Go down stairs, enter door
>    - Go up stairs, enter door
<g1B><g18><g19><g1A> - Move
.    - Rest" },
			{ "Other keys", "..." },
			{ "Credits", "..." },
			{ "Memory stats", "..." },
#if DEBUG
			{ "Debug cheats", "..." },
#endif
			{ "Save and exit", "Press Enter to save." },
		};
		private static UIList list;
		private static UILabel text;

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;

			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				UIManager.Initialize();
				UIManager.Elements.Add(new UIWindow("PAUSED") { Left = 5, Top = 4, Width = 22, Height = pages.Count + 2, Background = Color.Black, Foreground = Color.Maroon });
				UIManager.Elements.Add(new UIWindow("") { Left = 28, Top = 4, Width = 42, Height = 16, Background = Color.Black, Foreground = Color.Blue });
				list = new UIList() { Background = Color.Black, Foreground = Color.Silver, Width = 20, Height = pages.Count, Left = 6, Top = 5 };
				list.Items.AddRange(pages.Keys);
				list.Change += (s, e) =>
					{
						page = list.Index;
						text.Text = pages.Values.ElementAt(page);
						UIManager.Draw();
					};
				list.Enter += (s, e) =>
					{
						if (list.Index == list.Items.Count - 1) //can't use absolute index because Debug might be missing.
						{
							host.Noxico.SaveGame();
							host.Close();
						}
					};
				text = new UILabel("...") { Background = Color.Black, Foreground = Color.Silver, Left = 30, Top = 5 };
				UIManager.Elements.Add(list);
				UIManager.Elements.Add(text);
				list.Index = IniFile.GetBool("misc", "rememberpause", true) ? page : 0;
				UIManager.Highlight = list;

				//Toolkit.DrawWindow(5, 4, 21, 1 + pages.Count, "PAUSED", Color.Maroon, Color.Black, Color.Red);
				Subscreens.Redraw = true;
				keys[(int)Keys.Escape] = false;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				UIManager.Draw();

				//Toolkit.DrawWindow(28, 4, 41, 16, null, Color.Blue, Color.Black, Color.Blue);
				//var titles = pages.Keys.ToArray();
				//for (var i = 0; i < pages.Count; i++)
				//	host.Write((' ' + titles[i]).PadRight(20), i == page ? Color.Black : Color.Silver, i == page ? Color.Silver : Color.Black, 6, 5 + i);
				//var text = pages.Values.ElementAt(page).Split('\n');
				//for (var i = 0; i < text.Length; i++)
				//	host.Write(text[i], Color.Silver, Color.Black, 30, 5 + i);
			}

			if (keys[(int)Keys.Escape])
			{
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else
			{
				UIManager.CheckKeys();
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
		private static string title;
		private static Action onYes, onNo;


		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var rows = text.Length - 2;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				Toolkit.DrawWindow(5, 5, 69, rows + 2, isQuestion ? "Question" : title, Color.Gray, Color.Black, Color.White);
				for (int i = 0; i < text.Length; i++)
					host.Write(text[i], Color.Silver, Color.Black, 7, 6 + i);
				if (isQuestion)
					host.Write("<g2561><cWhite> Y/N <cGray><g255E>", Color.Gray, Color.Black, 66, 7 + rows);
				else
					host.Write("<g2561><cWhite><g2026><cGray><g255E>", Color.Gray, Color.Black, 70, 7 + rows);
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

		public static void Ask(string question, Action yes, Action no, bool dontPush = false, string title = "")
		{
			if (!dontPush && NoxicoGame.Subscreen != null)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			isQuestion = true;
			MessageBox.title = title;
			text = Toolkit.Wordwrap(question.Trim(), 68).Split('\n');
			onYes = yes;
			onNo = no;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Message(string message, bool dontPush = false, string title = "")
		{
			if (!dontPush)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			MessageBox.title = title;
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
				host.SetCell(21, 19, (char)0x2561, Color.Navy, Color.Black);
				host.SetCell(21, 60, (char)0x255E, Color.Navy, Color.Black);
				host.Write(" Press <b>\u2191<b> and <b>\u2193<b> to scroll, <b>Esc<b> to return ", Color.Cyan, Color.Black, 20, 21);
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
						host.SetCell(i, 74, (char)0x2551, Color.Navy, Color.Black, true);
					float pct = (float)(scroll - 1) / (float)((text.Length - 16 < 0) ? 1 : text.Length - 16);
					int tp = (int)(pct * 17) + 4;
					host.SetCell(tp, 74, (char)0x2195, Color.Black, Color.Silver, true);
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

		public static void LookAt(BoardChar target)
		{
			//var host = NoxicoGame.HostForm;
			var pa = target;
			//var keys = NoxicoGame.KeyMap;
			var sb = new StringBuilder();

			var chr = ((BoardChar)pa).Character;
			sb.Append(chr.Name.ToString(true));

			sb.AppendLine();
			sb.Append(chr.LookAt(pa));

			text = Toolkit.Wordwrap(sb.ToString(), 68).Split('\n');
			Subscreens.FirstDraw = true;
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

	public class Inventory
	{
		//TODO: <del>Rewrite to use UIManager</del> Adapt the Drop key to, it's the only one left.
		//TODO: Verify that dropping <del>or consuming</del> works.
		private static int selection = 0; //TODO: Use this.
		//Split up the Dictionary for easier access to both halves. It was a silly setup anyway.
		private static List<Token> inventoryTokens = new List<Token>();
		private static List<InventoryItem> inventoryItems = new List<InventoryItem>();
		private static UIList itemList;
		private static UILabel howTo, itemDesc;

		private static void TryUse(Character character, Token token, InventoryItem chosen)
		{
			//Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			chosen.Use(character, token);
			Subscreens.Redraw = true;
		}

		private static void TryDrop(BoardChar boardchar, Token token, InventoryItem chosen)
		{
			//Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			if (token.HasToken("equipped"))
				try
				{
					chosen.Unequip(boardchar.Character, token);
				}
				catch (ItemException x)
				{
					MessageBox.Message(x.Message);
				}
			if (!token.HasToken("equipped"))
			{
				chosen.Drop(boardchar, token);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Update();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
				MessageBox.Message("Dropped " + chosen.ToString(token, true, true) + ".");
				/*
				Subscreens.PreviousScreen.Clear();
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				Subscreens.FirstDraw = true;
				NoxicoGame.AddMessage("Dropped " + chosen.ToString(token, true, true) + ".");
				*/
			}
		}

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (!player.Character.HasToken("items") || player.Character.GetToken("items").Tokens.Count == 0)
			{
				MessageBox.Message("You are carrying nothing.", true);
				Subscreens.PreviousScreen.Clear();
				Subscreens.FirstDraw = true;
				return;
			}

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;

				inventoryTokens.Clear();
				inventoryItems.Clear();
				var itemTexts = new List<string>();
				foreach (var carriedItem in player.Character.GetToken("items").Tokens)
				{
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					inventoryTokens.Add(carriedItem);
					inventoryItems.Add(find);

					var item = find;
					var sigil = "";
					var carried = carriedItem;
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
					itemTexts.Add(item.ToString(carried).PadRight(30) + "<cBlack> " + sigil);
				}
				var height = inventoryItems.Count;
				if (height > 20)
					height = 20;
				if (selection >= inventoryItems.Count)
					selection = inventoryItems.Count - 1;

				UIManager.Elements.Add(new UIWindow("Inventory") { Left = 1, Top = 1, Width = 37, Height = 2 + height, Background = Color.Black, Foreground = Color.Magenta });
				UIManager.Elements.Add(new UIWindow(string.Empty) { Left = 39, Top = 1, Width = 40, Height = 8, Background = Color.Black, Foreground = Color.Navy });
				howTo = new UILabel("") { Left = 0, Top = 24, Width = 79, Height = 1, Background = Color.Black, Foreground = Color.Silver };
				itemDesc = new UILabel("") { Left = 41, Top = 2, Width = 38, Height = 4, Foreground = Color.Silver, Background = Color.Black };
				itemList = new UIList("", null, itemTexts) { Left = 2, Top = 2, Width = 36, Height = height, Background = Color.Black, Foreground = Color.Gray, Index = selection };
				itemList.Change = (s, e) =>
				{
					selection = itemList.Index;

					var t = inventoryTokens[itemList.Index];
					var i = inventoryItems[itemList.Index];
					var r = string.Empty;
					var d = i.HasToken("description") && !t.HasToken("unidentified") ? i.GetToken("description").Text : "This is " + i.ToString() + ".";

					d = Toolkit.Wordwrap(d, itemDesc.Width);

					if (i.ID == "book")
						r = "Press Enter to read.";
					else if (i.HasToken("equipable"))
					{
						if (t.HasToken("equipped"))
						{
							if (t.Path("cursed/known") != null)
								r = "Cannot unequip.";
							else
								r = "Press Enter to unequip.";
						}
						else
							r = "Press Enter to equip.";
					}
					else if (i.HasToken("quest"))
						r = "This is a quest key item.";
					else
						r = "Press Enter to try and use.";

					howTo.Text = (' ' + r).PadRight(80);
					itemDesc.Text = d;
					//howTo.Draw();
					//itemDesc.Draw();
					UIManager.Draw();
				};
				itemList.Enter = (s, e) =>
				{
					TryUse(player.Character, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
				};
				UIManager.Elements.Add(howTo);
				UIManager.Elements.Add(itemList);
				UIManager.Elements.Add(itemDesc);
				UIManager.Elements.Add(new UIButton("Drop", (s, e) => { TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]); }) { Left = 72, Top = 7, Width = 6, Height = 1 });
				UIManager.Highlight = itemList;
				itemList.Index = selection;

				UIManager.Draw();
			}

			if (keys[(int)Keys.Escape] || keys[(int)Keys.I])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else if (keys[(int)Keys.D])
			{
				TryDrop(player, inventoryTokens[itemList.Index], inventoryItems[itemList.Index]);
			}
			else
				UIManager.CheckKeys();
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
				host.Write("\u2500\u2500\u2500\u2500\u2524 <cTeal>Press <cAqua>ENTER <cTeal>to begin <cGray>\u251C\u2500\u2500\u2500\u2500", Color.Gray, Color.Transparent, 8, 11);
				//host.Write("<cSilver>\u263A   <cYellow,Red>\u263B<c>    <cAqua,Navy>\u263B<c>    <cYellow,Navy>\u263B<c>   <cWhite,Gray>\u263B<c>", Color.Silver, Color.Transparent, 14, 10);
				host.SetCell(3, 48, (char)0x2122, Color.Silver, Color.Transparent);
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
							NoxicoGame.AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
							NoxicoGame.AddMessage("Remember, press F1 for help and options.");
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
			Console.WriteLine("Collecting playables...");
			xDoc.Load("Noxico.xml");
			//var playables = xDoc.SelectNodes("//playable").OfType<XmlElement>();
			//foreach (var playable in playables)
			var bodyPlans = xDoc.SelectNodes("//bodyplan");
			foreach (var bodyPlan in bodyPlans.OfType<XmlElement>())
			{
				//var bodyPlan = playable.ParentNode as XmlElement;
				//var id = bodyPlan.GetAttribute("id");
				var id = bodyPlan.GetAttribute("id");
				if (bodyPlan.ChildNodes[0].Value == null)
				{
					Console.WriteLine(" * Skipping {0} -- old format.", id);
					continue;
				}
				var plan = bodyPlan.ChildNodes[0].Value.Replace("\r\n", "\n");
				if (!plan.Contains("playable"))
					continue;
				Console.WriteLine(" * Parsing {0}...", id);

				var genlock = plan.Contains("only\n"); //(bodyPlan.("maleonly") != null) || (bodyPlan.SelectSingleNode("femaleonly") != null) ||
					//(bodyPlan.SelectSingleNode("hermonly") != null) || (bodyPlan.SelectSingleNode("neuteronly") != null);
	
				var name = id;

				//TODO: write a support function that grabs everything for a specific token?
				//That is, given "terms \n \t generic ... \n tallness" it'd grab everything up to but not including tallness.
				//Use that to find subtokens like specific terms or colors.
				var terms = Toolkit.GrabToken(plan, "terms");
				var genOffset = terms.IndexOf("\tgeneric: ");
				var maleOffset = terms.IndexOf("\tmale: ");
				var femaleOffset = terms.IndexOf("\tfemale: ");
				if (genOffset != -1)
				{
					name = terms.Substring(genOffset + 11);
					name = name.Remove(name.IndexOf('\"')).Titlecase();
				}
				var male = name;
				var female = name;
				if (maleOffset != -1)
				{
					male = terms.Substring(maleOffset + 8);
					male = male.Remove(male.IndexOf('\"')).Titlecase();
				}
				if (femaleOffset != -1)
				{
					female = terms.Substring(femaleOffset + 10);
					female = female.Remove(female.IndexOf('\"')).Titlecase();
				}
				var genders = new List<string>() { male, female };

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
						oneof.ForEach(x => hairs.Add(Toolkit.NameColor(x.Trim()).Titlecase()));
					}
					else
					{
						hairs[0] = c.Remove(c.IndexOf('\n')).Titlecase();
					}
				}

				var eyes = new List<string>() { "Brown" };
				var eye = Toolkit.GrabToken(plan, "eyes");
				if (eye != null)
				{
					var c = eye.Substring(eye.IndexOf("color: ") + 7).Trim();
					if (c.StartsWith("oneof"))
					{
						eyes.Clear();
						c = c.Substring(6);
						c = c.Remove(c.IndexOf('\n'));
						var oneof = c.Split(',').ToList();
						oneof.ForEach(x => eyes.Add(Toolkit.NameColor(x.Trim()).Titlecase()));
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
						oneof.ForEach(x => skins.Add(Toolkit.NameColor(x.Trim()).Titlecase()));
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

				ret.Add(new PlayableRace() { ID = id, Name = name, GenderNames = genders, HairColors = hairs, SkinColors = skins, Skin = skinName, EyeColors = eyes, GenderLocked = genlock });

			}
			return ret;
		}
		private static List<PlayableRace> playables;

		private static Dictionary<string, UIElement> controls;
		private static List<UIElement>[] pages;

		private static int page = 0;
		private static Action<int> loadPage, loadColors;

		public static void CharacterCreator()
		{
			if (Subscreens.FirstDraw)
			{
				var traitsDoc = new XmlDocument();
				traitsDoc.LoadXml(global::Noxico.Properties.Resources.BonusTraits);
				var traits = new List<string>();
				foreach (var trait in traitsDoc.SelectNodes("//trait").OfType<XmlElement>())
					traits.Add(trait.GetAttribute("name"));

				controls = new Dictionary<string, UIElement>()
				{
					{ "backdrop", new UIPNGBackground(global::Noxico.Properties.Resources.CharacterGenerator) },
					{ "header", new UILabel("\u2500\u2500\u2524 Character Creation \u251C\u2500\u2500") { Left = 44, Top = 4, Foreground = Color.Black } },
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
					{ "gift", new UIList("", null, traits) { Left = 45, Top = 8, Width = 24, Height = 8 } },

					{ "topHeader", new UILabel("Starting a New Game") { Left = 1, Top = 0, Foreground = Color.Silver } },
					{ "helpLine", new UILabel("") { Tag = "worldGen", Left = 1, Top = 24, Foreground = Color.Silver } },
				};

				pages = new List<UIElement>[]
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

				playables = CollectPlayables();

				loadPage = new Action<int>(p =>
				{
					UIManager.Elements.Clear();
					UIManager.Elements.AddRange(pages[page]);
					UIManager.Highlight = UIManager.Elements[0];
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
					NoxicoGame.Sound.PlayMusic(NoxicoGame.HostForm.Noxico.CurrentBoard.Music);
					//NoxicoGame.HostForm.Noxico.SaveGame();
					NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
					NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
					Subscreens.FirstDraw = true;
					NoxicoGame.Immediate = true;
					NoxicoGame.AddMessage("Welcome to Noxico, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
					NoxicoGame.AddMessage("Remember, press F1 for help and options.");
					TextScroller.LookAt(NoxicoGame.HostForm.Noxico.Player);
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
