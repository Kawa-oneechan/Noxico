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
		private static List<Token> wordStructor;
		private static List<Token> impediments;
		private static Dictionary<string, Func<Character, IList<string>, Neo.IronLua.LuaResult>> SubCommands;

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

#if DEBUG
			var sanityCheck = new[] {
				"A|pie|a",
				"A|apple|an",
				"P|hoof|hooves",
				"S|hooves|hoof",
				"P|cheap piece of shit|cheap pieces of shit",
				"S|cheap pieces of shit|cheap piece of shit",
				"P|vortex|vortices",
				"S|cortices|cortex",
				"P|pegasus|pegasori",
				"S|pegasori|pegasus",
				"P|alga|algæ",
				"S|kunai|kunai",
				"P|kunai of the dawn|kunai of the dawn",
				"p|Kawa|Kawa's",
				"p|it|its",
				"p|Sassafrass|Sassafrass'",
			};
			foreach (var test in sanityCheck.Select(t => t.Split('|')))
			{
				var checkFrom = test[1];
				var checkTo = test[2];
				var result = string.Empty;
				if (test[0] == "P")
					result = Pluralize(checkFrom, 2);
				else if (test[0] == "S")
					result = Singularize(checkFrom);
				else if (test[0] == "A")
					result = GetArticle(checkFrom);
				else if (test[0] == "p")
					result = Possessive(checkFrom);
				if (result != checkTo)
					throw new Exception(string.Format("Sanity check on pluralizer failed. Expected \"{0}\" but got \"{1}\".", checkTo, result));
			}
