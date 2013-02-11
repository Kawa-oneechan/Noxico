using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace GamerServices
{
	public struct Achievement
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public string IconRef { get; set; }
	}

	public static class Profile
	{
		public static Uri Server { get; private set; }
		private static string profilePath, settingsFile;

		public static string GameName { get; set; }
		public static bool AskForOnline { get; set; }
		public static bool UseOnline { get; set; }

		public static bool IsValid { get; private set; }
		public static long LastSave { get; private set; }
		public static string Name { get; private set; }
		public static Action<string> OnMessage { get; set; }
		public static Action<Achievement> OnAchievement { get; set; }

		private static Dictionary<string, string> arbitraries;
		private static Dictionary<string, Achievement> knownAchievements;
		private static List<string> unlockedAchievements;

		static Profile()
		{
			GameName = "<ERROR>";

			unlockedAchievements = new List<string>();
			arbitraries = new Dictionary<string, string>();
			knownAchievements = new Dictionary<string, Achievement>();
			AskForOnline = false;
			UseOnline = false;

			Server = new Uri(Noxico.IniFile.GetString("profile", "server", "http://helmet.kafuka.org/gamerservice.php"));
		}

		public static void Prepare()
		{
			profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kafuka");
			if (!Directory.Exists(profilePath))
				Directory.CreateDirectory(profilePath);
			settingsFile = Path.Combine(profilePath, "settings");
			if (File.Exists(settingsFile))
			{
				var settings = File.ReadAllBytes(settingsFile);
				AskForOnline = settings[0] == 1;
				UseOnline = settings[1] == 1;
			}
			else
			{
				AskForOnline = true;
				UseOnline = true;
			}
		}

		public static void Create(string name)
		{
			Name = name;
			IsValid = true;
			if (OnMessage != null)
				OnMessage("Profile created.");
		}

		public static bool Load(string fileName = "")
		{
			if (string.IsNullOrWhiteSpace(profilePath))
				fileName = "profile";
			if (fileName == "")
				fileName = Path.Combine(profilePath, GameName + "_profile");
			if (!File.Exists(fileName))
			{
				if (OnMessage != null)
					OnMessage("Profile not found.");
				return false;
			}
			using (var f = File.Open(fileName, FileMode.Open))
			{
				using (var stream = new BinaryReader(f))
				{
					{
						var header = new string(stream.ReadChars(20));
						if (header != "Kafuka Gamer Profile")
						{
							if (OnMessage != null)
								OnMessage("Your profile is corrupted.");
							stream.Close();
							return false;
						}
						Name = stream.ReadString();
						LastSave = stream.ReadInt64();
						unlockedAchievements.Clear();
						arbitraries.Clear();
						try
						{
							var numAchievements = stream.ReadInt16();
							for (var i = 0; i < numAchievements; i++)
								unlockedAchievements.Add(stream.ReadString());
							var numArbitraries = stream.ReadInt16();
							for (var i = 0; i < numArbitraries; i++)
								arbitraries.Add(stream.ReadString(), string.Empty);
							var keys = arbitraries.Keys.ToArray();
							foreach (var k in keys)
								arbitraries[k] = stream.ReadString();
						}
						catch (Exception)
						{
							if (OnMessage != null)
							{
								OnMessage("Your profile is corrupted.");
								return false;
							}
							else
								throw;
						}
						IsValid = true;
						return true;
					}
				}
			}
		}

		public static void Save(string fileName = "")
		{
			if (!IsValid)
				return;
			if (string.IsNullOrWhiteSpace(profilePath) && fileName == "")
				fileName = "profile";
			if (fileName == "")
				fileName = Path.Combine(profilePath, GameName + "_profile");

			LastSave = DateTime.Now.ToUniversalTime().ToBinary();
			using (var f = File.Open(fileName, FileMode.Create))
			{
				using (var stream = new BinaryWriter(f))
				{
					stream.Write("Kafuka Gamer Profile".ToCharArray());
					stream.Write(Name);
					stream.Write(LastSave);
					stream.Write((Int16)unlockedAchievements.Count);
					unlockedAchievements.ForEach(x => stream.Write(x));
					stream.Write((Int16)arbitraries.Count);
					foreach (var x in arbitraries)
						stream.Write(x.Key);
					foreach (var x in arbitraries)
						stream.Write(x.Value);
					stream.Close();
				}
			}
		}

		public static void UnlockAchievement(string achievementID)
		{
			if (!knownAchievements.ContainsKey(achievementID))
				//throw new Exception("Tried to unlock unregistered achievement.");
				return;
			if (!IsValid)
				return;
			if (unlockedAchievements.Contains(achievementID))
				return;
			unlockedAchievements.Add(achievementID);
			if (OnAchievement != null)
				OnAchievement(knownAchievements[achievementID]);
		}

		public static void RegisterAchievement(string id, string name, string description, string iconRef = "")
		{
			if (knownAchievements.ContainsKey(id))
				return;
			knownAchievements.Add(id, new Achievement() { Name = name, Description = description, IconRef = iconRef });
		}

		public static int GetArbitraryInt(string id)
		{
			if (!IsValid)
				return 0;
			if (!arbitraries.ContainsKey(id))
				return 0;
			int i = 0;
			if (int.TryParse(arbitraries[id], out i))
				return i;
			return 0;
		}

		public static void SetArbitraryInt(string id, int value)
		{
			if (!IsValid)
				return;
			if (!arbitraries.ContainsKey(id))
				arbitraries.Add(id, value.ToString());
			else
				arbitraries[id] = value.ToString();
		}

		public static string GetArbitraryString(string id)
		{
			if (!IsValid)
				return string.Empty;
			if (!arbitraries.ContainsKey(id))
				return string.Empty;
			return arbitraries[id];
		}

		public static void SetArbitraryString(string id, string value)
		{
			if (!IsValid)
				return;
			if (!arbitraries.ContainsKey(id))
				arbitraries.Add(id, value);
			else
				arbitraries[id] = value;
		}

		public static bool IsConnected()
		{
			var online = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
			if (!online && Server.Host != "localhost")
				return false;
			if (AskForOnline)
			{
				AskForOnline = false;
				OnMessage("Okay to go online?");
				return UseOnline;
			}
			return UseOnline;
		}

		public static bool LoadFromServer(string name)
		{
			if (!IsConnected())
			{
				return false;
			}
			var client = new WebClient();
			var data = new System.Collections.Specialized.NameValueCollection();
			data.Add("r", name);
			data.Add("g", GameName);
			var response = client.UploadValues(Server, data);
			var respText = new string(Array.ConvertAll<byte, char>(response, (b) => { return (char)b; }));
			if (respText.StartsWith("MSG:"))
			{
				//if (OnMessage != null)
				//	OnMessage(respText.Substring(4));
				return false;
			}
			else
			{
				var timestamp = long.Parse(respText.Substring(0, respText.IndexOf('\n')).Trim());
				if (DateTime.FromBinary(timestamp).ToUniversalTime() < DateTime.FromBinary(LastSave).ToUniversalTime())
					return false;
				var filename = "publishingCopy";
				File.WriteAllBytes(filename, Convert.FromBase64String(respText.Substring(respText.IndexOf('\n')).Trim()));
				var ret = Load(filename);
				File.Delete(filename);
				return ret;
			}
		}

		public static void Publish()
		{
			if (!IsValid)
				return;
			if (!IsConnected())
			{
				return;
			}
			var filename = "publishingCopy";
			Save(filename);
			var raw = Convert.ToBase64String(File.ReadAllBytes(filename));
			File.Delete(filename);
			var client = new WebClient();
			var data = new System.Collections.Specialized.NameValueCollection();
			data.Add("p", Name);
			data.Add("g", GameName);
			data.Add("r", raw);
			data.Add("l", LastSave.ToString());
			var response = client.UploadValues(Server, data);
			var respText = new string(Array.ConvertAll<byte, char>(response, (b) => { return (char)b; }));
			if (respText.StartsWith("MSG:") && OnMessage != null)
				OnMessage(respText.Substring(4));
		}

		public static bool IsTaken(string name)
		{
			if (!IsConnected())
				return false;
			var client = new WebClient();
			var response = client.DownloadString(Server + "?t=" + name);
			if (response == "MSG:Profile name taken.")
				return true;
			return false;
		}
	}
}
