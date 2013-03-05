using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public class Pause
	{
		private static int page = 0;
		private static Dictionary<string, string> pages = new Dictionary<string, string>()
		{
			{ "Character stats", "..." },
			{ "Skill levels", "..." },
			{ "Important keys", "..." },
			{ "Other keys", "..." },
			{ "Credits", "Press Enter to view full credits." },
			{ "Memory stats", "..." },
#if DEBUG
			{ "Debug cheats", "..." },
#endif
			{ "Open settings",
@"Press Enter to open noxico.ini.

From there, you can change a bunch of
things, including key mappings and how
long you get until auto-rest triggers." },
			{ "Save and exit",
@"Press Enter to save.

The game will automatically exit when
done. Note that clicking the Close
button or pressing Alt-F4 or whatever
your operating system's methods are
has the same effect." },

		};
		private static UIList list;
		private static UILabel text;

		public static void Handler()
		{
			var host = NoxicoGame.HostForm;

			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				UIManager.Initialize();
				UIManager.Elements.Add(new UIWindow("PAUSED") { Left = 5, Top = 4, Width = 22, Height = pages.Count + 2 });
				UIManager.Elements.Add(new UIWindow("") { Left = 28, Top = 2, Width = 44, Height = 18 });
				list = new UIList() { Width = 20, Height = pages.Count, Left = 6, Top = 5 };
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
						host.Close();
					}
					else if (list.Index == list.Items.Count - 2) //same
					{
						System.Diagnostics.Process.Start(host.IniPath);
					}
					else if (list.Index == 4)
					{
						TextScroller.Plain(Mix.GetString("credits.txt"), "Credits", false);
					}
				};
				text = new UILabel("...") { Left = 30, Top = 3 };
				UIManager.Elements.Add(list);
				UIManager.Elements.Add(text);
				list.Index = IniFile.GetValue("misc", "rememberpause", true) ? page : 0;
				UIManager.Highlight = list;

				//Toolkit.DrawWindow(5, 4, 21, 1 + pages.Count, "PAUSED", Color.Maroon, Color.Black, Color.Red);
				Subscreens.Redraw = true;
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

			var hpNow = player.GetToken("health").Value;
			var hpMax = player.GetMaximumHealth();
			var health = hpNow + " / " + hpMax;
			if (hpNow <= hpMax / 4)
				health = "<cRed>" + health + "<cSilver>";
			else if (hpNow <= hpMax / 2)
				health = "<cYellow>" + health + "<cSilver>";

			var sb = new StringBuilder();
			sb.AppendLine("Name                " + player.Name);
			sb.AppendLine("Health              " + health);
			sb.AppendLine("Money               " + player.GetToken("money").Value.ToString("C"));
			sb.AppendLine("Play time           " + nox.Player.PlayingTime.ToString());
			sb.AppendLine("World time          " + NoxicoGame.InGameTime.ToString());

			var statNames = Enum.GetNames(typeof(Stat));
			player.RecalculateStatBonuses();
			player.CheckHasteSlow();
			foreach (var stat in statNames)
			{
				if (stat == "Health")
					continue;
				var bonus = "";
				var statBonus = player.GetToken(stat.ToLowerInvariant() + "bonus").Value;
				var statBase = player.GetToken(stat.ToLowerInvariant()).Value;
				var total = statBase + statBonus;
				if (statBonus > 0)
					bonus = "<cGray> (" + statBase + "+" + statBonus + ")<cSilver>";
				else if (statBonus < 0)
					bonus = "<cMaroon> (" + statBase + "-" + (-statBonus) + ")<cSilver>";
				sb.AppendLine(stat.PadRight(20) + total + bonus);
			}

			sb.Append("Modifiers".PadRight(20));
			var haveMods = false;
			if (player.HasToken("haste"))
			{
				haveMods = true;
				sb.Append("Haste ");
			}
			if (player.HasToken("slow"))
			{
				haveMods = true;
				sb.Append("Slow  ");
			}
			if (!haveMods)
				sb.Append("<cGray>None<cSilver>");
			sb.AppendLine();

			var paragadeLength = 18;
			var renegadeLight = (int)Math.Ceiling((player.GetToken("renegade").Value / 100) * paragadeLength);
			var paragonLight = (int)Math.Ceiling((player.GetToken("paragon").Value / 100) * paragadeLength);
			var renegadeDark = 18 - renegadeLight;
			var paragonDark = 18 - paragonLight;
			sb.Append("\u2660 ");
			sb.Append("<cMaroon>" + new string('-', renegadeDark) + "<cRed>" + new string('=', renegadeLight));
			sb.Append("<cBlue>" + new string('=', paragonLight) + "<cNavy>" + new string('-', paragonDark));
			sb.AppendLine(" <cSilver>\u2665");
	
			pages["Character stats"] = sb.ToString();

			sb.Clear();
			foreach (var skill in player.GetToken("skills").Tokens)
			{
				if ((int)skill.Value > 0)
					sb.AppendLine(skill.Name.Replace('_', ' ').Titlecase().PadRight(30) + ((int)skill.Value).ToString());
			}
			pages["Skill levels"] = sb.ToString();

			sb.Clear();
			for (var i = 0; i < 4; i++)
				sb.Append(Toolkit.TranslateKey((KeyBinding)i));
			sb.AppendLine(" - Move");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Interact).PadRight(4) + " - Interact with something");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Activate).PadRight(4) + " - Use something");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Rest).PadRight(4) + " - Rest");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Fly).PadRight(4) + " - Fly/Land");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Items).PadRight(4) + " - Inventory");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Travel).PadRight(4) + " - Travel");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Accept).PadRight(4) + " - Accept");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Back).PadRight(4) + " - Go back");
			pages["Important keys"] = sb.ToString();
			sb.Clear();
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Pause).PadRight(4) + " - Open this menu");
#if DEBUG
			sb.AppendLine(Toolkit.TranslateKey(System.Windows.Forms.Keys.F3).PadRight(4) + " - Dump board to HTML (debug only)");
#endif
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Screenshot).PadRight(4) + " - Take screenshot");
			pages["Other keys"] = sb.ToString();

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

			if (!IniFile.GetValue("misc", "rememberpause", true))
				page = 0;

			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.ClearKeys();
		}
	}

}
