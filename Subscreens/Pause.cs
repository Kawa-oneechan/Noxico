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
	public class Pause
	{
		//TODO: Rewrite to use UIManager
		private static int page = 0;
		private static Dictionary<string, string> pages = new Dictionary<string, string>()
		{
			{ "Character stats", "dynamic page" },
			{ "Skill levels", "dynamic page" },
			{ "Important keys",
@"<g2194> <g2195> - Move
l / - Look
i   - Inventory
p , - Pick up
c   - Chat with someone
a   - Aim a shot or throw at someone
f   - Attempt to have sex with someone
<g21B2>   - Use stairs, enter door, use bed
.   - Rest" },
			{ "Other keys",
@"F1  - Open this menu
" +
#if DEBUG
@"F3  - Dump board to HTML (debug only)
" +
#endif
@"F12 - Take screenshot" },
			{ "Credits",
@"Programming and idea              Kawa

Inspiration from:           Tarn Adams
                           Greg Janson
                      The NetHack team

 Check our website for music credits:
   http://helmet.kafuka.org/noxico

Thanks to:     Hammy, Nicole, Seru-kun
            CyclopsCaveman, Mega-Mario
     all #rgrd and RogueBasin coolkids
   all GameDev.StackExchange.com users
 and mom for not making a fuss over it" },
			{ "Memory stats", "..." },
#if DEBUG
			{ "Debug cheats", "..." },
#endif
			{ "Save and exit", "Press Enter to save." },
		};
		private static UIList list;
		private static UILabel text;

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;
			var keys = NoxicoGame.KeyMap;
			var trig = NoxicoGame.KeyTrg;

			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				UIManager.Initialize();
				UIManager.Elements.Add(new UIWindow("PAUSED") { Left = 5, Top = 4, Width = 22, Height = pages.Count + 2, Background = Color.Black, Foreground = Color.Maroon });
				UIManager.Elements.Add(new UIWindow("") { Left = 28, Top = 4, Width = 42, Height = 16, Background = Color.Black, Foreground = Color.Blue });
				list = new UIList() { Background = Color.Black, Foreground = Color.Silver, Width = 20, Height = pages.Count, Left = 6, Top = 5 };
				list.Items.AddRange(pages.Keys);
				list.Change += (s, e) =>
				{
					page = list.Index;
					text.Text = pages.Values.ElementAt(page);
					UIManager.Draw();
				};
				list.Enter += (s, e) =>
				{
					if (list.Index == list.Items.Count - 1) //can't use absolute index because Debug might be missing.
					{
						host.Noxico.SaveGame();
						host.Close();
					}
				};
				text = new UILabel("...") { Background = Color.Black, Foreground = Color.Silver, Left = 30, Top = 5 };
				UIManager.Elements.Add(list);
				UIManager.Elements.Add(text);
				list.Index = IniFile.GetBool("misc", "rememberpause", true) ? page : 0;
				UIManager.Highlight = list;

				//Toolkit.DrawWindow(5, 4, 21, 1 + pages.Count, "PAUSED", Color.Maroon, Color.Black, Color.Red);
				Subscreens.Redraw = true;
				keys[(int)Keys.Escape] = false;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				UIManager.Draw();

				//Toolkit.DrawWindow(28, 4, 41, 16, null, Color.Blue, Color.Black, Color.Blue);
				//var titles = pages.Keys.ToArray();
				//for (var i = 0; i < pages.Count; i++)
				//	host.Write((' ' + titles[i]).PadRight(20), i == page ? Color.Black : Color.Silver, i == page ? Color.Silver : Color.Black, 6, 5 + i);
				//var text = pages.Values.ElementAt(page).Split('\n');
				//for (var i = 0; i < text.Length; i++)
				//	host.Write(text[i], Color.Silver, Color.Black, 30, 5 + i);
			}

			if (keys[(int)Keys.Escape])
			{
				NoxicoGame.Immediate = true;
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else
			{
				UIManager.CheckKeys();
			}

		}

		private static int CountTokens(Token t)
		{
			var r = t.Tokens.Count;
			if (r > 0)
				foreach (var t2 in t.Tokens)
					r += CountTokens(t2);
			return r;
		}

		public static void Open()
		{
			var host = NoxicoGame.HostForm;
			var nox = host.Noxico;
			var player = nox.Player.Character;

			var sb = new StringBuilder();
			sb.AppendLine("Name                " + player.Name);
			sb.AppendLine("Health              " + player.GetToken("health").Value + " / " + player.GetMaximumHealth());
			sb.AppendLine("Money               " + player.GetToken("money").Value + " Z");
			sb.AppendLine("Play time           " + "lol");
			sb.AppendLine("Charisma            " + player.GetToken("charisma").Value);
			sb.AppendLine("Climax              " + player.GetToken("climax").Value);
			sb.AppendLine("Cunning             " + player.GetToken("cunning").Value);
			sb.AppendLine("Carnality           " + player.GetToken("carnality").Value);
			sb.AppendLine("Stimulation         " + player.GetToken("stimulation").Value);
			sb.AppendLine("Sensitivity         " + player.GetToken("sensitivity").Value);
			sb.AppendLine("Speed               " + player.GetToken("speed").Value);
			sb.AppendLine("Strength            " + player.GetToken("strength").Value);
			pages["Character stats"] = sb.ToString();

			sb.Clear();
			foreach (var skill in player.GetToken("skills").Tokens)
				if ((int)skill.Value > 0)
					sb.AppendLine(skill.Name.Replace('_', ' ').Titlecase().PadRight(20) + ((int)skill.Value).ToString());
			pages["Skill levels"] = sb.ToString();

			var entities = 0;
			var tokens = 0;
			foreach (var x in nox.Boards.Where(x => x != null))
			{
				if (x == null)
					continue;
				entities += x.Entities.Count;
				foreach (var c in x.Entities.OfType<BoardChar>())
				{
					tokens += c.Character.Tokens.Count;
					foreach (var t in c.Character.Tokens)
						tokens += CountTokens(t);
				}
			};
			NoxicoGame.KnownItems.ForEach(x =>
			{
				tokens += x.Tokens.Count;
				foreach (var t in x.Tokens)
					tokens += CountTokens(t);
			});
			sb.Clear();
			sb.AppendLine("Number of boards            " + nox.Boards.Count.ToString("G"));
			sb.AppendLine("   Active boards            " + nox.Boards.Where(b => b != null).Count().ToString("G"));
			sb.AppendLine("Number of known items       " + NoxicoGame.KnownItems.Count.ToString("G"));
			sb.AppendLine("Number of entities          " + entities.ToString("G"));
			sb.AppendLine("Total set of tokens         " + tokens.ToString("G"));
			pages["Memory stats"] = sb.ToString();

			if (!IniFile.GetBool("misc", "rememberpause", true))
				page = 0;

			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.ClearKeys();
		}
	}

}
