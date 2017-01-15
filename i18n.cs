/* Some rules on Private Use characters
 * ------------------------------------
 * U+E200 to U+E3FF are considered controllers.
 * They do not represent printable characters.
 * U+E400 to U+E4FF are wide.
 * 
 * U+E200	Left
 * U+E201	Right
 * U+E202	Up
 * U+E203	Down
 * U+E204	Rest
 * U+E205	Activate
 * U+E206	Items
 * U+E207	Interact
 * U+E208	Fly
 * U+E209	Travel
 * U+E20A	Accept
 * U+E20B	Back
 * U+E20C	Pause
 * U+E20D	Screenshot
 * U+E20E	Tab Focus
 * U+E20F	Scroll Up
 * U+E210	Scroll Down
 * 
 * U+E220	Player's name
 * 
 * U+E2FC	Indicates failure (for use with Character.Mutate() reporting)
 * U+E2FD	Hide message from backlog
 * U+E2FE	Shorthand flag for key substitution
 * U+E2FF	Wide character placeholder
 * U+E300	Do not translate!
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Noxico
{
	public static class i18n
	{
		private static Dictionary<string, string> words;
		private static Token pluralizerData;

		private static List<string> notFound = new List<string>();

		private static void Initialize()
		{
			if (words != null)
				return;
			words = new Dictionary<string, string>();
			var x = Mix.GetTokenTree("i18n.tml");
			foreach (var word in x.Find(t => t.Name == "words").Tokens)
			{
				if (word.Text == null && word.HasToken("#text"))
					words[word.Name] = word.GetToken("#text").Text;
				else
					words[word.Name] = word.Text;
			}

			pluralizerData = x.Find(t => t.Name == "pluralizer");
			//Sanity check!
			if (pluralizerData.HasToken("sanitycheck"))
			{
				foreach (var check in pluralizerData.GetToken("sanitycheck").Tokens)
				{
					var checkFrom = check.GetToken("from").Text;
					var checkTo = check.GetToken("to").Text;
					var result = string.Empty;
					if (check.Name == "pluralize")
						result = Pluralize(checkFrom, 2);
					else if (check.Name == "singularize")
						result = Singularize(checkFrom);
					if (result != checkTo)
						throw new Exception(string.Format("Sanity check on pluralizer failed. Expected \"{0}\" but got \"{1}\".", checkTo, result));
				}
			}
		}

		public static string Entitize(string input)
		{
			var longhand = !input.Contains('\uE2FE');
			for (var i = 0; i <= 0x10; i++)
				input = input.Replace(((char)(0xE200 + i)).ToString(), Toolkit.TranslateKey((KeyBinding)i, longhand));
			if (NoxicoGame.HostForm.Noxico.Player != null && NoxicoGame.HostForm.Noxico.Player.Character != null)
				input = input.Replace("\uE220", NoxicoGame.HostForm.Noxico.Player.Character.Name.ToString());
			else
				input = input.Replace("\uE220", "????");
			input = input.Replace("\uE2FE", "");
			return input;
		}

		public static string GetString(string key, bool brackets = true)
		{
			if (key[0] == '\uE300')
				return key.Substring(1);
			Initialize();
			if (words.ContainsKey(key))
				return Entitize(words[key]);
			if (brackets)
			{
				if (!notFound.Contains(key))
					notFound.Add(key);
				return '[' + key + ']';
			}
			return key;
		}

		public static string Format(string key, params object[] arg)
		{
			return string.Format(GetString(key), arg);
		}

		public static string[] GetArray(string key)
		{
			return GetString(key).Split(',').Select(x => x.TrimStart()).ToArray();
		}

		public static List<string> GetList(string key)
		{
			return GetString(key).Split(',').Select(x => x.Trim()).ToList();
		}
		
		public static string Pluralize(this string singular)
		{
			var of = pluralizerData.GetToken("of").Text;
			var simple = pluralizerData.GetToken("simple").Text;

			if (words.ContainsKey(singular))
				singular = words[singular];

			if (singular.Contains(of))
			{
				var ofPos = singular.IndexOf(of);
				var key = singular.Substring(0, ofPos);
				var ofWhat = singular.Substring(ofPos);
				return Pluralize(key) + ofWhat;
			}
			foreach (var uncountable in pluralizerData.GetToken("uncountable").Tokens)
				if (singular.EndsWith(uncountable.Name, StringComparison.InvariantCultureIgnoreCase))
					return singular;
			foreach (var irregular in pluralizerData.GetToken("irregular").Tokens)
			{
				if (irregular.HasToken("fullword"))
				{
					if (singular.Equals(irregular.Name, StringComparison.InvariantCultureIgnoreCase))
						return irregular.Text;
				}
				else
					if (singular.EndsWith(irregular.Name, StringComparison.InvariantCultureIgnoreCase))
						return singular.Remove(singular.LastIndexOf(irregular.Name)) + irregular.Text;
			}
			foreach (var regular in pluralizerData.GetToken("regular").Tokens)
				if (singular.EndsWith(regular.Name, StringComparison.InvariantCultureIgnoreCase))
					return singular.Remove(singular.LastIndexOf(regular.Name)) + regular.Text;

			return singular + simple;
		}

		public static string Pluralize(this string singular, int count)
		{
			if (count == 1)
				return singular;
			return Pluralize(singular);
		}

		public static string Singularize(this string plural)
		{
			var of = pluralizerData.GetToken("of").Text;
			var simple = pluralizerData.GetToken("simple").Text;

			if (plural.Contains(of))
			{
				var ofPos = plural.IndexOf(of);
				var key = plural.Substring(0, ofPos);
				var ofWhat = plural.Substring(ofPos);
				return Singularize(key) + ofWhat;
			}
			foreach (var uncountable in pluralizerData.GetToken("uncountable").Tokens)
				if (plural.EndsWith(uncountable.Name, StringComparison.InvariantCultureIgnoreCase))
					return plural;
			foreach (var irregular in pluralizerData.GetToken("irregular").Tokens)
				if (plural.EndsWith(irregular.Text, StringComparison.InvariantCultureIgnoreCase))
					return plural.Remove(plural.LastIndexOf(irregular.Text)) + irregular.Name;
			foreach (var regular in pluralizerData.GetToken("regular").Tokens)
				if (plural.EndsWith(regular.Text, StringComparison.InvariantCultureIgnoreCase))
					return plural.Remove(plural.LastIndexOf(regular.Text)) + regular.Name;

			if (plural.EndsWith(simple))
				return plural.Remove(plural.Length - simple.Length);
			return plural;
		}


		/// <summary>Gets the number of effective tiles needed to draw the current String.</summary>
		public static int Length(this string input)
		{
			if (input.Length == 1 && input[0] == '<')
				return 1;
			var ret = 0;
			for (var i = 0; i < input.Length; i++)
			{
				var c = input[i];
				if (c == '<' && input[i + 1] == 'c') //skip over color tags
					i = input.IndexOf('>', i + 1);
				else if (i < input.Length - 8 && input.Substring(i, 8) == "<nowrap>") //skip nowrap tag
					i += 8;
				else if (i < input.Length - 3 && input.Substring(i, 3) == "<b>") //skip bold tag
					i += 3;
				else if ((c >= 0x3000 && c < 0x4000) || (c >= 0x4E00 && c < 0xA000) || (c >= 0xE400 && c < 0xE500)) //report double the length for Japanese
					ret += 2;
				else if (c >= 0xE200 && c < 0xE400) //skip private use controllers
					i++;
				else
					ret++;
			}
			return ret;
		}

		public static string PadEffective(this string input, int length)
		{
			var lengthNow = input.Length();
			if (length - lengthNow < 0)
				return input;
			return input + new string(' ', length - lengthNow);
		}

		public static string PadLeftEffective(this string input, int length)
		{
			var lengthNow = input.Length();
			return new string(' ', length - lengthNow) + input;
		}

		public static string Viewpoint(this string message, Character top, Character bottom = null)
		{
			var player = NoxicoGame.HostForm.Noxico.Player.Character;
			if (top == null)
				top = player;
			if (bottom == null)
				bottom = top;
			//var tIP = player == top;
			#region Definitions
			var subcoms = new Dictionary<string, Func<Character, IList<string>, string>>()
			{
				{ "You", (c, s) => { return c == player ? "You" : c.HeSheIt(); } },
				{ "Your", (c, s) => { return c == player ? "Your" : c.HisHerIts(); } },
				{ "you", (c, s) => { return c == player ? "you" : c.HeSheIt(true); } },
				{ "your", (c, s) => { return c == player ? "your" : c.HisHerIts(true); } },

				{ "Youorname", (c, s) => { return c == player ? "You" : c.GetKnownName(false, false, true, true); } },
				{ "youorname", (c, s) => { return c == player ? "you" : c.GetKnownName(false, false, true); } },
				{ "Yourornames", (c, s) => { return c == player ? "Your" : c.GetKnownName(false, false, true, true) + "'s" /* i18n.GetString("possessive") */; } },
				{ "yourornames", (c, s) => { return c == player ? "your" : c.GetKnownName(false, false, true, false) + "'s" /* i18n.GetString("possessive") */; } },

				{ "isme", (c, s) => { return c == player ? s[0] : s[1]; } },
				{ "g", (c, s) => { var g = c.Gender; return g == Gender.Male ? s[0] : (g == Gender.Herm && !string.IsNullOrEmpty(s[2]) ? s[2] : s[1]); } },
				{ "t", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Text.ToLower(); } },
				{ "T", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Text; } },
				{ "v", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Value.ToString(); } },
				{ "l", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : Descriptions.Length(t.Value); } },
				{ "p", (c, s) => { return string.Format("{0} {1}", s[0], Pluralize(s[1], int.Parse(s[0]))); } },
				{ "P", (c, s) => { return Pluralize(s[1], int.Parse(s[0])); } },

				{ "name", (c, s) => { return c.GetKnownName(false, false, true); } },
				{ "fullname", (c, s) => { return c.GetKnownName(true, false, true); } },
				{ "title", (c, s) => { return c.Title; } },
				{ "gender", (c, s) => { return c.Gender.ToString().ToLowerInvariant(); } },
				{ "His", (c, s) => { return c == player ? "Your" : c.HisHerIts(); } },
				{ "He", (c, s) => { return c == player ? "You" : c.HeSheIt(); } },
				{ "Him", (c, s) => { return c == player ? "Your" : c.HimHerIt(); } },
				{ "his", (c, s) => { return c == player ? "your" : c.HisHerIts(true); } },
				{ "he", (c, s) => { return c == player ? "you" : c.HeSheIt(true); } },
				{ "him", (c, s) => { return c == player ? "you" : c.HimHerIt(true); } },
				{ "is", (c, s) => { return c == player ? "are" : "is"; } },
				{ "has", (c, s) => { return c == player ? "have" : "has"; } },
				{ "does", (c, s) => { return c == player ? "do" : "does"; } },

				{ "breastsize", (c, s) => { if (s[0].Length == 0) s[0] = "0"; return Descriptions.BreastSize(c.Path("breastrow[" + s[0] + "]")); } },
                { "breastcupsize", (c, s) => { if (s[0].Length == 0) s[0] = "0"; return Descriptions.BreastSize(c.Path("breastrow[" + s[0] + "]"), true); } },
				{ "nipplesize", (c, s) => { if (s[0].Length == 0) s[0] = "0"; return Descriptions.NippleSize(c.Path("breastrow[" + s[0] + "]/nipples")); } },
				{ "waistsize", (c, s) => { return Descriptions.WaistSize(c.Path("waist")); } },
				{ "buttsize", (c, s) => { return Descriptions.ButtSize(c.Path("ass")); } },

				#region PillowShout's additions
				{ "cocktype", (c, s) => { if (s[0].Length == 0) s[0] = "0"; return Descriptions.CockType(c.Path("penis[" + s[0] + "]")); } },
				//{ "cockrand", (c, s) => { return Descriptions.CockRandom(); } },
				//{ "pussyrand", (c, s) => { return Descriptions.PussyRandom(); } },
				//{ "clitrand", (c, s) => { return Descriptions.ClitRandom(); } },
				//{ "anusrand", (c, s) => { return Descriptions.AnusRandom(); } },
                //{ "buttrand", (c, s) => { return Descriptions.ButtRandom(); } },
				//{ "breastrand", (c, s) => { return Descriptions.BreastRandom(); } },
				//{ "breastsrand", (c, s) => { return Descriptions.BreastRandom(true); } },
				{ "pussywetness", (c, s) => { if (s[0].Length == 0) s[0] = "0"; return Descriptions.Wetness(c.Path("vagina[" + s[0] + "]/wetness")); } },
				{ "pussylooseness", (c, s) => { return Descriptions.Looseness(c.Path("vagina[" + s[0] + "]/looseness")); } },
				{ "anuslooseness", (c, s) => { return Descriptions.Looseness(c.Path("ass/looseness"), true); } },
				{ "foot", (c, s) => { return Descriptions.Foot(c.GetToken("legs")); } },
				{ "feet", (c, s) => { return Descriptions.Foot(c.GetToken("legs"), true); } },
				//{ "cumrand", (c, s) => { return Descriptions.CumRandom(); } },
				{ "equipment", (c, s) => {var i = c.GetEquippedItemBySlot(s[0]); return (s[1] == "color" || s[1] == "c") ? Descriptions.Item(i, i.tempToken, s[2], true) : Descriptions.Item(i, i.tempToken, s[1]); } },
				{ "tonguetype", (c, s) => { return Descriptions.TongueType(c.GetToken("tongue")); } },
				{ "tailtype", (c, s) => { return Descriptions.TailType(c.GetToken("tail")); } },
                { "hipsize", (c, s) => { return Descriptions.HipSize(c.GetToken("hips")); } },
                { "haircolor", (c, s) => { return Descriptions.HairColor(c.GetToken("hair")); } },
                { "hairlength", (c, s) => { return Descriptions.HairLength(c.GetToken("hair")); } },
                { "ballsize", (c, s) => { return Descriptions.BallSize(c.GetToken("balls")); } },
				#endregion

				{ "hand", (c, s) => { return Descriptions.Hand(c); } },
				{ "hands", (c, s) => { return Descriptions.Hand(c, true); } },

				//{ "voc", (c, s) => { return Toolkit.PickOne(i18n.GetArray("vocalize_" + s[0] + (c == player ? string.Empty : "s"))); } },
				{ "voc", (c, s) => { return Toolkit.PickOne(i18n.GetArray("vocalize_" + s[0])) + "{s}"; } },
			};
			#endregion
			#region [] Parser
			var regex = new Regex(@"
\[
	(?:(?<target>[\w\?]):)?		#Optional target and :

	(?:						#One or more subcommands
		(?:\:?)				#Separating :, optional in case target already had one
		(?<subcom>[\w_]+)	#Command
	)*
\]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
			while (regex.IsMatch(message))
			{
				message = regex.Replace(message, (match =>
				{
					var target = bottom;
					var subcom = string.Empty;
					var parms = new List<string>();

					if (match.Groups["target"].Value == "?")
					{
						if (SceneSystem.Placeholders == null)
							SceneSystem.Placeholders = Mix.GetTokenTree("dialogue.tml", true).First(x => x.Name == "placeholders");

						var pToks = SceneSystem.Placeholders.Tokens.Where(x => x.Name == match.Groups["subcom"].Value).ToList();
						var pTok = pToks[Random.Next(pToks.Count)];
						return pTok.Tokens[Random.Next(pTok.Tokens.Count)].Text;
					}
					else if (!match.Groups["subcom"].Success)
					{
						subcom = match.Groups["target"].Value;
					}
					else
					{
						if (match.Groups["target"].Length == 1 && "tb".Contains(match.Groups[1].Value[0]))
							target = (match.Groups["target"].Value[0] == 't' ? top : bottom);
						subcom = match.Groups["subcom"].Value;

						if (match.Groups["subcom"].Captures.Count > 1)
						{
							//subcom = match.Groups["target"].Value;
							subcom = match.Groups["subcom"].Captures[0].Value;
							for (var i = 1; i < match.Groups["subcom"].Captures.Count; i++)
							{
								var c = match.Groups["subcom"].Captures[i];
								Console.WriteLine(c);
								parms.Add(c.Value.Replace('(', '[').Replace(')', ']'));
							}
						}
					}

					parms.Add(string.Empty);
					parms.Add(string.Empty);
					parms.Add(string.Empty);

					if (subcoms.ContainsKey(subcom))
						return subcoms[subcom](target, parms);
					return string.Format("(?{0}?)", subcom);
				}));
			}
			#endregion

			regex = new Regex(@"{(?:{)? (?<first>\w*)   (?: \| (?<second>\w*) )? }(?:})?", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
			message = regex.Replace(message, (match => top == player ? (match.Groups["second"].Success ? match.Groups["second"].Value : string.Empty) : match.Groups["first"].Value));
			message = Regex.Replace(message, @"\[\!(?<keybinding>.+?)\]", (match => Toolkit.TranslateKey(match.Groups["keybinding"].Value)));

			return message;
		}
	}
}
