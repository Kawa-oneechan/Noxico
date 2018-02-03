using System;
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

			if (x.InnerException is Neo.IronLua.LuaParseException)
			{
				label3.Text = x.Message;
				//x = x.InnerException as Neo.IronLua.LuaParseException;
				//If we unwrap it, we can't see the context in the main copypasta.
				tabControl1.TabPages[1].Text = "Lua context";
				tabControl1.SelectedIndex = 1;
			}

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

			if (textBox1.Text.Contains("Player.LoadFromFile"))
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
			else if (x.Message.Contains("bodyplan is missing the"))
			{
				label3.Text = "The specified bodyplan data is missing a token that's required for various things to work correctly. Contact Kawa and quote only the exception's message.";
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
				if (textBox1.Text.Contains("not found in the NOX"))
					label3.Text = "The requested file was not found in the NOX archives, nor in the \\data override folder." + Environment.NewLine + Environment.NewLine + "If it was not music or sound, and you don't have any mods installed, it's probably a corrupted Noxico.nox file. Redownload it to try and fix it. If that doesn't help, contact Kawa.";
				else if (textBox1.Text.Contains("Required DLL"))
					label3.Text = "The requested DLL file was found missing, and is required to properly run the game. Redownload the game to regain all the required DLL files." + Environment.NewLine + Environment.NewLine + "We checked for this at startup so things would remain graceful.";
				else if (textBox1.Text.Contains("Board #"))
					label3.Text = "There's a board missing from the current worldsave. There's not much you can do about that, except to remove the worldsave altogether and start over.";
				else
					label3.Text = "Some file was expected to be found, but is missing. This is one point where posting the exception data would be helpful.";
			}
			else if (typeName == "ArgumentException" && textBox1.Text.Contains("System.Drawing.Bitmap..ctor"))
				label3.Text = "The problem is a bitmap that is not actually a bitmap." + Environment.NewLine + Environment.NewLine + "Noxico only uses PNG files, but can load BMP, GIF, and JPEG as well. If an image is requested, but the file is not actually an image of one of those types, or not even an image at all, things break.";
			else if (textBox1.Text.Contains("bodyplan is defined twice"))
				label3.Text = "Probably, a mod tried to define its own version of the specified bodyplan, which is not allowed." + Environment.NewLine + Environment.NewLine + "Try to identify the offender and remove it, then contact the mod's author.";
			else if (textBox1.Text.Contains("indented too far"))
				label3.Text = "A TML file somewhere has a malformed structure, as described on the Data tab." + Environment.NewLine + Environment.NewLine + "Check the stack trace for a reference to \"GetTokenTree\", then look at the line directly below that one. That's where the TML file was requested from, and that's what you should bring up on the support forum." + Environment.NewLine + Environment.NewLine + "For example, if the line directly below \"at Noxico.Mix.GetTokenTree\" is \"at Noxico.Character.GetUnique\", the problem is in GetUnique, or rather uniques.tml, and that should be mentioned as the critical point.";
			else if (textBox1.Text.Contains("has an incorrect header"))
				label3.Text = "The NOX file mentioned is technically just a ZIP file. The system that parses this file is -very- picky and does not play nice with certain features that a ZIP file may support. For example, ZIP64 is not supported, nor are encryption, comments, or any storage method other than Deflate and Store. At any rate, something was encountered that the loader did not except.";
			else if (textBox1.Text.Contains("Can not format") && textBox1.Text.Contains("to dec"))
				label3.Text = "This may indicate an attempt to use a string value where a number is expected. For example, using the addition \"+\" instead of concatenation \"..\". A string value containing a number is okay, though.";
			else if (textBox1.Text.Contains("No operator is defined") && textBox1.Text.Contains("Object Add Object"))
				label3.Text = "This may indicate an undefined variable, or an expression of a type that can't be mathematically added to another.";

			if (label3.Text.IsBlank())
				tabControl1.TabPages.RemoveAt(1);
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

		private static string PrepareParseError(string block, int line, int column)
		{
			var lines = block.Split('\r');
			if (lines.Length == 1)
				return lines[0].Trim() + " <---";
			var first = line - 4;
			var last = line + 4;
			if (first < 0) first = 0;
			if (last > lines.Length) last = lines.Length;
			var ret = new StringBuilder();
			for (var i = first; i < last; i++)
			{
				ret.Append(lines[i].Trim());
				if (i == line - 1)
					ret.Append(" <---");
				ret.AppendLine();
			}
			return ret.ToString();
		}
	}
}
