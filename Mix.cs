using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Drawing;

namespace Noxico
{
	public static class Mix
	{
		private class MixFileEntry
		{
			public string MixFile, Filename;
			public int Offset, Length;
			public bool IsAlphaPNG;
		}

		private static Dictionary<string, MixFileEntry> fileList;

		/// <summary>
		/// Populates the Mix database for usage
		/// </summary>
		public static void Initialize(string mainFile = "main")
		{
			Console.WriteLine("Mix.Initialize()");
			fileList = new Dictionary<string, MixFileEntry>();
			var mixfiles = new List<string>() { mainFile + ".mix" };
			mixfiles.AddRange(Directory.EnumerateFiles(".", "*.mix").Select(x => x.Substring(2)).Where(x => !x.Equals(mainFile + ".mix", StringComparison.InvariantCultureIgnoreCase)));
			Console.WriteLine("Mixfiles enumerated. Indexing contents...");
			foreach (var mixfile in mixfiles)
			{
				if (!File.Exists(mixfile))
				{
					Console.WriteLine("Mixfile \"{0}\" in list but nonexistant.", mixfile);
					continue;
				}
				using (var mStream = new BinaryReader(File.Open(mixfile, FileMode.Open)))
				{
					var header = mStream.ReadChars(8);
					var count = mStream.ReadInt32();
					var dummy = mStream.ReadInt32();
					for (var i = 0; i < count; i++)
					{
						var entry = new MixFileEntry();
						entry.Offset = mStream.ReadInt32();
						entry.Length = mStream.ReadInt32();
						entry.IsAlphaPNG = mStream.ReadByte() == 1;
						var fileNameC = mStream.ReadChars(55);
						var fileName = new string(fileNameC).Replace("\0", "");
						entry.MixFile = mixfile;
						entry.Filename = fileName;
						if (fileList.ContainsKey(fileName))
							fileList[fileName] = entry;
						else
							fileList.Add(fileName, entry);
					}
				}
			}
		}

		public static bool FileExists(string filename)
		{
			if (File.Exists(Path.Combine("data", filename)))
				return true;
			return (fileList.ContainsKey(filename));
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns a <see cref="MemoryStream"/> for it.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="MemoryStream"/> if found, <see cref="null"/> otherwise.</returns>
		public static Stream GetStream(string filename)
		{
			Console.WriteLine("Mix.GetStream({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return new MemoryStream(File.ReadAllBytes(Path.Combine("data", filename)));
			if (!fileList.ContainsKey(filename))
				return null;
			MemoryStream ret;
			var entry = fileList[filename];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				ret = new MemoryStream(mStream.ReadBytes(entry.Length));
			}
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="string"/>.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="string"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static string GetString(string filename)
		{
			Console.WriteLine("Mix.GetString({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return File.ReadAllText(Path.Combine("data", filename));
			if (!fileList.ContainsKey(filename))
				return null;
			var bytes = GetBytes(filename);
			var ret = Encoding.UTF8.GetString(bytes);
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as an <see cref="XMLDocument"/>, merging in the nodes from same-named files in subdirectories to allow mod expansions.
		/// </summary>
		/// <param name="filename">The file to find. If this is in the root of the Mix system, mod expansions are merged in.</param>
		/// <returns>Returns an <see cref="XMLDocument"/> with the file(s)'s contents if found, <see cref="null"/> otherwise.</returns>
		public static XmlDocument GetXMLDocument(string filename, bool injectOnTop = false)
		{
			var x = new System.Xml.XmlDocument();
			if (filename.Contains('\\'))
			{
				var s = GetString(filename);
				if (s == null)
					return null;
				x.LoadXml(s);
				return x;
			}

			var otherFiles = fileList.Keys.Where(e => e != filename && e.EndsWith(filename));
			if (otherFiles.Count() == 0)
			{
				x.LoadXml(GetString(filename));
				return x;
			}

			Console.WriteLine("{0} has mod expansions!", filename);
			x.LoadXml(GetString(filename));
			foreach (var f in otherFiles)
			{
				Console.WriteLine("Splicing in {0}...", f);
				var otherX = new System.Xml.XmlDocument();
				otherX.LoadXml(GetString(f));
				foreach (var e in otherX.DocumentElement.ChildNodes.OfType<XmlNode>())
					x.DocumentElement.InnerXml = (injectOnTop ? e.OuterXml + x.DocumentElement.InnerXml : x.DocumentElement.InnerXml + e.OuterXml);
			}
			return x;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="Bitmap"/>.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="Bitmap"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		//TODO: cache the returns.
		public static Bitmap GetBitmap(string filename)
		{
			Console.WriteLine("Mix.GetBytes({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return new Bitmap(Path.Combine("data", filename));
			if (!fileList.ContainsKey(filename))
				return null;
			Bitmap ret;
			var entry = fileList[filename];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				using (var str = new MemoryStream(mStream.ReadBytes(entry.Length)))
				{
					ret = (Bitmap)Bitmap.FromStream(str);
				}
			}
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="byte[]"/>.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="byte[]"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static byte[] GetBytes(string filename)
		{
			Console.WriteLine("Mix.GetBytes({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return File.ReadAllBytes(Path.Combine("data", filename));
			if (!fileList.ContainsKey(filename))
				return null;
			byte[] ret;
			var entry = fileList[filename];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				ret = mStream.ReadBytes(entry.Length);
			}
			return ret;
		}

		/// <summary>
		/// Returns a list of all the files in a given path.
		/// </summary>
		/// <param name="path">The path to look in.</param>
		/// <returns>A <see cref="string[]"/> with all the files found, which may be empty.</returns>
		public static string[] GetFilesInPath(string path)
		{
			var ret = new List<string>();
			foreach (var entry in fileList.Values.Where(x => x.Filename.StartsWith(path)))
				ret.Add(entry.Filename);
			if (Directory.Exists(Path.Combine("data", path)))
				ret.AddRange(Directory.GetFiles(Path.Combine("data", path)).Select(x => x.Substring("data\\".Length)).Where(x => !ret.Contains(x)));
			return ret.ToArray();
		}
		
		public static void GetFileRange(string filename, out int offset, out int length, out string mixFile)
		{
			offset = -1;
			length = -1;
			mixFile = string.Empty;
			if (!fileList.ContainsKey(filename))
				return;
			var entry = fileList[filename];
			offset = entry.Offset;
			length = entry.Length;
			mixFile = entry.MixFile;
		}
	}
}