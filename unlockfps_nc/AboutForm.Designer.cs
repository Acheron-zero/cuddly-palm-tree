namespace unlockfps_nc
{
    partial class AboutForm
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
            LabelTitle = new Label();
            LabelDescription = new Label();
            LinkLabelSource = new LinkLabel();
            LinkLabelIssues = new LinkLabel();
            SuspendLayout();
            // 
            // LabelTitle
            // 
            LabelTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            LabelTitle.Location = new Point(12, 9);
            LabelTitle.Name = "LabelTitle";
            LabelTitle.Size = new Size(320, 36);
            LabelTitle.TabIndex = 0;
            LabelTitle.Text = "原神FPS解锁器\r\nv3.3.0";
            LabelTitle.TextAlign = ContentAlignment.TopCenter;
            // 
            // LabelDescription
            // 
            LabelDescription.Location = new Point(12, 45);
            LabelDescription.Name = "LabelDescription";
            LabelDescription.Size = new Size(320, 20);
            LabelDescription.TabIndex = 1;
            LabelDescription.Text = "此程序免费开源";
            LabelDescription.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // LinkLabelSource
            // 
            LinkLabelSource.LinkArea = new LinkArea(3, 5);
            LinkLabelSource.Location = new Point(12, 65);
            LinkLabelSource.Name = "LinkLabelSource";
            LinkLabelSource.Size = new Size(320, 20);
            LinkLabelSource.TabIndex = 2;
            LinkLabelSource.TabStop = true;
            LinkLabelSource.Text = "地址: 花火吃了";
            LinkLabelSource.TextAlign = ContentAlignment.MiddleCenter;
            LinkLabelSource.UseCompatibleTextRendering = true;
            LinkLabelSource.LinkClicked += LinkLabelSource_LinkClicked;
            // 
            // LinkLabelIssues
            // 
            LinkLabelIssues.LinkArea = new LinkArea(0, 19);
            LinkLabelIssues.Location = new Point(12, 85);
            LinkLabelIssues.Name = "LinkLabelIssues";
            LinkLabelIssues.Size = new Size(320, 67);
            LinkLabelIssues.TabIndex = 3;
            LinkLabelIssues.TabStop = true;
            LinkLabelIssues.Text = "小灰毛遇到任何问题或有建议都与花火无关";
            LinkLabelIssues.TextAlign = ContentAlignment.MiddleCenter;
            LinkLabelIssues.UseCompatibleTextRendering = true;
            LinkLabelIssues.LinkClicked += LinkLabelIssues_LinkClicked;
            // 
            // AboutForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(344, 171);
            Controls.Add(LinkLabelIssues);
            Controls.Add(LinkLabelSource);
            Controls.Add(LabelDescription);
            Controls.Add(LabelTitle);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "About";
            ResumeLayout(false);
        }

        #endregion

        private Label LabelTitle;
        private Label LabelDescription;
        private LinkLabel LinkLabelSource;
        private LinkLabel LinkLabelIssues;
    }
}