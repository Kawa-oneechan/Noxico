using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Noxico
{
	class SceneSystem
	{
		private static List<Token> sceneList;
		private static Character top, bottom;
		private static Character[] actors;

		private static bool letBottomChoose;

		public static void Engage(Character top, Character bottom)
		{
			Engage(top, bottom, "(start)");
		}

		public static void Engage(Character top, Character bottom, string name = "(start)")
		{
			if (sceneList == null)
				sceneList = Mix.GetTokenTree("dialogue.tml", true);

			SceneSystem.top = top;
			SceneSystem.bottom = bottom;
			SceneSystem.actors = new[] { top, bottom };
			var dreaming = top.HasToken("dream");

			if (name.Contains('\xE064'))
				name = name.Remove(name.LastIndexOf('\xE064'));

			var openings = sceneList.Where(x => x.Name == "scene" && x.GetToken("name").Text == name).ToList();
			if (openings.Count == 0)
			{
				MessageBox.Notice(string.Format("Could not find a proper opening for scene name \"{0}\". Aborting.", name), true, "Uh-oh.");
				return;
			}
			var firstScene = openings.FirstOrDefault(i => SexManager.LimitsOkay(actors, i));
			var scenes = new List<Token>() { firstScene };
			if (firstScene.HasToken("random"))
			{
				var randomKey = firstScene.GetToken("random").Text;
				foreach (var s in openings.Where(i => i != firstScene && i.HasToken("random") && i.GetToken("random").Text == randomKey && SexManager.LimitsOkay(actors, i)))
					scenes.Add(s);
			}
			var scene = scenes.PickOne();

			var message = i18n.Viewpoint(ExtractParagraphsAndScripts(scene), SceneSystem.top, SceneSystem.bottom);
			var actions = ExtractActions(scene);

			if (actions.Count == 1)
			{
				var target = actions.First().Key;
				actions.Clear();
				actions.Add(target, "==>");
			}

			if (bottom == NoxicoGame.Me.Player.Character && !letBottomChoose)
			{
				if (actions.Count == 0)
				{
					MessageBox.Notice(message, true, bottom.Name.ToString(true));
				}
				else
				{
					var randomAction = actions.Keys.ToArray().PickOne();
					actions.Clear();
					actions.Add(randomAction, "==>");
					MessageBox.List(message, actions, () => { Engage(SceneSystem.top, SceneSystem.bottom, (string)MessageBox.Answer); }, false, true, bottom.GetKnownName(true, true));
				}
			}
			else
			{
				letBottomChoose = false;
				if (actions.Count == 0)
					MessageBox.Notice(message, !dreaming, bottom.GetKnownName(true, true));
				else
					MessageBox.List(message, actions, () => { Engage(SceneSystem.top, SceneSystem.bottom, (string)MessageBox.Answer); }, false, !dreaming, bottom.GetKnownName(true, true));
			}

			if (dreaming)
			{
				new UIPNGBackground(Mix.GetBitmap("dream.png")).Draw();
			}
			else
			{
				//NoxicoGame.Me.CurrentBoard.Redraw();
				//NoxicoGame.Me.CurrentBoard.Draw();
			}
		}

		private static Dictionary<object, string> ExtractActions(Token scene)
		{
			var ret = new Dictionary<object, string>();
			foreach (var action in scene.Tokens.Where(x => x.Name == "action"))
			{
				foreach (var s in sceneList.Where(x => x.Name == "scene" && x.GetToken("name").Text == action.Text && SexManager.LimitsOkay(actors, x)))
				{
					var key = action.Text;
					var listAs = s.HasToken("list") ? s.GetToken("list").Text : string.Format("[missing \"list\"!] {0}", key);
					if (action.HasToken("listas"))
					{
						key = string.Format("{0}\xE064{1}", s.GetToken("name").Text, ret.Count);
						listAs = action.GetToken("listas").Text;
					}
					if (listAs.Contains('['))
						listAs = i18n.Viewpoint(listAs, top, bottom);
					if (!ret.ContainsKey(key))
						ret.Add(key, listAs);
				}
			}
			return ret;
		}

		private static string ExtractParagraphsAndScripts(Token scene)
		{
			var ret = new StringBuilder();
			foreach (var part in scene.Tokens.Where(x => x.Name == "$" || x.Name == "#a" || x.Name == "script"))
			{
				if (part.Name == "$" || part.Name == "#a")
				{
					if (part.HasToken("#text"))
						ret.AppendLine(part.GetToken("#text").Text.SmartQuote());
					else
						ret.AppendLine(part.Text.SmartQuote());
					ret.AppendLine();
				}
				else
				{
					var buffer = new StringBuilder();
					var env = Lua.Environment;
					Lua.Ascertain();
					env.top = top;
					env.bottom = bottom;
					env.print = new Action<string>(x => buffer.Append(x));
					env.LetBottomChoose = new Action<string>(x => letBottomChoose = true);
					env.thisBoard = bottom.BoardChar.ParentBoard;
					env.thisRealm = bottom.BoardChar.ParentBoard.Realm;
					Lua.Run(part.Tokens[0].Text, env);
					ret.AppendLine(buffer.ToString());
					ret.AppendLine();
				}
			}
			return ret.ToString().TrimEnd();
		}
	}

	partial class Character
	{
		public bool HasRelation(Character with)
		{
			return (this.Path("ships/" + with.ID) != null);
		}

		public bool HasRole(string role)
		{
			var t = this.Path("role/vendor/class");
			return (t != null && t.Text == role);
		}
	}
}
