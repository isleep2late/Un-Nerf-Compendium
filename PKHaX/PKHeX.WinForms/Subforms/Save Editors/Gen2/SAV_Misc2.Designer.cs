namespace PKHeX.WinForms
{
    partial class SAV_Misc2
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
            B_Save = new System.Windows.Forms.Button();
            B_Cancel = new System.Windows.Forms.Button();
            B_VirtualConsoleGSBall = new System.Windows.Forms.Button();
            // PKHaX: Lucky Number Show group
            GB_LuckyNumber = new System.Windows.Forms.GroupBox();
            L_LuckyID = new System.Windows.Forms.Label();
            NUD_LuckyID = new System.Windows.Forms.NumericUpDown();
            L_LuckyDay = new System.Windows.Forms.Label();
            NUD_LuckyDay = new System.Windows.Forms.NumericUpDown();
            L_LuckyNote = new System.Windows.Forms.Label();
            GB_LuckyNumber.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_LuckyID).BeginInit();
            ((System.ComponentModel.ISupportInitialize)NUD_LuckyDay).BeginInit();
            SuspendLayout();
            // 
            // B_Save
            // 
            B_Save.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            B_Save.Location = new System.Drawing.Point(248, 220);
            B_Save.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            B_Save.Name = "B_Save";
            B_Save.Size = new System.Drawing.Size(88, 27);
            B_Save.TabIndex = 73;
            B_Save.Text = "Save";
            B_Save.UseVisualStyleBackColor = true;
            B_Save.Click += B_Save_Click;
            // 
            // B_Cancel
            // 
            B_Cancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            B_Cancel.Location = new System.Drawing.Point(154, 220);
            B_Cancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            B_Cancel.Name = "B_Cancel";
            B_Cancel.Size = new System.Drawing.Size(88, 27);
            B_Cancel.TabIndex = 72;
            B_Cancel.Text = "Cancel";
            B_Cancel.UseVisualStyleBackColor = true;
            B_Cancel.Click += B_Cancel_Click;
            // 
            // B_VirtualConsoleGSBall
            // 
            B_VirtualConsoleGSBall.Location = new System.Drawing.Point(12, 12);
            B_VirtualConsoleGSBall.Name = "B_VirtualConsoleGSBall";
            B_VirtualConsoleGSBall.Size = new System.Drawing.Size(160, 64);
            B_VirtualConsoleGSBall.TabIndex = 74;
            B_VirtualConsoleGSBall.Text = "Enable GS Ball Event (Virtual Console)";
            B_VirtualConsoleGSBall.UseVisualStyleBackColor = true;
            B_VirtualConsoleGSBall.Click += B_VirtualConsoleGSBall_Click;
            // 
            // GB_LuckyNumber (PKHaX)
            // 
            GB_LuckyNumber.Controls.Add(L_LuckyID);
            GB_LuckyNumber.Controls.Add(NUD_LuckyID);
            GB_LuckyNumber.Controls.Add(L_LuckyDay);
            GB_LuckyNumber.Controls.Add(NUD_LuckyDay);
            GB_LuckyNumber.Controls.Add(L_LuckyNote);
            GB_LuckyNumber.Location = new System.Drawing.Point(12, 84);
            GB_LuckyNumber.Name = "GB_LuckyNumber";
            GB_LuckyNumber.Size = new System.Drawing.Size(324, 126);
            GB_LuckyNumber.TabIndex = 75;
            GB_LuckyNumber.TabStop = false;
            GB_LuckyNumber.Text = "Lucky Number Show";
            // 
            // L_LuckyID
            // 
            L_LuckyID.AutoSize = true;
            L_LuckyID.Location = new System.Drawing.Point(12, 26);
            L_LuckyID.Name = "L_LuckyID";
            L_LuckyID.Size = new System.Drawing.Size(60, 15);
            L_LuckyID.TabIndex = 0;
            L_LuckyID.Text = "Lucky ID:";
            // 
            // NUD_LuckyID
            // 
            NUD_LuckyID.Location = new System.Drawing.Point(96, 24);
            NUD_LuckyID.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            NUD_LuckyID.Name = "NUD_LuckyID";
            NUD_LuckyID.Size = new System.Drawing.Size(88, 23);
            NUD_LuckyID.TabIndex = 1;
            // 
            // L_LuckyDay
            // 
            L_LuckyDay.AutoSize = true;
            L_LuckyDay.Location = new System.Drawing.Point(12, 56);
            L_LuckyDay.Name = "L_LuckyDay";
            L_LuckyDay.Size = new System.Drawing.Size(70, 15);
            L_LuckyDay.TabIndex = 2;
            L_LuckyDay.Text = "Rolled on day:";
            // 
            // NUD_LuckyDay
            // 
            NUD_LuckyDay.Location = new System.Drawing.Point(96, 54);
            NUD_LuckyDay.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
            NUD_LuckyDay.Name = "NUD_LuckyDay";
            NUD_LuckyDay.Size = new System.Drawing.Size(88, 23);
            NUD_LuckyDay.TabIndex = 3;
            // 
            // L_LuckyNote
            // 
            L_LuckyNote.Location = new System.Drawing.Point(12, 82);
            L_LuckyNote.Name = "L_LuckyNote";
            L_LuckyNote.Size = new System.Drawing.Size(300, 38);
            L_LuckyNote.TabIndex = 4;
            L_LuckyNote.Text = "Match the last digits of a Pokemon's ID No. for a prize. The game re-rolls this number when the day counter moves past the day above.";
            // 
            // SAV_Misc2
            // 
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            ClientSize = new System.Drawing.Size(344, 261);
            Controls.Add(B_VirtualConsoleGSBall);
            Controls.Add(GB_LuckyNumber);
            Controls.Add(B_Save);
            Controls.Add(B_Cancel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = Properties.Resources.Icon;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimumSize = new System.Drawing.Size(231, 167);
            Name = "SAV_Misc2";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Misc Editor";
            GB_LuckyNumber.ResumeLayout(false);
            GB_LuckyNumber.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_LuckyID).EndInit();
            ((System.ComponentModel.ISupportInitialize)NUD_LuckyDay).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Button B_Save;
        private System.Windows.Forms.Button B_Cancel;
        private System.Windows.Forms.Button B_VirtualConsoleGSBall;
        // PKHaX: Lucky Number Show
        private System.Windows.Forms.GroupBox GB_LuckyNumber;
        private System.Windows.Forms.Label L_LuckyID;
        private System.Windows.Forms.NumericUpDown NUD_LuckyID;
        private System.Windows.Forms.Label L_LuckyDay;
        private System.Windows.Forms.NumericUpDown NUD_LuckyDay;
        private System.Windows.Forms.Label L_LuckyNote;
    }
}
