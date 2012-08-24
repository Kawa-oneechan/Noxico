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

		public static void SleepAWhile()
		{
			var player = NoxicoGame.HostForm.Noxico.Player.Character;

			var max = player.GetMaximumHealth();
			var now = player.GetToken("health").Value;
			if (player.GetToken("health").Value < max)
			{
				player.GetToken("health").Value += 0.2f;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Update(true);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				for (var i = 0; i < 25; i++)
					for (var j = 0; j < 80; j++)
						NoxicoGame.HostForm.DarkenCell(i, j);
				return;
			}
			else
			{
				player.GetToken("health").Value = max;
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				player.RemoveToken("helpless");
			}
		}

		public static void CreateDungeon()
		{
			if (Subscreens.FirstDraw)
			{
				NoxicoGame.HostForm.LoadBitmap(Toolkit.ResOrFile(global::Noxico.Properties.Resources.MakeCave, "makecave.png"));
				NoxicoGame.HostForm.Write("Generating dungeon. Please wait.", Color.Silver, Color.Transparent, 2, 1);
				Subscreens.FirstDraw = false;
				return;
			}

			var nox = NoxicoGame.HostForm.Noxico;

			var dunGen = new StoneDungeonGenerator();
			var caveGen = new CaveGenerator();
			
			//First, create the entrance cavern.
			WorldGen.LoadBiomes();
			var biomeData = WorldGen.Biomes[3]; //TODO: replace 3 with DungeonGeneratorBiome -- this is for testing.
			var newBoard = new Board();
			caveGen.Create(biomeData);
			caveGen.ToTilemap(ref newBoard.Tilemap);
			newBoard.Name = "Dungeon Entrance";
			newBoard.ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_E";
			newBoard.Music = "set://Dungeon";
			newBoard.Type = BoardType.Dungeon;
			//newBoard.Tokens = Token.Tokenize("name: \"" + newBoard.Name + "\"\nid: \"" + newBoard.ID + "\"\nmusic: \"" + newBoard.Music + "\"\ntype: 2\nbiome: " + DungeonGeneratorBiome + "\nencounters: " + biomeData.MaxEncounters + "\n");
			var encounters = newBoard.GetToken("encounters");
			foreach (var e in biomeData.Encounters)
				encounters.Tokens.Add(new Token() { Name = e });
			encounters.Value = biomeData.MaxEncounters;
			newBoard.RespawnEncounters();

			//Find a good spot for the cave exit.
			var okay = false;
			var eX = 0;
			var eY = 0;
			while (!okay)
			{
				eX = Toolkit.Rand.Next(1, 79);
				eY = Toolkit.Rand.Next(1, 24);

				var sides = 0;
				if (newBoard.IsSolid(eY - 1, eX))
					sides++;
				if (newBoard.IsSolid(eY + 1, eX))
					sides++;
				if (newBoard.IsSolid(eY, eX - 1))
					sides++;
				if (newBoard.IsSolid(eY, eX + 1))
					sides++;
				if (sides < 3 && sides > 1)
					okay = true;
			}

			var exit = new Warp()
			{
				XPosition = eX,
				YPosition = eY,
				ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_Exit",
			};
			newBoard.Warps.Add(exit);
			newBoard.SetTile(eY, eX, '<', Color.Silver, Color.Black);

			//Slot in the new board
			newBoard.BoardNum = nox.Boards.Count;
			nox.Boards.Add(newBoard);

			//Now hook the two up.
			var entrance = nox.CurrentBoard.Warps.Find(w => w.ID == DungeonGeneratorEntranceWarpID);
			entrance.TargetBoard = newBoard.BoardNum; //should be this one.
			entrance.TargetWarpID = exit.ID;
			exit.TargetBoard = nox.CurrentBoard.BoardNum;
			exit.TargetWarpID = entrance.ID;

			var originalExit = exit;
			var entranceBoard = newBoard;
			
			var excavateFrom = entranceBoard;
			var depth = 0;
			var deepest = 0;
			Board deepestBoard = null;
			var amount = 0;
			Excavate(entranceBoard, biomeData, ref amount, depth, ref deepest, ref deepestBoard);
			//TODO: Plant unique treasure in deepestBoard.

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

		private static void Excavate(Board excavateFrom, BiomeData biomeData, ref int amount, int depth, ref int deepest, ref Board deepestBoard)
		{
			amount++;
			var nox = NoxicoGame.HostForm.Noxico;
			var excavateBoard = new Board();
			var caveGen = new CaveGenerator();
			caveGen.Create(biomeData);
			caveGen.ToTilemap(ref excavateBoard.Tilemap);
			excavateBoard.Name = "Dungeon level " + (depth + 1).ToString();
			excavateBoard.ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_" + amount.ToString();
			excavateBoard.Music = "set://Dungeon";
			excavateBoard.Type = BoardType.Dungeon;

			if (depth > deepest)
			{
				deepest = depth;
				deepestBoard = excavateBoard;
			} 

			//excavateBoard.Tokens = Token.Tokenize("name: \"" + excavateBoard.Name + "\"\nid: \"" + excavateBoard.ID + "\"\nmusic: \"" + excavateBoard.Music + "\"\ntype: 2\nbiome: " + DungeonGeneratorBiome + "\nencounters: " + biomeData.MaxEncounters + "\n");
			var encounters = excavateBoard.GetToken("encounters");
			foreach (var e in biomeData.Encounters)
				encounters.Tokens.Add(new Token() { Name = e });
			encounters.Value = biomeData.MaxEncounters;
			excavateBoard.RespawnEncounters();

			var numExits = Toolkit.Rand.Next(1, 3);
			for (var i = 0; i < numExits; i++)
			{
				//Decide on a connection type
				var isSameLevel = Toolkit.Rand.NextDouble() > 0.7;
				var isUpward = Toolkit.Rand.NextDouble() > 0.5;
				if (depth == 0 && isUpward)
					isUpward = false;

				//Find a good spot for the cave exit.
				var okay = false;
				var eX = 0;
				var eY = 0;
				while (!okay)
				{
					eX = Toolkit.Rand.Next(1, 79);
					eY = Toolkit.Rand.Next(1, 24);

					var sides = 0;
					if (excavateBoard.IsSolid(eY - 1, eX))
						sides++;
					if (excavateBoard.IsSolid(eY + 1, eX))
						sides++;
					if (excavateBoard.IsSolid(eY, eX - 1))
						sides++;
					if (excavateBoard.IsSolid(eY, eX + 1))
						sides++;
					if (sides < 3 && sides > 1)
						okay = true;
				}

				var exit = new Warp()
				{
					XPosition = eX,
					YPosition = eY,
					ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_Exit" + i.ToString(),
				};
				excavateBoard.Warps.Add(exit);
				var exitChar = isSameLevel ? '\x2261' : isUpward ? '>' : '<';
				excavateBoard.SetTile(eY, eX, exitChar, Color.Silver, Color.Black);

				//Slot in the new board
				excavateBoard.BoardNum = nox.Boards.Count;
				nox.Boards.Add(excavateBoard);

				//Find a spot for the exit on excavateFrom
				okay = false;
				eX = 0;
				eY = 0;
				while (!okay)
				{
					eX = Toolkit.Rand.Next(1, 79);
					eY = Toolkit.Rand.Next(1, 24);

					var sides = 0;
					if (excavateFrom.IsSolid(eY - 1, eX))
						sides++;
					if (excavateFrom.IsSolid(eY + 1, eX))
						sides++;
					if (excavateFrom.IsSolid(eY, eX - 1))
						sides++;
					if (excavateFrom.IsSolid(eY, eX + 1))
						sides++;
					if (sides < 3 && sides > 1)
						okay = true;
				}

				var entrance = new Warp()
				{
					XPosition = eX,
					YPosition = eY,
					ID = "Dng_" + DungeonGeneratorEntranceBoardNum + "_Entrance" + i.ToString(),
				};
				excavateFrom.Warps.Add(entrance);
				exitChar = isSameLevel ? '\x2261' : isUpward ? '<' : '>';
				excavateFrom.SetTile(eY, eX, exitChar, Color.Silver, Color.Black);

				//Connect excavateBoard to excavateFrom
				entrance.TargetBoard = excavateBoard.BoardNum; //should be this one.
				entrance.TargetWarpID = exit.ID;
				exit.TargetBoard = excavateFrom.BoardNum;
				exit.TargetWarpID = entrance.ID;

				if (amount < 30 && depth < 16)
					Excavate(excavateBoard, biomeData, ref amount, depth + (isSameLevel ? 0 : isUpward ? -1 : 1), ref deepest, ref deepestBoard);
			}

			var corner = (depth + 1).ToString();
			for (var i = 0; i < corner.Length; i++)
				excavateBoard.SetTile(0, i, corner[i], Color.Silver, Color.Black, true);
		}
	}

}
