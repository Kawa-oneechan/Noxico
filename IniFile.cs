using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Noxico
{
	class IniFile
	{
		private static Dictionary<string, Dictionary<string, string>> settings = new Dictionary<string,Dictionary<string,string>>();

		public static void Load(string filename)
		{
			settings.Clear();
			var thisSection = "";
			var lines = File.ReadAllLines(filename);
			foreach (var line in lines)
			{
				var l = line;
				if (l.Contains(';'))
					l = l.Remove(l.IndexOf(';'));
				if (string.IsNullOrWhiteSpace(l))
					continue;
				if (l.StartsWith("[") && l.EndsWith("]"))
				{
					var key = l.Trim('[', ']');
					settings.Add(key, new Dictionary<string, string>());
					thisSection = key;
				}
				else if (l.Contains('=') && thisSection != "")
				{
					var sep = l.IndexOf('=');
					var key = l.Substring(0, sep).Trim();
					var val = l.Substring(sep + 1).Trim();
					if (settings[thisSection].ContainsKey(key))
					{
						throw new Exception("There's an error in the INI file: the key \"" + key + "\" in section \"" + thisSection + "\" has already been used in that section.");
						//settings[thisSection][key] = val;
					}
					else
						settings[thisSection].Add(key, val);
				}
			}
		}

		public static void Save(string filename)
		{
			if (!File.Exists(filename))
			{
				var sb = new StringBuilder("");
				foreach (var section in settings)
				{
					sb.AppendFormat("[{0}]", section.Key);
					foreach (var entry in section.Value)
					{
						sb.AppendLine();
						sb.AppendFormat("{0}={1}", entry.Key, entry.Value);
					}
					sb.AppendLine();
					sb.AppendLine();
				}
				File.WriteAllText(filename, sb.ToString());
			}
			else
			{
				var lines = File.ReadAllLines(filename).Select(l => l.Trim()).ToArray();
				foreach (var section in settings)
				{
					for (var i = 0; i < lines.Length; i++)
					{
						if (lines[i].StartsWith("[" + section.Key + "]"))
						{
							var sStart = i + 1;
							var sEnd = lines.Length;
							for (i = sStart; i < lines.Length; i++)
							{
								if (lines[i].StartsWith("["))
								{
									sEnd = i;
									break;
								}
							}
							foreach (var entry in section.Value)
							{
								for (i = sStart; i < sEnd; i++)
								{
									if (lines[i].Contains('=') && lines[i].StartsWith(entry.Key))
									{
										var comment = string.Empty;
										if (lines[i].Contains(';'))
											comment = ' ' + lines[i].Substring(lines[i].IndexOf(';'));
										lines[i] = entry.Key + "=" + entry.Value + comment;
										break;
									}
								}
							}
							break;
						}
					}
				}
				File.WriteAllLines(filename, lines);
			}
		}

		public static string GetString(string section, string key, string def)
		{
			if (settings.ContainsKey(section) && settings[section].ContainsKey(key))
				return settings[section][key];
			return def;
		}

		public static int GetInt(string section, string key, int def)
		{
			if (settings.ContainsKey(section) && settings[section].ContainsKey(key))
			{
				int i = 0;
				if (int.TryParse(settings[section][key], out i))
					return i;
			}
			return def;
		}

		public static bool GetBool(string section, string key, bool def)
		{
			if (settings.ContainsKey(section) && settings[section].ContainsKey(key))
			{
				bool i = false;
				if (bool.TryParse(settings[section][key].Titlecase(), out i))
					return i;
			}
			return def;
		}

		public static void SetValue(string section, string key, object value)
		{
			if (!settings.ContainsKey(section))
				settings.Add(section, new Dictionary<string,string>());
			if (!settings[section].ContainsKey(key))
				settings[section].Add(key, value.ToString());
			else
				settings[section][key] = value.ToString();
		}
	}
}
