using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Noxico
{
	class Dialogue
	{
		private static XmlDocument xDoc;
		private static Character player, target;

		public static void Engage(Character player, Character target, string topic = "hello")
		{
			if (xDoc == null)
			{
				xDoc = new XmlDocument();
				xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Dialogue, "dialogue.xml"));
			}
			/*
			 * When engaging a character in dialogue, collect all the infos for the "hello" opening topic (where an opening topic is any topic element without a text attribute).
			 * For each info, in order of appearance, check any filters it may have. If all of them match, parse out the p elements in order of appearance.
			 * Now, find all the non-opening topics (those that do have a text attribute), and check their filters. If they all match, add that topic to a list of responses.
			 * If the list of responses is empty, there is only one paragraph worth of text to display, and it's less than 80 characters long (?), use the Message system.
			 * Otherwise, show the lines in a window, named after the target, with all the possible responses in a list below.
			 * Picking a response repeats the process with the response's topic.
			 */

			Dialogue.player = player;
			Dialogue.target = target;
			var openings = FindOpenings(topic);
			var info = openings.FirstOrDefault(i => FiltersOkay(i)); //FindFirstMatchingInfo(openings);
			var message = ExtractParagraphs(info);

#if DEBUG
			//player.Path("stimulation").Value = 50;
#endif
			var replies = FindReplies();
			foreach (var reply in replies)
			{
				message += "<cGray>(option: " + reply.GetAttribute("text") + " [" + reply.GetAttribute("id") + "])\r\n";
			}

			foreach (var s in info.ChildNodes.OfType<XmlElement>().Where(s => s.Name == "script"))
			{
				Console.WriteLine("------\nSCRIPT\n------");
				var script = s.InnerText.Replace("$speaker", target.Name.ToID()).Split('\n');
				var boardchar = NoxicoGame.HostForm.Noxico.CurrentBoard.Entities.OfType<BoardChar>().First(x => x.Character == target); 
				boardchar.ScriptRunning = true;
				boardchar.ScriptPointer = 0;
				Noxicobotic.Run(boardchar, script);
				boardchar.ScriptRunning = false;
			}

			MessageBox.Message(message, true, target.Name.ToString(true));
			//TODO: allow selecting a reply from a list. Either make this a function of MessageBox, or make it unique to Dialogue with the UI Subsystem.
		}

		private static List<XmlElement> FindOpenings(string topicId)
		{
			var ret = new List<XmlElement>();
			foreach (var topic in xDoc.SelectNodes("//topic").OfType<XmlElement>().Where(t => t.GetAttribute("id") == topicId))
				foreach (var info in topic.ChildNodes.OfType<XmlElement>().Where(i => i.Name == "info"))
					ret.Add(info);
			return ret;
		}

		private static List<XmlElement> FindReplies()
		{
			var ret = new List<XmlElement>();
			foreach (var topic in xDoc.SelectNodes("//topic").OfType<XmlElement>().Where(t => t.HasAttribute("text") && FiltersOkay(t)))
				ret.Add(topic);
			return ret;
		}

		private static string ExtractParagraphs(XmlElement info)
		{
			var ret = new StringBuilder();
			foreach (var p in info.ChildNodes.OfType<XmlElement>().Where(p => p.Name == "p"))
			{
				ret.AppendLine(p.InnerText.Trim());
				ret.AppendLine();
			}
			return ret.ToString();
		}

		private static bool FiltersOkay(XmlElement subject)
		{
			foreach (var filter in subject.ChildNodes.OfType<XmlElement>().Where(f => f.Name == "filter"))
			{
				var fType = filter.GetAttribute("type");
				var fName = filter.GetAttribute("name");
				var fValue = filter.GetAttribute("value");
				var fPrimary = filter.HasAttribute("target") ? (filter.GetAttribute("target") == "player" ? player : target) : target;
				var fSecondary = fPrimary == player ? target : player;
				var fValueF = 0f;
				float.TryParse(fValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fValueF);
				switch (fType)
				{
					case "has":
						if (fPrimary.Path(fName) == null)
							return false;
						break;
					case "hasnot":
						if (fPrimary.Path(fName) != null)
							return false;
						break;
					case "stat":
					case "value_gteq":
						if (fPrimary.Path(fName).Value < fValueF)
							return false;
						break;
					case "value_equal":
						if (fPrimary.Path(fName).Value != fValueF)
							return false;
						break;
					case "value_lower":
						if (fPrimary.Path(fName).Value >= fValueF)
							return false;
						break;
					case "relation":
						var path = "ships/" + fSecondary.Name + "/" + fValue;
						if (fPrimary.Path(path) == null)
							return false;
						break;
					case "gender":
						if (fValue == "male" && fPrimary.GetGender() != "male")
							return false;
						else if (fValue == "female" && fPrimary.GetGender() != "female")
							return false;
						break;
				}
			}
			return true;
		}
	}
}
