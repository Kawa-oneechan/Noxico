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
			{ i18n.GetString("pause_charstats"), "..." },
			{ i18n.GetString("pause_skills"), "..." },
			{ i18n.GetString("pause_keys1"), "..." },
			{ i18n.GetString("pause_keys2"), "..." },
			{ i18n.GetString("pause_credits"), i18n.GetString("pause_creditscontent") },
			{ i18n.GetString("pause_memstats"), "..." },
#if DEBUG
			{ "Debug cheats", "..." },
#endif
			{ i18n.GetString("pause_opensettings"), i18n.GetString("pause_opensettingscontent") },
			{ i18n.GetString("pause_saveandexit"), i18n.GetString("pause_saveandexitcontent") },

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
				UIManager.Elements.Add(new UIWindow(i18n.GetString("pause_title")) { Left = 5, Top = 4, Width = 22, Height = pages.Count + 2 });
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
						//System.Diagnostics.Process.Start(host.IniPath);
						Options.Open();
					}
					else if (list.Index == 4)
					{
						TextScroller.Plain(Mix.GetString("credits.txt"), i18n.GetString("pause_credits"), false);
					}
				};
				text = new UILabel("...") { Left = 30, Top = 3 };
				UIManager.Elements.Add(list);
				UIManager.Elements.Add(text);
				list.Index = IniFile.GetValue("misc", "rememberpause", true) ? page : 0;
				UIManager.Highlight = list;

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
			var hpMax = player.MaximumHealth;
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
				sb.AppendLine(stat.PadEffective(20) + total + bonus);
			}

			sb.Append("Modifiers".PadEffective(20));
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

			pages[i18n.GetString("pause_charstats")] = sb.ToString();

			sb.Clear();
			foreach (var skill in player.GetToken("skills").Tokens)
			{
				if ((int)skill.Value > 0)
					sb.AppendLine(skill.Name.Replace('_', ' ').Titlecase().PadEffective(30) + ((int)skill.Value).ToString());
			}
			pages[i18n.GetString("pause_skills")] = sb.ToString();

			sb.Clear();
			for (var i = 0; i < 4; i++)
				sb.Append(Toolkit.TranslateKey((KeyBinding)i));
			sb.AppendLine("     - Move");
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Interact).PadEffective(8) + " - " + i18n.GetString("pause_keyinteract"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Activate).PadEffective(8) + " - " + i18n.GetString("pause_keyactivate"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Rest).PadEffective(8) + " - " + i18n.GetString("pause_keyrest"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Fly).PadEffective(8) + " - " + i18n.GetString("pause_keyfly"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Items).PadEffective(8) + " - " + i18n.GetString("pause_keyitems"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Travel).PadEffective(8) + " - " + i18n.GetString("pause_keytravel"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Accept).PadEffective(8) + " - " + i18n.GetString("pause_keyaccept"));
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Back).PadEffective(8) + " - " + i18n.GetString("pause_keyback"));
			pages[i18n.GetString("pause_keys1")] = sb.ToString();
			sb.Clear();
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Pause).PadEffective(4) + " - " + i18n.GetString("pause_keypause"));
#if DEBUG
			sb.AppendLine(Toolkit.TranslateKey(System.Windows.Forms.Keys.F3).PadEffective(4) + " - Dump board to HTML (debug only)");
#endif
			sb.AppendLine(Toolkit.TranslateKey(KeyBinding.Screenshot).PadEffective(4) + " - " + i18n.GetString("pause_keyscreenshot"));
			pages[i18n.GetString("pause_keys2")] = sb.ToString();
	
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

			pages[i18n.GetString("pause_memstats")] = i18n.Format("pause_memstatscontent",
				nox.Boards.Count.ToString("G"),
				nox.Boards.Where(b => b != null).Count().ToString("G"),
				NoxicoGame.KnownItems.Count.ToString("G"),
				entities.ToString("G"),
				tokens.ToString("G"));

			if (!IniFile.GetValue("misc", "rememberpause", true))
				page = 0;

			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.ClearKeys();
		}
	}

}
