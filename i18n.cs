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
			//TODO: inflect right. THIS IS VERY NAIVE AND STUPID.
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
	}
}
