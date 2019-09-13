using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Noxico
{
	class Travel
	{
		public static void Open()
		{
			if (NoxicoGame.TravelTargets.Count < 2)
			{
				Subscreens.PreviousScreen.Clear();
				MessageBox.Notice(i18n.GetString("travel_nowhere"), true);
				return;
			}
			Subscreens.FirstDraw = true;
			NoxicoGame.Subscreen = Travel.Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
		}

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;

				var backdrop = new Bitmap(Program.Cols, Program.Rows * 2, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				var ccback = Mix.GetBitmap("travelback.png");
				var ccpage = Mix.GetBitmap("ccpage.png");
				var ccbars = Mix.GetBitmap("ccbars.png");
				var pageTop = Program.Rows - (ccpage.Height / 2);
				using (var gfx = Graphics.FromImage(backdrop))
				{
					gfx.Clear(Color.Black);
					gfx.DrawImage(ccback, 0, 0, Program.Cols, Program.Rows * 2);
					gfx.DrawImage(ccpage, 0, pageTop, ccpage.Width, ccpage.Height);
					var barsSrc = new System.Drawing.Rectangle(0, 3, ccbars.Width, 7);
					var barsDst = new System.Drawing.Rectangle(0, 0, Program.Cols, 7);
					gfx.DrawImage(ccbars, barsDst, barsSrc, GraphicsUnit.Pixel);
					barsSrc = new System.Drawing.Rectangle(0, 0, ccbars.Width, 5);
					barsDst = new System.Drawing.Rectangle(0, (Program.Rows * 2) - 5, Program.Cols, 5);
					gfx.DrawImage(ccbars, barsDst, barsSrc, GraphicsUnit.Pixel);
				}

				var list = new UIList()
				{
					Left = 4,
					Top = (pageTop / 2) + 4,
					Width = 34,
					Height = (ccpage.Height / 2) - 6,
					Background = Color.White,
					Foreground = Color.Black,
				};

				UIManager.Elements.Add(new UIPNGBackground(backdrop));
				UIManager.Elements.Add(new UILabel(i18n.GetString("travel_header")) { Left = 1, Top = 0, Foreground = Color.Silver });
				UIManager.Elements.Add(new UILabel(i18n.GetString("travel_footer")) { Left = 1, Top = Program.Rows - 1, Foreground = Color.Silver });
				UIManager.Elements.Add(new UILabel(i18n.GetString("travel_current") + "\n \x07<cCyan> " + (host.Noxico.CurrentBoard.Name ?? "Somewhere"))
				{
					Left = 44,
					Top = (pageTop / 2) + 4,
					Width = Program.Cols - 46,
					Foreground = Color.Teal
				});
				UIManager.Elements.Add(list);
				
				var targets = new List<int>();
				foreach (var target in NoxicoGame.TravelTargets)
					targets.Add(target.Key);
				targets.Sort();
				list.Items.AddRange(targets.Select(x => NoxicoGame.TravelTargets[x]));
				list.Index = 0; //fixes crash when pressing Enter right away

				list.Enter = (s, e) =>
				{
					var newBoard = NoxicoGame.TravelTargets.First(tn => tn.Value == list.Text).Key;
					if (host.Noxico.CurrentBoard.BoardNum == newBoard)
						return;

					NoxicoGame.Mode = UserMode.Walkabout;
					Subscreens.FirstDraw = true;

					NoxicoGame.InGameTime = NoxicoGame.InGameTime.AddDays(1);
					while (Toolkit.IsNight())
						NoxicoGame.InGameTime = NoxicoGame.InGameTime.AddHours(Random.Next(1, 3));
					NoxicoGame.InGameTime = NoxicoGame.InGameTime.AddMinutes(Random.Next(10, 50));

					host.Noxico.Player.OpenBoard(newBoard);
					var hereNow = host.Noxico.Player.ParentBoard;
					if (hereNow.BoardType == BoardType.Dungeon)
					{
						//find the exit and place the player there
						//TODO: something similar for towns
						var dngExit = hereNow.Warps.FirstOrDefault(w => w.TargetBoard == -2);
						if (dngExit != null)
						{
							host.Noxico.Player.XPosition = dngExit.XPosition;
							host.Noxico.Player.YPosition = dngExit.YPosition;
						}
					}
					else
						host.Noxico.Player.Reposition();
				};

				if (host.Noxico.CurrentBoard.Name != null)
				{
					var thisBoard = NoxicoGame.TravelTargets.FirstOrDefault(tn => host.Noxico.CurrentBoard.Name.StartsWith(tn.Value));
					if (thisBoard.Value != null)
						list.Index = list.Items.FindIndex(i => thisBoard.Value.StartsWith(i));
				}
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				UIManager.Draw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				host.Noxico.CurrentBoard.Redraw();
				host.Noxico.CurrentBoard.Draw(true);
				host.Noxico.CurrentBoard.AimCamera();
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else
				UIManager.CheckKeys();
		}
	}
}
