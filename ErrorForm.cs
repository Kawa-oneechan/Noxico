using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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
			sb.AppendLine("Main message: " + x.Message);
			sb.AppendLine();
			sb.AppendLine("Stack trace:");
			sb.AppendLine(x.StackTrace);
			sb.AppendLine();
			sb.AppendLine("Background info:");
			sb.AppendLine(Application.ProductName + " " + Application.ProductVersion);
			sb.AppendLine(Environment.OSVersion.ToString());
			sb.Append(Environment.Is64BitOperatingSystem ? "64-bit OS, " : "32-bit OS, ");
			sb.AppendLine(Environment.Is64BitProcess ? "64-bit process." : "32-bit process.");
			sb.AppendLine(Environment.CurrentDirectory);

			textBox1.Text = sb.ToString();
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
