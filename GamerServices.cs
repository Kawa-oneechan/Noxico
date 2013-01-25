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
		public static List<string> Achievements { get; private set; }
		public static Dictionary<string, string> Arbitraries { get; private set; }
		public static Dictionary<string, Achievement> KnownAchievements { get; private set; }
		public static Action<string> OnMessage { get; set; }
		public static Action<Achievement> OnAchievement { get; set; }

		static Profile()
		{
			Achievements = new List<string>();
			Arbitraries = new Dictionary<string, string>();
			KnownAchievements = new Dictionary<string, Achievement>();
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

		public static bool Load(string filename = "")
		{
			if (string.IsNullOrWhiteSpace(profilePath))
				filename = "profile";
			if (filename == "")
				filename = Path.Combine(profilePath, GameName + "_profile");
			if (!File.Exists(filename))
			{
				if (OnMessage != null)
					OnMessage("Profile not found.");
				return false;
			}
			using (var f = File.Open(filename, FileMode.Open))
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
						Achievements.Clear();
						Arbitraries.Clear();
						try
						{
							var numAchievements = stream.ReadInt16();
							for (var i = 0; i < numAchievements; i++)
								Achievements.Add(stream.ReadString());
							var numArbitraries = stream.ReadInt16();
							for (var i = 0; i < numArbitraries; i++)
								Arbitraries.Add(stream.ReadString(), string.Empty);
							var keys = Arbitraries.Keys.ToArray();
							foreach (var k in keys)
								Arbitraries[k] = stream.ReadString();
						}
						catch (Exception x)
						{
							if (OnMessage != null)
							{
								OnMessage("Your profile is corrupted.");
								return false;
							}
							else
								throw x;
						}
						IsValid = true;
						return true;
					}
				}
			}
		}

		public static void Save(string filename = "")
		{
			if (!IsValid)
				return;
			if (string.IsNullOrWhiteSpace(profilePath) && filename == "")
				filename = "profile";
			if (filename == "")
				filename = Path.Combine(profilePath, GameName + "_profile");

			LastSave = DateTime.Now.ToUniversalTime().ToBinary();
			using (var f = File.Open(filename, FileMode.Create))
			{
				using (var stream = new BinaryWriter(f))
				{
					stream.Write("Kafuka Gamer Profile".ToCharArray());
					stream.Write(Name);
					stream.Write(LastSave);
					stream.Write((Int16)Achievements.Count);
					Achievements.ForEach(x => stream.Write(x));
					stream.Write((Int16)Arbitraries.Count);
					foreach (var x in Arbitraries)
						stream.Write(x.Key);
					foreach (var x in Arbitraries)
						stream.Write(x.Value);
					stream.Close();
				}
			}
		}

		public static void UnlockAchievement(string achievementID)
		{
			if (!KnownAchievements.ContainsKey(achievementID))
				//throw new Exception("Tried to unlock unregistered achievement.");
				return;
			if (!IsValid)
				return;
			if (Achievements.Contains(achievementID))
				return;
			Achievements.Add(achievementID);
			if (OnAchievement != null)
				OnAchievement(KnownAchievements[achievementID]);
		}

		public static void RegisterAchievement(string id, string name, string description, string iconRef = "")
		{
			if (KnownAchievements.ContainsKey(id))
				return;
			KnownAchievements.Add(id, new Achievement() { Name = name, Description = description, IconRef = iconRef });
		}

		public static int GetArbitraryInt(string id)
		{
			if (!IsValid)
				return 0;
			if (!Arbitraries.ContainsKey(id))
				return 0;
			int i = 0;
			int.TryParse(Arbitraries[id], out i);
			return i;
		}

		public static void SetArbitraryInt(string id, int value)
		{
			if (!IsValid)
				return;
			if (!Arbitraries.ContainsKey(id))
				Arbitraries.Add(id, value.ToString());
			else
				Arbitraries[id] = value.ToString();
		}

		public static string GetArbitraryString(string id)
		{
			if (!IsValid)
				return string.Empty;
			if (!Arbitraries.ContainsKey(id))
				return string.Empty;
			return Arbitraries[id];
		}

		public static void SetArbitraryString(string id, string value)
		{
			if (!IsValid)
				return;
			if (!Arbitraries.ContainsKey(id))
				Arbitraries.Add(id, value);
			else
				Arbitraries[id] = value;
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
