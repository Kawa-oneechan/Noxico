using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public class Culture
	{
		public enum NameType
		{
			Male, Female, Surname, Town, Location
		}

		public string ID { get; private set; }
		public string TownName { get; private set; }
		public List<Token> Bodyplans { get; private set; }
		public double Marriage { get; private set; }
		public double Monogamous { get; private set; }
		public Dictionary<string, string> Terms { get; private set; }
		public string SpeechFilter { get; private set; }

		public static Culture DefaultCulture;
		public static Token DefaultNameGen;

		public static Dictionary<string, Culture> Cultures;
		public static Dictionary<string, Token> NameGens;

		public override string ToString()
		{
			return ID;
		}

		static Culture()
		{
			Program.WriteLine("Loading cultures...");
			Cultures = new Dictionary<string, Culture>();
			NameGens = new Dictionary<string, Token>();
			var cultures = Mix.GetTokenTree("culture.tml");
			foreach (var c in cultures.Where(t => t.Name == "culture"))
				Cultures.Add(c.Text, Culture.FromToken(c));
			DefaultCulture = Cultures.ElementAt(0).Value;
			foreach (var c in cultures.Where(t => t.Name == "namegen"))
				NameGens.Add(c.Text, c);
			DefaultNameGen = NameGens.ElementAt(0).Value;
		}

		public static Culture FindCultureByName(string s)
		{
			if (Cultures.ContainsKey(s))
				return Cultures[s];
			else
				return Culture.DefaultCulture;
		}

		public static Culture FromToken(Token t)
		{
			var nc = new Culture();
			nc.ID = t.Text;
			nc.Bodyplans = t.GetToken("bodyplans").Tokens;
			nc.Marriage = t.HasToken("marriage") ? t.GetToken("marriage").Value : 0.0f;
			nc.Monogamous = t.HasToken("monogamous") ? t.GetToken("monogamous").Value : 0.0f;
			nc.TownName = t.HasToken("townname") ? t.GetToken("townname").Text : null;
			if (t.HasToken("terms"))
				nc.Terms = t.GetToken("terms").Tokens.ToDictionary(x => x.Name.Replace('_', ' '), x => x.Text);
			if (t.HasToken("speechfilter"))
				nc.SpeechFilter = t.GetToken("speechfilter").Text;
			return nc;
		}

		public static string GetName(string id, NameType type)
		{
			Func<Token, string[]> split = new Func<Token, string[]>(toSplit =>
			{
				if (toSplit == null || toSplit.Text.IsBlank())
					return new string[0];
				else
					return toSplit.Text.Split(',').Select(x => x.Trim()).ToArray();
			});

			var namegen = DefaultNameGen;
			if (!id.IsBlank())
			{
				if (type == NameType.Town && Cultures.ContainsKey(id) && !Cultures[id].TownName.IsBlank())
					namegen = NameGens[Cultures[id].TownName];
				else if (NameGens.ContainsKey(id))
					namegen = NameGens[id];
			}
			var sets = new Dictionary<string, string[]>();
			foreach (var set in namegen.GetToken("sets").Tokens)
				sets.Add(set.Name, split(set));
			var typeName = type.ToString().ToLowerInvariant();
			var typeSet = namegen.GetToken(typeName);
			if (typeSet == null)
				return GetName(DefaultNameGen.Text, type);
			if (typeSet.HasToken("copy"))
				return GetName(typeSet.GetToken("copy").Text, type);

			if (type == NameType.Surname)
			{
				var patro = typeSet.GetToken("patronymic");
				if (patro != null)
					return "#patronym/" + patro.GetToken("male").Text + "/" + patro.GetToken("female").Text;
			}

			var prohibit = split(typeSet.GetToken("prohibit"));
			var rules = typeSet.Tokens.Where(x => x.Name == "rule").ToArray();
			while (true)
			{
				var rule = rules.PickOne();
				var name = new StringBuilder();
				foreach (var part in rule.Tokens)
				{
					if (part.Name == "markov")
						name.Append(Markov(part));
					if (part.Name == "_")
						name.Append(' ');
					else if (part.Name == "$")
						name.Append(part.Text);
					else if (sets.ContainsKey(part.Name))
					{
						if (part.Value > 0 && Random.Next(100) > part.Value)
							continue;
						var list = sets[part.Name];
						var word = list.PickOne();
						name.Append(word);
					}
				}
				var reject = false;
				var lowerName = name.ToString().ToLowerInvariant();
				foreach (var p in prohibit)
				{
					if (lowerName.Contains(p))
					{
						reject = true;
						break;
					}
				}
				if (!reject)
					return name.ToString().Trim();
			}
		}

		private static string Markov(Token settings)
		{
			var order = (int)settings.GetToken("order").Value;
			var minLength = (int)settings.GetToken("minlength").Value;
			var samples = settings.GetToken("sourcenames").Text.Split(',').Select(n => n.Trim()).ToArray();
			var chains = new Dictionary<string, List<char>>();

			Func<string, char> getLetter = new Func<string, char>(token =>
			{
				if (!chains.ContainsKey(token))
					return '?';
				List<char> letters = chains[token];
				return letters.PickOne();
			});

			foreach (string word in samples)
			{
				for (int letter = 0; letter < word.Length - order; letter++)
				{
					var token = word.Substring(letter, order);
					List<char> entry = null;
					if (chains.ContainsKey(token))
						entry = chains[token];
					else
					{
						entry = new List<char>();
						chains[token] = entry;
					}
					entry.Add(word[letter + order]);
				}
			}

			var ret = string.Empty;
			do
			{
				var n = Random.Next(samples.Length);
				int nameLength = samples[n].Length;
				//ret = (samples[n].Substring(Random.Next(0, samples[n].Length - order), order));
				ret = samples[n].Substring(0, order);
				while (ret.Length < nameLength)
				{
					string token = ret.Substring(ret.Length - order, order);
					char c = getLetter(token);
					if (c != '?')
						ret += getLetter(token);
					else
						break;
				}

				if (ret.Contains(' '))
				{
					string[] tokens = ret.Split(' ');
					ret = string.Empty;
					for (int t = 0; t < tokens.Length; t++)
					{
						if (tokens[t].IsBlank())
							continue;
						if (tokens[t].Length == 1)
							tokens[t] = tokens[t].ToUpper();
						else
							tokens[t] = tokens[t].Substring(0, 1) + tokens[t].Substring(1).ToLower();
						if (!ret.IsBlank())
							ret += ' ';
						ret += tokens[t];
					}
				}
				else
					ret = ret.Substring(0, 1) + ret.Substring(1).ToLower();
			}
			while (/* _used.Contains(s) || */ ret.Length < minLength);
			return ret;
		}
	}
}
