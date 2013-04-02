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
			var x = Mix.GetXMLDocument("words.xml");
			foreach (var word in x.SelectNodes("//word").OfType<XmlElement>())
				words.Add(word.GetAttribute("id"), word.InnerText);
		}

		public static string Entitize(string input)
		{
			var longhand = !input.Contains('\uE1FE');
			for (var i = 0; i <= 0x10; i++)
				input = input.Replace(((char)(0xE200 + i)).ToString(), Toolkit.TranslateKey((KeyBinding)i, longhand));
			if (NoxicoGame.HostForm.Noxico.Player != null && NoxicoGame.HostForm.Noxico.Player.Character != null)
				input = input.Replace("\uE220", NoxicoGame.HostForm.Noxico.Player.Character.Name.ToString());
			else
				input = input.Replace("\uE220", "????");
			input = input.Replace("\uE1FE", "");
			return input;
		}

		public static string GetString(string key)
		{
			Initialize();
			if (words.ContainsKey(key))
				return Entitize(words[key]);
			return '[' + key + ']';
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

		/// <summary>Gets the number of effective tiles needed to draw the current String.</summary>
		public static int Length(this string input)
		{
			var ret = 0;
			for (var i = 0; i < input.Length; i++)
			{
				var c = input[i];
				if (c == '<' && input[i + 1] == 'c') //skip over color tags
					i = input.IndexOf('>', i + 1);
				else if (i < input.Length - 8 && input.Substring(i, 8) == "<nowrap>") //skip nowrap tag
					i += 8;
				else if ((c >= 0x3000 && c < 0x4000) || (c >= 0x4E00 && c < 0xA000)) //report double the length for Japanese
					ret += 2;
				else if (c >= 0xE000 && c < 0xF900) //skip private use
					i++;
				else
					ret++;
			}
			return ret;
		}

		public static string PadEffective(this string input, int length)
		{
			var lengthNow = input.Length();
			return input + new string(' ', length - lengthNow);
		}

		public static string PadLeftEffective(this string input, int length)
		{
			var lengthNow = input.Length();
			return new string(' ', length - lengthNow) + input;
		}
	}
}
