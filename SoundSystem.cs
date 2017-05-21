using System;
using System.Collections.Generic;
using System.Linq;

namespace Noxico
{
	public class SoundSystem
	{
		private class Sound
		{
			private FMOD.System system;
			public FMOD.Sound InnerSound { get; private set; }
			public FMOD.Channel Channel { get; private set; }
			public Sound(string file, FMOD.System system)
			{
				this.system = system;
				FMOD.Sound ns = null;
				var data = Mix.GetBytes(file);
				var fCSex = new FMOD.CreateSoundExInfo()
				{
					Size = 216,
					Length = (uint)data.Length
				};
				SoundSystem.CheckError(system.CreateSound(data, FMOD.SoundMode.Default | FMOD.SoundMode.OpenMemory, ref fCSex, ref ns));
				InnerSound = ns;
			}
			public void Play()
			{
				if (InnerSound == null)
					return;
				var chan = Channel;
				SoundSystem.CheckError(system.PlaySound(InnerSound, false, ref chan));
				Channel = chan;
			}
		}

		private FMOD.System system;
		private Dictionary<string, Sound> sounds;
		private string musicPlaying = "";
		private string currentSet = "";
		private FMOD.Sound music;
		private FMOD.Channel musicChannel;
		private float musicVolume, soundVolume;
		private List<Token> musicLibrary, soundLibrary;

		public float MusicVolume
		{
			get
			{
				return musicVolume;
			}
			set
			{
				if (value < 0)
					musicVolume = 0;
				else if (value > 1)
					musicVolume = 1;
				else
					musicVolume = value;
			}
		}
		public float SoundVolume
		{
			get
			{
				return soundVolume;
			}
			set
			{
				if (value < 0)
					soundVolume = 0;
				else if (value > 1)
					soundVolume = 1;
				else
					soundVolume = value;
			}
		}

		public string FadeTarget { get; private set; }
		private string targetSet;
		private int fadeProcess;

		public bool Enabled
		{
			get
			{
				return system != null;
			}
		}

		public SoundSystem()
		{
			var ret = FMOD.Result.OK;
			system = null;
			Program.WriteLine("SoundSystem: _ctor");
			if (!IniFile.GetValue("audio", "enabled", true))
				return;
			musicVolume = IniFile.GetValue("audio", "musicvolume", 100) / 100f;
			soundVolume = IniFile.GetValue("audio", "soundvolume", 100) / 100f;

			Program.Write("SoundSystem: trying to create FMOD system...");
			try
			{
				FMOD.Factory.CreateSystem(ref system);
			}
			catch (DllNotFoundException)
			{
				Program.WriteLine(" Library not found. Disabling sound.");
				return;
			}
			Program.WriteLine(" " + ret.ToString());
			if (ret != FMOD.Result.OK)
				return;
			Program.Write("SoundSystem: system.init...");
			ret = system.Initialize(8, FMOD.InitFlags.Normal, IntPtr.Zero);
			Program.WriteLine(" " + ret.ToString());
			if (ret != FMOD.Result.OK)
			{
				system = null;
				return;
			}
			music = null;
			musicChannel = null;

			sounds = new Dictionary<string, Sound>();

			Program.WriteLine("SoundSystem: loading libraries...");
			musicLibrary = Mix.GetTokenTree("music.tml");
			soundLibrary = Mix.GetTokenTree("sound.tml");

			Program.WriteLine("SoundSystem: _ctor DONE");
			Program.WriteLine("Null report:");
			if (system == null)
				Program.WriteLine(" * system");
			if (music == null)
				Program.WriteLine(" * music");
			if (musicChannel == null)
				Program.WriteLine(" * musicChannel");
		}

		public void PlayMusic(string name, bool fade = true)
		{
			if (!Enabled || musicVolume == 0)
				return;

			var set = targetSet;
			if (name.StartsWith("set://"))
			{
				set = name.Substring(6);
				var setNode = musicLibrary.FirstOrDefault(x => x.Name == set);
				if (setNode == null)
					name = "-";
				else
				{
					name = "-";
					var unmarked = new List<Token>();
					foreach (var t in setNode.Tokens)
					{
						if (!t.HasToken("used"))
							unmarked.Add(t);
					}
					if (unmarked.Count == 0)
					{
						foreach (var t in setNode.Tokens)
						{
							t.RemoveToken("used");
							unmarked.Add(t);
						}
					}
					while (unmarked.Count > 0)
					{
						var pick = Random.Next(unmarked.Count);
						var file = unmarked[pick].Name;
						if (!Mix.FileExists(file))
							unmarked.RemoveAt(pick);
						else
						{
							name = file;
							unmarked[pick].AddToken("used");
							break;
						}
					}
				}
			}

			if (set == currentSet)
				return;

			if (fade && string.IsNullOrEmpty(FadeTarget) && musicChannel != null && name != musicPlaying)
			{
				FadeTarget = name;
				targetSet = set;
				fadeProcess = 300;
				return;
			}
			FadeTarget = null;
			if (name == "-")
			{
				if (musicChannel != null)
					musicChannel.Stop();
				if (music != null)
					music.Release();
				currentSet = "";
				musicPlaying = "";
			}
			else if (musicPlaying != name)
			{
				if (Mix.FileExists(name))
				{
					if (musicChannel != null)
						musicChannel.Stop();
					if (music != null)
						music.Release();
					var data = Mix.GetBytes(name);
					var fCSex = new FMOD.CreateSoundExInfo()
					{
						Size = 216,
						Length = (uint)data.Length
					};
					CheckError(system.CreateSound(data, FMOD.SoundMode.LoopNormal | FMOD.SoundMode.OpenMemory, ref fCSex, ref music));
					system.PlaySound(music, false, ref musicChannel);
					musicPlaying = name;
					currentSet = set;
					musicChannel.SetVolume(musicVolume);
					//NoxicoGame.HostForm.Text = file;
				}
				else
					Program.WriteLine("PlayMusic: couldn't load song \"{0}\".", name);
			}
		}

