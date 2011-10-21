using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DimScreen
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            _glass.Bind(this);
            _glass.ShowGlass();

            this.trackBar1.ValueChanged += (o, e) =>
            {
                _glass.Opacity = this.trackBar1.Value / 10f;
            };
        }

        private GlassPanelForm _glass = new GlassPanelForm();
    }
}
