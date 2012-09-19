using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace Noxico
{
	public class Culture
	{
		private static XmlDocument xDoc;
		public string ID;
		public string[] Bodyplans;
		public double Marriage = 0.25, Monogamous = 1;

		public enum NameType
		{
			Male, Female, Surname, Town, Location
		}

		public static Culture DefaultCulture;

		public static Dictionary<string, Culture> Cultures;

		static Culture()
		{
			Console.WriteLine("Loading cultures...");
			Cultures = new Dictionary<string, Culture>();
			xDoc = Mix.GetXMLDocument("culture.xml");
			//xDoc = new XmlDocument();
			//xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Cultures, "culture.xml"));
			foreach (var c in xDoc.SelectNodes("//culture").OfType<XmlElement>())
				Cultures.Add(c.GetAttribute("id"), Culture.FromXml(c));
			DefaultCulture = Cultures["default"];
		}

		private static Culture FromXml(XmlElement x)
		{
			var nc = new Culture();
			nc.ID = x.GetAttribute("id");
			var info = x.SelectSingleNode("cultureinfo") as XmlElement;
			if (info == null)
			{
				Console.WriteLine("Culture \"{0}\" has no cultureinfo element.", nc.ID);
				return nc;
			}
			var plans = new List<string>();
			foreach (var plan in info.SelectSingleNode("bodyplans").ChildNodes.OfType<XmlElement>())
				plans.Add(plan.GetAttribute("name"));
			nc.Bodyplans = plans.ToArray();
			var marriage = info.SelectSingleNode("marriage") as XmlElement;
			var monogamous = info.SelectSingleNode("monogamous") as XmlElement;
			if (marriage != null)
				nc.Marriage = double.Parse(marriage.InnerText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
			if (monogamous != null)
				nc.Monogamous = double.Parse(monogamous.InnerText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
			return nc;
		}

		private string[] TrySelect(string name, XmlNode parent)
		{
			var n = parent.SelectSingleNode(name);
			if (n == null)
				return new string[0];
			return n.InnerText.Trim().Split(',');
		}

		public string GetName(NameType type)
		{
			var namegen = "//culture[@id='" + ID + "']/namegen";
			var rand = Toolkit.Rand;
			var sets = new Dictionary<string, string[]>();
			var x = xDoc.SelectNodes(namegen + "/set");
			foreach(var set in x.OfType<XmlElement>())
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
				var rule = rules[rand.Next(rules.Count)];
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
						if (rand.Next(100) > chance)
							continue;
					}
					if (!sets.ContainsKey(part.GetAttribute("id")))
						continue;
					var list = sets[part.GetAttribute("id")];
					var word = list[rand.Next(list.Length)].Trim();
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

		public static string GetName(Culture culture, NameType type)
		{
			return culture.GetName(type);
		}

		public static string GetName(string culture, NameType type)
		{
			if (Cultures.ContainsKey(culture))
				return Cultures[culture].GetName(type);
			return DefaultCulture.GetName(type);
		}
	}
}
