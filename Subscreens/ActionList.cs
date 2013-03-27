using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public class ActionList
	{
		private static Action onChoice;
		private static Dictionary<object, string> options;
		private static int option;

		private static UIWindow win;
		private static UIList lst;

		public static object Answer { get; private set; }

		public static void Show(string title, int x, int y, Dictionary<object, string> options, Action okay)
		{
			option = 0;
			onChoice = okay;
			ActionList.options = options;

			var width = title.Length + 4;
			foreach (var o in options.Values)
			{
				if (o.Length > width)
					width = o.Length;
			}
			width += 4;
			if (x + 1 + width >= 100)
				x = x - width;
			else
				x++;
			var height = options.Count + 2;
			if (y + height >= 30)
				y = 30 - height;
			if (x < 0)
				x = 0;
			if (y < 0)
				y = 0;

			UIManager.Initialize();
			win = new UIWindow(title) { Left = x, Top = y, Width = width, Height = height };
			lst = new UIList("", Enter, options.Values.ToList(), 0) { Left = x + 1, Top = y + 1, Width = width - 2, Height = height - 2 };
			lst.Change += (s, e) =>
			{
				option = lst.Index;
				ActionList.Answer = options.Keys.ToArray()[option];
			};
			lst.Change(null, null);
			UIManager.Elements.Add(win);
			UIManager.Elements.Add(lst);

			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.Subscreen = ActionList.Handler;
		}

		public static void Handler()
		{
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
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
					option = -1;

				Enter(null, null);

				NoxicoGame.Sound.PlaySound(option == -1 ? "Put Item" : "Get Item");
				ActionList.Answer = option == -1 ? -1 : options.ElementAt(option).Key;
				onChoice();
				NoxicoGame.ClearKeys();
			}
			else
			{
				UIManager.CheckKeys();
			}
		}

		private static void Enter(object sender, EventArgs args)
		{
			UIManager.Elements.Clear();
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
	}

}