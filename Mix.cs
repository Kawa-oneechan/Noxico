using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using Bitmap = System.Drawing.Bitmap;

namespace Noxico
{
	public static class Mix
	{
		private class MixFileEntry
		{
			public string MixFile, Filename;
			public int Offset, Length;
			public bool NeedsPremultiply;
			public bool IsCompressed;
		}

		private static Dictionary<string, MixFileEntry> fileList;

		private static Dictionary<string, string> stringCache;

		/// <summary>
		/// Populates the Mix database for usage
		/// </summary>
		public static void Initialize(string mainFile = "Noxico")
		{
			Program.WriteLine("Mix.Initialize()");
			fileList = new Dictionary<string, MixFileEntry>();
			stringCache = new Dictionary<string, string>();
			var mixfiles = new List<string>() { mainFile + ".mix" };
			mixfiles.AddRange(Directory.EnumerateFiles(".", "*.mix").Select(x => x.Substring(2)).Where(x => !x.Equals(mainFile + ".mix", StringComparison.OrdinalIgnoreCase)));
			Program.WriteLine("Mixfiles enumerated. Indexing contents...");
			foreach (var mixfile in mixfiles)
			{
				if (!File.Exists(mixfile))
				{
					Program.WriteLine("Mixfile \"{0}\" in list but nonexistant.", mixfile);
					continue;
				}
				using (var mStream = new BinaryReader(File.Open(mixfile, FileMode.Open)))
				{
					var header = mStream.ReadChars(8);
					if (!(new string(header).Equals("KawaPack", StringComparison.Ordinal)))
						throw new FileLoadException(string.Format("MIX file '{0}' has an incorrect header.", mixfile));
					var count = mStream.ReadInt32();
					mStream.ReadInt32();
					for (var i = 0; i < count; i++)
					{
						var entry = new MixFileEntry();
						entry.Offset = mStream.ReadInt32();
						entry.Length = mStream.ReadInt32();
						var flags = mStream.ReadByte();
						entry.NeedsPremultiply = ((flags & 1) == 1);
						entry.IsCompressed = ((flags & 2) == 2);
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

		public static bool FileExists(string fileName)
		{
			if (File.Exists(Path.Combine("data", fileName)))
				return true;
			return (fileList.ContainsKey(fileName));
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns a <see cref="MemoryStream"/> for it.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="MemoryStream"/> if found, <see cref="null"/> otherwise.</returns>
		public static Stream GetStream(string fileName)
		{
			Program.WriteLine("Mix.GetStream({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return new MemoryStream(File.ReadAllBytes(Path.Combine("data", fileName)));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			MemoryStream ret;
			var entry = fileList[fileName];
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
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="string"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static string GetString(string fileName, bool cache = true)
		{
			if (cache && stringCache.ContainsKey(fileName))
				return stringCache[fileName];
			Program.WriteLine("Mix.GetString({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return File.ReadAllText(Path.Combine("data", fileName));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			var bytes = GetBytes(fileName);
			var ret = Encoding.UTF8.GetString(bytes);
			if (cache)
				stringCache[fileName] = ret;
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as an <see cref="XmlDocument"/>, merging in the nodes from same-named files in subdirectories to allow mod expansions.
		/// </summary>
		/// <param name="fileName">The file to find. If this is in the root of the Mix system, mod expansions are merged in.</param>
		/// <returns>Returns an <see cref="XmlDocument"/> with the file(s)'s contents if found, <see cref="null"/> otherwise.</returns>
		public static XmlDocument GetXmlDocument(string fileName, bool injectOnTop = false)
		{
			var x = new System.Xml.XmlDocument();
			if (fileName.Contains('\\'))
			{
				var s = GetString(fileName);
				if (s == null)
					throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
				x.LoadXml(s);
				return x;
			}

			var otherFiles = fileList.Keys.Where(e => e != fileName && e.EndsWith(fileName)).ToList();
			if (Directory.Exists("data"))
			{
				var externalOtherFiles = Directory.GetFiles("data", "*.xml", SearchOption.AllDirectories).Select(e => e.Substring(5)).Where(e => e != fileName && e.EndsWith(fileName));
				otherFiles.AddRange(externalOtherFiles);
			}
			if (otherFiles.Count() == 0)
			{
				x.LoadXml(GetString(fileName));
				return x;
			}

			//Program.WriteLine("{0} has mod expansions!", fileName);
			x.LoadXml(GetString(fileName));
			foreach (var f in otherFiles)
			{
				//Program.WriteLine("Splicing in {0}...", f);
				var otherX = new System.Xml.XmlDocument();
				otherX.LoadXml(GetString(f));
				//x.DocumentElement.InnerXml = otherX.DocumentElement.InnerXml + x.DocumentElement.InnerXml;
				var docElement = x.DocumentElement;
				var newStuff = new StringBuilder();
				foreach (var e in otherX.DocumentElement.ChildNodes.OfType<XmlNode>())
				{
					if (e is XmlElement)
					{
						var eX = e as XmlElement;
						if (eX.HasAttribute("id"))
						{
							var xPath = "//" + eX.Name + "[@id=\"" + eX.GetAttribute("id") + "\"]";
							var otherEX = docElement.SelectSingleNode(xPath);
							if (otherEX != null)
							{
								otherEX.InnerXml = eX.InnerXml;
								otherEX.Attributes.RemoveAll();
								foreach (var attrib in eX.Attributes.OfType<XmlAttribute>())
								{
									var newAttrib = x.CreateAttribute(attrib.Name);
									newAttrib.Value = attrib.Value;
									otherEX.Attributes.Append(newAttrib);
								}
								//so we don't add it later on
								continue;
							}
						}
					}
					newStuff.AppendLine(e.OuterXml);
				}
				if (injectOnTop)
					docElement.InnerXml = newStuff.ToString() + docElement.InnerXml;
				else
					docElement.InnerXml += newStuff.ToString();
				//foreach (var e in otherX.DocumentElement.ChildNodes.OfType<XmlNode>())
				//	x.DocumentElement.InnerXml = (injectOnTop ? e.OuterXml + x.DocumentElement.InnerXml : x.DocumentElement.InnerXml + e.OuterXml);
			}

			//Prioritize nodes
			var priorityNodes = x.SelectNodes("//*[@priority='high']");
			if (priorityNodes.Count > 0)
			{
				var placeholder = x.CreateComment("Priority placeholder");
				x.DocumentElement.InsertBefore(placeholder, x.DocumentElement.FirstChild);
				foreach (var n in priorityNodes.OfType<XmlElement>())
				{
					var p = n.ParentNode;
					p.RemoveChild(n);
					p.InsertBefore(n, placeholder);
				}
			}

			return x;
		}

		public static List<Token> GetTokenTree(string fileName, bool cache = false)
		{
			var carrier = new TokenCarrier();
			var tml = String.Empty;
				
			var otherFiles = fileList.Keys.Where(e => e != fileName && e.EndsWith(fileName)).ToList();
			if (Directory.Exists("data"))
			{
				var externalOtherFiles = Directory.GetFiles("data", "*.tml", SearchOption.AllDirectories).Select(e => e.Substring(5)).Where(e => e != fileName && e.EndsWith(fileName));
				otherFiles.AddRange(externalOtherFiles);
			}
			if (otherFiles.Count() == 0)
			{
				tml = GetString(fileName, cache);
			}
			else
			{
				Program.WriteLine("{0} has mod expansions!", fileName);
				tml = GetString(fileName, cache);
				var onTop = tml.Contains("-- #mergeontop");
				foreach (var f in otherFiles)
				{
					Program.WriteLine("Splicing in {0}...", f);
					if (onTop)
					{
						tml = "\n\n-- #log Splice: " + f + "\n\n" + tml;
						tml = GetString(f, cache) + tml;
					}
					else
					{
						tml += "\n\n-- #log Splice: " + f + "\n\n";
						tml += GetString(f, cache);
					}
				}
			}

			var replaceKeys = new List<string>();
			while (tml.Contains("-- #replace"))
			{
				var replaceStart = tml.IndexOf("-- #replace");
				var replaceEnd = tml.IndexOf('\n', replaceStart + 1);
				var replaceKey = tml.Substring(replaceStart, replaceEnd - replaceStart).Trim().Split(' ')[2];
				replaceKeys.Add(replaceKey);
				tml = tml.Substring(0, replaceStart - 1) + tml.Substring(replaceEnd);
			}
			var mergeDeep = tml.Contains("-- #mergedeep");
			carrier.Tokenize(tml);

			if (replaceKeys.Count > 0)
			{
				foreach (var key in replaceKeys)
				{
					for (var i = 0; i < carrier.Tokens.Count; i++)
					{
						if (carrier.Tokens[i].Name == key)
						{
							var firstKey = carrier.Tokens[i];
							var value = firstKey.Text;
							for (var j = i + 1; j < carrier.Tokens.Count; j++)
							{
								if (carrier.Tokens[j].Name == key && carrier.Tokens[j].Text == value)
								{
									var newKey = carrier.Tokens[j];
									if (mergeDeep)
									{
										foreach (var t in newKey.Tokens)
										{
											var ft = firstKey.GetToken(t.Name);
											if (ft == null)
												ft = firstKey.AddToken(t.Name);
											ft.Text = t.Text;
											ft.Value = t.Value;
											ft.Tokens.Clear();
											ft.Tokens.AddRange(t.Tokens);
										}
									}
									else
									{
										firstKey.Tokens.Clear();
										firstKey.Tokens.AddRange(newKey.Tokens);
									}
									carrier.RemoveToken(j);
									j--;
								}
							}
						}
					}
				}
			}
			return carrier.Tokens;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="Bitmap"/>.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="Bitmap"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		//TODO: cache the returns.
		public static Bitmap GetBitmap(string fileName)
		{
			Program.WriteLine("Mix.GetBytes({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return new Bitmap(Path.Combine("data", fileName));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			Bitmap ret;
			var entry = fileList[fileName];
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
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="byte[]"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static byte[] GetBytes(string fileName)
		{
			Program.WriteLine("Mix.GetBytes({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return File.ReadAllBytes(Path.Combine("data", fileName));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			byte[] ret;
			var entry = fileList[fileName];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				if (!entry.IsCompressed)
				{
					ret = mStream.ReadBytes(entry.Length);
				}
				else
				{
					var cStream = new MemoryStream(mStream.ReadBytes(entry.Length));
					var decompressor = new System.IO.Compression.GZipStream(cStream, System.IO.Compression.CompressionMode.Decompress);
					var outStream = new MemoryStream();
					var buffer = new byte[1024];
					var recieved = 1;
					while (recieved > 0)
					{
						recieved = decompressor.Read(buffer, 0, buffer.Length);
						outStream.Write(buffer, 0, recieved);
					}
					ret = new byte[outStream.Length];
					outStream.Seek(0, SeekOrigin.Begin);
					outStream.Read(ret, 0, ret.Length);
				}
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
			{
				var getFiles = Directory.GetFiles(Path.Combine("data", path), "*", SearchOption.AllDirectories);
				ret.AddRange(getFiles.Select(x => x.Substring("data\\".Length)).Where(x => !ret.Contains(x)));
			}
			return ret.ToArray();
		}
		
		public static void GetFileRange(string fileName, out int offset, out int length, out string mixFile)
		{
			offset = -1;
			length = -1;
			mixFile = string.Empty;
			if (!fileList.ContainsKey(fileName))
				return;
			var entry = fileList[fileName];
			offset = entry.Offset;
			length = entry.Length;
			mixFile = entry.MixFile;
		}

		public static string[] GetFilesWithPattern(string pattern)
		{
			var ret = new List<string>();
			var regex = new System.Text.RegularExpressions.Regex(pattern.Replace("*", "(.*)"));
			foreach (var entry in fileList.Values.Where(x => regex.IsMatch(x.Filename)))
				ret.Add(entry.Filename);
			if (Directory.Exists("data"))
			{
				var getFiles = Directory.GetFiles("data", pattern, SearchOption.AllDirectories);
				ret.AddRange(getFiles);
			}
			return ret.ToArray();
		}

		public static void SpreadEm()
		{
			Program.WriteLine("Spreadin' em...");
			if (fileList == null || fileList.Count == 0)
				Initialize();
			foreach (var entry in fileList.Values)
			{
				if (entry.Length == 0)
				{
					Program.WriteLine("* Bogus entry with zero length: \"{0}\", offset {1}", entry.Filename, entry.Offset);
					continue;
				}
				var targetPath = Path.Combine("data", /* entry.MixFile.Remove(entry.MixFile.Length - 4),*/ entry.Filename);
				var targetDir = Path.GetDirectoryName(targetPath);
				if (!Directory.Exists(targetDir))
					Directory.CreateDirectory(targetDir);
				using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
				{
					mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
					if (!entry.IsCompressed)
					{
						File.WriteAllBytes(targetPath, mStream.ReadBytes(entry.Length));
					}
					else
					{
						var cStream = new MemoryStream(mStream.ReadBytes(entry.Length));
						var decompressor = new System.IO.Compression.GZipStream(cStream, System.IO.Compression.CompressionMode.Decompress);
						var outStream = new MemoryStream();
						var buffer = new byte[1024];
						var recieved = 1;
						while (recieved > 0)
						{
							recieved = decompressor.Read(buffer, 0, buffer.Length);
							outStream.Write(buffer, 0, recieved);
						}
						var unpacked = new byte[outStream.Length];
						outStream.Seek(0, SeekOrigin.Begin);
						outStream.Read(unpacked, 0, unpacked.Length);
						File.WriteAllBytes(targetPath, unpacked);
					} 
				}
			}
			Program.WriteLine("All entries in mix files extracted. Happy Hacking.");
		}
	}
}