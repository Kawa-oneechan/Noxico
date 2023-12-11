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
			var left = (Program.Cols / 2) - (72 / 2);
			if (Subscreens.FirstDraw)
			{
				scroll = 1;
				Subscreens.FirstDraw = false;

				window = new UIWindow(text[0]) { Left = left, Top = 1, Width = 74, Height = Program.Rows - 3 };
				window.Draw();
				var help = "\u0328 " + i18n.GetString("textscroller_help") + " \u0329";
				host.Write(help, UIColors.WindowBorder, Color.Transparent, Program.Rows - 3, (Program.Cols / 2) - (help.Length() / 2));
				var empty = new string(' ', 70);
				for (int i = 1; i < Program.Rows - 5; i++)
					host.Write(empty, UIColors.WindowBorder, UIColors.WindowBackground, 1 + i, left + 2);
				for (int i = scroll; i < text.Length && i - scroll < Program.Rows - 5; i++)
				{
					if (i < 1)
						continue;
					host.Write(' ' + text[i].PadEffective(70), UIColors.RegularText, UIColors.WindowBackground, 1 + i, left + 2);
				}
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				NoxicoGame.HostForm.SetCell(3, left + 73, (scroll > 1) ? '\u030A' : '\u0302', UIColors.WindowBorder, UIColors.WindowBackground);
				NoxicoGame.HostForm.SetCell(Program.Rows - 4, left + 73, (scroll + 21 < text.Length) ? '\u032A' : '\u0302', UIColors.WindowBorder, UIColors.WindowBackground);
				Subscreens.Redraw = false;
			}

			if (keys[System.Windows.Forms.Keys.S])
			{
				File.WriteAllText("current.txt", string.Join("\n", text).ToUnicode());
				File.WriteAllText("current.html", string.Join("\n", text).ToUnicode().ToHtml());
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
					host.ScrollDown(2, Program.Rows - 4, left + 1, left + 72, UIColors.DarkBackground);
					var i = scroll;
					host.Write(new string(' ', 72), UIColors.RegularText, UIColors.WindowBackground, 2, left + 1);
					host.Write(text[i], UIColors.RegularText, UIColors.WindowBackground, 2, left + 3);
					Subscreens.Redraw = true;
				}
			}
			if ((NoxicoGame.IsKeyDown(KeyBinding.ScrollDown) || Vista.DPad == XInputButtons.Down) && (DateTime.Now - slow).Milliseconds >= 100)
			{
				slow = DateTime.Now;
				scroll++;
				if (scroll > text.Length - Program.Rows + 4)
					scroll = text.Length - Program.Rows + 4;
				else if (scroll < 1)
					scroll = 1;
				else
				{
					host.ScrollUp(2, Program.Rows - 4, left + 1, left + 72, UIColors.DarkBackground);
					var i = scroll + Program.Rows - 6;
					host.Write(new string(' ', 72), UIColors.RegularText, UIColors.WindowBackground, Program.Rows - 4, left + 1);
					if (i < text.Length)
						host.Write(text[i], UIColors.RegularText, UIColors.WindowBackground, Program.Rows - 4, left + 3);
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
			if (text.Length < Program.Rows - 10 && !forceScroller)
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
			if (!identification.IsBlank())
			{
				foreach (var item in NoxicoGame.KnownItems.Where(ki => ki.HasToken("identify") && ki.GetToken("identify").Text == identification && !NoxicoGame.Identifications.Contains(ki.ID)))
					NoxicoGame.Identifications.Add(item.ID);
				//text += "<cLime>(Your " + skillProper + " knowledge has gone up.)";
			}
			Plain(text.ToString(), header);
		}
	}

}
