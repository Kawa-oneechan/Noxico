using System;

namespace Noxico
{
	class Vista
	{
		[System.Runtime.InteropServices.DllImport("shell32.dll")]
		private static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
		public static readonly Guid SavedGames = new Guid("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");
		//Add more GUIDs here whenever interesting.

		public static string GetInterestingPath(Guid target)
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version.Major < 6)
				return null;

			string ret = null;
			IntPtr pPath;
			if (SHGetKnownFolderPath(SavedGames, 0, IntPtr.Zero, out pPath) == 0)
			{
				ret = System.Runtime.InteropServices.Marshal.PtrToStringUni(pPath);
				System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pPath);
			}
			return ret;
		}
	}
}
