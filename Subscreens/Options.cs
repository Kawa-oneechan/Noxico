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
		private static UIToggle rememberPause, vistaSaves, xInput, imperial, fourThirtySeven, enableAudio;
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
					Left = 0,
					Top = 0,
					Width = 80,
					Height = 25,
				};

				var speedLabel = new UILabel(i18n.GetString("opt_speed"))
				{
					Left = 3,
					Top = 2,
				};
				speed = new UITextBox(IniFile.GetValue("misc", "speed", "15"))
				{
					Left = 4,
					Top = 3,
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
					Left = 3,
					Top = 5,
				};
				font = new UIList(string.Empty, null, fonts, currentFontIndex)
				{
					Left = 4,
					Top = 6,
					Width = 20,
					Height = 8,
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
					Left = 27,
					Top = 2,
					Width = 50,
					Height = 11,
				};

				rememberPause = new UIToggle(i18n.GetString("opt_rememberpause"))
				{
					Left = 29,
					Top = 3,
					Checked = IniFile.GetValue("misc", "rememberpause", true),
					Background = Color.Transparent,
				};

				vistaSaves = new UIToggle(i18n.GetString("opt_vistasaves"))
				{
					Left = 29,
					Top = 5,
					Checked = IniFile.GetValue("misc", "vistasaves", true),
					Enabled = Vista.IsVista,
					Background = Color.Transparent,
				};
				
				xInput = new UIToggle(i18n.GetString("opt_xinput"))
				{
					Left = 29,
					Top = 7,
					Checked = IniFile.GetValue("misc", "xinput", true),
					Enabled = Vista.IsVista,
					Background = Color.Transparent,
				};

				imperial = new UIToggle(i18n.GetString("opt_imperial"))
				{
					Left = 29,
					Top = 9,
					Checked = IniFile.GetValue("misc", "imperial", false),
					Background = Color.Transparent,
				};

				fourThirtySeven = new UIToggle(i18n.GetString("opt_437"))
				{
					Left = 29,
					Top = 11,
					Checked = IniFile.GetValue("misc", "437", false),
					Background = Color.Transparent,
				};

				var audioWindow = new UIWindow(i18n.GetString("opt_audio"))
				{
					Left = 3,
					Top = 15,
					Width = 32,
					Height = 8,
				};

				enableAudio = new UIToggle(i18n.GetString("opt_enableaudio"))
				{
					Left = 5,
					Top = 16,
					Checked = IniFile.GetValue("audio", "enabled", true),
					Background = Color.Transparent,
				};

				var musicVolumeLabel = new UILabel(i18n.GetString("opt_musicvolume"))
				{
					Left = 5,
					Top = 18
				};
				musicVolume = new UITextBox(IniFile.GetValue("audio", "musicvolume", "100"))
				{
					Left = 6,
					Top = 19,
				};
				var soundVolumeLabel = new UILabel(i18n.GetString("opt_soundvolume"))
				{
					Left = 5,
					Top = 20,
				};
				soundVolume = new UITextBox(IniFile.GetValue("audio", "soundvolume", "100"))
				{
					Left = 6,
					Top = 21,
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
						IniFile.SetValue("misc", "rememberpause", rememberPause.Checked);
						IniFile.SetValue("misc", "vistasaves", vistaSaves.Checked);
						IniFile.SetValue("misc", "xinput", xInput.Checked);
						IniFile.SetValue("misc", "imperial", imperial.Checked);
						IniFile.SetValue("misc", "437", fourThirtySeven.Checked);
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
							if (NoxicoGame.Me.CurrentBoard != null)
								NoxicoGame.Me.CurrentBoard.PlayMusic();
						}

						//if (previousFont != font.Text)
						NoxicoGame.HostForm.RestartGraphics();

						IniFile.Save(string.Empty);
						cancelButton.DoEnter();
					}) { Left = 60, Top = 16, Width = 16 };
				openButton = new UIButton(i18n.GetString("opt_open"), (s, e) =>
					{
						System.Diagnostics.Process.Start(NoxicoGame.HostForm.IniPath);
					}) { Left = 60, Top = 18, Width = 16 };
				cancelButton = new UIButton(i18n.GetString("opt_cancel"), (s, e) =>
					{
						UIManager.Elements.Clear();
						NoxicoGame.ClearKeys();
						NoxicoGame.Immediate = true;
						NoxicoGame.Me.CurrentBoard.Redraw();
						NoxicoGame.Me.CurrentBoard.Draw(true);
						NoxicoGame.Mode = UserMode.Walkabout;
						Subscreens.FirstDraw = true;
					}) { Left = 60, Top = 20, Width = 16 };
				UIManager.Elements.Add(window);
				UIManager.Elements.Add(speedLabel);
				UIManager.Elements.Add(speed);
				UIManager.Elements.Add(fontLabel);
				UIManager.Elements.Add(font);
				UIManager.Elements.Add(miscWindow);
				UIManager.Elements.Add(rememberPause);
				UIManager.Elements.Add(vistaSaves);
				UIManager.Elements.Add(xInput);
				UIManager.Elements.Add(imperial);
				UIManager.Elements.Add(fourThirtySeven);
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
