﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace unlockfps_nc
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
        }

        private void LinkLabelSource_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLink("https://ys.mihoyo.com/");
        }

        private void LinkLabelIssues_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenLink("https://ys.mihoyo.com/");
        }

        private void OpenLink(string url)
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(psi);
        }

    }
}
