using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

namespace Noxico
{
	public class SoundSystem
	{
		private class Sound
		{
			private FMOD.System system;
			public FMOD.Sound InnerSound { get; private set; }
			public FMOD.Channel Channel { get; private set; }
			public Sound(string file, FMOD.System system, FMOD.MODE mode = FMOD.MODE.DEFAULT)
			{
				this.system = system;
				FMOD.Sound ns = null;
				var res = FMOD.RESULT.OK;

				if (File.Exists(Path.Combine("data", file)))
					res = system.createSound(Path.Combine("data", file), mode, ref ns);
				else if (Mix.FileExists(file))
				{
					var offset = -1;
					var length = -1;
					var mixFile = string.Empty;
					Mix.GetFileRange(file, out offset, out length, out mixFile);
					var exInfo = new FMOD.CREATESOUNDEXINFO()
					{
						cbsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
						fileoffset = (uint)offset,
						length = (uint)length,
					};
					res = system.createSound(mixFile, mode, ref exInfo, ref ns);
				}
				else
					return;
				if (res != FMOD.RESULT.OK)
					throw new Exception("FMOD error loading sound " + file + ": " + FMOD.Error.String(res));
				
				InnerSound = ns;
				Channel = new FMOD.Channel();
			}
			public void Play()
			{
				if (InnerSound == null)
					return;
				var chan = Channel;
				system.playSound(FMOD.CHANNELINDEX.REUSE, InnerSound, false, ref chan);
			}
		}

		private FMOD.System system;
		private Dictionary<string, Sound> sounds;
		private string musicPlaying = "";
		private string currentSet = "";
		private FMOD.Sound music;
		private FMOD.Channel musicChannel;
		private float musicVolume, soundVolume;
		private XmlDocument library;

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
			system = null;
			Program.WriteLine("SoundSystem: _ctor");
			//var is64Bit = System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) == 8;
			if (!IniFile.GetValue("audio", "enabled", true)) // || !is64Bit)
				return;
			musicVolume = IniFile.GetValue("audio", "musicvolume", 100) / 100f;
			soundVolume = IniFile.GetValue("audio", "soundvolume", 100) / 100f;
			if (musicVolume + soundVolume == 0)
				return;

			var ret = FMOD.RESULT.OK;

			Program.Write("SoundSystem: creating FMOD system... ");
			try
			{
				ret = FMOD.Factory.System_Create(ref system);
				Program.WriteLine(ret.ToString());
				if (ret != FMOD.RESULT.OK)
					return;
			}
			catch (DllNotFoundException)
			{
				Program.WriteLine("FMOD not found, disabling sound.");
				return;
			}
			Program.Write("SoundSystem: system.init... ");
			ret = system.init(8, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
			Program.WriteLine(ret.ToString());
			if (ret != FMOD.RESULT.OK)
			{
				system = null;
				return;
			}
			music = null;
			musicChannel = null;

			Program.WriteLine("SoundSystem: acquiring sounds...");
			sounds = new Dictionary<string, Sound>();
			foreach (var s in new[] { "Put Item", "Get Item", "Alert", "Firebomb", "Splorch", "Cursor", "Door Lock", "Open Gate", "Step" })
			{
				var file = s + ".wav";
				if (Mix.FileExists(file))
					sounds.Add(s, new Sound(file, system));
			}

			Program.WriteLine("SoundSystem: loading library...");
			library = Mix.GetXMLDocument("music.xml");

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
			if (!Enabled || musicVolume == 0 || string.IsNullOrWhiteSpace(name))
				return;

			var set = targetSet;
			if (name.StartsWith("set://"))
			{
				set = name.Substring(6);
				var setNode = library.SelectSingleNode("//set[@name=\"" + set + "\"]") as XmlElement;
				if (setNode == null)
					name = "-";
				else
				{
					name = "-";
					while (setNode.HasChildNodes)
					{
						var pick = setNode.ChildNodes[Random.Next(setNode.ChildNodes.Count)] as XmlElement;
						if (!Mix.FileExists(pick.GetAttribute("href")))
							setNode.RemoveChild(pick);
						else
						{
							name = pick.GetAttribute("href");
							break;
						}
					}
				}
			}
			else
				currentSet = string.Empty;

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
					musicChannel.stop();
				if (music != null)
					music.release();
				currentSet = "";
				musicPlaying = "";
			}
			else if (musicPlaying != name)
			{
				var file = name;
				if (Mix.FileExists(file))
				{
					if (musicChannel != null)
						musicChannel.stop();
					if (music != null)
						music.release();
					var sound = new Sound(file, system, FMOD.MODE.LOOP_NORMAL);
					music = sound.InnerSound;
					system.playSound(FMOD.CHANNELINDEX.REUSE, music, false, ref musicChannel);
					musicPlaying = name;
					currentSet = set;
					musicChannel.setVolume(musicVolume);

					var songName = new StringBuilder();
					music.getName(songName, 256);
					Program.WriteLine(songName);
				}
				else
					Program.WriteLine("PlayMusic: couldn't load song \"{0}\".", name);
			}
		}

		public void PlaySound(string name)
		{
			if (!Enabled || soundVolume == 0)
				return;
			if (sounds.ContainsKey(name))
			{
				sounds[name].Play();
				sounds[name].Channel.setVolume(soundVolume);
			}
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
				musicChannel.setVolume(musicVolume - (1 - ((float)fadeProcess / 300)));
			}
		}

		public void Update()
		{
			if (!Enabled)
				return;
			if (!string.IsNullOrEmpty(FadeTarget))
				Fade();

			if (!IniFile.GetValue("audio", "debug", false))
				return;
			var musicchans = 0;
			var type = FMOD.SOUND_TYPE.UNKNOWN;
			var format = FMOD.SOUND_FORMAT.NONE;
			var channels = 0;
			var bits = 0;
			var row = 0u;
			var pattern = 0u;
			var rows = 0u;
			var patterns = 0u;
			var volume = 0f;
			music.getFormat(ref type, ref format, ref channels, ref bits);
			music.getMusicNumChannels(ref musicchans);
			musicChannel.getPosition(ref row, FMOD.TIMEUNIT.MODROW);
			musicChannel.getPosition(ref pattern, FMOD.TIMEUNIT.MODPATTERN);
			music.getLength(ref rows, FMOD.TIMEUNIT.MODROW);
			music.getLength(ref patterns, FMOD.TIMEUNIT.MODPATTERN);
			musicChannel.getVolume(ref volume);
			var debug = string.Format("{0}ch {1}, row {2}/{3}, pattern {4}/{5}. Volume: {6:P0}", musicchans, type.ToString(), row, rows, pattern, patterns, volume);
			NoxicoGame.HostForm.Write(musicPlaying.Remove(musicPlaying.LastIndexOf('.')).PadEffective(80), Color.Gray, System.Drawing.Color.Transparent, 0, 0);
			NoxicoGame.HostForm.Write(debug.PadEffective(80), Color.Gray, Color.Transparent, 1, 0);
		}

		public void ShutDown()
		{
			if (!Enabled)
				return;
			if (musicChannel != null)
				musicChannel.stop();
			if (music != null)
				music.release();
			if (sounds != null)
			{
				foreach (var s in sounds.Values)
				{
					s.Channel.stop();
					s.InnerSound.release();
				}
			}
			system.close();
			system = null;
		}
	}
}
