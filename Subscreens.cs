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
	public static class Subscreens
	{
		public static Stack<SubscreenFunc> PreviousScreen = new Stack<SubscreenFunc>();

		public static bool FirstDraw = true;
		public static bool Redraw = true;

		public static bool UsingMouse = false;
		public static bool Mouse = false;
		public static int MouseX = -1;
		public static int MouseY = -1;
	}

	public static class UnsortedSubscreens
	{
		//TODO: Refactor the below

		public static bool UntilMorning;
		public static void Sleep()
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
					pchar.GetToken("climax").Value = 0;
					pchar.GetToken("stimulation").Value = 0;
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
				pchar.GetToken("climax").Value = 0;
				pchar.GetToken("stimulation").Value = 0;
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.HostForm.Noxico.CurrentBoard.UpdateLightmap(player, true);
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				pchar.RemoveToken("helpless");
			}
		}

	}

}
