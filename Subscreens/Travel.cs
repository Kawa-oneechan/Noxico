using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Noxico
{
	class Travel
	{
		public static void Open()
		{
			if (NoxicoGame.KnownTargets.Count < 2)
			{
				Subscreens.PreviousScreen.Clear();
				MessageBox.Message("You don't know of any other place to go.", true);
				return;
			}
			Subscreens.FirstDraw = true;
			NoxicoGame.Subscreen = Travel.Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
		}

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;

			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;

				var list = new UIList()
				{
					//Items = NoxicoGame.KnownTargets.Select(kt => NoxicoGame.TargetNames[kt]).ToList(),
					Left = 1,
					Top = 3,
					Width = 40,
					Height = 20,
				};
				
				//NoxicoGame.KnownTargets.Select(kt => NoxicoGame.TargetNames[kt]).ToList();
				var targets = new List<int>();
				foreach (var target in NoxicoGame.KnownTargets.Where(kt => kt > -1))
					targets.Add(target);
				var moreTargets = new List<int>();
				foreach (var target in NoxicoGame.KnownTargets.Where(kt => kt < 0))
					moreTargets.Add(target);
				targets.Sort();
				moreTargets.Sort();
				list.Items = new List<string>();
				list.Items.AddRange(targets.Select(kt => NoxicoGame.TargetNames[kt]));
				list.Items.AddRange(moreTargets.Select(kt => NoxicoGame.TargetNames[kt]));

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
							MessageBox.Message("Something went wrong internally.\n\nCould not find expectation #" + newBoard + ".", true, "Fuck!");
							return;
						}
						Board thisMap = null;
						var expectation = NoxicoGame.Expectations[newBoard];
						if (!expectation.Dungeon)
							thisMap = WorldGen.CreateTown(expectation.Biome, expectation.Culture, NoxicoGame.TargetNames[key], true);
						else
							thisMap = WorldGen.CreateDungeon(expectation.Biome, expectation.Culture, NoxicoGame.TargetNames[key]);

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
					if (hereNow.Type == BoardType.Dungeon)
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

				UIManager.Elements.Add(list);
				UIManager.Elements.Add(new UILabel("Obvious WIP is obvious.") { Left = 1, Top = 1, Foreground = Color.Black, Background = Color.White });
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				UIManager.Draw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back))
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
