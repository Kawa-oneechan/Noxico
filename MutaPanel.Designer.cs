namespace Noxico
{
	partial class MutaPanel
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.sourceBox = new System.Windows.Forms.RichTextBox();
			this.targetBox = new System.Windows.Forms.RichTextBox();
			this.resultBox = new System.Windows.Forms.RichTextBox();
			this.messagesBox = new System.Windows.Forms.RichTextBox();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.menuStrip2 = new System.Windows.Forms.MenuStrip();
			this.initialMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.generateCharacterHereToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.tagetMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.deltasBox = new System.Windows.Forms.RichTextBox();
			this.todoSelectBodyplanHereToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStrip2.SuspendLayout();
			this.SuspendLayout();
			// 
			// sourceBox
			// 
			this.sourceBox.Location = new System.Drawing.Point(11, 24);
			this.sourceBox.Name = "sourceBox";
			this.sourceBox.Size = new System.Drawing.Size(242, 488);
			this.sourceBox.TabIndex = 2;
			this.sourceBox.Text = "Source goes here.\n";
			// 
			// targetBox
			// 
			this.targetBox.Location = new System.Drawing.Point(259, 24);
			this.targetBox.Name = "targetBox";
			this.targetBox.Size = new System.Drawing.Size(242, 488);
			this.targetBox.TabIndex = 3;
			this.targetBox.Text = "Target goes here.";
			// 
			// resultBox
			// 
			this.resultBox.Location = new System.Drawing.Point(755, 24);
			this.resultBox.Name = "resultBox";
			this.resultBox.Size = new System.Drawing.Size(242, 488);
			this.resultBox.TabIndex = 4;
			this.resultBox.Text = "Result goes here.";
			// 
			// messagesBox
			// 
			this.messagesBox.Location = new System.Drawing.Point(1003, 24);
			this.messagesBox.Name = "messagesBox";
			this.messagesBox.Size = new System.Drawing.Size(242, 488);
			this.messagesBox.TabIndex = 5;
			this.messagesBox.Text = "Mesasges go here.";
			// 
			// menuStrip1
			// 
			this.menuStrip1.Location = new System.Drawing.Point(0, 24);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(1255, 24);
			this.menuStrip1.TabIndex = 6;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// menuStrip2
			// 
			this.menuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.initialMenu,
            this.tagetMenu});
			this.menuStrip2.Location = new System.Drawing.Point(0, 0);
			this.menuStrip2.Name = "menuStrip2";
			this.menuStrip2.Size = new System.Drawing.Size(1255, 24);
			this.menuStrip2.TabIndex = 7;
			this.menuStrip2.Text = "menuStrip2";
			// 
			// initialMenu
			// 
			this.initialMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.generateCharacterHereToolStripMenuItem});
			this.initialMenu.Name = "initialMenu";
			this.initialMenu.Size = new System.Drawing.Size(145, 20);
			this.initialMenu.Text = "Choose Initial Character";
			// 
			// generateCharacterHereToolStripMenuItem
			// 
			this.generateCharacterHereToolStripMenuItem.Name = "generateCharacterHereToolStripMenuItem";
			this.generateCharacterHereToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
			this.generateCharacterHereToolStripMenuItem.Text = "todo select sample character";
			this.generateCharacterHereToolStripMenuItem.Click += new System.EventHandler(this.generateCharacterHereToolStripMenuItem_Click);
			// 
			// tagetMenu
			// 
			this.tagetMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.todoSelectBodyplanHereToolStripMenuItem});
			this.tagetMenu.Name = "tagetMenu";
			this.tagetMenu.Size = new System.Drawing.Size(149, 20);
			this.tagetMenu.Text = "Choose Target Bodyplan";
			// 
			// deltasBox
			// 
			this.deltasBox.Location = new System.Drawing.Point(507, 24);
			this.deltasBox.Name = "deltasBox";
			this.deltasBox.Size = new System.Drawing.Size(242, 488);
			this.deltasBox.TabIndex = 8;
			this.deltasBox.Text = "Deltas go here.";
			// 
			// todoSelectBodyplanHereToolStripMenuItem
			// 
			this.todoSelectBodyplanHereToolStripMenuItem.Name = "todoSelectBodyplanHereToolStripMenuItem";
			this.todoSelectBodyplanHereToolStripMenuItem.Size = new System.Drawing.Size(211, 22);
			this.todoSelectBodyplanHereToolStripMenuItem.Text = "todo select bodyplan here";
			// 
			// MutaPanel
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1255, 521);
			this.Controls.Add(this.deltasBox);
			this.Controls.Add(this.messagesBox);
			this.Controls.Add(this.resultBox);
			this.Controls.Add(this.targetBox);
			this.Controls.Add(this.sourceBox);
			this.Controls.Add(this.menuStrip1);
			this.Controls.Add(this.menuStrip2);
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "MutaPanel";
			this.Text = "MutaTestPanel";
			this.menuStrip2.ResumeLayout(false);
			this.menuStrip2.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.RichTextBox sourceBox;
		private System.Windows.Forms.RichTextBox targetBox;
		private System.Windows.Forms.RichTextBox resultBox;
		private System.Windows.Forms.RichTextBox messagesBox;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.MenuStrip menuStrip2;
		private System.Windows.Forms.ToolStripMenuItem initialMenu;
		private System.Windows.Forms.ToolStripMenuItem tagetMenu;
		private System.Windows.Forms.RichTextBox deltasBox;
		private System.Windows.Forms.ToolStripMenuItem generateCharacterHereToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem todoSelectBodyplanHereToolStripMenuItem;
	}
}