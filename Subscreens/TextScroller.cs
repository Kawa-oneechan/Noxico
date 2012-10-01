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
				File.WriteAllText("current.html", Toolkit.HTMLize(string.Join("\n", text)));
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
			var xDoc = Mix.GetXMLDocument("books.xml"); //new XmlDocument();
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
				text = "Can't find the content for this book.";
			else
			{
				header = book.GetAttribute("title");
				text = book.Noxicize();
			}

			Plain(text, header);
		}

		[Obsolete]
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

}
