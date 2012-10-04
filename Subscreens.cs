//This file holds UNSORTED subscreens that need to be filtered out into the /subscreens folder.
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
	public class Subscreens
	{
		public static Stack<SubscreenFunc> PreviousScreen = new Stack<SubscreenFunc>();

		public static bool FirstDraw = true;
		public static bool Redraw = true;

		public static bool UsingMouse = false;
		public static bool Mouse = false;
		public static int MouseX = -1;
		public static int MouseY = -1;
	}

	public class UnsortedSubscreens
	{
		//TODO: Refactor the below
		public static int DungeonGeneratorEntranceBoardNum;
		public static string DungeonGeneratorEntranceWarpID;
		public static int DungeonGeneratorBiome;
		public static bool UntilMorning;

		public static void SleepAWhile()
		{
			var player = NoxicoGame.HostForm.Noxico.Player;
			var pchar = player.Character;
			var max = pchar.GetMaximumHealth();
			var heal = new TimeSpan(0, 10, 0);
			var sleep = new TimeSpan(0, 30, 0);

			if (UntilMorning)
			{
				if (NoxicoGame.InGameTime.Hour > 20 || NoxicoGame.InGameTime.Hour < 6)
				{
					for (var i = 0; i < 25; i++)
						for (var j = 0; j < 80; j++)
							NoxicoGame.HostForm.DarkenCell(i, j);
					player.PlayingTime = player.PlayingTime.Add(sleep);
					NoxicoGame.InGameTime.Add(sleep);

					if (NoxicoGame.InGameTime.Hour == 0 && NoxicoGame.InGameTime.Minute < 30)
					{
						//To sleep, perchance, to dream...
						var dlg = Mix.GetXMLDocument("scenesDlg.xml", true);
						var dreams = dlg.SelectNodes("//scene[@name='(dream)']");
						if (dreams.Count > 0)
						{
							var dream = new Character();
							dream.Name = new Name("Dream");
							dream.IsProperNamed = true;
							SceneSystem.Dreaming = true;
							SceneSystem.Engage(pchar, dream, "(dream)", true);
						}
					}
				}
				else
				{
					SceneSystem.Dreaming = false;
					pchar.GetToken("health").Value = max;
					NoxicoGame.Mode = UserMode.Walkabout;
					NoxicoGame.HostForm.Noxico.CurrentBoard.UpdateLightmap(player, true);
					NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
					pchar.RemoveToken("helpless");
				}
				return;
			}

			var now = pchar.GetToken("health").Value;
			if (pchar.GetToken("health").Value < max)
			{
				pchar.GetToken("health").Value += 0.2f;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Update(true);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				for (var i = 0; i < 25; i++)
					for (var j = 0; j < 80; j++)
						NoxicoGame.HostForm.DarkenCell(i, j);

				player.PlayingTime = player.PlayingTime.Add(heal);
				NoxicoGame.InGameTime.Add(heal);
			}
			else
			{
				pchar.GetToken("health").Value = max;
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.UpdateLightmap(player, true);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				pchar.RemoveToken("helpless");
			}
		}

		public static void CreateDungeon()
		{
			Func<Board, Warp> findWarpSpot = (b) =>
			{
				var eX = 0;
				var eY = 0;
				while (true)
				{
					eX = Toolkit.Rand.Next(1, 79);
					eY = Toolkit.Rand.Next(1, 24);

					var sides = 0;
					if (b.IsSolid(eY - 1, eX))
						sides++;
					if (b.IsSolid(eY + 1, eX))
						sides++;
					if (b.IsSolid(eY, eX - 1))
						sides++;
					if (b.IsSolid(eY, eX + 1))
						sides++;
					if (sides < 3 && sides > 1)
						break;
				}
				return new Warp() { XPosition = eX, YPosition = eY };
			};

			if (Subscreens.FirstDraw)
			{
				NoxicoGame.HostForm.LoadBitmap(Mix.GetBitmap("makecave.png"));
				NoxicoGame.HostForm.Write("Generating dungeon. Please wait.", Color.Silver, Color.Transparent, 2, 1);
				Subscreens.FirstDraw = false;
				return;
			}

			var nox = NoxicoGame.HostForm.Noxico;

			var dunGen = new StoneDungeonGenerator();
			var caveGen = new CaveGenerator();

			Warp originalExit = null;

			WorldGen.LoadBiomes();
			var biomeData = WorldGen.Biomes[3]; //TODO: replace 3 with DungeonGeneratorBiome -- this is for testing.

			/* Step 1 - Randomize jagged array, make boards for each entry.
			 * ------------------------------------------------------------
			 * ("goal" board is boss/treasure room, picked at random from bottom floor set.)
			 * [EXIT] [ 01 ] [ 02 ]
			 * [ 03 ] [ 04 ]
			 * [ 05 ] [ 06 ] [ 07 ] [ 08 ]
			 * [ 09 ] [ 10 ] [ 11 ]
			 * [GOAL] [ 13 ]
			*/
			var levels = new List<List<Board>>();
			var depth = Toolkit.Rand.Next(3, 6);
			for (var i = 0; i < depth; i++)
			{
				levels.Add(new List<Board>());
				var length = Toolkit.Rand.Next(2, 5);
				for (var j = 0; j < length; j++)
				{
					var board = new Board();
					board.BoardNum = nox.Boards.Count;
					if (i > 0)
						board.AddToken("dark");
					nox.Boards.Add(board);
					levels[i].Add(board);
				}
			}

			//Decide which boards are the exit and goal
			var entranceBoard = levels[0][Toolkit.Rand.Next(levels[0].Count)];
			var goalBoard = levels[levels.Count - 1][Toolkit.Rand.Next(levels[levels.Count - 1].Count)];

			//Generate content for each board
			for (var i = 0; i < levels.Count; i++)
			{
				for (var j = 0; j < levels[i].Count; j++)
				{
					var board = levels[i][j];

					//TODO: uncomment this decision when the dungeon generator gets pathways.
					if (Toolkit.Rand.NextDouble() > 0.7 || board == entranceBoard)
					{
						caveGen.Board = board;
						caveGen.Create(biomeData);
						caveGen.ToTilemap(ref board.Tilemap);
					}
					else
					{
						dunGen.Board = board;
						dunGen.Create(biomeData);
						dunGen.ToTilemap(ref board.Tilemap);
					}

					board.Name = string.Format("Level {0}-{1}", i + 1, (char)('A' + j));
					board.ID = string.Format("Dng_{0}_{1}{2}", DungeonGeneratorEntranceBoardNum, i + 1, (char)('A' + j));
					board.Music = "set://Dungeon";
					board.Type = BoardType.Dungeon;
					var encounters = board.GetToken("encounters");
					foreach (var e in biomeData.Encounters)
						encounters.Tokens.Add(new Token() { Name = e });
					encounters.Value = biomeData.MaxEncounters;
					board.RespawnEncounters();

					//If this is the entrance board, add a warp back to the Overworld.
					if (board == entranceBoard)
					{
						var exit = findWarpSpot(board);
						originalExit = exit;
						exit.ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_Exit";
						board.Warps.Add(exit);
						board.SetTile(exit.YPosition, exit.XPosition, '<', Color.Silver, Color.Black);
					}
				}
			}

			/* Step 2 - Randomly add up/down links
			 * -----------------------------------
			 * (imagine for the moment that each board can have more than one exit and that this goes for both directions.)
			 * [EXIT] [ 01 ] [ 02 ]
			 *    |
			 * [ 03 ] [ 04 ]
			 * 	         |
			 * [ 05 ] [ 06 ] [ 07 ] [ 08 ]
			 *    |             |
			 * [ 09 ] [ 10 ] [ 11 ]
			 * 	                |
			 * 	      [GOAL] [ 13 ]
			 */
			var connected = new List<Board>();
			for (var i = 0; i < levels.Count; i++)
			{
				var j = Toolkit.Rand.Next(0, levels[i].Count);
				//while (connected.Contains(levels[i][j]))
				//	j = Toolkit.Rand.Next(0, levels[i].Count);

				var up = false;
				var destLevel = i + 1;
				if (destLevel == levels.Count)
				{
					up = true;
					destLevel = i - 1;
				}
				var dest = Toolkit.Rand.Next(0, levels[destLevel].Count);

				var boardHere = levels[i][j];
				var boardThere = levels[destLevel][dest];

				var here = findWarpSpot(boardHere);
				var there = findWarpSpot(boardThere);
				boardHere.Warps.Add(here);
				boardThere.Warps.Add(there);
				here.ID = boardHere.ID + boardHere.Warps.Count;
				there.ID = boardThere.ID + boardThere.Warps.Count;
				here.TargetBoard = boardThere.BoardNum;
				there.TargetBoard = boardHere.BoardNum;
				here.TargetWarpID = there.ID;
				there.TargetWarpID = here.ID;
				boardHere.SetTile(here.YPosition, here.XPosition, up ? '<' : '>', Color.Gray, Color.Black);
				boardThere.SetTile(there.YPosition, there.XPosition, !up ? '<' : '>', Color.Gray, Color.Black);

				Console.WriteLine("Connected {0} || {1}.", boardHere.ID, boardThere.ID);

				connected.Add(boardHere);
				connected.Add(boardThere);
			}

			/* Step 3 - Connect the Unconnected
			 * --------------------------------
			 * [EXIT]=[ 01 ]=[ 02 ]
			 * 	|
			 * [ 03 ]=[ 04 ]
			 *           |
			 * [ 05 ]=[ 06 ] [ 07 ]=[ 08 ]
			 *    |             |
			 * [ 09 ]=[ 10 ]=[ 11 ]
			 *                  |
			 *        [GOAL]=[ 13 ]
			 */

			for (var i = 0; i < levels.Count; i++)
			{
				for (var j = 0; j < levels[i].Count - 1; j++)
				{
					//Don't connect if this board AND the right-hand neighbor are already connected.
					//if (connected.Contains(levels[i][j]) && connected.Contains(levels[i][j + 1]))
					//	continue;

					var boardHere = levels[i][j];
					var boardThere = levels[i][j + 1];

					var here = findWarpSpot(boardHere);
					var there = findWarpSpot(boardThere);
					boardHere.Warps.Add(here);
					boardThere.Warps.Add(there);
					here.ID = boardHere.ID + boardHere.Warps.Count;
					there.ID = boardThere.ID + boardThere.Warps.Count;
					here.TargetBoard = boardThere.BoardNum;
					there.TargetBoard = boardHere.BoardNum;
					here.TargetWarpID = there.ID;
					there.TargetWarpID = here.ID;
					boardHere.SetTile(here.YPosition, here.XPosition, '\x2261', Color.Gray, Color.Black);
					boardThere.SetTile(there.YPosition, there.XPosition, '\x2261', Color.Gray, Color.Black);

					Console.WriteLine("Connected {0} -- {1}.", boardHere.ID, boardThere.ID);

					connected.Add(boardHere);
					connected.Add(boardThere);
				}
			}

			// Step 4 - place sick lewt in goalBoard
			var treasureX = 0;
			var treasureY = 0;
			while (true)
			{
				treasureX = Toolkit.Rand.Next(1, 79);
				treasureY = Toolkit.Rand.Next(1, 24);

				var sides = 0;
				if (goalBoard.IsSolid(treasureY - 1, treasureX))
					sides++;
				if (goalBoard.IsSolid(treasureY + 1, treasureX))
					sides++;
				if (goalBoard.IsSolid(treasureY, treasureX - 1))
					sides++;
				if (goalBoard.IsSolid(treasureY, treasureX + 1))
					sides++;
				if (sides < 3 && sides > 1 && goalBoard.Warps.FirstOrDefault(w => w.XPosition == treasureX && w.YPosition == treasureY) == null)
					break;
			}
			var treasure = InventoryItem.RollContainer(null, "dungeontreasure");
			var treasureChest = new Container("Treasure chest", treasure)
			{
				AsciiChar = (char)0x00C6,
				XPosition = treasureX,
				YPosition = treasureY,
				ForegroundColor = Color.SaddleBrown,
				BackgroundColor = Color.Black,
				ParentBoard = goalBoard,
				Blocking = false,
			};
			goalBoard.Entities.Add(treasureChest);

			var entrance = nox.CurrentBoard.Warps.Find(w => w.ID == DungeonGeneratorEntranceWarpID);
			entrance.TargetBoard = entranceBoard.BoardNum; //should be this one.
			entrance.TargetWarpID = originalExit.ID;
			originalExit.TargetBoard = nox.CurrentBoard.BoardNum;
			originalExit.TargetWarpID = entrance.ID;

			nox.CurrentBoard.EntitiesToRemove.Add(nox.Player);
			nox.CurrentBoard = entranceBoard;
			nox.Player.ParentBoard = entranceBoard;
			entranceBoard.Entities.Add(nox.Player);
			nox.Player.XPosition = originalExit.XPosition;
			nox.Player.YPosition = originalExit.YPosition;
			entranceBoard.Redraw();
			NoxicoGame.Sound.PlayMusic(entranceBoard.Music);
			NoxicoGame.Immediate = true;
			NoxicoGame.Mode = UserMode.Walkabout;
			NoxicoGame.HostForm.Noxico.SaveGame();
		}
	}

}
