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
		private static XmlDocument xSex, xDlg;
		private static XmlDocument xDoc;
		private static Character top, bottom;
		public static bool Dreaming, LeavingDream;

		private static bool letBottomChoose, wantToTrade; 

		public static void Engage(Character top, Character bottom, bool inDialogue)
		{
			Engage(top, bottom, "(starting node)", inDialogue);
		}

		public static void Engage(Character top, Character bottom, string name = "(starting node)", bool inDialogue = false)
		{
			if (xSex == null)
			{
				xSex = Mix.GetXMLDocument("scenesSex.xml", true);
				xDlg = Mix.GetXMLDocument("scenesDlg.xml", true);
			}

			if (Dreaming)
				NoxicoGame.Sound.PlayMusic("robric993.xm");
			wantToTrade = false;
			
			xDoc = inDialogue ? xDlg : xSex;
			SceneSystem.top = top;
			SceneSystem.bottom = bottom;

			if (name.Contains('!'))
				name = name.Remove(name.LastIndexOf('!'));

			var openings = FindOpenings(name);
			if (openings.Count == 0)
			{
				MessageBox.Message("Could not find a proper opening for scene name \"" + name + "\". Aborting.", true, "Uh-oh.");
				return;
			}
			var scene = openings.FirstOrDefault(i => SceneFiltersOkay(i));
			var message = ApplyTokens(ExtractParagraphsAndScripts(scene));
			var actions = ExtractActions(scene);

			if (!inDialogue)
			{
				if (top.GetToken("climax").Value >= 100 || bottom.GetToken("climax").Value >= 100)
				{
					actions.Clear();
					if (top.GetToken("climax").Value >= 100 && bottom.GetToken("climax").Value < 100)
					{
						actions.Add("(top climax)", "");
						top.GetToken("climax").Value = 0;
					}
					else if (top.GetToken("climax").Value >= 100 && bottom.GetToken("climax").Value >= 100)
					{
						actions.Add("(both climax)", "");
						top.GetToken("climax").Value = bottom.GetToken("climax").Value = 0;
					}
					else
					{
						actions.Add("(bottom climax)", "");
						bottom.GetToken("climax").Value = 0;
					}
				}
			}

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
					MessageBox.Message(message, true, bottom.Name.ToString(true));
				}
				else
				{
					var randomAction = actions.Keys.ToArray()[Toolkit.Rand.Next(actions.Count)];
					actions.Clear();
					actions.Add(randomAction, "==>");
					MessageBox.List(message, actions, () => { Engage(SceneSystem.top, SceneSystem.bottom, (string)MessageBox.Answer, inDialogue); }, false, true, bottom.Name.ToString(true));
				}
			}
			else
			{
				letBottomChoose = false;
				if (actions.Count == 0)
				{
					MessageBox.Message(message, !Dreaming, bottom.Name.ToString(true));
					if (Dreaming)
						LeavingDream = true;
					else if (wantToTrade)
						ContainerMan.Setup(bottom);
				}
				else
					MessageBox.List(message, actions, () => { Engage(SceneSystem.top, SceneSystem.bottom, (string)MessageBox.Answer, inDialogue); }, false, !Dreaming, bottom.Name.ToString(true));
			}

			if (Dreaming)
			{
				NoxicoGame.HostForm.LoadBitmap(Mix.GetBitmap("dream.png"));
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
				if (action.HasAttribute("listas"))
					foreach (var s in xDoc.SelectNodes("//scene").OfType<XmlElement>().Where(s => s.GetAttribute("name") == action.GetAttribute("name") && SceneFiltersOkay(s)))
						ret.Add(s.GetAttribute("name") + '!' + ret.Count.ToString(), action.GetAttribute("listas"));
				else
					foreach (var s in xDoc.SelectNodes("//scene").OfType<XmlElement>().Where(s => !ret.ContainsKey(s.GetAttribute("name")) && s.GetAttribute("name") == action.GetAttribute("name") && SceneFiltersOkay(s)))
						ret.Add(s.GetAttribute("name"), s.GetAttribute("list"));
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
						var js = Javascript.MainMachine;
						Javascript.Ascertain(js);

						js.SetParameter("top", top);
						js.SetParameter("bottom", bottom);
						js.SetFunction("print", new Action<string>(x => buffer.Append(x)));
						js.SetFunction("LetBottomChoose", new Action<string>(x => letBottomChoose = true));
						js.SetFunction("ExpectTown", new Func<string, int, Expectation>(Expectation.ExpectTown));
						js.SetParameter("Expectations", NoxicoGame.Expectations);
						js.SetFunction("Trade", new Action<string>(x => wantToTrade = true));
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
			foreach (var node in part.ChildNodes)
			{
				if (node is XmlText)
				{
					var text = (node as XmlText).Value;
					if (trimmers.Contains(text[0]))
						text = text.TrimStart();
					if (trimmers.Contains(text[text.Length - 1]))
						text = text.TrimEnd();
					ret.Append(text);
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
			float.TryParse(fValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fValueF);
			switch (fType)
			{
				case "name":
					if (!(fPrimary.Name.ToString(true).Trim().Equals(fName, StringComparison.InvariantCultureIgnoreCase)))
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
					else if (fValue != "")
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
				case "gender":
					if (fValue == "male" && fPrimary.GetGender() != "male")
						return false;
					else if (fValue == "female" && fPrimary.GetGender() != "female")
						return false;
					break;
				case "bodylev":
					var primaryLev = Toolkit.GetLevenshteinString(fPrimary);
					var distance = Toolkit.Levenshtein(primaryLev, NoxicoGame.BodyplanLevs[fName]);
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

		private static string ApplyTokens(string message)
		{
			var player = NoxicoGame.HostForm.Noxico.Player.Character;
			var tIP = player == top;
			#region Definitions
			var subcoms = new Dictionary<string, Func<Character, string[], string>>()
			{
				{ "You", (c, s) => { return tIP && c == top ? "You" : c.HeSheIt(); } },
				{ "Your", (c, s) => { return tIP && c == top ? "Your" : c.HisHerIts(); } },
				{ "you", (c, s) => { return tIP && c == top ? "you" : c.HeSheIt(true); } },
				{ "your", (c, s) => { return tIP && c == top ? "your" : c.HisHerIts(true); } },

				{ "isme", (c, s) => { return c == player ? s[0] : s[1]; } },
				{ "g", (c, s) => { var g = c.GetGender(); return g == "male" ? s[0] : (g == "hermaphrodite" && s[2] != "" ? s[2] : s[1]); } },
				{ "t", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Text.ToLowerInvariant(); } },
				{ "T", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Text; } },
				{ "v", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Value.ToString(); } },
				{ "l", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : Descriptions.Length(t.Value); } },

				{ "name", (c, s) => { return c.Name.ToString(); } },
				{ "fullname", (c, s) => { return c.Name.ToString(true); } },
				{ "title", (c, s) => { return c.Title; } },
				{ "gender", (c, s) => { return c.GetGender(); } },
				{ "His", (c, s) => { return tIP && c == top ? "Your" : c.HisHerIts(); } },
				{ "He", (c, s) => { return tIP && c == top ? "You" : c.HeSheIt(); } },
				{ "his", (c, s) => { return tIP && c == top ? "your" : c.HisHerIts(true); } },
				{ "he", (c, s) => { return tIP && c == top ? "you" : c.HeSheIt(true); } },
				{ "him", (c, s) => { return tIP && c == top ? "you" : c.HimHerIt(); } },
				{ "is", (c, s) => { return tIP && c == top ? "are" : "is"; } },
				{ "has", (c, s) => { return tIP && c == top ? "have" : "has"; } },
				{ "does", (c, s) => { return tIP && c == top ? "do" : "does"; } },

				{ "hair", (c, s) => { return Descriptions.Hair(c.Path("hair")); } },
				{ "breasts", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.Breasts(c.Path("breastrow[" + s[0] + "]")); } },
				{ "nipple", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.Nipples(c.Path("breastrow[" + s[0] + "]/nipples")); } },
				{ "nipples", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.Nipples(c.Path("breastrow[" + s[0] + "]/nipples")) + 's'; } },
				{ "waist", (c, s) => { return Descriptions.Waist(c.Path("waist")); } },
				{ "hips", (c, s) => { return Descriptions.Hips(c.Path("hips")); } },
				{ "ass", (c, s) => { return Descriptions.Butt(c.Path("ass")); } },
				{ "tail", (c, s) => { return Descriptions.Tail(c.Path("tail")); } },
				//{ "multicock", (c, s) => { return Descriptions.MultiCock(c); } },
				{ "multicock", (c, s) => { return Descriptions.Cock(c.Path("penis[0]")); } },
				{ "cock", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.Cock(c.Path("penis[" + s[0] + "]")); } },
				//{ "pussy", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.Pussy(c.Path("vagina[" + s[0] + "]")); } },
				{ "pussy", (c, s) => { return "pussy"; } },

				#region PillowShout's additions
				{ "cocktype", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.CockType(c.Path("penis[" + s[0] + "]")); } },
				{ "cockrand", (c, s) => { return Descriptions.CockRandom(); } },
				{ "pussyrand", (c, s) => { return Descriptions.PussyRandom(); } },
				{ "clitrand", (c, s) => { return Descriptions.ClitRandom(); } },
				{ "anusrand", (c, s) => { return Descriptions.AnusRandom(); } },
				{ "breastrand", (c, s) => { return Descriptions.BreastRandom(); } },
				{ "breastsrand", (c, s) => { return Descriptions.BreastRandom(true); } },
				{ "pussywetness", (c, s) => { if (s[0] == "") s[0] = "0"; return Descriptions.Wetness(c.Path("vagina[" + s[0] + "]/wetness")) ?? "moist"; } },
				{ "pussylooseness", (c, s) => { return Descriptions.Looseness(c.Path("vagina[" + s[0] + "]/looseness")) ?? "average"; } },
				{ "anuslooseness", (c, s) => { return Descriptions.Looseness(c.Path("ass/looseness"), true) ?? "average"; } },
				{ "foot", (c, s) => {return Descriptions.Foot(c.GetToken("legs")); } },
				{ "feet", (c, s) => {return Descriptions.Foot(c.GetToken("legs"), true); } },
				{ "cumrand", (c, s) => {return Descriptions.CumRandom(); } },
				{ "equipment", (c, s) => {var i = c.GetEquippedItemBySlot(s[0]); return (s[1] == "color" || s[1] == "c") ? Descriptions.Item(i, i.tempToken, s[2], true) : Descriptions.Item(i, i.tempToken, s[1]); } },
				{ "tonguetype", (c, s) => {return Descriptions.TongueType(c.GetToken("tongue")); } },
				{ "tailtype", (c, s) => {return Descriptions.TailType(c.GetToken("tail")); } },
				#endregion
			};
			#endregion
			#region Parser
			var regex = @"\[ ([A-Za-z]+) (?: \: ([A-Za-z/_]+) )* \]";
			var ro = RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline;
			while (Regex.IsMatch(message, regex, ro))
			{
				var match = Regex.Match(message, regex, ro);
				var replace = match.ToString();
				var with = "";
				var target = bottom;
				var subcom = "";
				var parms = new List<string>();

				if (!match.Groups[2].Success)
				{
					subcom = match.Groups[1].Value;
				}
				else
				{
					target = (match.Groups[1].Value[0] == 't' ? top : bottom);
					subcom = match.Groups[2].Value;

					if (match.Groups[2].Captures.Count > 1)
					{
						subcom = match.Groups[2].Captures[0].Value;
						foreach (Capture c in match.Groups[2].Captures)
						{
							Console.WriteLine(c);							
							parms.Add(c.Value.Replace('(', '[').Replace(')', ']'));
						}
						parms.RemoveAt(0);
					}
					}

				parms.Add("");
				parms.Add("");
				parms.Add("");

				if (subcoms.ContainsKey(subcom))
					with = subcoms[subcom](target, parms.ToArray());
				//possibility: allow unknown tokens with no extra parameters to just "be as-is": "[b:clit]" -> just "clit", until further notice.

				//message = message.Replace(replace, with);
				var left = message.Substring(0, match.Groups[0].Index);
				var right = message.Substring(match.Groups[0].Index + match.Groups[0].Length);
				message = left + with + right;
			}
			#endregion

			return message;
		}
	}
}
