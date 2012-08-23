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
	public class MessageBox
	{
		private enum BoxType { Message, Question, List };
		private static string[] text = { };
		private static BoxType type;
		private static string title;
		private static Action onYes, onNo;
		private static Dictionary<object, string> options;
		private static int option;
		private static bool allowEscape;
		public static object Answer { get; private set; }

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var rows = text.Length - 2;
			if (type == BoxType.List)
				rows += options.Count + 1;
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				Toolkit.DrawWindow(5, 5, 69, rows + 2, type == BoxType.Question ? "Question" : title, Color.Gray, Color.Black, Color.White);
				for (int i = 0; i < text.Length; i++)
					host.Write(text[i], Color.Silver, Color.Black, 7, 6 + i);
				if (type == BoxType.Question)
					host.Write("<g2561><cWhite> Y/N <cGray><g255E>", Color.Gray, Color.Black, 66, 7 + rows);
				else if (type == BoxType.List)
				{
					for (int i = 0; i < options.Count; i++)
						host.Write(options.ElementAt(i).Value.PadRight(66), i == option ? Color.White : Color.Gray, i == option ? Color.Navy : Color.Black, 7, 8 + text.Length - 2 + i);
					host.Write("<g2561><cWhite> <g2191>/<g2193> <cGray><g255E>", Color.Gray, Color.Black, 66, 7 + rows);
				}
				else
					host.Write("<g2561><cWhite><g2026><cGray><g255E>", Color.Gray, Color.Black, 70, 7 + rows);
			}
			if (type == BoxType.List)
			{
				if (keys[(int)Keys.Up])
				{
					NoxicoGame.ClearKeys();
					NoxicoGame.Sound.PlaySound("Cursor");
					if (option == 0)
						option = options.Count;
					option--;
					Subscreens.FirstDraw = true;
				}
				else if (keys[(int)Keys.Down])
				{
					NoxicoGame.ClearKeys();
					NoxicoGame.Sound.PlaySound("Cursor");
					option++;
					if (option == options.Count)
						option = 0;
					Subscreens.FirstDraw = true;
				}
			}
			if (keys[(int)Keys.Escape] || keys[(int)Keys.Enter] || (type == BoxType.Question && (keys[(int)Keys.Y] || keys[(int)Keys.N])))
			{
				if (type == BoxType.List && keys[(int)Keys.Escape])
				{
					if (!allowEscape)
						return;
					else
						option = -1;
				}

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
				if (type == BoxType.Question)
				{
					if ((keys[(int)Keys.Enter] || keys[(int)Keys.Y]) && onYes != null)
					{
						NoxicoGame.Sound.PlaySound("Get Item");
						NoxicoGame.ClearKeys();
						onYes();
					}
					else if ((keys[(int)Keys.Escape] || keys[(int)Keys.N]) && onNo != null)
					{
						NoxicoGame.Sound.PlaySound("Put Item");
						NoxicoGame.ClearKeys();
						onNo();
					}
				}
				else if (type == BoxType.List)
				{
					NoxicoGame.Sound.PlaySound(option == -1 ? "Put Item" : "Get Item");
					Answer = options.ElementAt(option).Key;
					onYes();
					NoxicoGame.ClearKeys();
				}
				else
				{
					type = BoxType.Message;
					NoxicoGame.ClearKeys();
				}
			}
		}

		public static void List(string question, Dictionary<object, string> options, Action okay, bool allowEscape = false, bool dontPush = false, string title = "")
		{
			if (!dontPush && NoxicoGame.Subscreen != null)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			type = BoxType.List;
			MessageBox.title = title;
			text = Toolkit.Wordwrap(question.Trim(), 68).Split('\n');
			option = 0;
			onYes = okay;
			MessageBox.options = options;
			MessageBox.allowEscape = allowEscape;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Ask(string question, Action yes, Action no, bool dontPush = false, string title = "")
		{
			if (!dontPush && NoxicoGame.Subscreen != null)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			type = BoxType.Question;
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
			type = BoxType.Message;
			text = Toolkit.Wordwrap(message.Trim(), 68).Split('\n');
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}
	}
}
