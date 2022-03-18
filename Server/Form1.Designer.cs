
namespace Server
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.sendCameraCB = new System.Windows.Forms.CheckBox();
            this.fpsLabel = new System.Windows.Forms.Label();
            this.cameraBTN = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.cameraPB = new System.Windows.Forms.PictureBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.serverBTN = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.interImageDelayNUM = new System.Windows.Forms.NumericUpDown();
            this.aspectRatioCB = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.subfolderCB = new System.Windows.Forms.CheckBox();
            this.cycleImageBTN = new System.Windows.Forms.Button();
            this.rgbTestBTN = new System.Windows.Forms.Button();
            this.sendImageBTN = new System.Windows.Forms.Button();
            this.logTextBox = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.cameraPB)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.interImageDelayNUM)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Cursor = System.Windows.Forms.Cursors.VSplit;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.groupBox3);
            this.splitContainer1.Panel1.Controls.Add(this.groupBox2);
            this.splitContainer1.Panel1.Controls.Add(this.groupBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.logTextBox);
            this.splitContainer1.Size = new System.Drawing.Size(661, 392);
            this.splitContainer1.SplitterDistance = 448;
            this.splitContainer1.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.comboBox1);
            this.groupBox3.Controls.Add(this.sendCameraCB);
            this.groupBox3.Controls.Add(this.fpsLabel);
            this.groupBox3.Controls.Add(this.cameraBTN);
            this.groupBox3.Controls.Add(this.groupBox4);
            this.groupBox3.Location = new System.Drawing.Point(188, 11);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(243, 367);
            this.groupBox3.TabIndex = 18;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Camera passthrough";
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(22, 106);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(121, 23);
            this.comboBox1.TabIndex = 4;
            // 
            // sendCameraCB
            // 
            this.sendCameraCB.AutoSize = true;
            this.sendCameraCB.Enabled = false;
            this.sendCameraCB.Location = new System.Drawing.Point(6, 51);
            this.sendCameraCB.Name = "sendCameraCB";
            this.sendCameraCB.Size = new System.Drawing.Size(133, 19);
            this.sendCameraCB.TabIndex = 3;
            this.sendCameraCB.Text = "Pass source through";
            this.sendCameraCB.UseVisualStyleBackColor = true;
            // 
            // fpsLabel
            // 
            this.fpsLabel.AutoSize = true;
            this.fpsLabel.Location = new System.Drawing.Point(162, 26);
            this.fpsLabel.Name = "fpsLabel";
            this.fpsLabel.Size = new System.Drawing.Size(46, 15);
            this.fpsLabel.TabIndex = 2;
            this.fpsLabel.Text = "FPS: XX";
            // 
            // cameraBTN
            // 
            this.cameraBTN.Location = new System.Drawing.Point(6, 22);
            this.cameraBTN.Name = "cameraBTN";
            this.cameraBTN.Size = new System.Drawing.Size(150, 23);
            this.cameraBTN.TabIndex = 1;
            this.cameraBTN.Text = "Select source";
            this.cameraBTN.UseVisualStyleBackColor = true;
            this.cameraBTN.Click += new System.EventHandler(this.cameraBTN_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.cameraPB);
            this.groupBox4.Location = new System.Drawing.Point(19, 186);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(200, 179);
            this.groupBox4.TabIndex = 0;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Camera view";
            // 
            // cameraPB
            // 
            this.cameraPB.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cameraPB.Location = new System.Drawing.Point(3, 19);
            this.cameraPB.Name = "cameraPB";
            this.cameraPB.Size = new System.Drawing.Size(194, 157);
            this.cameraPB.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.cameraPB.TabIndex = 0;
            this.cameraPB.TabStop = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.serverBTN);
            this.groupBox2.Location = new System.Drawing.Point(11, 11);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(171, 100);
            this.groupBox2.TabIndex = 17;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Connection";
            // 
            // serverBTN
            // 
            this.serverBTN.Location = new System.Drawing.Point(9, 22);
            this.serverBTN.Name = "serverBTN";
            this.serverBTN.Size = new System.Drawing.Size(147, 23);
            this.serverBTN.TabIndex = 14;
            this.serverBTN.Text = "StartServer";
            this.serverBTN.UseVisualStyleBackColor = true;
            this.serverBTN.Click += new System.EventHandler(this.connectBTN_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.interImageDelayNUM);
            this.groupBox1.Controls.Add(this.aspectRatioCB);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.subfolderCB);
            this.groupBox1.Controls.Add(this.cycleImageBTN);
            this.groupBox1.Controls.Add(this.rgbTestBTN);
            this.groupBox1.Controls.Add(this.sendImageBTN);
            this.groupBox1.Location = new System.Drawing.Point(11, 117);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(171, 261);
            this.groupBox1.TabIndex = 16;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Images";
            // 
            // interImageDelayNUM
            // 
            this.interImageDelayNUM.Location = new System.Drawing.Point(6, 209);
            this.interImageDelayNUM.Maximum = new decimal(new int[] {
            2147483647,
            0,
            0,
            0});
            this.interImageDelayNUM.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.interImageDelayNUM.Name = "interImageDelayNUM";
            this.interImageDelayNUM.Size = new System.Drawing.Size(147, 23);
            this.interImageDelayNUM.TabIndex = 19;
            this.interImageDelayNUM.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // aspectRatioCB
            // 
            this.aspectRatioCB.AutoSize = true;
            this.aspectRatioCB.Location = new System.Drawing.Point(6, 128);
            this.aspectRatioCB.Name = "aspectRatioCB";
            this.aspectRatioCB.Size = new System.Drawing.Size(116, 19);
            this.aspectRatioCB.TabIndex = 15;
            this.aspectRatioCB.Text = "Keep aspect ratio";
            this.aspectRatioCB.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 191);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 15);
            this.label2.TabIndex = 18;
            this.label2.Text = "InterImageDelay(ms)";
            // 
            // subfolderCB
            // 
            this.subfolderCB.AutoSize = true;
            this.subfolderCB.Location = new System.Drawing.Point(6, 153);
            this.subfolderCB.Name = "subfolderCB";
            this.subfolderCB.Size = new System.Drawing.Size(123, 19);
            this.subfolderCB.TabIndex = 17;
            this.subfolderCB.Text = "Include subfolders";
            this.subfolderCB.UseVisualStyleBackColor = true;
            // 
            // cycleImageBTN
            // 
            this.cycleImageBTN.Enabled = false;
            this.cycleImageBTN.Location = new System.Drawing.Point(6, 51);
            this.cycleImageBTN.Name = "cycleImageBTN";
            this.cycleImageBTN.Size = new System.Drawing.Size(149, 23);
            this.cycleImageBTN.TabIndex = 16;
            this.cycleImageBTN.Text = "Cycle images";
            this.cycleImageBTN.UseVisualStyleBackColor = true;
            this.cycleImageBTN.Click += new System.EventHandler(this.cycleImageBTN_Click);
            // 
            // rgbTestBTN
            // 
            this.rgbTestBTN.Enabled = false;
            this.rgbTestBTN.Location = new System.Drawing.Point(6, 80);
            this.rgbTestBTN.Name = "rgbTestBTN";
            this.rgbTestBTN.Size = new System.Drawing.Size(150, 24);
            this.rgbTestBTN.TabIndex = 11;
            this.rgbTestBTN.Text = "Start RGB test frames";
            this.rgbTestBTN.UseVisualStyleBackColor = true;
            this.rgbTestBTN.Click += new System.EventHandler(this.rgbTestBTN_Click);
            // 
            // sendImageBTN
            // 
            this.sendImageBTN.Enabled = false;
            this.sendImageBTN.Location = new System.Drawing.Point(6, 22);
            this.sendImageBTN.Name = "sendImageBTN";
            this.sendImageBTN.Size = new System.Drawing.Size(150, 23);
            this.sendImageBTN.TabIndex = 10;
            this.sendImageBTN.Text = "Send image";
            this.sendImageBTN.UseVisualStyleBackColor = true;
            this.sendImageBTN.Click += new System.EventHandler(this.sendImageBTN_Click);
            // 
            // logTextBox
            // 
            this.logTextBox.AcceptsTab = true;
            this.logTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logTextBox.Location = new System.Drawing.Point(0, 0);
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.Size = new System.Drawing.Size(207, 390);
            this.logTextBox.TabIndex = 0;
            this.logTextBox.Text = "";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(661, 392);
            this.Controls.Add(this.splitContainer1);
            this.Name = "Form1";
            this.Text = "TCPVirtualCam Server";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.cameraPB)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.interImageDelayNUM)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.RichTextBox logTextBox;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button serverBTN;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox aspectRatioCB;
        private System.Windows.Forms.Button sendImageBTN;
        private System.Windows.Forms.Button rgbTestBTN;
        private System.Windows.Forms.Button cycleImageBTN;
        private System.Windows.Forms.CheckBox subfolderCB;
        private System.Windows.Forms.NumericUpDown interImageDelayNUM;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.PictureBox cameraPB;
        private System.Windows.Forms.Button cameraBTN;
        private System.Windows.Forms.Label fpsLabel;
        private System.Windows.Forms.CheckBox sendCameraCB;
        private System.Windows.Forms.ComboBox comboBox1;
    }
}