#endif
		}

		public static string Entitize(string input)
		{
			var longhand = !input.Contains('\uE2FE');
			for (var i = 0; i <= 0x10; i++)
				input = input.Replace(((char)(0xE200 + i)).ToString(), Toolkit.TranslateKey((KeyBinding)i, longhand));
			if (NoxicoGame.Me.Player != null && NoxicoGame.Me.Player.Character != null)
				input = input.Replace("\uE220", NoxicoGame.Me.Player.Character.Name.ToString());
			else
				input = input.Replace("\uE220", "????");
			input = input.Replace("\uE2FE", string.Empty);
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
			if (words.ContainsKey(singular))
				singular = words[singular];
			var result = Lua.Environment.Pluralize(singular);
			return result.ToString();
		}

		public static string Pluralize(this string singular, int count)
		{
			if (count == 1)
				return singular;
			return Pluralize(singular);
		}

		public static string Singularize(this string plural)
		{
			if (words.ContainsKey(plural))
				plural = words[plural];
			var result = Lua.Environment.Singularize(plural);
			return result.ToString();
		}

		/// <summary>
		/// From Nethack. Returns the ordinal suffix for the given number -- insert 4, get "th" as in "4th".
		/// </summary>
		public static string Ordinal(this int number)
		{
			var result = Lua.Environment.Ordinal(number);
			return result.ToString();
		}
		public static string Ordinal(this float number)
		{
			return ((int)Math.Floor(number)).Ordinal();
		}

		public static string Possessive(this string subject)
		{
			var result = Lua.Environment.Possessive(subject);
			return result.ToString();
		}

		public static string GetArticle(this string topic, bool capitalize = false)
		{
			return Lua.Environment.GetArticle(topic, capitalize);
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

		public static void RegisterVPTags(Neo.IronLua.LuaTable table)
		{
			SubCommands = new Dictionary<string, Func<Character, IList<string>, Neo.IronLua.LuaResult>>();
			foreach (var item in table)
			{
				var v = item.Value as Func<Character, IList<string>, Neo.IronLua.LuaResult>;
				if (v == null)
					continue;
				SubCommands.Add(item.Key.ToString(), v);
			}
		}

		public static string Viewpoint(this string message, Character top, Character bottom = null)
		{
#if DEBUG
			var player = NoxicoGame.Me.Player == null ? null : NoxicoGame.Me.Player.Character;
#else
			var player = NoxicoGame.Me.Player.Character;
#endif
			if (top == null)
				top = player;
			if (bottom == null)
				bottom = top;
			//var tIP = player == top;

			//Definitions used to be here. Now they're defined in i18n.lua.

			#region WordStructor filter
			var wordStructFilter = new Func<Token, Character, bool>((filter, who) =>
			{
				var env = Lua.Environment;
				env.cultureID = who.Culture.ID;
				env.culture = who.Culture;
				env.gender = who.Gender;
				foreach (var stat in env.stats)
				{
					var statName = ((Neo.IronLua.LuaTable)stat.Value)["name"].ToString().ToLowerInvariant();
					env[statName] = who.GetStat(statName);
				}

				env.pussyAmount = who.HasToken("vagina") ? (who.GetToken("vagina").HasToken("dual") ? 2 : 1) : 0;
				env.penisAmount = who.HasToken("penis") ? (who.GetToken("penis").HasToken("dual") ? 2 : 1) : 0;
				env.pussyWetness = who.HasToken("vagina") && who.GetToken("vagina").HasToken("wetness") ? who.GetToken("vagina").GetToken("wetness").Value : 0;
				env.cumAmount = who.CumAmount;
				env.slime = who.IsSlime;
				//return env.DoChunk("return " + filter.Text, "lol.lua").ToBoolean();
				return Lua.Run("return " + filter.Text, env);
			});
			#endregion

			#region {} parser
			var regex = new Regex(@"{(?:{)? (?<first>\w*)   (?: \| (?<second>\w*) )? }(?:})?", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
			message = regex.Replace(message, (match => top == player ? (match.Groups["second"].Success ? match.Groups["second"].Value : string.Empty) : match.Groups["first"].Value));
			#endregion
			#region [] parser
			regex = new Regex(@"
\[
	(?:(?<target>[tb\?]{1,2}):)?	#Optional target and :

	(?:								#One or more subcommands
		(?:\:?)						#Separating :, optional in case target already had one
		(?<subcom>[\w\/\-_\{\}]+)	#Command
	)*
\]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);

			var allMatches = regex.Matches(message);

			while (regex.IsMatch(message))
			{
				message = regex.Replace(message, (match =>
				{
					var target = bottom;
					var subcom = match.Groups["subcom"].Captures[0].Value;
					var parms = new List<string>();

					var targetGroup = match.Groups["target"].Value;

					if (targetGroup.StartsWith('?'))
					{
						if (i18n.wordStructor == null)
							i18n.wordStructor = Mix.GetTokenTree("wordstructor.tml", true);

						if (targetGroup.Length == 2 && "tb".Contains(targetGroup[1]))
							target = (targetGroup[1] == 't' ? top : bottom);

						Lua.Environment.top = top;
						Lua.Environment.bottom = bottom;
						Lua.Environment.target = target;

						var pToks = wordStructor.Where(x => x.Name == match.Groups["subcom"].Value).ToList();
						if (pToks.Count == 0)
							return string.Format("[[WordStructor fail: {0}]]", match.Groups["subcom"].Value);
						var pTok = pToks.PickWeighted(); //pToks[Random.Next(pToks.Count)];
						var pRes = pTok.Tokens.Where(x => !x.HasToken("filter") || wordStructFilter(x.GetToken("filter"), target)).ToList();
						//Remove all less-specific options if any specific are found.
						if (pRes.Any(x => x.HasToken("filter")))
							pRes.RemoveAll(x => !x.HasToken("filter"));
						return pRes.PickOne().Text;
					}
					else if (targetGroup.StartsWith('t'))
					{
						target = top;
					}

					Lua.Environment.isPlayer = (target == player);
					Lua.Environment.target = target;

					//subcom = targetGroup;
					//subcom = match.Groups["subcom"].Captures[0].Value;
					for (var i = 1; i < match.Groups["subcom"].Captures.Count; i++)
					{
						var c = match.Groups["subcom"].Captures[i];
						//Console.WriteLine(c);
						parms.Add(c.Value.Replace('(', '[').Replace(')', ']'));
					}

					parms.Add(string.Empty);
					parms.Add(string.Empty);
					parms.Add(string.Empty);

					//if (subcoms.ContainsKey(subcom)) return subcoms[subcom](target, parms);
					if (SubCommands.ContainsKey(subcom)) return SubCommands[subcom](target, parms).ToString();
					return string.Format("(?{0}?)", subcom);
				}));
			}
			message = Regex.Replace(message, @"\[\!(?<keybinding>.+?)\]", (match => Toolkit.TranslateKey(match.Groups["keybinding"].Value)));
			#endregion

			if (!message.Contains('"'))
				return message;

			if (top == null)
				return message;

			SpeechFilter speechFilter = top.SpeechFilter;
			if (speechFilter == null)
			{
				if (top.Culture.SpeechFilter != null)
					speechFilter = new SpeechFilter(x =>
					{
						Lua.RunFile(top.Culture.SpeechFilter);
						x = Lua.Environment.SpeechFilter(x);
						return x;
					});
				if (impediments == null)
					impediments = Mix.GetTokenTree("impediments.tml");
				foreach (var impediment in impediments)
				{
					var apply = true;
					foreach (var filter in impediment.Tokens.Where(t => t.Name == "have"))
					{
						var f = filter.Text.Split('=');
						var p = top.Path(f[0]);
						if (p == null || p.Text != f[1])
						{
							apply = false;
							break;
						}
					}
					if (apply)
					{
						var oldFilter = speechFilter;
						speechFilter = new SpeechFilter(x =>
						{
							Lua.RunFile(impediment.GetToken("file").Text);
							x = Lua.Environment.SpeechFilter(x);
							return oldFilter(x);
						});
					}
				}
				if (speechFilter == null) //Still?
					speechFilter = new SpeechFilter(x => x); //Then just assign a dummy so we don't do this all over and over again.
				top.SpeechFilter = speechFilter;
			}
			message = message.SmartQuote(speechFilter);

			return message;
		}
	}

	public delegate string SpeechFilter(string input);
}