		public void PlaySound(string name)
		{
			if (!Enabled || musicVolume == 0)
				return;

			if (name.StartsWith("set://"))
			{
				name = name.Substring(6);
				var setNode = soundLibrary.FirstOrDefault(x => x.Name == name);
				if (setNode == null)
					name = "-";
				else
				{
					name = "-";
					var unmarked = new List<Token>();
					foreach (var t in setNode.Tokens)
					{
						if (!t.HasToken("used"))
							unmarked.Add(t);
					}
					if (unmarked.Count == 0)
					{
						foreach (var t in setNode.Tokens)
						{
							t.RemoveToken("used");
							unmarked.Add(t);
						}
					}
					while (unmarked.Count > 0)
					{
						var pick = Random.Next(unmarked.Count);
						var file = unmarked[pick].Name;
						if (!Mix.FileExists(file))
							unmarked.RemoveAt(pick);
						else
						{
							name = file;
							unmarked[pick].AddToken("used");
							break;
						}
					}
				}
			}

			if (sounds.ContainsKey(name) && sounds[name] == null)
				return;
			if (!sounds.ContainsKey(name))
			{
				if (Mix.FileExists(name))
					sounds.Add(name, new Sound(name, system));
				else
				{
					sounds.Add(name, null);
					return;
				}
			}
			sounds[name].Play();
			sounds[name].Channel.SetVolume(soundVolume);
		}

		private void Fade()
		{
			if (!Enabled)
				return;
			fadeProcess--;
			if (fadeProcess < 0)
				PlayMusic(FadeTarget, false);
			else
			{
				fadeProcess -= 1;
				musicChannel.SetVolume(musicVolume - (1 - ((float)fadeProcess / 300)));
			}
		}

		public void Update()
		{
			if (!Enabled)
				return;
			if (!string.IsNullOrEmpty(FadeTarget))
				Fade();
			system.Update();
#if DEBUG
			if (!IniFile.GetValue("audio", "debug", false))
				return;
			var musicchans = 0;
			var type = FMOD.SoundType.Unknown;
			var format = FMOD.SoundFormat.None;
			var channels = 0;
			var bits = 0;
			var volume = 0f;
			var debug = string.Empty;
			music.GetFormat(out type, out format, out channels, out bits);
			musicChannel.GetVolume(out volume);
			if (type == FMOD.SoundType.Mp3 | type == FMOD.SoundType.OggVorbis)
			{
				var ms = 0u;
				var len = 0u;
				musicChannel.GetPosition(out ms, FMOD.TimeUnit.Milliseconds);
				music.GetLength(out len, FMOD.TimeUnit.Milliseconds);
				debug = string.Format("{0}ch {1}, {2} / {3}. Volume: {4:P0}", musicchans, type.ToString(), new TimeSpan(ms * 10000), new TimeSpan(len * 10000), volume);
			}
			else
			{
				var row = 0u;
				var pattern = 0u;
				var rows = 0u;
				var patterns = 0u;
				music.GetMusicNumChannels(out musicchans);
				musicChannel.GetPosition(out row, FMOD.TimeUnit.ModRow);
				musicChannel.GetPosition(out pattern, FMOD.TimeUnit.ModPattern);
				music.GetLength(out rows, FMOD.TimeUnit.ModRow);
				music.GetLength(out patterns, FMOD.TimeUnit.ModPattern);
				debug = string.Format("{0}ch {1}, row {2}/{3}, pattern {4}/{5}. Volume: {6:P0}", musicchans, type.ToString(), row, rows, pattern, patterns, volume);
			}
			NoxicoGame.HostForm.Write(debug.PadRight(100), System.Drawing.Color.Gray, System.Drawing.Color.Black, 0, 0);
#endif
		}

		public void ShutDown()
		{
			if (!Enabled)
				return;
			if (musicChannel != null)
				musicChannel.Stop();
			if (music != null)
				music.Release();
			if (sounds != null)
			{
				foreach (var s in sounds.Values)
				{
					s.Channel.Stop();
					s.InnerSound.Release();
				}
			}
			system.Close();
			system = null;
		}

		public static void CheckError(FMOD.Result result)
		{
			if (result != FMOD.Result.OK)
				throw new Exception(GetError(result));
		}
		public static string GetError(FMOD.Result result)
		{
			var message = i18n.GetString(string.Format("fmod_{0}", result.ToString().ToLowerInvariant()));
			if (message.StartsWith("[fmod_"))
				return i18n.Format("fmod_genericerror", result);
			return i18n.Format("fmod_error", message);
		}
	}
}
