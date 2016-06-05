using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public static class Options
	{
		private static UIButton saveButton, cancelButton, openButton;
		private static UITextBox speed, musicVolume, soundVolume;
		private static UIList font;
		private static UIToggle skipIntro, rememberPause, vistaSaves, xInput, imperial, enableAudio;
		//private static string previousFont;

		public static void Handler()
		{
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				UIManager.Initialize();
				UIManager.Elements.Clear();

				var window = new UIWindow(i18n.GetString("opt_title"))
				{
					Left = 1,
					Top = 1,
					Width = 92,
					Height = 25,
				};

				var speedLabel = new UILabel(i18n.GetString("opt_speed"))
				{
					Left = 4,
					Top = 3,
				};
				speed = new UITextBox(IniFile.GetValue("misc", "speed", "15"))
				{
					Left = 6,
					Top = 4,
					Width = 4,
					Numeric = true
				};

				var fonts = Mix.GetFilesWithPattern("fonts\\*.png").Select(x => System.IO.Path.GetFileNameWithoutExtension(x)).ToArray();
				var currentFont = IniFile.GetValue("misc", "font", "8x8-thin");
				var currentFontIndex = 0;
				if (fonts.Contains(currentFont))
				{
					for (currentFontIndex = 0; currentFontIndex < fonts.Length; currentFontIndex++)
					{
						if (fonts[currentFontIndex] == currentFont)
							break;
					}
				}
				//previousFont = fonts[currentFontIndex];
				var fontLabel = new UILabel(i18n.GetString("opt_font"))
				{
					Left = 4,
					Top = 6,
				};
				font = new UIList(string.Empty, null, fonts, currentFontIndex)
				{
					Left = 6,
					Top = 7,
					Width = 32,
					Height = 20,
				};
				font.Enter = (s, e) =>
				{
					var previousFont = font.Text;
					IniFile.SetValue("misc", "font", font.Text);
					NoxicoGame.HostForm.RestartGraphics();
					IniFile.SetValue("misc", "font", previousFont);
				};

				var miscWindow = new UIWindow(i18n.GetString("opt_misc"))
				{
					Left = 40,
					Top = 3,
					Width = 50,
					Height = 11,
				};

				skipIntro = new UIToggle(i18n.GetString("opt_skipintro"))
				{
					Left = 42,
					Top = 4,
					Checked = IniFile.GetValue("misc", "skipintro", false),
					Background = Color.Transparent,
				};

				rememberPause = new UIToggle(i18n.GetString("opt_rememberpause"))
				{
					Left = 42,
					Top = 6,
					Checked = IniFile.GetValue("misc", "rememberpause", true),
					Background = Color.Transparent,
				};

				vistaSaves = new UIToggle(i18n.GetString("opt_vistasaves"))
				{
					Left = 42,
					Top = 8,
					Checked = IniFile.GetValue("misc", "vistasaves", true),
					Enabled = Vista.IsVista,
					Background = Color.Transparent,
				};
				
				xInput = new UIToggle(i18n.GetString("opt_xinput"))
				{
					Left = 42,
					Top = 10,
					Checked = IniFile.GetValue("misc", "xinput", true),
					Enabled = Vista.IsVista,
					Background = Color.Transparent,
				};

				imperial = new UIToggle(i18n.GetString("opt_imperial"))
				{
					Left = 42,
					Top = 12,
					Checked = IniFile.GetValue("misc", "imperial", false),
					Background = Color.Transparent,
				};

				var audioWindow = new UIWindow(i18n.GetString("opt_audio"))
				{
					Left = 4,
					Top = 16,
					Width = 32,
					Height = 8,
				};

				enableAudio = new UIToggle(i18n.GetString("opt_enableaudio"))
				{
					Left = 6,
					Top = 17,
					Checked = IniFile.GetValue("audio", "enabled", true),
					Background = Color.Transparent,
				};

				var musicVolumeLabel = new UILabel(i18n.GetString("opt_musicvolume"))
				{
					Left = 6,
					Top = 19
				};
				musicVolume = new UITextBox(IniFile.GetValue("audio", "musicvolume", "100"))
				{
					Left = 8,
					Top = 20,
				};
				var soundVolumeLabel = new UILabel(i18n.GetString("opt_soundvolume"))
				{
					Left = 6,
					Top = 21,
				};
				soundVolume = new UITextBox(IniFile.GetValue("audio", "soundvolume", "100"))
				{
					Left = 8,
					Top = 22,
				};

				saveButton = new UIButton(i18n.GetString("opt_save"), (s, e) =>
					{
						var i = int.Parse(speed.Text);
						if (i < 1)
							i = 1;
						if (i > 200)
							i = 200;
						IniFile.SetValue("misc", "speed", i);
						IniFile.SetValue("misc", "font", font.Text);
						IniFile.SetValue("misc", "skipintro", skipIntro.Checked);
						IniFile.SetValue("misc", "rememberpause", rememberPause.Checked);
						IniFile.SetValue("misc", "vistasaves", vistaSaves.Checked);
						IniFile.SetValue("misc", "xinput", xInput.Checked);
						IniFile.SetValue("misc", "imperial", imperial.Checked);
						Vista.GamepadEnabled = xInput.Checked;

						IniFile.SetValue("misc", "imperial", imperial.Checked);
						IniFile.SetValue("audio", "enabled", enableAudio.Checked);
						i = int.Parse(musicVolume.Text);
						if (i < 0)
							i = 0;
						if (i > 100)
							i = 100;
						NoxicoGame.Sound.MusicVolume = i / 100f;
						IniFile.SetValue("audio", "musicvolume", i);
						i = int.Parse(soundVolume.Text);
						if (i < 0)
							i = 0;
						if (i > 100)
							i = 100;
						NoxicoGame.Sound.SoundVolume = i / 100f;
						IniFile.SetValue("audio", "soundvolume", i);

						if (!enableAudio.Checked && NoxicoGame.Sound != null)
							NoxicoGame.Sound.ShutDown();
						else if (enableAudio.Checked && !NoxicoGame.Sound.Enabled)
						{
							NoxicoGame.Sound = new SoundSystem();
							if (NoxicoGame.HostForm.Noxico.CurrentBoard != null)
								NoxicoGame.HostForm.Noxico.CurrentBoard.PlayMusic();
						}

						//if (previousFont != font.Text)
						NoxicoGame.HostForm.RestartGraphics();

						IniFile.Save();
						cancelButton.DoEnter();
					}) { Left = 74, Top = 19, Width = 16 };
				openButton = new UIButton(i18n.GetString("opt_open"), (s, e) =>
					{
						System.Diagnostics.Process.Start(NoxicoGame.HostForm.IniPath);
					}) { Left = 74, Top = 21, Width = 16 };
				cancelButton = new UIButton(i18n.GetString("opt_cancel"), (s, e) =>
					{
						UIManager.Elements.Clear();
						NoxicoGame.ClearKeys();
						NoxicoGame.Immediate = true;
						NoxicoGame.HostForm.Noxico.CurrentBoard.Redraw();
						NoxicoGame.HostForm.Noxico.CurrentBoard.Draw(true);
						NoxicoGame.Mode = UserMode.Walkabout;
						Subscreens.FirstDraw = true;
					}) { Left = 74, Top = 23, Width = 16 };
				UIManager.Elements.Add(window);
				UIManager.Elements.Add(speedLabel);
				UIManager.Elements.Add(speed);
				UIManager.Elements.Add(fontLabel);
				UIManager.Elements.Add(font);
				UIManager.Elements.Add(miscWindow);
				UIManager.Elements.Add(skipIntro);
				UIManager.Elements.Add(rememberPause);
				UIManager.Elements.Add(vistaSaves);
				UIManager.Elements.Add(xInput);
				UIManager.Elements.Add(imperial);
				UIManager.Elements.Add(audioWindow);
				UIManager.Elements.Add(enableAudio);
				UIManager.Elements.Add(musicVolumeLabel);
				UIManager.Elements.Add(musicVolume);
				UIManager.Elements.Add(soundVolumeLabel);
				UIManager.Elements.Add(soundVolume); 
				UIManager.Elements.Add(saveButton);
				UIManager.Elements.Add(openButton);
				UIManager.Elements.Add(cancelButton);

				Subscreens.FirstDraw = false;
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				UIManager.Draw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				cancelButton.DoEnter();
			}
			else
			{
				UIManager.CheckKeys();
			}
		}

		public static void Open()
		{
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.Subscreen = Options.Handler;
		}
	}
}
