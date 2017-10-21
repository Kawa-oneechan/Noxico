using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	/// <summary>
	/// Displays a list of actions to choose from.
	/// </summary>
	public class ActionList
	{
		private static Action onChoice;
		private static Dictionary<object, string> options; //keys are used as values for Answer.
		private static int option;

		private static UIWindow win;
		private static UIList lst;

		/// <summary>
		/// The chosen action.
		/// </summary>
		public static object Answer { get; private set; }

		/// <summary>
		/// Displays a list at the given screen location or close to it.
		/// </summary>
		/// <param name="title">The title of the window.</param>
		/// <param name="x">The horizontal coordinate to aim the window at.</param>
		/// <param name="y">The vertical coordinate to aim the window at.</param>
		/// <param name="options">A list of options to display.</param>
		/// <param name="okay">What to do when an option is chosen.</param>
		public static void Show(string title, int x, int y, Dictionary<object, string> options, Action okay)
		{
			option = 0;
			onChoice = okay;
			ActionList.options = options;

			//Determine window width according to its contents.
			var width = title.Length() + 4;
			foreach (var o in options.Values)
			{
				if (o.Length() > width)
					width = o.Length();
			}
			width += 4;
			//Place the window just to the right of the specified location.
			//If this goes off-screen, try placing it to the left instead.
			if (x + 1 + width >= Program.Cols)
				x = x - width;
			else
				x++;
			var height = options.Count + 2;
			//Check if we're going off the bottom of the screen and correct.
			if (y + height >= Program.Rows)
				y = Program.Rows - height;
			//If we go off the left or top, fuck it -- overlap the target.
			if (x < 0)
				x = 0;
			if (y < 0)
				y = 0;

			UIManager.Initialize();
			win = new UIWindow(title) { Left = x, Top = y, Width = width, Height = height };
			lst = new UIList("", Enter, options.Values.ToList(), 0) { Left = x + 1, Top = y + 1, Width = width - 2, Height = height - 2, Background = UIColors.WindowBackground };
			lst.Change += (s, e) =>
			{
				option = lst.Index;
				ActionList.Answer = options.Keys.ToArray()[option];
			};
			lst.Change(null, null); //Make sure we have something -- the first item -- selected.
			UIManager.Elements.Add(win);
			UIManager.Elements.Add(lst);

			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.Subscreen = ActionList.Handler;
		}

		/// <summary>
		/// Generic Subscreen handler.
		/// </summary>
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
				//If we pressed Back/Esc or [B], pick the out-of-band -1 sentinel option.
				if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
					option = -1;

				Enter(null, null);

				ActionList.Answer = option == -1 ? -1 : options.ElementAt(option).Key;
				onChoice(); //Let the caller handle things.
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