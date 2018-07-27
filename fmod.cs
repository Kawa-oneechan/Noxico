using System;
using System.Text;
using System.Runtime.InteropServices;
namespace FMOD
{
	public class Version
	{
		public const int Number = 0x00010804;
#if THIRTYTWO
        public const string Dll = "fmod";
#else
		public const string Dll = "fmod64";
#endif
	}
	public enum Result : int
	{
		OK,
		BadCommand,
		ChannelAlloc,
		ChannelStolen,
		Dma,
		DspConnection,
		DspDontProcess,
		DspFormat,
		DspInUse,
		DspNotFound,
		DspReserved,
		DspSilence,
		DspType,
		FileBad,
		FileCouldNotSeek,
		FileDiskEjected,
		FileEOF,
		FileEndOfData,
		FileNotFound,
		UnknownFormat,
		HeaderMismatch,
		Http,
		HttpAccess,
		HttpProxyAuth,
		HttpServerError,
		HttpTimeOut,
		Initialization,
		Initialized,
		Interal,
		InvalidFloat,
		InvalidHandle,
		InvalidParameter,
		InvalidPosition,
		InvalidSpeaker,
		InvalidSyncPoint,
		InvalidThread,
		InvalidVector,
		MaxAudibleReached,
		NotEnoughMemory,
		MemoryCanNotPoint,
		Needs3D,
		NeedsHardware,
		NetCanNotConnect,
		NetSocketError,
		NetBadUrl,
		NetWouldBlock,
		NotReady,
		OutputAllocated,
		OutputCreateBuffer,
		OutputBadDriverCall,
		OutputFormat,
		OutputInit,
		OutputNoDrivers,
		Plugin,
		PluginMissing,
		PluginResource,
		PluginVersion,
		Record,
		ReverbChannelGroup,
		ReverbInstance,
		Subsounds,
		SubsoundAllocated,
		SubsoundCanNotMove,
		TagNotFound,
		TooManyChannels,
		Truncated,
		Unimplemented,
		Uninitialized,
		Unsupported,
		Version,
		EventAlreadyLoaded,
		EventLiveUpdateBusy,
		EventLiveUpdateMismatch,
		EventLiveUpdateTimeout,
		EventNotFound,
		StudioUninitialized,
		StudioNotLoaded,
		InvalidString,
		AlreadyLocked,
		NotLocked,
		RecordDisconnected,
		TooManySamples
	}
	public enum InitFlags : int
	{
		Normal = 1,
		StreamFromUpdate = 1,
		MixFromUpdate = 2, //Win/Wii/PS3/Xbox/360 only
		RightHanded3D = 4,
		LowPass = 0x100,
		DistanceFilter = 0x200,
		EnableProfiling = 0x10000,
		Volume0BecomesVirtual = 0x20000,
		GeometryUseClosest = 0x40000,
		PreferDolbyDownmix = 0x80000,
		ThreadUnsafe = 0x100000,
		ProfileMeterAll = 0x200000,
	}
	public enum SoundMode : uint
	{
		Default = 0,
		LoopOff = 1,
		LoopNormal = 2,
		LoopBiDi = 4,
		TwoD = 8,
		ThreeD = 0x10,
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
		ThreeDInverseTaperedRolloff = 0x800000,
		ThreeDCustomRolloff = 0x4000000,
		ThreeDIgnoreGeometry = 0x40000000,
		IgnoreTags = 0x2000000,
		LowMemory = 0x8000000,
		LoadSecondaryRam = 0x20000000,
		VirtualPlayFromStart = 0x80000000
	}
	[StructLayout(LayoutKind.Sequential)]
	public struct CreateSoundExInfo
	{
		public int Size; //216, set by CreateSound.
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
		public PcmReadCallback PcmReadCallback;
		public PcmSetPosCallback PcmSetPosCallback;
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
		public IntPtr FileUserData;
		public int FileBufferSize;
		public ChannelOrder ChannelOrder;
		public ChannelMask ChannelMask;
		public IntPtr InitialSoundGroup;
		public uint InitialSeekPosition;
		public TimeUnit InitialSeekPositionType;
		public int IgnoreSetFilesystem;
		public uint AudioQueuePolicy;
		public uint MinMidiGranularity;
		public int NonBlockThreadID;
		public IntPtr FsbGuid;
	}
	public enum SoundFormat : int
	{
		None,
		Pcm8,
		Pcm16,
		Pcm24,
		Pcm32,
		PcmFloat
	}
	public enum SoundType
	{
		Unknown,
		Aiff,
		Asf,
		Dls,
		Flac,
		Fsb,
		It,
		Midi,
		Mod,
		Mp3,
		OggVorbis,
		Playlist,
		Raw,
		S3M,
		UserCreated,
		Wave,
		XM,
		Xma,
		AudioQueue,
		Vorbis,
		MediaFoundation,
		AndroidMediaCodec,
		FmodAdpcm,
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
		Buffered = 0x10000000,
	}
	public enum ChannelMask : uint
	{
		FrontLeft = 1,
		FrontRight = 2,
		FrontCenter = 4,
		LowFrequency = 8,
		SurroundLeft = 0x10,
		SurroundRight = 0x20,
		BackLeft = 0x40,
		BackRight = 0x80,
		BackCenter = 0x100,
		Mono = FrontLeft,
		Stereo = FrontLeft | FrontRight,
		LeftRightCenter = FrontLeft | FrontRight | FrontCenter,
		Quad = FrontLeft | FrontRight | SurroundLeft | SurroundRight,
		FivePtOne = FrontLeft | FrontRight | FrontCenter | LowFrequency | SurroundLeft | SurroundRight,
		FivePtOneRears = FrontLeft | FrontRight | FrontCenter | LowFrequency | BackLeft | BackRight,
		SevenPtOh = FrontLeft | FrontRight | FrontCenter | SurroundLeft | SurroundRight | BackLeft | BackRight,
		SevenPtOne = FrontLeft | FrontRight | FrontCenter | LowFrequency | SurroundLeft | SurroundRight | BackLeft | BackRight,
	}
	public enum ChannelOrder : int
	{
		Default,
		WaveFormat,
		ProTools,
		AllMono,
		AllStereo,
		Alsa
	}
	public enum SystemCallbackType : int
	{
		DeviceListChanged,
		DeviceLost,
		MemoryAllocFailed,
		ThreadCreated,
		BadDspConnection,
		BadDspLevel,
		PreMix,
		PostMix,
		Error,
		MidMix,
		ThreadDestroyed,
		PreUpdate,
		PostUpdate,
		RecordListChanged,

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
	public delegate Result PcmReadCallback(IntPtr soundRaw, IntPtr data, uint dataLength);
	public delegate Result PcmSetPosCallback(IntPtr soundRaw, int subsound, uint position, TimeUnit positionType);
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
			system = null;
			var rawPtr = new IntPtr();
			var result = FMOD_System_Create(out rawPtr);
			if (result != Result.OK)
				return result;
			system = new System(rawPtr);
			return result;
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_Create(out IntPtr system);
		#endregion
	}
	public class HandleBase
	{
		protected IntPtr rawPtr;
		public HandleBase(IntPtr newPtr)
		{
			rawPtr = newPtr;
		}
		public bool isValid()
		{
			return rawPtr != IntPtr.Zero;
		}
		public IntPtr getRaw()
		{
			return rawPtr;
		}
		#region equality
		public override bool Equals(Object obj)
		{
			return Equals(obj as HandleBase);
		}
		public bool Equals(HandleBase p)
		{
			return ((object)p != null && rawPtr == p.rawPtr);
		}
		public override int GetHashCode()
		{
			return rawPtr.ToInt32();
		}
		public static bool operator ==(HandleBase a, HandleBase b)
		{
			if (Object.ReferenceEquals(a, b))
				return true;
			if (((object)a == null) || ((object)b == null))
				return false;
			return (a.rawPtr == b.rawPtr);
		}
		public static bool operator !=(HandleBase a, HandleBase b)
		{
			return !(a == b);
		}
		#endregion
	}
	public class System : HandleBase
	{
		/// <summary>
		/// Closes and frees a system object and its resources.
		/// </summary>
		/// <returns></returns>
		public Result Release()
		{
			Result result = FMOD_System_Release(rawPtr);
			if (result == Result.OK)
				rawPtr = IntPtr.Zero;
			return result;
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
			return FMOD_System_Close(rawPtr);
		}
		/// <summary>
		/// Updates the FMOD system. This should be called once per 'game' tick, or once per frame in your application
		/// </summary>
		/// <returns></returns>
		public Result Update()
		{
			return FMOD_System_Update(rawPtr);
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
			return FMOD_System_Init(rawPtr, maxChannels, flags, extraDriverData);
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
		public Result CreateSound(string name, SoundMode mode, ref CreateSoundExInfo exInfo, out Sound sound)
		{
			sound = null;
			byte[] stringData;
			stringData = Encoding.UTF8.GetBytes(name + Char.MinValue);
			exInfo.Size = Marshal.SizeOf(exInfo);
			IntPtr soundraw;
			Result result = FMOD_System_CreateSound(rawPtr, stringData, mode, ref exInfo, out soundraw);
			sound = new Sound(soundraw);
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
			sound = null;
			exInfo.Size = Marshal.SizeOf(exInfo);
			IntPtr soundraw;
			Result result = FMOD_System_CreateSound(rawPtr, data, mode, ref exInfo, out soundraw);
			sound = new Sound(soundraw);
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
			var exinfo = new CreateSoundExInfo();
			exinfo.Size = Marshal.SizeOf(exinfo);
			return CreateSound(name, mode, ref exinfo, out sound);
		}
		/// <summary>
		/// Plays a sound object on a particular channel.
		/// </summary>
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
		public Result PlaySound(Sound sound, bool paused, ref Channel channel)
		{
			channel = null;
			var channelGroupRaw = IntPtr.Zero;
			IntPtr channelraw;
			Result result = FMOD_System_PlaySound(rawPtr, sound.getRaw(), channelGroupRaw, paused, out channelraw);
			channel = new Channel(channelraw);
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
		private static extern Result FMOD_System_CreateSound(IntPtr system, string name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, out IntPtr sound);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, string name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, out IntPtr sound);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, string name_or_data, SoundMode mode, int exinfo, out IntPtr sound);
		[DllImport(Version.Dll, CharSet = CharSet.Unicode)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, string name_or_data, SoundMode mode, int exinfo, out IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, byte[] name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, out IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, byte[] name_or_data, SoundMode mode, ref CreateSoundExInfo exinfo, out IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateSound(IntPtr system, byte[] name_or_data, SoundMode mode, int exinfo, out IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_CreateStream(IntPtr system, byte[] name_or_data, SoundMode mode, int exinfo, out IntPtr sound);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_System_PlaySound(IntPtr system, IntPtr sound, IntPtr channelGroup, bool paused, out IntPtr channel);
		#endregion
		#region wrapperinternal
		public System(IntPtr raw)
			: base(raw)
		{
		}

		#endregion
	}
	public class Sound : HandleBase
	{
		/// <summary>
		/// Frees a sound object
		/// </summary>
		/// <returns></returns>
		public Result Release()
		{
			var result = FMOD_Sound_Release(rawPtr);
			if (result == Result.OK)
				rawPtr = IntPtr.Zero;
			return result;
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Sound_Release(IntPtr sound);
		#endregion
		#region wrapperinternal
		public Sound(IntPtr raw)
			: base(raw)
		{
		}
		#endregion
#if DEBUG
		public Result GetFormat(out SoundType type, out SoundFormat format, out int channels, out int bits)
		{
			return FMOD_Sound_GetFormat(rawPtr, out type, out format, out channels, out bits);
		}
		public Result GetMusicNumChannels(out int numchannels)
		{
			return FMOD_Sound_GetMusicNumChannels(rawPtr, out numchannels);
		}
		public Result GetLength(out uint length, TimeUnit lengthtype)
		{
			return FMOD_Sound_GetLength(rawPtr, out length, lengthtype);
		}
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Sound_GetFormat(IntPtr sound, out SoundType type, out SoundFormat format, out int channels, out int bits);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Sound_GetMusicNumChannels(IntPtr sound, out int numchannels);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Sound_GetLength(IntPtr sound, out uint length, TimeUnit lengthtype);
#endif
	}
	public class Channel : HandleBase
	{
		/// <summary>
		/// Stops the channel from playing. Makes it available for re-use by the priority system.
		/// </summary>
		/// <returns></returns>
		public Result Stop()
		{
			return FMOD_ChannelGroup_Stop(rawPtr);
		}
		/// <summary>
		/// Sets the volume for the channel linearly.
		/// </summary>
		/// <param name="volume">A linear volume level, from 0.0 to 1.0 inclusive. 0.0 = silent, 1.0 = full volume.</param>
		/// <returns></returns>
		public Result SetVolume(float volume)
		{
			return FMOD_ChannelGroup_SetVolume(rawPtr, volume);
		}
		/// <summary>
		/// Sets the paused state of the channel.
		/// </summary>
		/// <param name="paused">Paused state to set.</param>
		/// <returns></returns>
		public Result SetPaused(bool paused)
		{
			return FMOD_ChannelGroup_SetPaused(rawPtr, paused);
		}
		#region importfunctions
		[DllImport(Version.Dll)]
		private static extern Result FMOD_ChannelGroup_Stop(IntPtr channelgroup);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_ChannelGroup_SetVolume(IntPtr channel, float volume);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_ChannelGroup_SetPaused(IntPtr channel, bool paused);
		#endregion
		#region wrapperinternal
		public Channel(IntPtr raw)
			: base(raw)
		{
		}
		#endregion
#if DEBUG
		public Result GetVolume(out float volume)
		{
			return FMOD_ChannelGroup_GetVolume(rawPtr, out volume);
		}

		public Result GetPosition(out uint position, TimeUnit postype)
		{
			return FMOD_Channel_GetPosition(getRaw(), out position, postype);
		}
		[DllImport(Version.Dll)]
		private static extern Result FMOD_ChannelGroup_GetVolume(IntPtr channelgroup, out float volume);
		[DllImport(Version.Dll)]
		private static extern Result FMOD_Channel_GetPosition(IntPtr channel, out uint position, TimeUnit postype);
#endif
	}
}

