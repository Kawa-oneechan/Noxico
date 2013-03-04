using System.Collections.Generic;
using System.Linq;

namespace Noxico
{
	class Travel
	{
		private static int expectationStart;

		public static void Open()
		{
			if (NoxicoGame.KnownTargets.Count < 2)
			{
				Subscreens.PreviousScreen.Clear();
				MessageBox.Notice("You don't know of any other place to go.", true);
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

				var list = new UIList()
				{
					//Items = NoxicoGame.KnownTargets.Select(kt => NoxicoGame.TargetNames[kt]).ToList(),
					Left = 4,
					Top = 4,
					Width = 28,
					Height = 16,
					Background = Color.White,
					Foreground = Color.Black,
				};
				UIManager.Elements.Add(new UIPNGBackground(Mix.GetBitmap("travel.png")));
				UIManager.Elements.Add(new UILabel("Travel Mode") { Left = 1, Top = 0, Foreground = Color.Silver });
				UIManager.Elements.Add(new UILabel(Toolkit.TranslateKey(KeyBinding.Up, true) + '/' + Toolkit.TranslateKey(KeyBinding.Down, true) + '/' + Toolkit.TranslateKey(KeyBinding.Accept, true) + " to select, " + Toolkit.TranslateKey(KeyBinding.Back, true) + " to cancel.") { Left = 1, Top = 24, Foreground = Color.Silver });
				UIManager.Elements.Add(new UILabel("Current location:\n \u2022<cCyan> " + host.Noxico.CurrentBoard.Name) { Left = 34, Top = 2, Width = 30, Foreground = Color.Teal });
				UIManager.Elements.Add(new UILabel("You have not been here before.") { Left = 34, Top = 6, Tag = "expect", Foreground = Color.Teal, Hidden = true });
				UIManager.Elements.Add(list);
				
				//NoxicoGame.KnownTargets.Select(kt => NoxicoGame.TargetNames[kt]).ToList();
				var targets = new List<int>();
				foreach (var target in NoxicoGame.KnownTargets.Where(kt => kt > -1))
					targets.Add(target);
				var moreTargets = new List<int>();
				foreach (var target in NoxicoGame.KnownTargets.Where(kt => kt < 0))
					moreTargets.Add(target);
				targets.Sort();
				moreTargets.Sort();
				//list.Items = new List<string>();
				list.Items.AddRange(targets.Select(kt => NoxicoGame.TargetNames[kt]));
				expectationStart = list.Items.Count;
				list.Items.AddRange(moreTargets.Select(kt => NoxicoGame.TargetNames[kt]));

				list.Change = (s, e) =>
				{
					UIManager.Elements.Find(x => x.Tag == "expect").Hidden = (list.Index < expectationStart);
					UIManager.Draw();
				};
				list.Enter = (s, e) =>
				{
					var key = NoxicoGame.TargetNames.First(tn => tn.Value == list.Text).Key; //NoxicoGame.TargetNames.Keys.ToArray()[list.Index];
					var newBoard = NoxicoGame.KnownTargets.Find(kt => kt == key);

					if (newBoard < 0)
					{
						//Expectation! Get the expectation index by abs(newBoard), then fullfill it.
						if (!NoxicoGame.Expectations.ContainsKey(newBoard))
						{
							NoxicoGame.KnownTargets.Remove(key);
							NoxicoGame.TargetNames.Remove(key);
							MessageBox.Notice("Something went wrong internally.\n\nCould not find expectation #" + newBoard + ".", true, "Fuck!");
							return;
						}
						Board thisMap = null;
						var expectation = NoxicoGame.Expectations[newBoard];
						if (expectation.Type == BoardType.Town)
							thisMap = WorldGen.CreateTown(expectation.Biome, expectation.Culture, NoxicoGame.TargetNames[key], true);
						else if (expectation.Type == BoardType.Dungeon)
							thisMap = WorldGen.CreateDungeon(expectation.Biome, expectation.Culture, NoxicoGame.TargetNames[key]);

						Expectation.AddCharacters(thisMap, expectation.Characters);

						NoxicoGame.KnownTargets.Remove(key);
						NoxicoGame.TargetNames.Remove(key);
						NoxicoGame.KnownTargets.Add(thisMap.BoardNum);
						NoxicoGame.TargetNames.Add(thisMap.BoardNum, thisMap.Name);

						newBoard = thisMap.BoardNum;
					}

					if (host.Noxico.CurrentBoard.BoardNum == newBoard)
						return;

					NoxicoGame.Mode = UserMode.Walkabout;
					Subscreens.FirstDraw = true;

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
				};

				//var thisBoard = NoxicoGame.KnownTargets.FirstOrDefault(kt => kt == host.Noxico.CurrentBoard.BoardNum);
				//list.Index = NoxicoGame.TargetNames.First(tn => tn.Key == thisBoard).Key;
				var thisBoard = NoxicoGame.TargetNames.FirstOrDefault(tn => host.Noxico.CurrentBoard.Name.StartsWith(tn.Value));
				list.Index = list.Items.FindIndex(i => thisBoard.Value.StartsWith(i));
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
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else
				UIManager.CheckKeys();
		}
	}
}
