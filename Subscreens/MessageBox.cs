using System;
using System.Collections.Generic;
using System.Linq;

namespace Noxico
{
	public class MessageBox
	{
		private enum BoxType { Notice, Question, List, Input };
		//private static string[] text = { };
		private static string text;
		private static BoxType type;
		private static string title;
		private static Action onYes, onNo;
		private static Dictionary<object, string> options;
		private static int option;
		private static bool allowEscape;
		public static object Answer { get; private set; }
		public static Action ScriptPauseHandler { get; set; }

		private static UIWindow win;
		private static UILabel lbl;
		private static UIList lst;
		private static UILabel key;
		private static UITextBox txt;
		private static bool fromWalkaround;

		public static void Handler()
		{
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				var lines = text.Split('\n').Length;
				var height = lines + 1;
				if (type == BoxType.List)
					height += 1 + options.Count;
				else if (type == BoxType.Input)
					height += 2;
				var top = 10 - (height / 2);
				if (top < 0)
					top = 0;
				if (UIManager.Elements == null || fromWalkaround)
					UIManager.Initialize();

				win = new UIWindow(type == BoxType.Question ? i18n.GetString("msgbox_question") : title) { Left = 15, Top = top, Width = 70, Height = height };
				UIManager.Elements.Add(win);
				lbl = new UILabel(text) { Left = 17, Top = top + 1, Width = 68, Height = lines };
				UIManager.Elements.Add(lbl);
				lst = null;
				txt = null;
				if (type == BoxType.List)
				{
					lst = new UIList("", Enter, options.Values.ToList(), 0) { Left = 17, Top = top + lines + 1, Width = 66, Height = options.Count };
					lst.Change += (s, e) =>
						{
							option = lst.Index;
							Answer = options.Keys.ToArray()[option];
						};
					lst.Change(null, null);
					UIManager.Elements.Add(lst);
				}
				else if (type == BoxType.Input)
				{
					txt = new UITextBox((string)Answer) { Left = 17, Top = top + lines + 1, Width = 65, Height = 1 };
					UIManager.Elements.Add(txt);
				}
				var keys = string.Empty;
				if (type == BoxType.Notice || type == BoxType.Input)
					keys = "  \u2026  ";
				else if (type == BoxType.Question)
					keys = " " + Toolkit.TranslateKey(KeyBinding.Accept) + "/" + Toolkit.TranslateKey(KeyBinding.Back) + " ";
				else if (type == BoxType.List)
					keys = " \u2191/\u2193 ";
				key = new UILabel(keys) { Top = top + height - 1, Left = 82 - keys.Length() };
				UIManager.Elements.Add(key);
				
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				UIManager.Draw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || NoxicoGame.IsKeyDown(KeyBinding.Accept) || Vista.Triggers == XInputButtons.A || Vista.Triggers == XInputButtons.B)
			{
				if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
				{
					if (type == BoxType.List)
					{
						if (!allowEscape)
							return;
						else
							option = -1;
					}
					else if (type == BoxType.Input)
					{
						UIManager.CheckKeys();
						return;
					}
				}

				Enter(null, null);

				if (type == BoxType.Question)
				{
					if ((NoxicoGame.IsKeyDown(KeyBinding.Accept) || Vista.Triggers == XInputButtons.A) && onYes != null)
					{
						NoxicoGame.ClearKeys();
						onYes();
					}
					else if ((NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B) && onNo != null)
					{
						NoxicoGame.ClearKeys();
						onNo();
					}
				}
				else if (type == BoxType.List)
				{
					Answer = option == -1 ? -1 : options.ElementAt(option).Key;
					onYes();
					NoxicoGame.ClearKeys();
				}
				else if (type == BoxType.Input)
				{
					Answer = txt.Text;
					onYes();
					NoxicoGame.ClearKeys();
				}
				else
				{
					type = BoxType.Notice;
					NoxicoGame.ClearKeys();
				}
				if (ScriptPauseHandler != null)
				{
					ScriptPauseHandler();
					ScriptPauseHandler = null;
				}
			}
			else
			{
				UIManager.CheckKeys();
			}
		}

		private static void Enter(object sender, EventArgs args)
		{
			Remove();
			var host = NoxicoGame.HostForm;
			if (Subscreens.PreviousScreen.Count == 0)
			{
				UIManager.Initialize();
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
		}

		private static void Remove()
		{
			UIManager.Elements.Remove(win);
			UIManager.Elements.Remove(lbl);
			if (lst != null)
				UIManager.Elements.Remove(lst);
			UIManager.Elements.Remove(key);
		}

		public static void List(string question, Dictionary<object, string> options, Action okay, bool allowEscape = false, bool doNotPush = false, string title = "")
		{
			fromWalkaround = NoxicoGame.Subscreen == null || Subscreens.PreviousScreen.Count == 0;
			if (!doNotPush)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			type = BoxType.List;
			MessageBox.title = title;
			text = Toolkit.Wordwrap(question.Trim(), 66); //.Split('\n');
			option = 0;
			onYes = okay;
			MessageBox.options = options;
			MessageBox.allowEscape = allowEscape;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Ask(string question, Action yes, Action no, bool doNotPush = false, string title = "")
		{
			fromWalkaround = NoxicoGame.Subscreen == null || Subscreens.PreviousScreen.Count == 0;
			if (!doNotPush)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			type = BoxType.Question;
			MessageBox.title = title;
			text = Toolkit.Wordwrap(question.Trim(), 66); //.Split('\n');
			onYes = yes;
			onNo = no;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Notice(string message, bool doNotPush = false, string title = "")
		{
			fromWalkaround = NoxicoGame.Subscreen == null || Subscreens.PreviousScreen.Count == 0;
			if (!doNotPush)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			MessageBox.title = title;
			type = BoxType.Notice;
			text = Toolkit.Wordwrap(message.Trim(), 66); //.Split('\n');
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}

		public static void Input(string message, string defaultValue, Action okay, bool doNotPush = false, string title = "")
		{
			fromWalkaround = NoxicoGame.Subscreen == null || Subscreens.PreviousScreen.Count == 0;
			if (!doNotPush)
				Subscreens.PreviousScreen.Push(NoxicoGame.Subscreen);
			NoxicoGame.Subscreen = MessageBox.Handler;
			MessageBox.title = title;
			type = BoxType.Input;
			text = Toolkit.Wordwrap(message.Trim(), 66); //.Split('\n');
			Answer = defaultValue;
			onYes = okay;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
		}
	}
}
