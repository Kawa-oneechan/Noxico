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
 * U+E2FD	Hide message from backlog
 * U+E2FE	Shorthand flag for key substitution
 * U+E2FF	Wide character placeholder
 * U+E300	Do not translate!
 * U+E000	Different styles
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Noxico
{
	public static class i18n
	{
		private static Dictionary<string, string> words;

		private static void Initialize()
		{
			if (words != null)
				return;
			words = new Dictionary<string, string>();
			var x = Mix.GetXmlDocument("words.xml");
			foreach (var word in x.SelectNodes("//word").OfType<XmlElement>())
				words[word.GetAttribute("id")] = word.InnerText;
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
				return '[' + key + ']';
			return key;
		}

		public static string Format(string key, params object[] arg)
		{
			return string.Format(GetString(key), arg);
		}

		public static string[] GetArray(string key)
		{
			return GetString(key).Split(',').Select(x => x.Trim()).ToArray();
		}

		public static List<string> GetList(string key)
		{
			return GetString(key).Split(',').Select(x => x.Trim()).ToList();
		}

		public static string Pluralize(string singular, int amount)
		{
			if (words.ContainsKey(singular))
				singular = words[singular];
			if (amount == 1)
				return singular;
			//TODO: inflect right. THIS IS VERY NAIVE AND STUPID. THERE IS A BETTER SYSTEM IN INFLECTOR.SLN.
			return singular + 's';
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

				{ "isme", (c, s) => { return c == player ? s[0] : s[1]; } },
				{ "g", (c, s) => { var g = c.Gender; return g == Gender.Male ? s[0] : (g == Gender.Herm && !string.IsNullOrEmpty(s[2]) ? s[2] : s[1]); } },
				{ "t", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Text.ToLower(); } },
				{ "T", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Text; } },
				{ "v", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : t.Value.ToString(); } },
				{ "l", (c, s) => { var t = c.Path(s[0]); return t == null ? "<404>" : Descriptions.Length(t.Value); } },
				{ "p", (c, s) => { return string.Format("{0} {1}", s[0], Pluralize(s[1], int.Parse(s[0]))); } },

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
				{ "cockrand", (c, s) => { return Descriptions.CockRandom(); } },
				{ "pussyrand", (c, s) => { return Descriptions.PussyRandom(); } },
				{ "clitrand", (c, s) => { return Descriptions.ClitRandom(); } },
				{ "anusrand", (c, s) => { return Descriptions.AnusRandom(); } },
                { "buttrand", (c, s) => { return Descriptions.ButtRandom(); } },
				{ "breastrand", (c, s) => { return Descriptions.BreastRandom(); } },
				{ "breastsrand", (c, s) => { return Descriptions.BreastRandom(true); } },
				{ "pussywetness", (c, s) => { if (s[0].Length == 0) s[0] = "0"; return Descriptions.Wetness(c.Path("vagina[" + s[0] + "]/wetness")); } },
				{ "pussylooseness", (c, s) => { return Descriptions.Looseness(c.Path("vagina[" + s[0] + "]/looseness")); } },
				{ "anuslooseness", (c, s) => { return Descriptions.Looseness(c.Path("ass/looseness"), true); } },
				{ "foot", (c, s) => {return Descriptions.Foot(c.GetToken("legs")); } },
				{ "feet", (c, s) => {return Descriptions.Foot(c.GetToken("legs"), true); } },
				{ "cumrand", (c, s) => {return Descriptions.CumRandom(); } },
				{ "equipment", (c, s) => {var i = c.GetEquippedItemBySlot(s[0]); return (s[1] == "color" || s[1] == "c") ? Descriptions.Item(i, i.tempToken, s[2], true) : Descriptions.Item(i, i.tempToken, s[1]); } },
				{ "tonguetype", (c, s) => {return Descriptions.TongueType(c.GetToken("tongue")); } },
				{ "tailtype", (c, s) => {return Descriptions.TailType(c.GetToken("tail")); } },
                { "hipsize", (c, s) => {return Descriptions.HipSize(c.GetToken("hips")); } },
                { "haircolor", (c, s) => {return Descriptions.HairColor(c.GetToken("hair")); } },
                { "hairlength", (c, s) => {return Descriptions.HairLength(c.GetToken("hair")); } },
                { "ballsize", (c, s) => {return Descriptions.BallSize(c.GetToken("balls")); } },
				#endregion

				{ "hand", (c, s) => {return Descriptions.Hand(c); } },
				{ "hands", (c, s) => {return Descriptions.Hand(c, true); } },
			};
			#endregion
			#region [] Parser
			var regex = new Regex(@"
\[
	(?:(?<target>\w):)?		#Optional target and :

	(?:						#One or more subcommands
		(?:\:?)				#Separating :, optional in case target already had one
		(?<subcom>[\w_]+)	#Command
	)*
\]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
			message = regex.Replace(message, (match =>
			{
				var target = bottom;
				var subcom = string.Empty;
				var parms = new List<string>();

				if (!match.Groups["subcom"].Success)
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
						subcom = match.Groups["target"].Value;
						foreach (Capture c in match.Groups[2].Captures)
						{
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
			#endregion

			regex = new Regex(@"{(?:{)? (?<first>\w*)   (?: \| (?<second>\w*) )? }(?:})?", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
			message = regex.Replace(message, (match =>
			{
				return top == player ? (match.Groups["second"].Success ? match.Groups["second"].Value : string.Empty) : match.Groups["first"].Value;
			}));

			return message;
		}
	}
}
