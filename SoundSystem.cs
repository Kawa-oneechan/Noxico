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
			public Sound(string file, FMOD.System system)
			{
				this.system = system;
				FMOD.Sound ns = null;
				var res = system.createSound(file, FMOD.MODE.DEFAULT, ref ns);
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

		public string FadeTarget { get; private set; }
		private string targetSet;
		private int fadeProcess;

		public SoundSystem()
		{
			system = null;
			Console.WriteLine("SoundSystem: _ctor");
			if (!IniFile.GetBool("audio", "enabled", true))
				return;
			musicVolume = (float)IniFile.GetInt("audio", "musicvolume", 100) / 100;
			soundVolume = (float)IniFile.GetInt("audio", "soundvolume", 100) / 100;

			Console.Write("SoundSystem: creating FMOD system...");
			var ret = FMOD.Factory.System_Create(ref system);
			Console.WriteLine(" " + ret.ToString());
			if (ret != FMOD.RESULT.OK)
				return;
			Console.Write("SoundSystem: system.init...");
			ret = system.init(8, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
			Console.WriteLine(" " + ret.ToString());
			if (ret != FMOD.RESULT.OK)
			{
				system = null;
				return;
			}
			music = null;
			musicChannel = null;

			Console.WriteLine("SoundSystem: acquiring sounds...");
			sounds = new Dictionary<string, Sound>();
			foreach (var s in new[] { "Coin", "Door Lock", "Key", "Open Door", "Open Gate", "Push", "Shoot", "Alert" })
			{
				var file = Path.Combine("sounds", s + ".wav");
				if (File.Exists(file))
				{
					sounds.Add(s, new Sound(file, system));
					//sounds.Add(s, ns);
				}
			}

			Console.WriteLine("SoundSystem: loading library...");
			library = new XmlDocument();
			library.Load("music.xml");

			Console.WriteLine("SoundSystem: _ctor DONE");
			Console.WriteLine("Null report:");
			if (system == null)
				Console.WriteLine(" * system");
			if (music == null)
				Console.WriteLine(" * music");
			if (musicChannel == null)
				Console.WriteLine(" * musicChannel");
		}

		public void PlayMusic(string name, bool fade = true)
		{
			if (system == null)
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
						var pick = setNode.ChildNodes[Toolkit.Rand.Next(setNode.ChildNodes.Count)] as XmlElement;
						if (!File.Exists(Path.Combine("music", pick.GetAttribute("href"))))
							setNode.RemoveChild(pick);
						else
						{
							name = pick.GetAttribute("href");
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
					musicChannel.stop();
				if (music != null)
					music.release();
				currentSet = "";
				musicPlaying = "";
			}
			else if (musicPlaying != name)
			{
				var file = Path.Combine("music", name);
				if (File.Exists(file))
				{
					if (musicChannel != null)
						musicChannel.stop();
					if (music != null)
						music.release();
					//var ret = system.createStream(file, FMOD.MODE.LOOP_NORMAL, ref music);
					var ret = system.createSound(file, FMOD.MODE.LOOP_NORMAL, ref music);
					Console.WriteLine("system.createStream(\"{0}\"... {1}", file, ret);
					if (ret != FMOD.RESULT.OK)
						return;
					system.playSound(FMOD.CHANNELINDEX.REUSE, music, false, ref musicChannel);
					musicPlaying = name;
					currentSet = set;
					musicChannel.setVolume(musicVolume);
					//NoxicoGame.HostForm.Text = file;
				}
				else
					Console.WriteLine("PlayMusic: couldn't load song \"{0}\".", name);
				//else
				//	System.Windows.Forms.MessageBox.Show(string.Format("Couldn't load song \"{0}\".", name), "404");
			}
		}

		public void PlaySound(string name)
		{
			if (system == null)
				return;
			if (sounds.ContainsKey(name))
			{
				sounds[name].Play();
				sounds[name].Channel.setVolume(soundVolume);
			}
		}

		private void Fade()
		{
			if (system == null)
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
			if (system == null)
				return;
			if (!string.IsNullOrEmpty(FadeTarget))
				Fade();

			if (!IniFile.GetBool("audio", "debug", false))
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
			NoxicoGame.HostForm.Write(musicPlaying.Remove(musicPlaying.LastIndexOf('.')).PadRight(80), System.Drawing.Color.Gray, System.Drawing.Color.Transparent, 0, 0);
			NoxicoGame.HostForm.Write(debug.PadRight(80), System.Drawing.Color.Gray, System.Drawing.Color.Transparent, 0, 1);
		}
	}
}
