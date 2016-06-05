using System;
//using System.Text;
using System.Runtime.InteropServices;
namespace FMOD
{
	public class Version
	{
		public const int Number = 0x43705;
#if THIRTYTWO
        public const string Dll = "fmodex";
#else
		public const string Dll = "fmodex64";
#endif
	}
	public enum Result : int
	{
		OK,
		AlreadyLocked,
		BadCommand,
		CDDADrivers,
		CDDAInit,
		CDDAInvalidDevice,
		CDDANoAudio,
		CDDANoDevice,
		CDDANoDisc,
		CDDARead,
		ChannelAlloc,
		ChannelStolen,
		COM,
		DMA,
		DSPConnection,
		DSPFormat,
		DSPNotFound,
		DSPRunning,
		DSPTooManyConnections,
		FileBad,
		FileCouldNotSeek,
		FileDiskEjected,
		FileEOF,
		FileNotFound,
		FileUnwantedAccess,
		UnknownFormat,
		HTTP,
		HTTPAccess,
		HTTPProxyAuth,
		HTTPServerError,
		HTTPTimeOut,
		Initialization,
		Initialized,
		Interal,
		InvalidAddress,
		InvalidFloat,
		InvalidHandle,
		InvalidParameter,
		InvalidPosition,
		InvalidSpeaker,
		InvalidSyncPoint,
		InvalidVector,
		MaxAudibleReached,
		NotEnoughMemory,
		MemoryCanNotPoint,
		NotEnoughConsoleMemory,
		Needs2D,
		Needs3D,
		NeedsHardware,
		NeedsSoftware,
		NetCanNotConnect,
		NetSocketError,
		NetBadURL,
		NetWouldBlock,
		NotReady,
		OutputAllocated,
		OutputHardwareBuffer,
		OutputBadDriverCall,
		OutputEnumeration,
		OutputFormat,
		OutputInit,
		OutputNoHardware,
		OutputNoSoftware,
		Pan,
		Plugin,
		PluginInstances,
		PluginMissing,
		PluginResource,
		Preloaded,
		ProgrammerSound,
		Record,
		ReverbInstance,
		SubsoundAllocated,
		SubsoundCanNotMove,
		SubsoundMode,
		Subsounds,
		TagNotFound,
		TooManyChannels,
		Unimplemented,
		Uninitialized,
		Unsupported,
		Update,
		Version,
		EventFailed,
		EventInfoOnly,
		EventInternal,
		EventMaxStreams,
		EventMismatch,
		EventNameConflict,
		EventNotFound,
		EventNeedsSimple,
		EventGUIDConflict,
		EventAlreadyLoaded,
		MusicUninitialized,
		MusicNotFound,
		MusicNoCallback,
	}
	public enum InitFlags : int
	{
		Normal = 1,
		StreamFromUpdate = 1,
		RightHanded3D = 2,
		SoftwareDisabled = 4,
		OcclusionLowPass = 8,
		HRTFLowPass = 0x10,
		SoftwareReverbLowMemory = 0x40,
		EnableProfiling = 0x20,
		Volume0BecomesVirtual = 0x80,
		WasapiExclusive = 0x100,
		DisableDolby = 0x100000,
		DisableDolbyOnWii = 0x100000,
		Xbox360MusicMuteNotPause = 0x200000,
		SyncMixerWithUpdate = 0x400000,
		DTSNeuralSurround = 0x2000000,
		GeometryUseClosest = 0x4000000,
		DisableMyEarsAutodetect = 0x8000000
	}
	public enum SoundMode : uint
	{
		Default = 0,
		LoopOff = 1,
		LoopNormal = 2,
		LoopBiDi = 4,
		TwoD = 8,
		ThreeD = 0x10,
		Hardware = 0x20,
		Software = 0x40,
		CreateStream = 0x80,
		CreateSample = 0x100,
		CreateCompressedSample = 0x200,
		OpenUser = 0x400,
		OpenMemory = 0x800,
		OpenMemoryPoint = 0x10000000,
		OpenRaw = 0x1000,
		OpenOnly = 0x2000,
		AccurateTime = 0x4000,
		MpegSearch = 0x8000,
		NonBlocking = 0x10000,
		Unique = 0x20000,
		ThreeDHeadRelative = 0x40000,
		ThreeDWorldRelative = 0x80000,
		ThreeDInverseRolloff = 0x100000,
		ThreeDLinearSquareRolloff = 0x400000,
		ThreeDLogRoloff = 0x100000,
		ThreeDLinearRolloff = 0x200000,
		ThreeDCustomRolloff = 0x4000000,
		ThreeDIgnoreGeometry = 0x40000000,
		CDDAForceAspi = 0x400000,
		CDDAJitterCorrect = 0x800000,
		Unicode = 0x1000000,
		IgnoreTags = 0x2000000,
		LowMemory = 0x8000000,
		LoadSecondaryRam = 0x20000000,
		VirtualPlayFromStart = 0x80000000
	}
	public enum ChannelIndex
	{
		Free = -1,
		Reuse = -2
	}
	[StructLayout(LayoutKind.Sequential)]
	public struct CreateSoundExInfo
	{
		public int Size; //216
		public uint Length;
		public uint FileOffset;
		public int NumChannels;
		public int DefaultFrequency;
		public SoundFormat Format;
		public uint DecodeBufferSize;
		public int InitialSubsound;
		public int NumSubsounds;
		public IntPtr InclusionList;
		public int InclusionListNum;
		public PCMReadCallback PCMReadCallback;
		public PCMSetPosCallback PCMSetPosCallback;
		public NonBlockCallback NonBlockCallback;
		public string DLSName;
		public string EncryptionKey;
		public int MaxPolyphony;
		public IntPtr UserDate;
		public SoundType SuggestedSoundType;
		public FileOpenCallback FileOpenCallback;
		public FileCloseCallback FileCloseCallback;
		public FileReadCallback FileReadCallback;
		public FileSeekCallback FileSeekCallback;
		public FileAsyncReadCallback FileAsyncReadCallback;
		public FileAsyncCancelCallback FileAsyncCancelCallback;
		public SpeakerMap SpeakerMap;
		public IntPtr InitialSoundGroup;
		public uint InitialSeekPosition;
		public TimeUnit InitialSeekPositionType;
		public int IgnoreSetFilesystem;
		public int CDDAForceAspi;
		public uint AudioQueuePolicy;
		public uint MinMidiGranularity;
		public int NonBlockThreadID;
	}
	public enum SoundFormat : int
	{
		None,
		Pcm8,
		Pcm16,
		Pcm24,
		Pcm32,
		PcmFloat,
		GCADPCM,
		IMAADPCM,
		VAG,
		HEVAG,
		XMA,
		MPEG
	}
	public enum SoundType
	{
		Unknown,
		AIFF,
		ASF,
		AT3,
		CDDA,
		DLS,
		FLAC,
		FSB,
		GCADPCM,
		IT,
		MIDI,
		MOD,
		MPEG,
		OggVorbis,
		Playlist,
		Raw,
		S3M,
		SF2,
		User,
		WAV,
		XM,
		XMA,
		VAG,
		AudioQueue,
		XWMA,
		BCWAV,
		AT9,
	}
	public enum TimeUnit
	{
		Milliseconds = 1,
		PcmSamples = 2,
		PcmBytes = 4,
		RawBytes = 8,
		PcmFraction = 0x10,
		ModOrder = 0x100,
		ModRow = 0x200,
		ModPattern = 0x400,
		SentenceMilliseconds = 0x10000,
		SentencePcm = 0x20000,
		SentencePcmBytes = 0x40000,
		Sentence = 0x80000,
		SentenceSubsound = 0x100000,
		Buffered = 0x10000000,
	}
	public enum SpeakerMap
	{
		Default,
		AllMono,
		AllStereo,
		Dolby51ProTools
	}
	public enum SystemCallbackType : int
	{
		DeviceListChanged,
		DeviceLost,
		MemoryAllocFailed,
		ThreadCreated,
		BadDSPConnection,
		BadDSPLevel
	}
	public enum ChannelCallbackType : int
	{
		End,
		VirtualVoice,
		SyncPoint,
		Occlusion
	}
	public delegate Result SystemCallback(IntPtr systemRaw, SystemCallbackType type, IntPtr commanddata1, IntPtr commanddata2);
	public delegate Result ChannelCallback(IntPtr channelRaw, ChannelCallbackType type, IntPtr commanddata1, IntPtr commanddata2);
	public delegate Result NonBlockCallback(IntPtr soundRaw, Result result);
	public delegate Result PCMReadCallback(IntPtr soundRaw, IntPtr data, uint dataLength);
	public delegate Result PCMSetPosCallback(IntPtr soundRaw, int subsound, uint position, TimeUnit positionType);
	public delegate Result FileOpenCallback([MarshalAs(UnmanagedType.LPWStr)]string name, int unicode, ref uint fileSize, ref IntPtr handle, ref IntPtr userData);
	public delegate Result FileCloseCallback(IntPtr handle, IntPtr userData);
	public delegate Result FileReadCallback(IntPtr handle, IntPtr buffer, uint sizeBytes, ref uint bytesRead, IntPtr userData);
	public delegate Result FileSeekCallback(IntPtr handle, int pos, IntPtr userData);
	public delegate Result FileAsyncReadCallback(IntPtr handle, IntPtr info, IntPtr userData);
	public delegate Result FileAsyncCancelCallback(IntPtr handle, IntPtr userData);
	public class Factory
	{
		public static Result CreateSystem(ref System system)
		{
			Result result = Result.OK;
			IntPtr systemraw = new IntPtr();
			System systemnew = null;
			result = FMOD_System_Create(ref systemraw);
			if (result != Result.OK)
			{
				return result;
			}
			systemnew = new System();
			systemnew.SetRaw(systemraw);
			system = systemnew;
			return result;
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_Create(ref IntPtr system);
		#endregion
	}
	public class System
	{
		/// <summary>
		/// Closes and frees a system object and its resources.
		/// </summary>
		/// <returns></returns>
		public Result Release()
		{
			return FMOD_System_Release(systemRaw);
		}
		/// <summary>
		/// Closes the system object without freeing the object's memory, so the system handle will still be valid.
		/// </summary>
		/// <remarks>
		/// Closing the output renders objects created with this system object invalid. Make sure any sounds, channelgroups,
		/// geometry and dsp objects are released before closing the system object.
		/// </remarks>
		/// <returns></returns>
		public Result Close()
		{
			return FMOD_System_Close(systemRaw);
		}
		/// <summary>
		/// Updates the FMOD system. This should be called once per 'game' tick, or once per frame in your application
		/// </summary>
		/// <returns></returns>
		public Result Update()
		{
			return FMOD_System_Update(systemRaw);
		}
		/// <summary>
		/// Initializes the system object, and the sound device. This has to be called at the start of the user's program.
		/// </summary>
		/// <param name="maxChannels">
		/// The maximum number of channels to be used in FMOD. They are also called 'virtual channels' as you can play as
		/// many of these as you want, even if you only have a small number of hardware or software voices. See remarks for more.
		/// </param>
		/// <param name="flags">
		/// See InitFlags. This can be a selection of flags bitwise OR'ed together to change the behaviour
		/// of FMOD at initialization time.
		/// </param>
		/// <param name="extraDriverData">
		/// Driver specific data that can be passed to the output plugin. For example, the filename for the wav writer plugin.
		/// See OutputType for what each output mode might take here. Optional. Specify 0 or null to ignore. 
		/// </param>
		/// <remarks>
		/// <para>
		/// Virtual voices are the types you work with using the FMOD Channel API.
		/// The advantage of virtual channels are, unlike older versions of FMOD, you can now play as many sounds as you like
		/// without fear of ever running out of voices, or playsound failing. You can also avoid 'channel stealing' if you
		/// specify enough virtual voices.
		/// </para>
		/// <para>
		/// As an example, you can play 1000 sounds at once, even on a 32 channel soundcard. FMOD will only play the most
		/// important/closest/loudest (determined by volume/distance/geometry and priority settings) voices, and the other
		/// 968 voices will be virtualized without expense to the CPU. The voice's cursor positions are updated. When the
		/// priority of sounds change or emulated sounds get louder than audible ones, they will swap the actual voice
		/// resource over (ie hardware or software buffer) and play the voice from its correct position in time as it should
		/// be heard. What this means is you can play all 1000 sounds, if they are scattered around the game world, and as
		/// you move around the world you will hear the closest or most important 32, and they will automatically swap in and
		/// out as you move.
		/// </para>
		/// <para>Currently the maximum channel limit is 4093.</para>
		/// </remarks>
		/// <returns></returns>
		public Result Initialize(int maxChannels, InitFlags flags, IntPtr extraDriverData)
		{
			return FMOD_System_Init(systemRaw, maxChannels, flags, extraDriverData);
		}
		/// <summary>
		/// Loads a sound into memory, or opens it for streaming.
		/// </summary>
		/// <param name="name">
		/// Name of the file or URL to open. For CD playback, Windows-only, the name should be a drive letter with a colon.
		/// </param>
		/// <param name="mode">Behaviour modifier for opening the sound. See SoundMode.</param>
		/// <param name="exInfo">
		/// Pointer to a CreateSoundExInfo structure which lets the user provide extended information while playing the sound.
		/// </param>
		/// <param name="sound">Address of a variable to receive a newly created FMOD Sound object.</param>
		/// <returns></returns>
		public Result CreateSound(string name, SoundMode mode, ref CreateSoundExInfo exInfo, ref Sound sound)
		{
			Result result = Result.OK;
			IntPtr soundraw = new IntPtr();
			Sound soundnew = null;
			mode = mode | FMOD.SoundMode.Unicode;
			try
			{
				result = FMOD_System_CreateSound(systemRaw, name, mode, ref exInfo, ref soundraw);
			}
			catch
			{
				result = Result.InvalidParameter;
			}
			if (result != Result.OK)
			{
				return result;
			}
			if (sound == null)
			{
				soundnew = new Sound();
				soundnew.SetRaw(soundraw);
				sound = soundnew;
			}
			else
			{
				sound.SetRaw(soundraw);
			}
			return result;
		}
		/// <summary>
		/// Loads a sound into memory, or opens it for streaming.
		/// </summary>
		/// <param name="data">A pointer to a preloaded sound.</param>
		/// <param name="mode">Behaviour modifier for opening the sound. See SoundMode.</param>
		/// <param name="exInfo">
		/// Pointer to a CreateSoundExInfo structure which lets the user provide extended information while playing the sound.
		/// </param>
		/// <param name="sound">Address of a variable to receive a newly created FMOD Sound object.</param>
		/// <returns></returns>
		public Result CreateSound(byte[] data, SoundMode mode, ref CreateSoundExInfo exInfo, ref Sound sound)
		{
			Result result = Result.OK;
			IntPtr soundraw = new IntPtr();
			Sound soundnew = null;
			try
			{
				result = FMOD_System_CreateSound(systemRaw, data, mode, ref exInfo, ref soundraw);
			}
			catch
			{
				result = Result.InvalidParameter;
			}
			if (result != Result.OK)
			{
				return result;
			}
			if (sound == null)
			{
				soundnew = new Sound();
				soundnew.SetRaw(soundraw);
				sound = soundnew;
			}
			else
			{
				sound.SetRaw(soundraw);
			}
			return result;
		}
		/// <summary>
		/// Loads a sound into memory, or opens it for streaming.
		/// </summary>
		/// <param name="name">
		/// Name of the file or URL to open. For CD playback, Windows-only, the name should be a drive letter with a colon.
		/// </param>
		/// <param name="mode">Behaviour modifier for opening the sound. See SoundMode.</param>
		/// <param name="sound">Address of a variable to receive a newly created FMOD Sound object.</param>
		/// <returns></returns>
		public Result CreateSound(string name, SoundMode mode, ref Sound sound)
		{
			Result result = Result.OK;
			IntPtr soundraw = new IntPtr();
			Sound soundnew = null;
			mode = mode | FMOD.SoundMode.Unicode;
			try
			{
				result = FMOD_System_CreateSound(systemRaw, name, mode, 0, ref soundraw);
			}
			catch
			{
				result = Result.InvalidParameter;
			}
			if (result != Result.OK)
			{
				return result;
			}
			if (sound == null)
			{
				soundnew = new Sound();
				soundnew.SetRaw(soundraw);
				sound = soundnew;
			}
			else
			{
				sound.SetRaw(soundraw);
			}
			return result;
		}
		/// <summary>
		/// Plays a sound object on a particular channel.
		/// </summary>
		/// <param name="channelIndex">
		/// Use ChannelIndex.Free or -1 to get FMOD to pick a free channel. Otherwise, specify a channel number
		/// from 0 to the maxChannels value specified in System.Initialize, minus 1.
		/// </param>
		/// <param name="sound">Pointer to the sound to play. This is opened with System.CreateSound.</param>
		/// <param name="paused">
		/// True or false flag to specify whether to start the channel paused or not. Starting a channel paused
		/// allows the user to alter its attributes without it being audible, and unpausing with Channel.SetPaused
		/// actually starts the sound.
		/// </param>
		/// <param name="channel">
		/// Address of a channel handle pointer that receives the newly playing channel. If ChannelIndex.Reuse is
		/// used, this can contain a previously used channel handle and FMOD will re-use it to play a sound on.
		/// </param>
		/// <returns></returns>
		public Result PlaySound(ChannelIndex channelIndex, Sound sound, bool paused, ref Channel channel)
		{
			Result result = Result.OK;
			IntPtr channelraw;
			Channel channelnew = null;
			if (channel != null)
			{
				channelraw = channel.GetRaw();
			}
			else
			{
				channelraw = new IntPtr();
			}
			try
			{
				result = FMOD_System_PlaySound(systemRaw, channelIndex, sound.GetRaw(), (paused ? 1 : 0), ref channelraw);
			}
			catch
			{
				result = Result.InvalidParameter;
			}
			if (result != Result.OK)
			{
				return result;
			}
			if (channel == null)
			{
				channelnew = new Channel();
				channelnew.SetRaw(channelraw);
				channel = channelnew;
			}
			else
			{
				channel.SetRaw(channelraw);
			}
			return result;
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_Release(IntPtr system);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_Init(IntPtr system, int maxchannels, InitFlags flags, IntPtr extradriverdata);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_Close(IntPtr system);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_Update(IntPtr system);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, string name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, ref IntPtr sound);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, string name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, ref IntPtr sound);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, string name_or_data, SoundMode mode, int exinfo, ref IntPtr sound);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, string name_or_data, SoundMode mode, int exinfo, ref IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, byte[] name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, ref IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, byte[] name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, ref IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, byte[] name_or_data, SoundMode mode, int exinfo, ref IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, byte[] name_or_data, SoundMode mode, int exinfo, ref IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_PlaySound(IntPtr system, ChannelIndex channelid, IntPtr sound, int paused, ref IntPtr channel);
		#endregion
		#region wrapperinternal
		private IntPtr systemRaw;
		public void SetRaw(IntPtr system)
		{
			systemRaw = new IntPtr();
			systemRaw = system;
		}
		public IntPtr GetRaw()
		{
			return systemRaw;
		}
		#endregion
	}
	public class Sound
	{
		/// <summary>
		/// Frees a sound object
		/// </summary>
		/// <returns></returns>
		public Result Release()
		{
			return FMOD_Sound_Release(soundRaw);
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Sound_Release(IntPtr sound);
		#endregion
		#region wrapperinternal
		private IntPtr soundRaw;
		public void SetRaw(IntPtr sound)
		{
			soundRaw = new IntPtr();
			soundRaw = sound;
		}
		public IntPtr GetRaw()
		{
			return soundRaw;
		}
		#endregion
	}
	public class Channel
	{
		/// <summary>
		/// Stops the channel from playing. Makes it available for re-use by the priority system.
		/// </summary>
		/// <returns></returns>
		public Result Stop()
		{
			return FMOD_Channel_Stop(channelRaw);
		}
		/// <summary>
		/// Sets the volume for the channel linearly.
		/// </summary>
		/// <param name="volume">A linear volume level, from 0.0 to 1.0 inclusive. 0.0 = silent, 1.0 = full volume.</param>
		/// <returns></returns>
		public Result SetVolume(float volume)
		{
			return FMOD_Channel_SetVolume(channelRaw, volume);
		}
		/// <summary>
		/// Sets the paused state of the channel.
		/// </summary>
		/// <param name="paused">Paused state to set.</param>
		/// <returns></returns>
		public Result SetPaused(bool paused)
		{
			return FMOD_Channel_SetPaused(channelRaw, (paused ? 1 : 0));
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Channel_SetVolume(IntPtr channel, float volume);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Channel_Stop(IntPtr channel);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Channel_SetPaused(IntPtr channel, int paused);
		#endregion
		#region wrapperinternal
		private IntPtr channelRaw;
		public void SetRaw(IntPtr channel)
		{
			channelRaw = new IntPtr();
			channelRaw = channel;
		}
		public IntPtr GetRaw()
		{
			return channelRaw;
		}
		#endregion
	}
}

