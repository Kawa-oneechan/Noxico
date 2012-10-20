using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WF = System.Windows.Forms;
using GamerServices;

namespace Noxico
{
	static class Achievements
	{
		public static string ProfilePath { get; set; }
		public static DateTime StartingTime { get; set; }

		private static void NameLoop(bool wasTaken)
		{
			MessageBox.Input(wasTaken ? "That name is already taken. Please try another. If this was your profile and you're running on another system, you may want to try copying the profile from the other system, or visit http://helmet.kafuka.org/noxico/board and ask for a profile download." : "Enter a name for your profile.", "", () =>
			{
				var name = ((string)MessageBox.Answer).Trim();
				if (string.IsNullOrWhiteSpace(name))
					return;
				if (Profile.IsTaken(name))
					NameLoop(true);
				else
				{
					Profile.Create(name);
					SaveProfile();
				}
			});
		}

		public static void Setup()
		{
			if (!IniFile.GetBool("profile", "useonline", true) || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
				return;
			Profile.GameName = "Noxico";

			Profile.RegisterAchievement("nethack", "It's Nethack All Over Again", "Die within five minutes.");
			Profile.RegisterAchievement("yasd", "Yet Another Stupid Death", "Die within five minutes... for the 100th time.");
			Profile.RegisterAchievement("townkiller", "Criminal Scum", "Kill twenty villagers.");

			Profile.OnAchievement += new Action<Achievement>(a =>
			{
				NoxicoGame.AddMessage("Achievement Get: " + a.Name);
				MessageBox.Message(a.Name, false, "Achievement Get!");
			});
			Profile.OnMessage += new Action<string>(m =>
			{
				if (m == "Profile not found." || m == "Your profile is corrupted.")
				{
					MessageBox.Ask("You do not have a gamer profile for this game yet. Would you like to create one?", () =>
					{
						NameLoop(false);
					},
						null);
				}
				else if (m == "Okay to go online?")
				{
					Profile.UseOnline = false;
					if (WF.MessageBox.Show(NoxicoGame.HostForm, "Is it okay to go online to manage your profile?", "Gamer Services", WF.MessageBoxButtons.YesNo) == WF.DialogResult.Yes)
						Profile.UseOnline = true;
				}
				else
					NoxicoGame.AddMessage("Profile: " + m);
			});
			if (LoadProfile())
				Profile.LoadFromServer(Profile.Name);
		}

		public static bool LoadProfile()
		{
			if (!GamerServices.Profile.UseOnline)
				return false;
			return Profile.Load(ProfilePath);
		}
		public static void SaveProfile(bool publish = false)
		{
			if (!GamerServices.Profile.UseOnline)
				return;
			if (NoxicoGame.HostForm.Noxico.Player.PlayingTime.TotalSeconds > 0)
			{
				Profile.SetArbitraryString("playtime", NoxicoGame.HostForm.Noxico.Player.PlayingTime.ToString());
				Profile.SetArbitraryString("lastchar", NoxicoGame.HostForm.Noxico.Player.Character.ToString());
			}
			Profile.Save(ProfilePath);
			if (publish)
				Profile.Publish();
		}

		public static void CheckYASD()
		{
			if ((DateTime.Now - Achievements.StartingTime).Duration().Minutes < 5)
				Profile.UnlockAchievement("nethack");
			var times = Profile.GetArbitraryInt("nethackcount");
			times++;
			Profile.SetArbitraryInt("nethackcount", times);
			if (times == 100)
				Profile.UnlockAchievement("yasd");
		}

		public static void CheckCriminalScum()
		{
			var times = Profile.GetArbitraryInt("criminalscum");
			times++;
			Profile.SetArbitraryInt("criminalscum", times);
			if (times == 20)
				Profile.UnlockAchievement("townkiller");
		}

	}
}
