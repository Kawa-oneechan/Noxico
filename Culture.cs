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
		private string id;

		public enum NameType
		{
			Male, Female, Town, Location
		}

		public static Culture DefaultCulture;

		private static Dictionary<string, Culture> cultures;

		static Culture()
		{
			cultures = new Dictionary<string, Culture>();
			xDoc = new XmlDocument();
			if (File.Exists(IniFile.GetString("misc", "culturefile", "culture.xml")))
				xDoc.Load(IniFile.GetString("misc", "culturefile", "culture.xml"));
			else
				xDoc.LoadXml(global::Noxico.Properties.Resources.Cultures);
			foreach (var c in xDoc.SelectNodes("//culture").OfType<XmlElement>())
			{
				cultures.Add(c.GetAttribute("id"), Culture.FromXml(c));
			}
			DefaultCulture = cultures["default"];
		}

		private static Culture FromXml(XmlElement x)
		{
			var nc = new Culture();
			nc.id = x.GetAttribute("id");
			//nc.nameGen = x.SelectSingleNode("//culture[@id='" + nc.id + "']/namegen") as XmlElement;
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
			var namegen = "//culture[@id='" + id + "']/namegen";
			var rand = Toolkit.Rand;
			var sets = new Dictionary<string, string[]>();
			var x = xDoc.SelectNodes(namegen + "/set");
			foreach(var set in x.OfType<XmlElement>())
				sets.Add(set.GetAttribute("id"), set.InnerText.Trim().Split(','));
			var typeName = type.ToString().ToLowerInvariant();
			var typeSet = xDoc.SelectSingleNode(namegen + "/" + typeName) as XmlElement;
			if (typeSet.HasAttribute("copy"))
				return GetName(typeSet.GetAttribute("copy"), type);

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
			if (cultures.ContainsKey(culture))
				return cultures[culture].GetName(type);
			return DefaultCulture.GetName(type);
		}
	}
}
