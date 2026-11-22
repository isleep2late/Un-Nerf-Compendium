namespace PKHeX.WinForms
{
    partial class SAV_EnigmaBerry3
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            PAN_Scroll = new System.Windows.Forms.Panel();
            TLP_Fields = new System.Windows.Forms.TableLayoutPanel();
            FLP_Buttons = new System.Windows.Forms.FlowLayoutPanel();
            B_Close = new System.Windows.Forms.Button();
            B_Revert = new System.Windows.Forms.Button();
            B_Apply = new System.Windows.Forms.Button();
            L_Heading = new System.Windows.Forms.Label();
            L_Status = new System.Windows.Forms.Label();
            L_Tag = new System.Windows.Forms.Label();
            L_PresetsHeader = new System.Windows.Forms.Label();
            FLP_Presets = new System.Windows.Forms.FlowLayoutPanel();
            L_EffectHeader = new System.Windows.Forms.Label();
            L_Effect = new System.Windows.Forms.Label();
            CB_Effect = new System.Windows.Forms.ComboBox();
            L_Parameter = new System.Windows.Forms.Label();
            NUD_Parameter = new System.Windows.Forms.NumericUpDown();
            L_Sentence = new System.Windows.Forms.Label();
            L_Help = new System.Windows.Forms.Label();
            L_Warning = new System.Windows.Forms.Label();
            L_RawEffect = new System.Windows.Forms.Label();
            NUD_RawEffect = new System.Windows.Forms.NumericUpDown();
            L_RawParameter = new System.Windows.Forms.Label();
            NUD_RawParameter = new System.Windows.Forms.NumericUpDown();
            L_NameHeader = new System.Windows.Forms.Label();
            L_Name = new System.Windows.Forms.Label();
            TB_Name = new System.Windows.Forms.TextBox();
            L_Legality = new System.Windows.Forms.Label();
            L_StatsHeader = new System.Windows.Forms.Label();
            L_Firmness = new System.Windows.Forms.Label();
            NUD_Firmness = new System.Windows.Forms.NumericUpDown();
            L_Size = new System.Windows.Forms.Label();
            NUD_Size = new System.Windows.Forms.NumericUpDown();
            L_MaxYield = new System.Windows.Forms.Label();
            NUD_MaxYield = new System.Windows.Forms.NumericUpDown();
            L_MinYield = new System.Windows.Forms.Label();
            NUD_MinYield = new System.Windows.Forms.NumericUpDown();
            L_Stage = new System.Windows.Forms.Label();
            NUD_Stage = new System.Windows.Forms.NumericUpDown();
            L_Spicy = new System.Windows.Forms.Label();
            NUD_Spicy = new System.Windows.Forms.NumericUpDown();
            L_Dry = new System.Windows.Forms.Label();
            NUD_Dry = new System.Windows.Forms.NumericUpDown();
            L_Sweet = new System.Windows.Forms.Label();
            NUD_Sweet = new System.Windows.Forms.NumericUpDown();
            L_Bitter = new System.Windows.Forms.Label();
            NUD_Bitter = new System.Windows.Forms.NumericUpDown();
            L_Sour = new System.Windows.Forms.Label();
            NUD_Sour = new System.Windows.Forms.NumericUpDown();
            L_Smoothness = new System.Windows.Forms.Label();
            NUD_Smoothness = new System.Windows.Forms.NumericUpDown();
            L_ItemEffectHeader = new System.Windows.Forms.Label();
            L_ItemEffect = new System.Windows.Forms.Label();
            TB_ItemEffect = new System.Windows.Forms.TextBox();
            FLP_ItemEffect = new System.Windows.Forms.FlowLayoutPanel();
            B_NoEffect = new System.Windows.Forms.Button();
            B_AsFound = new System.Windows.Forms.Button();
            L_TagHeader = new System.Windows.Forms.Label();
            L_Desc1 = new System.Windows.Forms.Label();
            TB_Desc1 = new System.Windows.Forms.TextBox();
            L_Desc2 = new System.Windows.Forms.Label();
            TB_Desc2 = new System.Windows.Forms.TextBox();
            L_DescNote = new System.Windows.Forms.Label();
            L_Pointers = new System.Windows.Forms.Label();
            B_Rom = new System.Windows.Forms.Button();
            L_WriteHeader = new System.Windows.Forms.Label();
            L_ItemSource = new System.Windows.Forms.Label();
            L_ActiveSlot = new System.Windows.Forms.Label();
            L_Result = new System.Windows.Forms.Label();
            PAN_Scroll.SuspendLayout();
            TLP_Fields.SuspendLayout();
            FLP_Buttons.SuspendLayout();
            FLP_ItemEffect.SuspendLayout();
            SuspendLayout();

            PAN_Scroll.AutoScroll = true;
            PAN_Scroll.Controls.Add(TLP_Fields);
            PAN_Scroll.Dock = System.Windows.Forms.DockStyle.Fill;
            PAN_Scroll.Name = "PAN_Scroll";
            PAN_Scroll.Padding = new System.Windows.Forms.Padding(8);

            TLP_Fields.AutoSize = true;
            TLP_Fields.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            TLP_Fields.ColumnCount = 2;
            TLP_Fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            TLP_Fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 380F));
            TLP_Fields.Location = new System.Drawing.Point(8, 8);
            TLP_Fields.Name = "TLP_Fields";

            B_Apply.AutoSize = true;
            B_Apply.Name = "B_Apply";
            B_Apply.Text = "Apply";
            B_Apply.UseVisualStyleBackColor = true;
            B_Revert.AutoSize = true;
            B_Revert.Name = "B_Revert";
            B_Revert.Text = "Revert";
            B_Revert.UseVisualStyleBackColor = true;
            B_Close.AutoSize = true;
            B_Close.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            B_Close.Name = "B_Close";
            B_Close.Text = "Close";
            B_Close.UseVisualStyleBackColor = true;
            FLP_Buttons.AutoSize = true;
            FLP_Buttons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            FLP_Buttons.Controls.Add(B_Close);
            FLP_Buttons.Controls.Add(B_Revert);
            FLP_Buttons.Controls.Add(B_Apply);
            FLP_Buttons.Dock = System.Windows.Forms.DockStyle.Bottom;
            FLP_Buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            FLP_Buttons.Name = "FLP_Buttons";
            FLP_Buttons.Padding = new System.Windows.Forms.Padding(4);

            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = B_Close;
            ClientSize = new System.Drawing.Size(640, 720);
            Controls.Add(PAN_Scroll);
            Controls.Add(FLP_Buttons);
            Icon = Properties.Resources.Icon;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(480, 360);
            Name = "SAV_EnigmaBerry3";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Enigma Berry (Gen 3)";
            FLP_ItemEffect.ResumeLayout(false);
            FLP_ItemEffect.PerformLayout();
            FLP_Buttons.ResumeLayout(false);
            FLP_Buttons.PerformLayout();
            TLP_Fields.ResumeLayout(false);
            TLP_Fields.PerformLayout();
            PAN_Scroll.ResumeLayout(false);
            PAN_Scroll.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.Panel PAN_Scroll;
        private System.Windows.Forms.TableLayoutPanel TLP_Fields;
        private System.Windows.Forms.FlowLayoutPanel FLP_Buttons;
        private System.Windows.Forms.Button B_Close;
        private System.Windows.Forms.Button B_Revert;
        private System.Windows.Forms.Button B_Apply;
        private System.Windows.Forms.Label L_Heading;
        private System.Windows.Forms.Label L_Status;
        private System.Windows.Forms.Label L_Tag;
        private System.Windows.Forms.Label L_PresetsHeader;
        private System.Windows.Forms.FlowLayoutPanel FLP_Presets;
        private System.Windows.Forms.Label L_EffectHeader;
        private System.Windows.Forms.Label L_Effect;
        private System.Windows.Forms.ComboBox CB_Effect;
        private System.Windows.Forms.Label L_Parameter;
        private System.Windows.Forms.NumericUpDown NUD_Parameter;
        private System.Windows.Forms.Label L_Sentence;
        private System.Windows.Forms.Label L_Help;
        private System.Windows.Forms.Label L_Warning;
        private System.Windows.Forms.Label L_RawEffect;
        private System.Windows.Forms.NumericUpDown NUD_RawEffect;
        private System.Windows.Forms.Label L_RawParameter;
        private System.Windows.Forms.NumericUpDown NUD_RawParameter;
        private System.Windows.Forms.Label L_NameHeader;
        private System.Windows.Forms.Label L_Name;
        private System.Windows.Forms.TextBox TB_Name;
        private System.Windows.Forms.Label L_Legality;
        private System.Windows.Forms.Label L_StatsHeader;
        private System.Windows.Forms.Label L_Firmness;
        private System.Windows.Forms.NumericUpDown NUD_Firmness;
        private System.Windows.Forms.Label L_Size;
        private System.Windows.Forms.NumericUpDown NUD_Size;
        private System.Windows.Forms.Label L_MaxYield;
        private System.Windows.Forms.NumericUpDown NUD_MaxYield;
        private System.Windows.Forms.Label L_MinYield;
        private System.Windows.Forms.NumericUpDown NUD_MinYield;
        private System.Windows.Forms.Label L_Stage;
        private System.Windows.Forms.NumericUpDown NUD_Stage;
        private System.Windows.Forms.Label L_Spicy;
        private System.Windows.Forms.NumericUpDown NUD_Spicy;
        private System.Windows.Forms.Label L_Dry;
        private System.Windows.Forms.NumericUpDown NUD_Dry;
        private System.Windows.Forms.Label L_Sweet;
        private System.Windows.Forms.NumericUpDown NUD_Sweet;
        private System.Windows.Forms.Label L_Bitter;
        private System.Windows.Forms.NumericUpDown NUD_Bitter;
        private System.Windows.Forms.Label L_Sour;
        private System.Windows.Forms.NumericUpDown NUD_Sour;
        private System.Windows.Forms.Label L_Smoothness;
        private System.Windows.Forms.NumericUpDown NUD_Smoothness;
        private System.Windows.Forms.Label L_ItemEffectHeader;
        private System.Windows.Forms.Label L_ItemEffect;
        private System.Windows.Forms.TextBox TB_ItemEffect;
        private System.Windows.Forms.FlowLayoutPanel FLP_ItemEffect;
        private System.Windows.Forms.Button B_NoEffect;
        private System.Windows.Forms.Button B_AsFound;
        private System.Windows.Forms.Label L_TagHeader;
        private System.Windows.Forms.Label L_Desc1;
        private System.Windows.Forms.TextBox TB_Desc1;
        private System.Windows.Forms.Label L_Desc2;
        private System.Windows.Forms.TextBox TB_Desc2;
        private System.Windows.Forms.Label L_DescNote;
        private System.Windows.Forms.Label L_Pointers;
        private System.Windows.Forms.Button B_Rom;
        private System.Windows.Forms.Label L_WriteHeader;
        private System.Windows.Forms.Label L_ItemSource;
        private System.Windows.Forms.Label L_ActiveSlot;
        private System.Windows.Forms.Label L_Result;
    }
}
