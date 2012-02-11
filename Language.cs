using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Noxico
{
	[Obsolete("Use the Culture system instead.")]
	public enum WordSearchType
	{
		Anything,
		Noun,
		Verb,
		Adjective,
	}

	[Obsolete("Use the Culture system instead.")]
	public enum KnownLanguage
	{
		Human, Goblin, Demonic
	}

	[Obsolete("Use the Culture system instead.")]
	public class Word
	{
		private Token theWord;

		public string Root;
		public List<Token> Tokens;
		
		public Word(Token word)
		{
			theWord = word;
			Root = theWord.Name.ToLowerInvariant();
			Tokens = word.Tokens;
		}

		public override string ToString()
		{
			//TODO: give singular if noun, "to foo" if verb?
			if (HasToken("Verb"))
				return GetToken("Verb").GetToken("I").Tokens[0].Name;
			if (HasToken("Noun"))
				return GetToken("Noun").Tokens[0].Tokens[0].Name;
			if (HasToken("Adjective"))
				return GetToken("Adjective").Tokens[0].Name;
			return theWord.Name;
		}

		public bool HasToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			return t != null;
		}

		public Token GetToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			return t;
		}

		public void RemoveToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			if (t != null)
				Tokens.Remove(t);
		}
	}

	[Obsolete("Use the Culture system instead.", true)]
	class Language
	{
		private static List<Token> dict;
		private static List<Token> symbols;
		private static List<Token> recentEntries = new List<Token>();

		private static void LoadDictionary()
		{
		}
		private static void LoadSymbols()
		{
		}

		private static Dictionary<KnownLanguage, Dictionary<string, string>> xenoDic = new Dictionary<KnownLanguage, Dictionary<string, string>>();

		public static string TranslateWord(Word word, KnownLanguage language)
		{
			if (language == KnownLanguage.Human)
				return word.Root;
			if (!xenoDic.ContainsKey(language))
			{
				var words = new Dictionary<string, string>();
				var resName = "Language" + language.ToString();
				var resString = global::Noxico.Properties.Resources.ResourceManager.GetString(resName);
				if (string.IsNullOrWhiteSpace(resString))
					return word.Root; //or throw new Exception("Tried to look up a translation for a known language without a dictionary.");
				var data = resString.Split('\n');
				foreach (var entry in data)
				{
					var parts = entry.Trim().Split('\t');
					words.Add(parts[0].ToLowerInvariant(), parts[1].ToLowerInvariant());
				}
				xenoDic.Add(language, words);
			}
			if (xenoDic[language].ContainsKey(word.Root))
				return xenoDic[language][word.Root];

			//Just give up and speak English.
			return word.Root;
		}

		public static Word GetWordBySymbol(string symbol, WordSearchType type = WordSearchType.Anything)
		{
			LoadDictionary();
			LoadSymbols();
			var s = symbols.Find(x => x.Name.Equals(symbol, StringComparison.InvariantCultureIgnoreCase));
			if (s == null)
				throw new Exception("Unknown symbol.");
			Token ret = null;
			if (type == WordSearchType.Anything)
			{
				ret = s.Tokens[Toolkit.Rand.Next(s.Tokens.Count)];
			}
			else
			{
				var t = type.ToString();
				var attempt = s.Tokens[Toolkit.Rand.Next(s.Tokens.Count)];
				var entry = dict.Find(x => x.Name == attempt.Name);
				while (!entry.HasToken(t) && !recentEntries.Contains(attempt))
				{
					attempt = s.Tokens[Toolkit.Rand.Next(s.Tokens.Count)];
					entry = dict.Find(x => x.Name == attempt.Name);
				}
				ret = entry;
			}

			recentEntries.Add(ret);
			if (recentEntries.Count == 20)
				recentEntries.RemoveAt(0);

			return new Word(ret);
		}

		public static Word GetWordByString(string word)
		{
			LoadDictionary();
			var wordTypes = new[] { "Noun", "Adjective", "Verb" };
			foreach (var entry in dict)
			{
				if (entry.Name.Equals(word, StringComparison.InvariantCultureIgnoreCase))
					return new Word(entry);
				else
				{
					foreach (var wordType in wordTypes)
						if (entry.HasToken(wordType))
							foreach (var x in entry.GetToken(wordType).Tokens)
								foreach (var y in x.Tokens)
									if (y.Name.Equals(word, StringComparison.InvariantCultureIgnoreCase))
										return new Word(entry);
				}
			}
			return null;
		}
	}
}
