using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using gma.System.Windows;

namespace FaceMouse
{
    public partial class FirstTimeRun : Form
    {
        UserActivityHook actHook;
        int step;

        public FirstTimeRun()
        {
            InitializeComponent();
        }

        private void FirstTimeRun_Load(object sender, EventArgs e)
        {
            actHook = new UserActivityHook(true, true); //Create an instance with global hooks
            actHook.OnMouseActivity += new MouseEventHandler(MouseActivity); //Currently there is no mouse activity
            actHook.Start();

            label1.Text = "Welcome to =] FaceMouse, this is the quick-start guide to learn using this program. Click anywhere to start the tutorial.";
            step = 0;
        }

        public void MouseActivity(object sender, MouseEventArgs e)
        {
            if (e.Button.ToString().CompareTo("Left") == 0)
            {                
                if (step == 0)
                {
                    tableLayoutPanel1.SetColumnSpan(label1, 1);
                    label1.Text = "";
                    label2.Text = "Be sure to have your webcam connected, if your webcam is not connected you will se a window like this. Click anywhere to continue.";
                    pictureBox1.Image = FaceMouse.Properties.Resources.facemouse1;
                }
                else if (step == 1)
                {
                    
                    label2.Text = "You will have a better experience with =] FaceMouse if your room is well lit. Click anywhere to continue.";
                    pictureBox1.Image = FaceMouse.Properties.Resources.facemouse2;
                }
                else if (step == 2)
                {
                    label2.Text = "If your camera is correctly connected, then as soon as this tutorial ends you should see your face like in the lower-right part of the image shown. Click anywhere to continue.";
                    pictureBox1.Image = FaceMouse.Properties.Resources.facemouse3;
                }
                else if (step == 3)
                {
                    pictureBox1.Image = null;
                    tableLayoutPanel1.SetColumnSpan(label1, 3);
                    label1.Text = "To start (or stop) moving the mouse press F12. Click anywhere to finish tutorial and start using =] FaceMouse.";
                    label2.Text = "";                    
                }
                else
                {
                    this.Close();
                }
                step++;
            }
        }
    }
}
