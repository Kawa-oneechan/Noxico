using System;
using System.IO;
using System.Linq;
using System.Xml;

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

				window = new UIWindow(text[0]) { Left = 5, Top = 1, Width = 90, Height = 26 };
				window.Draw();
				//Toolkit.DrawWindow(5, 3, 69, 18, text[0], Color.Navy, Color.Black, Color.Yellow);
				var help = ' ' + i18n.GetString("textscroller_help") + ' ';
				host.Write(help, UIColors.WindowBorder, Color.Transparent, 26, 50 - (help.Length() / 2));
				for (int i = scroll; i < text.Length && i - scroll < 24; i++)
				{
					if (i < 1)
						continue;
					host.Write(' ' + text[i].PadEffective(88), UIColors.RegularText, UIColors.DarkBackground, 1 + i, 6);
				}
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				if (text.Length > 24)
				{
					for (int i = 2; i < 26; i++)
						host.SetCell(i, 94, (char)0x2551, UIColors.Unfocused, UIColors.SelectedBackUnfocused, true);
					float pct = (float)(scroll - 1) / (float)((text.Length - 23 < 0) ? 1 : text.Length - 23);
					int tp = (int)(pct * 20) + 2;
					host.SetCell(tp, 94, (char)0x2195, Color.Black, Color.Silver, true);
				}
				Subscreens.Redraw = false;
			}

			if (keys[(int)System.Windows.Forms.Keys.S])
			{
				File.WriteAllText("current.txt", string.Join("\n", text));
				File.WriteAllText("current.html", Toolkit.HTMLize(string.Join("\n", text)));
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
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
					host.ScrollDown(2, 25, 6, 94, UIColors.DarkBackground);
					var i = scroll;
					host.Write(new string(' ', 88), UIColors.RegularText, UIColors.DarkBackground, 2, 6);
					host.Write(text[i], UIColors.RegularText, UIColors.DarkBackground, 2, 7);
					Subscreens.Redraw = true;
				}
			}
			if ((NoxicoGame.IsKeyDown(KeyBinding.ScrollDown) || Vista.DPad == XInputButtons.Down) && (DateTime.Now - slow).Milliseconds >= 100)
			{
				slow = DateTime.Now;
				scroll++;
				if (scroll > text.Length - 24)
					scroll = text.Length - 24;
				else if (scroll < 1)
					scroll = 1;
				else
				{
					host.ScrollUp(2, 25, 6, 94, UIColors.DarkBackground);
					var i = scroll + 23;
					host.Write(new string(' ', 88), UIColors.RegularText, UIColors.DarkBackground, 25, 6);
					if (i < text.Length)
						host.Write(text[i], UIColors.RegularText, UIColors.DarkBackground, 25, 7);
					Subscreens.Redraw = true;
				}
			}
		}

		public static void Plain(string message, string header = "", bool wrap = true)
		{
			if (wrap)
				text = (header + '\n' + message.SmartQuote().Wordwrap(68)).Split('\n');
			else
				text = (header + '\n' + message).SmartQuote().Split('\n');
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void LookAt(BoardChar target)
		{
			var pa = target;
			var chr = ((BoardChar)pa).Character;
			Plain(chr.LookAt(pa), chr.Name.ToString(true));
		}

		public static void ReadBook(int bookNum)
		{
			var xDoc = Mix.GetXMLDocument("books.xml");
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
			var text = "";
			var header = "";
			if (book == null)
				text = i18n.GetString("textscroller_book404");
			else
			{
				header = book.GetAttribute("title");
				text = book.Noxicize();
			}

			var identification = book.SelectSingleNode("identify");
			var player = NoxicoGame.HostForm.Noxico.Player.Character;
			if (identification != null)
			{
				var id = ((XmlElement)identification).GetAttribute("token");
				foreach (var item in NoxicoGame.KnownItems.Where(ki => ki.HasToken("identify") && ki.GetToken("identify").Text == id && !NoxicoGame.Identifications.Contains(ki.ID)))
					NoxicoGame.Identifications.Add(item.ID);
				//text += "<cLime>(Your " + skillProper + " knowledge has gone up.)";
			}
			if (player.Path("books/book_" + bookNum) == null)
			{
				if (!player.HasToken("books"))
					player.AddToken("books");
				var bookToken = player.GetToken("books").AddToken("book_" + bookNum);
				bookToken.Text = header;
			}

			Plain(text, header);
		}
	}

}
