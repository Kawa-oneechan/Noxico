using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;

namespace Noxico
{
	public partial class UpdateForm : Form
	{
		private bool didFirstFile;
		private string server = "http://helmet.kafuka.org/noxico/files/";
		private WebClient wc = new WebClient();

		public UpdateForm()
		{
			InitializeComponent();

			Refresh();
			Show();

			wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(wc_DownloadProgressChanged);
			wc.DownloadFileCompleted += new AsyncCompletedEventHandler(wc_DownloadFileCompleted);
			label1.Text = "Downloading executable...";
			wc.DownloadFileAsync(new Uri(server + "Noxico.exe"), "Noxico._xe");
						
		}

		void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			if (!didFirstFile)
			{
				didFirstFile = true;
				label1.Text = "Downloading data file...";
				wc.DownloadFileAsync(new Uri(server + "Noxico.nox"), "Noxico._ox");
			}
			else
			{
				File.WriteAllText("update.bat", "del Noxico.exe" + Environment.NewLine + "del Noxico.nox" + Environment.NewLine + "ren Noxico._xe Noxico.exe" + Environment.NewLine + "ren Noxico._ix Noxico.nox" + Environment.NewLine + "start Noxico.exe" + Environment.NewLine + "del update.bat");
				System.Diagnostics.Process.Start("update.bat");
				Application.Exit();
			}
		}

		void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			label2.Text = string.Format("{0} of {1}", e.BytesReceived, e.TotalBytesToReceive);
			progressBar1.Value = e.ProgressPercentage;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}
	}
}
