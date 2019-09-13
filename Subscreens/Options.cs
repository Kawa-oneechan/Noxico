using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public static class Options
	{
		private static UIWindow window;
		private static UIButton saveButton, cancelButton, keysButton, openButton;
		private static UITextBox speed, musicVolume, soundVolume, screenCols, screenRows;
		private static UIList font;
		private static UIToggle rememberPause, vistaSaves, xInput, imperial, fourThirtySeven, enableAudio;

		public static void Handler()
		{
			if (Subscreens.FirstDraw)
			{
				Subscreens.FirstDraw = false;
				UIManager.Initialize();
				UIManager.Elements.Clear();

				window = new UIWindow(i18n.GetString("opt_title"))
				{
					Left = 0,
					Top = 0,
					Width = 80,
					Height = 25,
				};
				window.Center();

				var speedLabel = new UILabel(i18n.GetString("opt_speed"));
				speedLabel.Move(3, 2, window);
				speed = new UITextBox(IniFile.GetValue("misc", "speed", "15"))
				{
					Width = 4,
					Numeric = true
				};
				speed.Move(1, 1, speedLabel);

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
				var fontLabel = new UILabel(i18n.GetString("opt_font"));
				fontLabel.Move(0, 3, speedLabel);
				font = new UIList(string.Empty, null, fonts, currentFontIndex)
				{
					Width = 20,
					Height = 8,
				};
				font.Move(1, 1, fontLabel);
				font.Enter = (s, e) =>
				{
					var previousFont = font.Text;
					IniFile.SetValue("misc", "font", font.Text);
					NoxicoGame.HostForm.RestartGraphics(false);
					IniFile.SetValue("misc", "font", previousFont);
				};
				font.EnsureVisible();

				var screenColsLabel = new UILabel(i18n.GetString("opt_screencols"));
				screenColsLabel.MoveBelow(-1, 1, font);
				screenCols = new UITextBox(IniFile.GetValue("misc", "screencols", "80"))
				{
					Width = 4,
					Numeric = true
				};
				screenCols.Move(1, 1, screenColsLabel);
				var screenRowsLabel = new UILabel(i18n.GetString("opt_screenrows"));
				screenRowsLabel.Move(-1, 1, screenCols);
				screenRows = new UITextBox(IniFile.GetValue("misc", "screenrows", "25"))
				{
					Width = 4,
					Numeric = true
				};
				screenRows.Move(1, 1, screenRowsLabel);
				screenCols.Enter = screenRows.Enter = (s, e) =>
				{
					var resetGraphics = false;
					var i = int.Parse(screenCols.Text);
					if (i < 80)
						i = 80;
					if (i > 300)
						i = 300;
					if (i != Program.Cols)
						resetGraphics = true;
					Program.Cols = i;
					IniFile.SetValue("misc", "screencols", i);
					i = int.Parse(screenRows.Text);
					if (i < 25)
						i = 25;
					if (i > 100)
						i = 100;
					if (i != Program.Rows)
						resetGraphics = true;
					Program.Rows = i;
					IniFile.SetValue("misc", "screenrows", i);
					if (resetGraphics)
					{
						NoxicoGame.HostForm.RestartGraphics(true);
						window.Center();
						UIManager.ReMove();
					}
					Subscreens.Redraw = true;
					NoxicoGame.Me.CurrentBoard.AimCamera();
					NoxicoGame.Me.CurrentBoard.Redraw();
					NoxicoGame.Me.CurrentBoard.Draw();
				};
	
				var miscWindow = new UIWindow(i18n.GetString("opt_misc"))
				{
					Width = 50,
					Height = 11,
				};
				miscWindow.MoveBeside(2, 0, speedLabel);

				rememberPause = new UIToggle(i18n.GetString("opt_rememberpause"))
				{
					Checked = IniFile.GetValue("misc", "rememberpause", true),
					Background = Color.Transparent,
				};
				rememberPause.Move(2, 1, miscWindow);

				vistaSaves = new UIToggle(i18n.GetString("opt_vistasaves"))
				{
					Checked = IniFile.GetValue("misc", "vistasaves", true),
					Enabled = Vista.IsVista,
					Background = Color.Transparent,
				};
				vistaSaves.MoveBelow(0, 1, rememberPause);
				
				xInput = new UIToggle(i18n.GetString("opt_xinput"))
				{
					Checked = IniFile.GetValue("misc", "xinput", true),
					Enabled = Vista.IsVista,
					Background = Color.Transparent,
				};
				xInput.MoveBelow(0, 1, vistaSaves);

				imperial = new UIToggle(i18n.GetString("opt_imperial"))
				{
					Checked = IniFile.GetValue("misc", "imperial", false),
					Background = Color.Transparent,
				};
				imperial.MoveBelow(0, 1, xInput);

				fourThirtySeven = new UIToggle(i18n.GetString("opt_437"))
				{
					Checked = IniFile.GetValue("misc", "437", false),
					Background = Color.Transparent,
				};
				fourThirtySeven.MoveBelow(0, 1, imperial);

				var audioWindow = new UIWindow(i18n.GetString("opt_audio"))
				{
					Width = 30,
					Height = 8,
				};
				audioWindow.MoveBelow(0, 1, miscWindow);

				enableAudio = new UIToggle(i18n.GetString("opt_enableaudio"))
				{
					Checked = IniFile.GetValue("audio", "enabled", true),
					Background = Color.Transparent,
				};
				enableAudio.Move(2, 1, audioWindow);

				var musicVolumeLabel = new UILabel(i18n.GetString("opt_musicvolume"));
				musicVolumeLabel.MoveBelow(0, 1, enableAudio);
				musicVolume = new UITextBox(IniFile.GetValue("audio", "musicvolume", "100"));
				musicVolume.Move(1, 1, musicVolumeLabel);
				var soundVolumeLabel = new UILabel(i18n.GetString("opt_soundvolume"));
				soundVolumeLabel.Move(-1, 1, musicVolume);
				soundVolume = new UITextBox(IniFile.GetValue("audio", "soundvolume", "100"));
				soundVolume.Move(1, 1, soundVolumeLabel);

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

						var resetGraphics = false;
						i = int.Parse(screenCols.Text);
						if (i < 80)
							i = 80;
						if (i > 300)
							i = 300;
						if (i != Program.Cols)
							resetGraphics = true;
						Program.Cols = i;
						IniFile.SetValue("misc", "screencols", i);
						i = int.Parse(screenRows.Text);
						if (i < 25)
							i = 25;
						if (i > 100)
							i = 100;
						if (i != Program.Rows)
							resetGraphics = true;
						Program.Rows = i;
						IniFile.SetValue("misc", "screenrows", i);

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

						if (resetGraphics)
							NoxicoGame.HostForm.RestartGraphics(true);

						IniFile.Save(string.Empty);
						cancelButton.DoEnter();
					}) { Width = 16 };
				saveButton.MoveBeside(2, 0, audioWindow);
				keysButton = new UIButton(i18n.GetString("opt_keys"), (s, e) =>
				{
					Controls.Open();
				}) { Width = 16 };
				keysButton.MoveBelow(0, 1, saveButton);
				openButton = new UIButton(i18n.GetString("opt_open"), (s, e) =>
					{
						System.Diagnostics.Process.Start(NoxicoGame.HostForm.IniPath);
					}) { Width = 16 };
				openButton.MoveBelow(0, 1, keysButton);
				cancelButton = new UIButton(i18n.GetString("opt_cancel"), (s, e) =>
					{
						UIManager.Elements.Clear();
						NoxicoGame.ClearKeys();
						NoxicoGame.Immediate = true;
						NoxicoGame.Me.CurrentBoard.Redraw();
						NoxicoGame.Me.CurrentBoard.Draw(true);
						NoxicoGame.Mode = UserMode.Walkabout;
						Subscreens.FirstDraw = true;
					}) { Width = 16 };
				cancelButton.MoveBelow(0, 1, openButton);

				UIManager.Elements.Add(window);
				UIManager.Elements.Add(speedLabel);
				UIManager.Elements.Add(speed);
				UIManager.Elements.Add(fontLabel);
				UIManager.Elements.Add(font);
				UIManager.Elements.Add(screenColsLabel);
				UIManager.Elements.Add(screenCols);
				UIManager.Elements.Add(screenRowsLabel);
				UIManager.Elements.Add(screenRows);
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
				UIManager.Elements.Add(keysButton);
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
