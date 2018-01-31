using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Noxico
{
	public class TextScroller
	{
		private static string[] text = { };
		private static int scroll = 0;
		private static DateTime slow = DateTime.Now;
		private static UIWindow window;

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			if (Subscreens.FirstDraw)
			{
				scroll = 1;
				Subscreens.FirstDraw = false;

				window = new UIWindow(text[0]) { Left = 3, Top = 1, Width = 74, Height = 22 };
				window.Draw();
				//Toolkit.DrawWindow(5, 3, 69, 18, text[0], Color.Navy, Color.Black, Color.Yellow);
				var help = ' ' + i18n.GetString("textscroller_help") + ' ';
				host.Write(help, UIColors.WindowBorder, Color.Transparent, 22, 40 - (help.Length() / 2));
				var empty = new string(' ', 70);
				for (int i = 1; i < 20; i++)
					host.Write(empty, UIColors.RegularText, UIColors.WindowBackground, 1 + i, 6);
				for (int i = scroll; i < text.Length && i - scroll < 20; i++)
				{
					if (i < 1)
						continue;
					host.Write(' ' + text[i].PadEffective(70), UIColors.RegularText, UIColors.WindowBackground, 1 + i, 4);
				}
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				if (text.Length > 24)
				{
					//TODO: Fix scrollbar, use better math. Perhaps the SCI Setup/Install code can help?
					/*
					for (int i = 2; i < 22; i++)
						host.SetCell(i, 74, (char)0x0ba, UIColors.Unfocused, UIColors.SelectedBackUnfocused, true);
					float pct = (float)(scroll - 1) / (float)((text.Length - 23 < 0) ? 1 : text.Length - 23);
					int tp = (int)(pct * 18) + 2;
					host.SetCell(tp, 74, (char)0x1f2, Color.Black, Color.Silver, true);
					*/
				}
				Subscreens.Redraw = false;
			}

			if (keys[System.Windows.Forms.Keys.S])
			{
				File.WriteAllText("current.txt", string.Join("\n", text));
				File.WriteAllText("current.html", string.Join("\n", text).ToHtml());
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.Me.CurrentBoard.Redraw();
				NoxicoGame.Me.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}

			if ((NoxicoGame.IsKeyDown(KeyBinding.ScrollUp) || Vista.DPad == XInputButtons.Up) && (DateTime.Now - slow).Milliseconds >= 100)
			{
				slow = DateTime.Now;
				scroll--;
				if (scroll < 1)
					scroll = 1;
				else
				{
					host.ScrollDown(2, 21, 4, 76, UIColors.DarkBackground);
					var i = scroll;
					host.Write(new string(' ', 72), UIColors.RegularText, UIColors.WindowBackground, 2, 4);
					host.Write(text[i], UIColors.RegularText, UIColors.WindowBackground, 2, 5);
					Subscreens.Redraw = true;
				}
			}
			if ((NoxicoGame.IsKeyDown(KeyBinding.ScrollDown) || Vista.DPad == XInputButtons.Down) && (DateTime.Now - slow).Milliseconds >= 100)
			{
				slow = DateTime.Now;
				scroll++;
				if (scroll > text.Length - 20)
					scroll = text.Length - 20;
				else if (scroll < 1)
					scroll = 1;
				else
				{
					host.ScrollUp(2, 21, 4, 76, UIColors.DarkBackground);
					var i = scroll + 19;
					host.Write(new string(' ', 72), UIColors.RegularText, UIColors.WindowBackground, 21, 4);
					if (i < text.Length)
						host.Write(text[i], UIColors.RegularText, UIColors.WindowBackground, 21, 5);
					Subscreens.Redraw = true;
				}
			}
		}

		public static void Plain(string message, string header = "", bool wrap = true, bool forceScroller = false)
		{
			if (wrap)
				text = (header + '\n' + message.SmartQuote().Wordwrap(76)).Split('\n');
			else
				text = (header + '\n' + message).SmartQuote().Split('\n');

			//If it's not worth a scroller, pass it through to a messagebox instead.
			if (text.Length < 40 && !forceScroller)
			{
				MessageBox.Notice(message.SmartQuote(), true, header);
				return;
			}

			NoxicoGame.HostForm.Cursor = new Point(-1, -1);
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void LookAt(BoardChar target)
		{
			var pa = target;
			var chr = ((BoardChar)pa).Character;
			Plain(chr.LookAt(pa), chr.GetKnownName(true), false, true); //Fix: disabled wrapping to prevent Look At from looking like shit with new wrapper.
		}

		public static void ReadBook(string bookID)
		{
			var bookData = new string[0];
			try
			{
				bookData = Mix.GetString("books\\" + bookID + ".txt").Split('\n');
			}
			catch (FileNotFoundException)
			{
				bookData = i18n.GetString("book_404").Split('\n');
			}
			var text = new StringBuilder();
			var header = string.Empty;
			var identification = string.Empty;
			/*
			var fonts = new Dictionary<string, int>()
			{
				{ "Hand", 0x200 },
				{ "Carve", 0x234 },
				{ "Daedric", 0x24E },
				{ "Alternian", 0x268 },
				{ "Felin", 0x282 },
			};
			*/
			for (var i = 0; i < bookData.Length; i++)
			{
				if (bookData[i].StartsWith("##"))
				{
					header = bookData[i].Substring(3);
					i++;
					if (bookData[i].StartsWith("##"))
						i++; //skip author
					if (bookData[i].StartsWith("##"))
					{
						identification = bookData[i].Substring(3);
						i++;
					}

					//var fontOffset = 0;
					//var fontHasLower = false;
					for (; i < bookData.Length; i++)
					{
						var line = bookData[i];
						if (line.StartsWith("## "))
							break;
						for (int j = 0; j < line.Length; j++)
						{
							if (j < line.Length - 2 && line.Substring(j, 3) == "<b>")
							{
								text.Append("<cYellow>");
								j += 2;
							}
							else if (j < line.Length - 3 && line.Substring(j, 4) == "</b>")
							{
								text.Append(" <c>");
								j += 3;
							}
							/*
							else if (j < line.Length - 2 && line.Substring(j, 2) == "<f")
							{
								var fontName = line.Substring(j + 2);
								fontName = fontName.Remove(fontName.IndexOf('>'));
								j = j + fontName.Length + 2;
								fontOffset = fonts.ContainsKey(fontName) ? fonts[fontName] : 0;
								fontHasLower = fontName == "Hand";
							}
							else
							{
								if (fontOffset == 0) */
									text.Append(line[j]);
							/*
								else
								{
									if (line[j] >= 'A' && line[j] <= 'Z')
									{
										text.Append((char)((line[j] - 'A') + fontOffset));
									}
									else if (fontHasLower && line[j] >= 'a' && line[j] <= 'z')
									{
										text.Append((char)((line[j] - 'a') + fontOffset + 0x1A));
									}
									else
									{
										text.Append(line[j]);
									}
								}
							}
							*/
						}
						//text.Append(bookData[i]);
						//text.AppendLine();
					}
					break;
				}
			}

			var player = NoxicoGame.Me.Player.Character;
			if (!string.IsNullOrWhiteSpace(identification))
			{
				foreach (var item in NoxicoGame.KnownItems.Where(ki => ki.HasToken("identify") && ki.GetToken("identify").Text == identification && !NoxicoGame.Identifications.Contains(ki.ID)))
					NoxicoGame.Identifications.Add(item.ID);
				//text += "<cLime>(Your " + skillProper + " knowledge has gone up.)";
			}
			Plain(text.ToString(), header);
		}
	}

}
