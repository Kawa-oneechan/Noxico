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
				if (toSplit == null || string.IsNullOrWhiteSpace(toSplit.Text))
					return new string[0];
				else
					return toSplit.Text.Split(',').Select(x => x.Trim()).ToArray();
			});

			var namegen = DefaultNameGen;
			if (!string.IsNullOrWhiteSpace(id))
			{
				if (type == NameType.Town && Cultures.ContainsKey(id) && !string.IsNullOrWhiteSpace(Cultures[id].TownName))
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

		//TODO: rework into cultural dialect/accent scripts
		/*
		public static Func<string, string> GetSpeechFilter(Culture culture, Func<string, string> original = null)
		{
			if (original == null)
				original = new Func<string, string>(x => x);
			if (culture.Terms == null || culture.Terms.Count == 0)
				return original;
			return new Func<string, string>(x =>
			{
				foreach (var term in culture.Terms)
				{
					x = x.Replace(term.Key, term.Value);
				}
				return original(x);
			});
		}

		public static Func<string, string> GetSpeechFilter(string culture, Func<string, string> original = null)
		{
			if (i18n.GetString("meta_nospeechfilters")[0] == '[')
				return original;
			if (!Cultures.ContainsKey(culture))
				return new Func<string, string>(x => x);
			return GetSpeechFilter(Cultures[culture], original);
		}
		*/
	}
}
