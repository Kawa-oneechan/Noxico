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
			var dreaming = top.HasToken("dream");

			if (name.Contains('\xE064'))
				name = name.Remove(name.LastIndexOf('\xE064'));

			var openings = sceneList.Where(x => x.Name == "scene" && x.GetToken("name").Text == name).ToList();
			if (openings.Count == 0)
			{
				MessageBox.Notice("Could not find a proper opening for scene name \"" + name + "\". Aborting.", true, "Uh-oh.");
				return;
			}
			var firstScene = openings.FirstOrDefault(i => SceneFiltersOkay(i));
			var scenes = new List<Token>() { firstScene };
			if (firstScene.HasToken("random"))
			{
				var randomKey = firstScene.GetToken("random").Text;
				foreach (var s in openings.Where(i => i != firstScene && i.HasToken("random") && i.GetToken("random").Text == randomKey && SceneFiltersOkay(i)))
					scenes.Add(s);
			}
			var scene = scenes[Random.Next(scenes.Count)];

			var message = i18n.Viewpoint(ExtractParagraphsAndScripts(scene), SceneSystem.top, SceneSystem.bottom);
			var actions = ExtractActions(scene);

			if (actions.Count == 1)
			{
				var target = actions.First().Key;
				actions.Clear();
				actions.Add(target, "==>");
			}

			if (bottom == NoxicoGame.HostForm.Noxico.Player.Character && !letBottomChoose)
			{
				if (actions.Count == 0)
				{
					MessageBox.Notice(message, true, bottom.Name.ToString(true));
				}
				else
				{
					var randomAction = actions.Keys.ToArray()[Random.Next(actions.Count)];
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
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
			}
		}

		private static Dictionary<object, string> ExtractActions(Token scene)
		{
			var ret = new Dictionary<object, string>();
			foreach (var action in scene.Tokens.Where(x => x.Name == "action"))
			{
				foreach (var s in sceneList.Where(x => x.Name == "scene" && x.GetToken("name").Text == action.Text && SceneFiltersOkay(x)))
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
					env.SetValue("top", top);
					env.SetValue("bottom", bottom);
					env.SetValue("print", new Action<string>(x => buffer.Append(x)));
					env.SetValue("LetBottomChoose", new Action<string>(x => letBottomChoose = true));
					env.SetValue("GetBoard", new Func<int, Board>(x => NoxicoGame.HostForm.Noxico.GetBoard(x)));
					//js.SetFunction("ExpectTown", new Func<string, int, Expectation>(Expectation.ExpectTown));
					//js.SetParameter("Expectations", NoxicoGame.Expectations);
					//js.SetFunction("LearnUnknownLocation", new Action<string>(NoxicoGame.LearnUnknownLocation));
					//env.DoChunk(part.Tokens[0].Text, "lol.lua");
					Lua.Run(part.Tokens[0].Text, env);
					ret.AppendLine(buffer.ToString());
					ret.AppendLine();
				}
			}
			return ret.ToString().TrimEnd();
		}

		/*
		private static void ParseParagraph(StringBuilder ret, XmlElement part)
		{
			var trimmers = new[] {'\t', '\n', '\r'};
			var hadFalseIf = false;
			foreach (var node in part.ChildNodes)
			{
				if (node is XmlText)
				{
					var text = (node as XmlText).Value;
					if (trimmers.Contains(text[0]))
						text = text.TrimStart();
					if (trimmers.Contains(text[text.Length - 1]))
						text = text.TrimEnd();
					ret.Append(text.SmartQuote());
				}
				else if (node is XmlElement)
				{
					var element = node as XmlElement;
					if (element.Name == "if")
					{
						if (FiltersOkay(element))
							ParseParagraph(ret, element);
						else
							hadFalseIf = true;
					}
					else if (element.Name == "else" && hadFalseIf)
					{
						ParseParagraph(ret, element);
						hadFalseIf = false;
					}
				}
			}
			//ret.AppendLine(part.InnerText.Trim());
			//ret.AppendLine();
		}
		*/

		private static bool SceneFiltersOkay(Token scene)
		{
			if (!scene.HasToken("filters"))
				return true;
			foreach (var filter in scene.GetToken("filters").Tokens)
				if (!FiltersOkay(filter))
					return false;
			return true;
		}

		//TODO: rewrite to use JS
		private static bool FiltersOkay(Token filter)
		{
			if (filter.Name == "debug")
			{
#if DEBUG
				return true; //Allow this scene in debug builds...
#else
				return false; //...but not in release builds.
#endif
			}
			var fPrimary = filter.Name == "top" ? top : bottom;
			var fSecondary = filter.Name == "top" ? bottom : top;
			var parts = string.IsNullOrWhiteSpace(filter.Text) ? new string[0] : filter.Text.SplitQ();
			var fType = parts[0];
			switch (fType)
			{
				case "name": //bottom: name "Joe Random"
					if (!fPrimary.Name.ToString(true).Trim().Equals(parts[1], StringComparison.OrdinalIgnoreCase))
						return false;
					break;
				case "male":
					if (fPrimary.Gender != Gender.Male)
						return false;
					break;
				case "female":
					if (fPrimary.Gender != Gender.Female)
						return false;
					break;
				case "bodyhash":
					var hash = Toolkit.GetBodyComparisonHash(fPrimary);
					var distance = Toolkit.GetHammingDistance(hash, NoxicoGame.BodyplanHashes[parts[1]]);
					if (distance > 0) //?
						return false;
					break;
				case "hasdildo":
					if (!fPrimary.HasToken("items"))
						return false;
					var hasDildo = false;
					var minArea = parts.Length > 1 ? float.Parse(parts[1]) : 0;
					foreach (var item in fPrimary.GetToken("items").Tokens)
					{
						var knownItem = NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID == item.Name);
						if (knownItem == null)
							continue;
						if (knownItem.HasToken("canfuck"))
						{
							if (minArea > 0)
							{
								var surface = knownItem.GetToken("thickness").Value * knownItem.GetToken("length").Value;
								if (surface < minArea)
									continue;
							}
							hasDildo = true;
							break;
						}
					}
					if (!hasDildo)
						return false;
					break;
				case "canfitdickinpussy":
					var dickSizes = fPrimary.GetPenisSizes(true);
					var pussySizes = fSecondary.GetVaginaCapacities();
					if (dickSizes.Length == 0 || pussySizes.Length == 0)
						return false; //no dicks to fit, or no pussies to fit in.
					var canFit = false;
					foreach (var dick in dickSizes)
					{
						foreach (var pussy in pussySizes)
						{
							if (dick < pussy)
							{
								canFit = true;
								break;
							}
						}
					}
					if (!canFit)
						return false;
					break;
				case "canfitdickinmouth":
					dickSizes = fPrimary.GetPenisSizes(true);
					if (dickSizes.Length == 0)
						return false; //no dicks to fit.
					canFit = false;
					foreach (var dick in dickSizes)
					{
						if (dick < 40)
						{
							canFit = true;
							break;
						}
					}
					if (!canFit)
						return false;
					break;
				case "isfather":
					var pregnancy = fSecondary.Path("pregnancy");
					if (pregnancy == null)
						return false;
					var father = fSecondary.Path("pregnancy/father");
					if (father == null)
						return false;
					if (father.Text != fPrimary.ID)
						return false;
					break;
				case "relation":
					var ship = parts[1];
					if (ship != "none")
					{
						if (fPrimary.Path("ships/" + fSecondary.ID + "/" + ship) == null)
							return false;
					}
					else
					{
						if (fPrimary.Path("ships/" + fSecondary.ID) != null)
							return false;
					}
					break;
				case "has": //bottom: has cooties
					var path = fPrimary.Path(parts[1]);
					if (path == null)
						return false;
					break;
				case "hasnot": //bottom: hasnot cooties
					path = fPrimary.Path(parts[1]);
					if (path != null)
						return false;
					break;
				case "text": //bottom: text cooties "lol"
					path = fPrimary.Path(parts[1]);
					if (path == null || path.Text != parts[2])
						return false;
					break;
				case "textnot": //bottom: textnot cooties "lol"
					path = fPrimary.Path(parts[1]);
					if (path == null || path.Text == parts[2])
						return false;
					break;
				case "value": //bottom: value charisma > 10
					path = fPrimary.Path(parts[1]);
					var cond = parts[2];
					var value = float.Parse(parts[3]);
					if (path == null)
						return false; //by default.
					switch (cond)
					{
						case "==":
							if (path.Value != value)
								return false;
							break;
						case "!=":
						case "<>":
							if (path.Value == value)
								return false;
							break;
						case ">":
							if (path.Value <= value)
								return false;
							break;
						case "<":
							if (path.Value >= value)
								return false;
							break;
						case ">=":
							if (path.Value < value)
								return false;
							break;
						case "<=":
							if (path.Value > value)
								return false;
							break;
					}
					break;
			}
			return true;
		}
	}
}
