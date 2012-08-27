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

		public static void Setup()
		{
			if (!GamerServices.Profile.UseOnline)
				return;
			Profile.GameName = "Noxico";

			Profile.RegisterAchievement("nethack", "It's Nethack All Over Again", "Die within five minutes.");
			Profile.RegisterAchievement("yasd", "Yet Another Stupid Death", "Die within five minutes... for the 100th time.");

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
						//TODO: add a MessageBox.Input() to get the profile name.
						Profile.Create(Environment.UserName);
						SaveProfile();
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
	}
}
