using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Noxico
{
	public class Culture
	{
		public enum NameType
		{
			Male, Female, Surname, Town, Location
		}

		private static XmlDocument xDoc;
		public string ID { get; private set; }
		public string TownName { get; private set; }
		public string[] Bodyplans { get; private set; }
		public double Marriage  { get; private set; }
		public double Monogamous { get; private set; }
		public Dictionary<string, string> Terms { get; private set; }

		public static List<Deity> Deities;

		public static Culture DefaultCulture;
		public static string DefaultNameGen;

		public static Dictionary<string, Culture> Cultures;
		public static List<string> NameGens;

		static Culture()
		{
			Program.WriteLine("Loading deities...");
			Deities = new List<Deity>();
			xDoc = Mix.GetXmlDocument("deities.xml");
			foreach (var d in xDoc.SelectNodes("//deity").OfType<XmlElement>())
				Deities.Add(new Deity(d)); 
			
			Program.WriteLine("Loading cultures...");
			Cultures = new Dictionary<string, Culture>();
			NameGens = new List<string>();
			xDoc = Mix.GetXmlDocument("culture.xml");
			foreach (var c in xDoc.SelectNodes("//culture").OfType<XmlElement>())
				Cultures.Add(c.GetAttribute("id"), Culture.FromXml(c));
			DefaultCulture = Cultures.ElementAt(0).Value;
			foreach (var c in xDoc.SelectNodes("//namegen").OfType<XmlElement>())
				NameGens.Add(c.GetAttribute("id"));
			DefaultNameGen = NameGens[0];
		}

		public static Culture FromXml(XmlElement x)
		{
			var nc = new Culture();
			nc.ID = x.GetAttribute("id");
			var info = x.SelectSingleNode("cultureinfo") as XmlElement;
			if (info == null)
			{
				Program.WriteLine("Culture \"{0}\" has no cultureinfo element.", nc.ID);
				return nc;
			}
			var plans = new List<string>();
			foreach (var plan in info.SelectSingleNode("bodyplans").ChildNodes.OfType<XmlElement>())
				plans.Add(plan.GetAttribute("name"));
			nc.Bodyplans = plans.ToArray();
			var marriage = info.SelectSingleNode("marriage") as XmlElement;
			var monogamous = info.SelectSingleNode("monogamous") as XmlElement;
			var terms = info.SelectSingleNode("terms") as XmlElement;
			if (marriage != null)
				nc.Marriage = double.Parse(marriage.InnerText, System.Globalization.NumberStyles.Float);
			if (monogamous != null)
				nc.Monogamous = double.Parse(monogamous.InnerText, System.Globalization.NumberStyles.Float);
			if (terms != null)
			{
				nc.Terms = new Dictionary<string, string>();
				foreach (var term in terms.ChildNodes.OfType<XmlElement>())
					nc.Terms[term.GetAttribute("from")] = term.GetAttribute("to");
			}
			return nc;
		}

		private static string[] TrySelect(string name, XmlNode parent)
		{
			var n = parent.SelectSingleNode(name);
			if (n == null)
				return new string[0];
			return n.InnerText.Trim().Split(',');
		}

		public static string GetName(string id, NameType type)
		{
			if (string.IsNullOrWhiteSpace(id))
				id = DefaultNameGen;
			var namegen = "//namegen[@id='" + id + "']";
			var sets = new Dictionary<string, string[]>();
			var x = xDoc.SelectNodes(namegen + "/set");
			foreach (var set in x.OfType<XmlElement>())
				sets.Add(set.GetAttribute("id"), set.InnerText.Trim().Split(','));
			var typeName = type.ToString().ToLowerInvariant();
			var typeSet = xDoc.SelectSingleNode(namegen + "/" + typeName) as XmlElement;
			if (typeSet.HasAttribute("copy"))
				return GetName(typeSet.GetAttribute("copy"), type);

			if (type == NameType.Surname)
			{
				var patro = typeSet.SelectSingleNode("patronymic") as XmlElement;
				if (patro != null)
					return "#patronym/" + patro.GetAttribute("malesuffix") + "/" + patro.GetAttribute("femalesuffix");
			}

			var illegal = TrySelect("illegal", typeSet);
			var rules = typeSet.SelectNodes("rules/rule");
			while (true)
			{
				var rule = rules[Random.Next(rules.Count)];
				var name = "";
				foreach (var part in rule.ChildNodes.OfType<XmlElement>())
				{
					if (part.Name == "space")
					{
						name += ' ';
						continue;
					}
					if (part.HasAttribute("chance"))
					{
						var chance = int.Parse(part.GetAttribute("chance"));
						if (Random.Next(100) > chance)
							continue;
					}
					if (!sets.ContainsKey(part.GetAttribute("id")))
						continue;
					var list = sets[part.GetAttribute("id")];
					var word = list[Random.Next(list.Length)].Trim();
					name += word;
				}
				var reject = false;
				foreach (var i in illegal)
				{
					if (name.ToLowerInvariant().Contains(i.Trim()))
					{
						reject = true;
						break;
					}
				}
				if (!reject)
					return name.Trim();
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

	public class NameGenerator
	{
		public string ID;

		public static NameGenerator FromXml(XmlElement x)
		{
			var ng = new NameGenerator();
			ng.ID = x.GetAttribute("id");
			var info = x.SelectSingleNode("cultureinfo") as XmlElement;
			if (info == null)
			{
				Program.WriteLine("Culture \"{0}\" has no cultureinfo element.", ng.ID);
				return ng;
			}
			return ng;
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
		public Deity(XmlElement x)
		{
			Name = x.GetAttribute("name");
			Color = Color.FromName(x.GetAttribute("color"));
			CanSummon = false;
			var summon = x.SelectSingleNode("date") as XmlElement;
			if (summon != null)
			{
				CanSummon = true;
				SummonMonth = int.Parse(summon.GetAttribute("month")) - 1;
				SummonDay = int.Parse(summon.GetAttribute("day")) - 1;
				DialogueHook = ((XmlElement)x.SelectSingleNode("dialogue")).GetAttribute("id");
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
