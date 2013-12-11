using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Jint;

namespace Noxico
{
	class SceneSystem
	{
		private static XmlDocument xDoc;
		private static Character top, bottom;
		public static bool Dreaming, LeavingDream;

		private static bool letBottomChoose;

		public static void Engage(Character top, Character bottom)
		{
			Engage(top, bottom, "(starting node)");
		}

		public static void Engage(Character top, Character bottom, string name = "(starting node)")
		{
			if (xDoc == null)
				xDoc = UnfoldIfs(Mix.GetXmlDocument("scenesDlg.xml", true));

			SceneSystem.top = top;
			SceneSystem.bottom = bottom;

			if (name.Contains('\xE064'))
				name = name.Remove(name.LastIndexOf('\xE064'));

			var openings = FindOpenings(name);
			if (openings.Count == 0)
			{
				MessageBox.Notice("Could not find a proper opening for scene name \"" + name + "\". Aborting.", true, "Uh-oh.");
				return;
			}
			var scene = openings.FirstOrDefault(i => SceneFiltersOkay(i));
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
				{
					MessageBox.Notice(message, !Dreaming, bottom.GetKnownName(true, true));
					if (Dreaming)
						LeavingDream = true;
				}
				else
					MessageBox.List(message, actions, () => { Engage(SceneSystem.top, SceneSystem.bottom, (string)MessageBox.Answer); }, false, !Dreaming, bottom.GetKnownName(true, true));
			}

			if (Dreaming)
			{
				new UIPNGBackground(Mix.GetBitmap("dream.png")).Draw();
			}
			else
			{
				NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
				NoxicoGame.HostForm.Noxico.CurrentBoard.Draw();
			}
		}

		private static List<XmlElement> FindOpenings(string sceneName)
		{
			var ret = new List<XmlElement>();
			foreach (var scene in xDoc.SelectNodes("//scene").OfType<XmlElement>().Where(t => t.GetAttribute("name") == sceneName))
				ret.Add(scene);
			return ret;
		}

		private static Dictionary<object, string> ExtractActions(XmlElement scene)
		{
			var ret = new Dictionary<object, string>();
			foreach (var action in scene.SelectNodes("action").OfType<XmlElement>())
			{
				foreach (var s in xDoc.SelectNodes("//scene").OfType<XmlElement>().Where(s => s.GetAttribute("name") == action.GetAttribute("name") && SceneFiltersOkay(s)))
				{
					var key = action.GetAttribute("name");
					var listAs = s.GetAttribute("list");
					if (action.HasAttribute("listas"))
					{
						key = s.GetAttribute("name") + '\xE064' + ret.Count.ToString();
						listAs = action.GetAttribute("listas");
					}
					if (listAs.Contains('['))
						listAs = i18n.Viewpoint(listAs, SceneSystem.top, SceneSystem.bottom);
					if (!ret.ContainsKey(key))
						ret.Add(key, listAs);
				}
			}
			return ret;
		}

		private static string ExtractParagraphsAndScripts(XmlElement scene)
		{
			var ret = new StringBuilder();
			foreach (var part in scene.ChildNodes.OfType<XmlElement>().Where(p => p.Name == "p" || p.Name == "script"))
			{
				if (part.Name == "p")
				{
					ParseParagraph(ret, part);
					ret.AppendLine();
				}
				else if (part.Name == "script")
				{
					if (part.GetAttribute("type") == "text/javascript")
					{
						var buffer = new StringBuilder();
						var js = JavaScript.MainMachine;
						JavaScript.Ascertain(js);

						js.SetParameter("top", top);
						js.SetParameter("bottom", bottom);
						js.SetFunction("print", new Action<string>(x => buffer.Append(x)));
						js.SetFunction("LetBottomChoose", new Action<string>(x => letBottomChoose = true));
						js.SetFunction("GetBoard", new Func<int, Board>(x => NoxicoGame.HostForm.Noxico.GetBoard(x)));
						//js.SetFunction("ExpectTown", new Func<string, int, Expectation>(Expectation.ExpectTown));
						//js.SetParameter("Expectations", NoxicoGame.Expectations);
						//js.SetFunction("LearnUnknownLocation", new Action<string>(NoxicoGame.LearnUnknownLocation));
						js.Run(part.InnerText);
						ret.AppendLine(buffer.ToString());
						ret.AppendLine();
					}
				}
			}
			return ret.ToString().TrimEnd();
		}

