using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace Noxico
{
	public static class Vista
	{
		public static bool IsVista = (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 6);

		public static readonly Guid SavedGames = new Guid("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");
		//Add more GUIDs here whenever interesting.

		public static string GetInterestingPath(Guid target)
		{
			if (!IsVista)
				return null;

			string ret = null;
			IntPtr pPath;
			try
			{
				if (SafeNativeMethods.SHGetKnownFolderPath(target, 0, IntPtr.Zero, out pPath) == 0)
				{
					ret = System.Runtime.InteropServices.Marshal.PtrToStringUni(pPath);
					System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pPath);
				}
				return ret;
			}
			catch (DllNotFoundException)
			{
				return null;
			}
		}

		public static bool GamepadEnabled { get; set; }

		public static bool GamepadAvailable { get; private set; }
		public static bool GamepadHasInput { get; private set; }
		public static XInputGamePad GamepadState { get; private set; }
		public static XInputButtons DPad { get; private set; }
		public static XInputButtons Triggers { get; private set; }

		private static XInputState padState;
		private static int padPacket;
		private static XInputButtons lastTrigs;

		//private static readonly XInputButtons triggerMask = (XInputButtons)0xFFF0;
		private static readonly XInputButtons directionMask = (XInputButtons)0x000F;

		public static XInputErrorCodes UpdateGamepad()
		{
			if (!GamepadEnabled || !IsVista)
				return XInputErrorCodes.AccessDenied;

			try
			{
				var ret = SafeNativeMethods.XInputGetState(0, out padState);
				GamepadAvailable = (ret == XInputErrorCodes.Success);
				if (ret == XInputErrorCodes.Success)
				{
					GamepadState = padState.GamePad;
					GamepadHasInput = (padPacket != padState.PacketNumber);

					if (GamepadHasInput)
					{
						var rawButtons = padState.GamePad.Buttons;
						DPad = rawButtons & directionMask;
						var newTrigs = rawButtons; // & triggerMask;
						if (lastTrigs != newTrigs)
							Triggers = lastTrigs = newTrigs;
						else if (newTrigs == lastTrigs)
							Triggers = lastTrigs = 0;
					}

					padPacket = padState.PacketNumber;
				}
				return ret;
			}
			catch (DllNotFoundException)
			{
				//Thanks to Adamsk on #rgrd for bringing this to my attention.
				GamepadEnabled = false;
				return XInputErrorCodes.AccessDenied;
			}
		}

		public static void ReleaseTriggers()
		{
			Triggers = 0;
		}

		public static bool GamepadFocused
		{
			set
			{
				if (GamepadEnabled && IsVista)
				{
					try
					{

						SafeNativeMethods.XInputEnable(value);
					}
					catch (DllNotFoundException)
					{
						GamepadEnabled = false;
					}
				}
			}
		}
	}

	public enum XInputErrorCodes : uint
	{
		Success,
		Pending = 997u,
		NotConnected = 1167u,
		Empty = 4306u,
		Busy = 170u,
		AccessDenied = 5u,
		AlreadyExists = 183u,
	}

	[Flags]
	public enum XInputButtons : ushort
	{
		Up = 0x01,
		Down = 0x02,
		Left = 0x04,
		Right = 0x08,
		Start = 0x10,
		Back = 0x20,
		LeftThumb = 0x40,
		RightThumb = 0x80,
		LeftShoulder = 0x0100,
		RightShoulder = 0x0200,
		//0x0400 is unlisted?
		BigButton = 0x0800,
		A = 0x1000,
		B = 0x2000,
		X = 0x4000,
		Y = 0x8000,
	}

	public struct XInputGamePad
	{
		public XInputButtons Buttons;
		public byte LeftTrigger;
		public byte RightTrigger;
		public short ThumbLX;
		public short ThumbLY;
		public short ThumbRX;
		public short ThumbRY;
	}

	public struct XInputState
	{
		public int PacketNumber;
		public XInputGamePad GamePad;
	}

	internal static class SafeNativeMethods
	{
		[DllImport("shell32.dll")]
		public static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

		[DllImport("xinput1_3.dll")]
		internal static extern XInputErrorCodes XInputGetState(int playerIndex, out XInputState pState);

		[DllImport("xinput1_3.dll")]
		internal static extern void XInputEnable([MarshalAs(UnmanagedType.Bool)]bool enable);
	}

	public static class UacHelper
	{
		private const string uacRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
		private const string uacRegistryValue = "EnableLUA";

		private static uint STANDARD_RIGHTS_READ = 0x00020000;
		private static uint TOKEN_QUERY = 0x0008;
		private static uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);

		[DllImport("advapi32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

		public enum TOKEN_INFORMATION_CLASS
		{
			TokenUser = 1,
			TokenGroups,
			TokenPrivileges,
			TokenOwner,
			TokenPrimaryGroup,
			TokenDefaultDacl,
			TokenSource,
			TokenType,
			TokenImpersonationLevel,
			TokenStatistics,
			TokenRestrictedSids,
			TokenSessionId,
			TokenGroupsAndPrivileges,
			TokenSessionReference,
			TokenSandBoxInert,
			TokenAuditPolicy,
			TokenOrigin,
			TokenElevationType,
			TokenLinkedToken,
			TokenElevation,
			TokenHasRestrictions,
			TokenAccessInformation,
			TokenVirtualizationAllowed,
			TokenVirtualizationEnabled,
			TokenIntegrityLevel,
			TokenUIAccess,
			TokenMandatoryPolicy,
			TokenLogonSid,
			MaxTokenInfoClass
		}

		public enum TOKEN_ELEVATION_TYPE
		{
			TokenElevationTypeDefault = 1,
			TokenElevationTypeFull,
			TokenElevationTypeLimited
		}

		public static bool IsUacEnabled
		{
			get
			{
				RegistryKey uacKey = Registry.LocalMachine.OpenSubKey(uacRegistryKey, false);
				bool result = uacKey.GetValue(uacRegistryValue).Equals(1);
				return result;
			}
		}

		public static bool IsProcessElevated
		{
			get
			{
				if (IsUacEnabled)
				{
					IntPtr tokenHandle;
					if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_READ, out tokenHandle))
					{
						throw new ApplicationException("Could not get process token.  Win32 Error Code: " + Marshal.GetLastWin32Error());
					}

					TOKEN_ELEVATION_TYPE elevationResult = TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;

					int elevationResultSize = Marshal.SizeOf((int)elevationResult);
					uint returnedSize = 0;
					IntPtr elevationTypePtr = Marshal.AllocHGlobal(elevationResultSize);

					bool success = GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevationType, elevationTypePtr, (uint)elevationResultSize, out returnedSize);
					if (success)
					{
						elevationResult = (TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(elevationTypePtr);
						bool isProcessAdmin = elevationResult == TOKEN_ELEVATION_TYPE.TokenElevationTypeFull;
						return isProcessAdmin;
					}
					else
					{
						throw new ApplicationException("Unable to determine the current elevation.");
					}
				}
				else
				{
					WindowsIdentity identity = WindowsIdentity.GetCurrent();
					WindowsPrincipal principal = new WindowsPrincipal(identity);
					bool result = principal.IsInRole(WindowsBuiltInRole.Administrator);
					return result;
				}
			}
		}
	}
}
