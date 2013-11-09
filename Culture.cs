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
		public string[] Bodyplans { get; private set; }
		public double Marriage  { get; private set; }
		public double Monogamous { get; private set; }
		public Dictionary<string, string> Terms { get; private set; }

		public static List<Deity> Deities;

		public static Culture DefaultCulture;
		public static Token DefaultNameGen;

		public static Dictionary<string, Culture> Cultures;
		public static Dictionary<string, Token> NameGens;

		static Culture()
		{
			Program.WriteLine("Loading deities...");
			Deities = new List<Deity>();
			var deities = Mix.GetTokenTree("deities.tml");
			foreach (var deity in deities.Where(t => t.Name == "deity"))
				Deities.Add(new Deity(deity));
			
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

		public static Culture FromToken(Token t)
		{
			var nc = new Culture();
			nc.ID = t.Text;
			nc.Bodyplans = t.GetToken("bodyplans").Tokens.Select(x => x.Name).ToArray();
			nc.Marriage = t.HasToken("marriage") ? t.GetToken("marriage").Value : 0.0f;
			nc.Monogamous = t.HasToken("monogamous") ? t.GetToken("monogamous").Value : 0.0f;
			nc.TownName = t.HasToken("townname") ? t.GetToken("townname").Text : null;
			if (t.HasToken("terms"))
			{
				nc.Terms = new Dictionary<string, string>();
				foreach (var term in t.GetToken("terms").Tokens)
					nc.Terms[term.Name.Replace('_', ' ')] = term.Text;
			}
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
			if (!string.IsNullOrWhiteSpace(id) && NameGens.ContainsKey(id))
				namegen = NameGens[id];
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
				var rule = rules[Random.Next(rules.Length)];
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
						var word = list[Random.Next(list.Length)];
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

		public static bool CheckSummoningDay()
		{
			var today = NoxicoGame.InGameTime;
			var month = today.Month;
			var day = today.Day;
			var deity = Deities.Find(d => d.CanSummon && d.SummonDay == day && d.SummonMonth == month);
			if (deity == null)
				return false;
			var summon = new Character();
			summon.Name = new Name(deity.Name);
			summon.IsProperNamed = true;
			SceneSystem.Engage(NoxicoGame.HostForm.Noxico.Player.Character, summon, deity.DialogueHook);
			return true;
		}

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
	}
	
	public class Deity
	{
		public string Name { get; private set; }
		public Color Color { get; private set; }
		public bool CanSummon { get; private set; }
		public string DialogueHook { get; private set; }
		public int SummonMonth { get; private set; }
		public int SummonDay { get; private set; }
		public Deity(Token t)
		{
			Name = t.HasToken("_n") ? t.GetToken("_n").Text : t.Text.Replace('_', ' ').Titlecase();
			Color = Color.FromName(t.GetToken("color").Text);
			CanSummon = false;
			var month = t.GetToken("month");
			if (month != null)
			{
				CanSummon = true;
				SummonMonth = (int)month.Value - 1;
				SummonDay = (int)t.GetToken("day").Value - 1;
				DialogueHook = t.GetToken("dialogue").Text;
			}
		}
		public override string ToString()
		{
			return Name;
		}
	}

	public class NoxicanDate
	{
		private static string[] months;
		static NoxicanDate()
		{
			months = i18n.GetArray("months");
		}

		public int Day { get; private set; }
		public int Month { get; private set; }
		public int Year { get; private set; }
		public int Hour { get; private set; }
		public int Minute { get; private set; }
		public int Second { get; private set; }
		public int Millisecond { get; private set; }
		public int DayOfYear { get { return (Month * 30) + Day; } }

		public NoxicanDate()
		{
		}

		public NoxicanDate(long val)
		{
			Year = (int)((long)(val >> 47) & 2047);
			Month = (int)((long)(val >> 36) & 15);
			Day = (int)((long)(val >> 31) & 31);
			Hour = (int)(val >> 23) & 31;
			Minute = (int)(val >> 17) & 63;
			Second = (int)(val >> 7) & 63;
			Millisecond = (int)(val >> 0) & 127;
		}

		public NoxicanDate(int year, int month, int day)
		{
			AddYears(year);
			AddMonths(month - 1);
			AddDays(day - 1);
		}

		public NoxicanDate(int year, int month, int day, int hour, int minute, int second)
		{
			AddYears(year);
			AddMonths(month - 1);
			AddDays(day - 1);
			AddHours(hour);
			AddMinutes(minute);
			AddSeconds(second);
		}

		public long ToBinary()
		{
			//........ ........ YYYYYYYY YYYMMMM. DDDDD HHHHHMMM MMMSSSSS Smmmmmmm
			//                  |        |        |        |
			//                  47       39       31       23
			var ret = 0UL;
			ret |= (ulong)Year << 47;
			ret |= (ulong)Month << 36;
			ret |= (ulong)Day << 31;
			ret |= (ulong)Hour << 23;
			ret |= (ulong)Minute << 17;
			ret |= (ulong)Second << 7;
			ret |= (uint)Millisecond;
			return (long)ret;
		}

		public static NoxicanDate FromBinary(long val)
		{
			return new NoxicanDate(val);
		}

		public void Add(TimeSpan time)
		{
			if (time == null)
				return;
			if (time.Days > 0) this.AddDays((int)time.Days);
			if (time.Hours > 0) this.AddHours((int)time.Hours);
			if (time.Minutes > 0) this.AddMinutes((int)time.Minutes);
			if (time.Seconds > 0) this.AddSeconds((int)time.Seconds);
			if (time.Milliseconds > 0) this.AddMilliseconds((int)time.Milliseconds);
		}

		public void AddMilliseconds(int milliseconds)
		{
			this.Millisecond += milliseconds;
			while (this.Millisecond >= 100)
			{
				this.Millisecond -= 100;
				this.AddSeconds(1);
			}
		}

		public void AddSeconds(int seconds)
		{
			this.Second += seconds;
			while (this.Second >= 60)
			{
				this.Second -= 60;
				this.AddMinutes(1);
			}
		}

		public void AddMinutes(int minutes)
		{
			this.Minute += minutes;
			if (this.Minute >= 60)
			{
				this.Minute -= 60;
				this.AddHours(1);
			}
		}

		public void AddHours(int hours)
		{
			this.Hour += hours;
			while (this.Hour >= 24)
			{
				this.Hour -= 24;
				this.AddDays(1);
			}
		}

		public void AddDays(int days)
		{
			this.Day += days;
			while (this.Day >= 30)
			{
				this.Day -= 30;
				this.AddMonths(1);
			}
		}

		public void AddMonths(int months)
		{
			this.Month += months;
			if (this.Month >= 12)
			{
				this.Month -= 12;
				this.AddYears(1);
			}
		}

		public void AddYears(int years)
		{
			this.Year += years;
		}

		public override string ToString()
		{
			return this.ToShortDateString() + ' ' + this.ToShortTimeString();
		}

		public string ToLongDateString()
		{
			return string.Format("{0}{1} of {2}, {3} AI", Day + 1, Ordinal(Day + 1), months[Month], Year);
		}

		public string ToShortDateString()
		{
			return string.Format("{0:00}/{1:00}/{2:0000}", Day + 1, Month + 1, Year);
		}

		public string ToShortTimeString()
		{
			return string.Format("{0:00}:{1:00}:{2:00}", Hour, Minute, Second);
		}

		private static string Ordinal(int number)
		{
			var i = number;
			var dd = i % 10;
			return (dd == 0 || dd > 3 || (i % 100) / 10 == 1) ? "th" : (dd == 1) ? "st" : (dd == 2) ? "nd" : "rd";
		}

		public void SetMidday()
		{
			Hour = 13;
		}

		public static bool operator >=(NoxicanDate left, NoxicanDate right)
		{
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
				return false;
			return (left.ToBinary() >= right.ToBinary());
		}
		public static bool operator <=(NoxicanDate left, NoxicanDate right)
		{
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
				return false;
			return (left.ToBinary() <= right.ToBinary());
		}
		public static bool operator ==(NoxicanDate left, NoxicanDate right)
		{
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
				return false;
			return (left.ToBinary() == right.ToBinary());
		}
		public static bool operator !=(NoxicanDate left, NoxicanDate right)
		{
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
				return false;
			return (left.ToBinary() != right.ToBinary());
		}
		public static bool operator >(NoxicanDate left, NoxicanDate right)
		{
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
				return false;
			return (left.ToBinary() > right.ToBinary());
		}
		public static bool operator <(NoxicanDate left, NoxicanDate right)
		{
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
				return false;
			return (left.ToBinary() < right.ToBinary());
		}
		public override bool Equals(object right)
		{
			if (!(right is NoxicanDate))
				return false;
			return (this.ToBinary() == (right as NoxicanDate).ToBinary());
		}
		public override int GetHashCode()
		{
			return (int)this.ToBinary();
		}
	}
}
