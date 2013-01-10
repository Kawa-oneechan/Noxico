using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Noxico
{
	public partial class ErrorForm : Form
	{
		public ErrorForm(Exception x)
		{
			InitializeComponent();
			this.Text = Application.ProductName + " " + Application.ProductVersion;
			pictureBox1.Image = Noxico.Properties.Resources.app.ToBitmap();

			var sb = new StringBuilder();
			var typeName = x.GetType().Name;
			if (typeName != "Exception")
				sb.AppendLine("Exception type: " + typeName);
			sb.AppendLine("Main message: " + x.Message);
			sb.AppendLine();
			sb.AppendLine("Stack trace:");
			var trace = x.StackTrace;

			var tokenLoad = new Regex(@"(?:.*)Noxico.Token.LoadFromFile(?:.*)", RegexOptions.Multiline);
			if (tokenLoad.IsMatch(trace))
			{
				var matches = tokenLoad.Matches(trace);
				if (matches.Count > 2)
				{
					var a = trace.Remove(matches[1].Index);
					var b = trace.Substring(matches[matches.Count - 1].Index + matches[matches.Count - 1].Length);
					trace = a + "(lots of Token.LoadFromFile calls removed for clarity)" + b;
				}
			}
			var formsLoad = new Regex(@"(?:.*)System.Windows.Forms(?:.*)", RegexOptions.Multiline);
			if (formsLoad.IsMatch(trace))
			{
				var matches = formsLoad.Matches(trace);
				if (matches.Count > 1)
					trace = trace.Remove(matches[1].Index) + "(WinForms stuff removed for clarity)";
			}

			sb.AppendLine(trace);
			sb.AppendLine();
			sb.AppendLine("Background info:");
			sb.AppendLine(Application.ProductName + " " + Application.ProductVersion);
			sb.AppendLine(Environment.OSVersion.ToString());
			sb.Append(Environment.Is64BitOperatingSystem ? "64-bit OS, " : "32-bit OS, ");
			sb.AppendLine(Environment.Is64BitProcess ? "64-bit process." : "32-bit process.");
			sb.AppendLine(Environment.CurrentDirectory);

			textBox1.Text = sb.ToString();

			if (typeName == "SecurityException" && x.Message.Contains("Tried to call"))
			{
				label3.Text = "The problem is a script error." + Environment.NewLine + Environment.NewLine;
				if (x.Message.Contains("not allowed."))
					label3.Text += "If you are a mod developer and this is your work, you tried to call a method that is meant for internal usage only.";
				else
					label3.Text += "Kawa made a mistake somewhere and tried to call a method that he himself marked as \"for Javascript use only\". What a dumbass. Call him out on it if you want, and it'll be fixed ASAP.";
			}
			else if (textBox1.Text.Contains("Player.LoadFromFile"))
			{
				label3.Text = "The problem is a corrupted player state." + Environment.NewLine + Environment.NewLine + "It's too bad we can't tell which world's player data it is, so the best we can suggest is that you delete (or rename) each world's player.bin file until you can proceed. You'll need to start over, though.";
			}
			else if (x.Message.Contains("open an old worldsave"))
			{
				label3.Text = "Exactly what it says on the tin. The worldsave format has changed and you somehow managed to bypass the initial checks." + Environment.NewLine + Environment.NewLine + "Delete the worldsave and start over.";
			}
			else if (x.Message.Contains("Expected to find"))
			{
				label3.Text = "The problem is a corrupted board state. Some data was expected but not found." + Environment.NewLine + Environment.NewLine + "Delete the worldsave and start over.";
			}
			else if (x.Message.Contains("error in the INI file"))
			{
				label3.Text = "Find the game's INI file (noxico.ini, in " + Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + " if not running in portable mode) and see if there's any repeated settings, such as the one mentioned on the Data tab.";
			}
			else if (typeName == "EndOfStreamException")
			{
				if (textBox1.Text.Contains("NoxicoGame.LoadGame"))
					label3.Text = "The problem is a corrupted world state, or maybe the savegame is from an older version that's missing new data." + Environment.NewLine + Environment.NewLine + "Delete the worldsave and start over.";
				else if (textBox1.Text.Contains("Board.LoadFromFile"))
					label3.Text = "The problem is a corrupted board state, or maybe the savegame is from an older version that's missing new data." + Environment.NewLine + Environment.NewLine + "Delete the worldsave and start over.";
				else
					label3.Text = "Something is missing a piece of data, but that's all we know. This is one point where posting the exception data would be helpful.";
			}
			else if (typeName == "FileNotFoundException")
			{
				if (textBox1.Text.Contains("not found in the MIX"))
					label3.Text = "The requested file was not found in the MIX archives, nor in the \\data override folder." + Environment.NewLine + Environment.NewLine + "If it was not music or sound, and you don't have any mods installed, it's probably a corrupted Noxico.mix file. Redownload it to try and fix it. If that doesn't help, contact Kawa.";
				else if (textBox1.Text.Contains("Required DLL"))
					label3.Text = "The requested DLL file was found missing, and is required to properly run the game. Redownload the game to regain all the required DLL files." + Environment.NewLine + Environment.NewLine + "We checked for this at startup so things would remain graceful.";
				else if (textBox1.Text.Contains("Board #"))
					label3.Text = "There's a board missing from the current worldsave. There's not much you can do about that, except to remove the worldsave altogether and start over.";
				else
					label3.Text = "Some file was expected to be found, but is missing. This is one point where posting the exception data would be helpful.";
			}
			else if (typeName == "XmlException")
			{
				if (textBox1.Text.Contains("start tag on line"))
					label3.Text = "An XML file somewhere has a malformed structure." + Environment.NewLine + Environment.NewLine + "Check the stack trace for a reference to \"GetXMLDocument\", then look at the line directly below that one. That's where the XML file was requested from, and that's what you should bring up on the support forum." + Environment.NewLine + Environment.NewLine + "For example, if the line directly below \"at Noxico.Mix.GetXMLDocument\" is \"at Noxico.WorldGen.LoadBiomes\", the problem is in LoadBiomes, or rather biomes.xml, and that should be mentioned as the critical point.";
				else
					label3.Text = "An XML file somewhere has gone wrong. This is one point where posting the exception data would be helpful.";
			}
			else if (typeName == "ArgumentException" && textBox1.Text.Contains("System.Drawing.Bitmap..ctor"))
				label3.Text = "The problem is a bitmap that is not actually a bitmap." + Environment.NewLine + Environment.NewLine + "Noxico only uses PNG files, but can load BMP, GIF, and JPEG as well. If an image is requested, but the file is not actually an image of one of those types, or not even an image at all, things break.";
			else
			{
				tabControl1.TabPages.RemoveAt(1);
			}
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Diagnostics.Process.Start("http://helmet.kafuka.org/noxico/board");
		}

		private void button2_Click(object sender, EventArgs e)
		{
			Clipboard.Clear();
			Clipboard.SetText(textBox1.Text);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Application.ExitThread();
		}
	}
}