		private static void ParseParagraph(StringBuilder ret, XmlElement part)
		{
			var trimmers = new[] {'\t', '\n', '\r'};
			var hadFalseIf = false;
			var entityMap = Enum.GetNames(typeof(KeyBinding)).Select(x => x.ToLowerInvariant()).ToList();
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
				else if (node is XmlEntityReference)
				{
					var entity = (XmlEntityReference)node;
					var keyIndex = entityMap.IndexOf(entity.Name);
					if (keyIndex > -1)
						ret.Append(Toolkit.TranslateKey((KeyBinding)keyIndex));
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

		private static bool SceneFiltersOkay(XmlElement scene)
		{
			foreach (var filter in scene.ChildNodes.OfType<XmlElement>().Where(f => f.Name == "filter"))
			{
				if (!FiltersOkay(filter))
					return false;
			}
			return true;
		}

		private static bool FiltersOkay(XmlElement filter)
		{
			var fType = filter.GetAttribute("type");
			var fName = filter.GetAttribute("name");
			var fValue = filter.GetAttribute("value");
			var fPrimary = filter.HasAttribute("target") ? (filter.GetAttribute("target") == "top" ? top : bottom) : bottom;
			var fSecondary = fPrimary == top ? bottom : top;
			var fValueF = 0f;
			var fValuePM = '\0';
			if (fValue.EndsWith("+") || fValue.EndsWith("-"))
			{
				fValuePM = fValue[fValue.Length - 1];
				fValue = fValue.Remove(fValue.Length - 1);
			}
			var wasFloat = float.TryParse(fValue, System.Globalization.NumberStyles.Float, null, out fValueF);

			switch (fType)
			{
				case "debug":
#if DEBUG
					//Allow this scene in debug builds...
					break;
#else
					//..but not in releases.
					return false;
#endif
				case "name":
					if (!(fPrimary.Name.ToString(true).Trim().Equals(fName, StringComparison.OrdinalIgnoreCase)))
						return false;
					break;
				case "has":
					if (fPrimary.Path(fName) == null)
						return false;
					if (fValueF > 0)
					{
						var num = fPrimary.Tokens.Count(t => t.Name == fName);
						if (fValuePM == '\0')
						{
							if (num != fValueF)
								return false;
						}
						else if (fValuePM == '+')
						{
							if (num < fValueF)
								return false;
						}
						else if (fValuePM == '-')
						{
							if (num > fValueF)
								return false;
						}
					}
					else if (!string.IsNullOrEmpty(fValue))
					{
						//Added this to allow checking for a specific text value, such as long tongues:
						//<filter target="bottom" type="has" name="tongue" value="long" />
						if (fPrimary.Path(fName).Text != fValue)
							return false;
					}
					break;
				case "hasnot":
					if (fPrimary.Path(fName) != null)
						return false;
					break;
				case "stat":
				case "value_gteq":
					if (fPrimary.Path(fName).Value < fValueF)
						return false;
					break;
				case "value_equal":
					if (fPrimary.Path(fName).Value != fValueF)
						return false;
					break;
				case "value_lower":
					if (fPrimary.Path(fName).Value >= fValueF)
						return false;
					break;
				case "relation":
					if (fValue != "none")
					{
						var path = "ships/" + fSecondary.ID + "/" + fValue;
						if (fPrimary.Path(path) == null)
							return false;
					}
					else
					{
						var path = "ships/" + fSecondary.ID;
						if (fPrimary.Path(path) != null)
							return false;
					}
					break;
				case "liking":
					var shipPath = "ships/" + fSecondary.ID;
					if (fPrimary.Path(shipPath) == null)
						return false;
					var liking = fPrimary.Path(shipPath).Value;
					if (fValuePM == '-' && liking >= fValueF)
						return false;
					else if (fValuePM != '-' && liking < fValueF)
						return false;
					break;
				case "gender":
					if (fValue == "male" && fPrimary.Gender != Gender.Male)
						return false;
					else if (fValue == "female" && fPrimary.Gender != Gender.Female)
						return false;
					break;
				case "bodyhash":
					var primaryHash = Toolkit.GetBodyComparisonHash(fPrimary);
					var distance = Toolkit.GetHammingDistance(primaryHash, NoxicoGame.BodyplanHashes[fName]);
					if (distance > 0) //?
						return false;
					break;
				case "hasdildo":
					if (!fPrimary.HasToken("items"))
						return false;
					var hasDildo = false;
					foreach (var item in fPrimary.GetToken("items").Tokens)
					{
						var knownItem = NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID == item.Name);
						if (knownItem == null)
							continue;
						if (knownItem.HasToken("canfuck"))
						{
							if (fValueF > 0)
							{
								var surface = knownItem.GetToken("thickness").Value * knownItem.GetToken("length").Value;
								if (fValuePM == '+' && surface < fValueF)
									continue;
								else if (surface > fValueF)
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
			}
			return true;
		}

		private static XmlDocument UnfoldIfs(XmlDocument original)
		{
			foreach (var paragraph in original.SelectNodes("//p").OfType<XmlElement>())
			{
				var input = paragraph.InnerXml;
				if (!input.Contains("{if"))
					continue;

				var output = new StringBuilder();
				var thingsToEnd = new Stack<bool>();
				for (var i = 0; i < input.Length; i++)
				{
					if (input[i] == '{')
					{
						//Found a tag. Filter out what it is.
						var tag = string.Empty;
						i += 1;
						for (var j = i; j < input.Length; j++)
						{
							if (input[j + 1] == '}')
							{
								tag = input.Substring(i, j - i + 1).Trim();
								i = j;
								break;
							}
						}
						var replacement = string.Empty;
						if (tag.StartsWith("if "))
						{
							output.AppendLine();
							//TODO
							var target = "top";
							var type = string.Empty;
							var value = string.Empty;
							var name = string.Empty;
							var keywords = tag.Substring(3).SplitQ().ToList();
							if (keywords[0] == "top")
							{
								target = "top";
								keywords.RemoveAt(0);
							}
							else if (keywords[1] == "bottom")
							{
								target = "bottom";
								keywords.RemoveAt(0);
							}
							if (keywords[0] == "name")
							{
								type = "name";
								name = keywords[1];
							}
							else if (keywords[0] == "has")
							{
								if (keywords[1] == "dildo")
								{
									type = "hasdildo";
								}
								else
								{
									type = "has";
									name = keywords[1];
									if (keywords.Count > 2)
									{
										value = keywords[2];
									}
								}
							}
							else if (keywords[0] == "hasnot")
							{
								type = "hasnot";
								name = keywords[1];
							}
							else if (new[] { "=", "<", ">=" }.Contains(keywords[1]))
							{
								if (keywords[1] == "=")
									type = "value_equal";
								else if (keywords[1] == "<")
									type = "value_lower";
								else if (keywords[1] == ">=")
									type = "value_gteq";
								name = keywords[0];
								value = keywords[2];
							}
							else if (keywords[0] == "relation")
							{
								type = "relation";
								if (keywords[1] == "is")
									keywords.RemoveAt(1);
								value = keywords[1];
							}
							else if (keywords[0] == "isa")
							{
								if (new[] { "male", "female" }.Contains(keywords[1]))
								{
									type = "gender";
									value = keywords[1];
								}
								else
								{
									type = "bodyhash";
									name = keywords[1];
								}
							}
							else if (keywords[0] == "likes")
							{
								type = "liking";
								value = keywords[1];
							}
							else if (new[] { "canfitdickinpussy", "canfitdickinmouth", "isfather" }.Contains(keywords[0]))
							{
								type = keywords[0];
							}

							output.Append("<if target=\"" + target + "\" type=\"" + type + "\"");
							if (!string.IsNullOrWhiteSpace(name))
								output.Append(" name=\"" + name + "\"");
							if (!string.IsNullOrWhiteSpace(value))
								output.Append(" name=\"" + value + "\"");
							output.Append(">");

							//output.Append("<if query=\"" + tag.Substring(3) + "\">");
							thingsToEnd.Push(false);
						}
						else if (tag == "else")
						{
							thingsToEnd.Pop();
							output.AppendLine();
							output.Append("</if>");
							output.Append("<else>");
							thingsToEnd.Push(true);
						}
						else if (tag == "endif")
						{
							output.AppendLine();
							var wasElse = thingsToEnd.Pop();
							if (wasElse)
								output.Append("</else>");
							else
								output.Append("</if>");
						}
						i++;
					}
					else
					{
						output.Append(input[i]);
					}
				}
				var final = output.ToString();
				paragraph.InnerXml = final;
			}
			return original;
		}
	}
}
