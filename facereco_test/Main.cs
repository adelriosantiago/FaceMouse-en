using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Media;
using gma.System.Windows;

namespace FaceMouse
{
    public partial class Main : Form
    {
        const double SMOOTH = 3; //Original is 3
        const int K_CONSTANT = 2; //The constant that changes mouse smoothing, original is 2

        public MyPipeline pipeline;
        public SoundPlayer player;
        public bool isRunning = false;
        //float fMouseXSmooth = 0;
        //float fMouseYSmooth = 0;
        public LinkedList<float> ollXValues, ollYValues; //Linked list to smooth the cursor position
        public UserActivityHook actHook; //The keyboard hook

        public Main()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            player = new SoundPlayer(Properties.Resources.click);
            
            int right = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size.Width;

            //Load the smooth linked list and load the first values
            ollXValues = new LinkedList<float>();
            ollYValues = new LinkedList<float>();
            for (int iA = 0; iA < SMOOTH; iA++)
            {
                ollXValues.AddFirst(0);
                ollYValues.AddFirst(0);
            }

            ////The following code causes random errors when used along with the actHook, how do we solve this!? Tried several approaches...
            ////Create overlay window
            //FirstTimeRun asdf = new FirstTimeRun();
            //asdf.ShowDialog();
            //asdf = null;

            //Create the keyboard hook
            actHook = new UserActivityHook(false, true); //Create an instance with global hooks
            actHook.KeyDown += new KeyEventHandler(MyKeyDown); //Not needed
            actHook.Start();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            this.Text = "=] FaceMouse";
            pipeline = new MyPipeline(this, pictureBox1);
            pipeline.LoopFrames();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopProcessing();
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            //
        }

        public void MyKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData.ToString().CompareTo("F12") == 0)
            {
                if (isRunning == true)
                {
                    isRunning = false;
                }
                else
                {
                    isRunning = true;
                }
            }
        }

        private void clickAllow_Tick(object sender, EventArgs e)
        {
            clickAllow.Enabled = false;
            pipeline.canClick = true;
        }

        public void StopProcessing()
        {
            pipeline.PauseFaceLandmark(true);
            pipeline.PauseFaceLocation(true);
            pipeline.Close();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            //fMouseXSmooth += (((float)ollXValues.Average()) - fMouseXSmooth) / (float)(K_CONSTANT);
            //fMouseYSmooth += (((float)ollYValues.Average()) - fMouseYSmooth) / (float)(K_CONSTANT);
            //Cursor.Position = new System.Drawing.Point((int)fMouseXSmooth, (int)fMouseYSmooth);
        }
    }

    public class MyPipeline : UtilMPipeline
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [Flags]
        public enum MouseEventFlags
        {
            LEFTDOWN = 0x00000002,
            LEFTUP = 0x00000004,
            MIDDLEDOWN = 0x00000020,
            MIDDLEUP = 0x00000040,
            MOVE = 0x00000001,
            ABSOLUTE = 0x00008000,
            RIGHTDOWN = 0x00000008,
            RIGHTUP = 0x00000010
        }

        //Core variables
        ulong timeStamp;
        int faceId;
        uint fidx = 0; //Unknown variable, what does this does?

        //Statuses
        pxcmStatus locationStatus;
        pxcmStatus landmarkStatus;
        pxcmStatus attributeStatus;
        public bool takeRecoSnapshot = false;
        public bool canClick = true;

        //Form variables
        Main parent;
        string detectionConfidence;
        Bitmap lastProcessedBitmap;

        //Attribute array
        uint[] blink = new uint[2];
        
        //PXCM variables
        PXCMFaceAnalysis faceAnalysis;
        PXCMSession session;
        PXCMFaceAnalysis.Detection faceLocation;
        PXCMFaceAnalysis.Attribute faceAttributes;
        PXCMFaceAnalysis.Landmark faceLandmark;
        PXCMFaceAnalysis.Detection.Data faceLocationData;
        PXCMFaceAnalysis.Landmark.LandmarkData[] faceLandmarkData;
        PXCMFaceAnalysis.Landmark.ProfileInfo landmarkProfile;        
        PXCMFaceAnalysis.Attribute.ProfileInfo attributeProfile;

        //Main data (eye position, distance, core values)
        Point facePosition, oldFacePosition; //The x, y center coordinates of the iris
        Point cursorPosition;

        //Data smoothers        
        int[] MOUSE_ARRAY = { 3, 3, 3, 5, 5, 8, 10, 12 };
        const int MAX_CURSOR_SMOOTH = 20;
        const int FACE_SMOOTH = 3;
        int currentCursorSmooth = MAX_CURSOR_SMOOTH;
        int cursorPositionIndex;
        
        int largestIndex = 1;
        Point[] cursorPositionArray = new Point[MAX_CURSOR_SMOOTH];
        int facePositionIndex;
        Point[] facePositionArray = new Point[FACE_SMOOTH];

        //Algorithm and internal variables
        int xDiff;
        int yDiff;

        //Face data
        PictureBox recipient; //Where the image will be drawn

        public MyPipeline(Main parent, PictureBox recipient)
        {
            faceLandmarkData = new PXCMFaceAnalysis.Landmark.LandmarkData[7];

            lastProcessedBitmap = new Bitmap(640, 480);            

            this.recipient = recipient;
            this.parent = parent;
                        
            attributeProfile = new PXCMFaceAnalysis.Attribute.ProfileInfo();

            EnableImage(PXCMImage.ColorFormat.COLOR_FORMAT_RGB24);
            EnableFaceLocation();
            EnableFaceLandmark();
        }

        public override bool OnNewFrame()
        {
            faceAnalysis = QueryFace();
            faceAnalysis.QueryFace(fidx, out faceId, out timeStamp);
            
            //Get face location
            faceLocation = (PXCMFaceAnalysis.Detection)faceAnalysis.DynamicCast(PXCMFaceAnalysis.Detection.CUID);
            locationStatus = faceLocation.QueryData(faceId, out faceLocationData);
            detectionConfidence = faceLocationData.confidence.ToString();

            //Get face attributes (smile, age group, gender, eye blink, etc)
            faceAttributes = (PXCMFaceAnalysis.Attribute)faceAnalysis.DynamicCast(PXCMFaceAnalysis.Attribute.CUID);
            faceAttributes.QueryProfile(PXCMFaceAnalysis.Attribute.Label.LABEL_EYE_CLOSED, 0, out attributeProfile);
            attributeProfile.threshold = 50; //Must be here!
            faceAttributes.SetProfile(PXCMFaceAnalysis.Attribute.Label.LABEL_EYE_CLOSED, ref attributeProfile);
            attributeStatus = faceAttributes.QueryData(PXCMFaceAnalysis.Attribute.Label.LABEL_EYE_CLOSED, faceId, out blink);

            //Get face landmarks (eye, mouth, nose position)
            faceLandmark = (PXCMFaceAnalysis.Landmark)faceAnalysis.DynamicCast(PXCMFaceAnalysis.Landmark.CUID);
            faceLandmark.QueryProfile(1, out landmarkProfile);
            faceLandmark.SetProfile(ref landmarkProfile);
            landmarkStatus = faceLandmark.QueryLandmarkData(faceId, PXCMFaceAnalysis.Landmark.Label.LABEL_7POINTS, faceLandmarkData);

            ShowAttributesOnForm();
            
            //Do the application events
            try
            {
                Application.DoEvents(); //TODO: This should be avoided using a different thread, but how?
            }
            catch (AccessViolationException e)
            {
                //TODO: Handle exception!
            }
            return true;
        }

        public override void OnImage(PXCMImage image)
        {
            session = QuerySession();
            image.QueryBitmap(session, out lastProcessedBitmap);
            using (Graphics drawer = Graphics.FromImage(lastProcessedBitmap))
            {
                if (locationStatus != pxcmStatus.PXCM_STATUS_ITEM_UNAVAILABLE)
                {
                    drawer.DrawRectangle(new Pen(new SolidBrush(Color.Red), 1), new Rectangle(new Point((int)faceLocationData.rectangle.x, (int)faceLocationData.rectangle.y), new Size((int)faceLocationData.rectangle.w, (int)faceLocationData.rectangle.h)));
                }
            }

            oldFacePosition = FacePosition();
            AddFacePositionToSmooth(new Point((int)faceLandmarkData[6].position.x, (int)faceLandmarkData[6].position.y));
            facePosition = FacePosition();
                        
            int xDiffAnt = xDiff;
            int yDiffAnt = yDiff;

            xDiff = (oldFacePosition.X - facePosition.X);
            yDiff = (oldFacePosition.Y - facePosition.Y);

            double dist = Math.Sqrt(xDiff * xDiff + yDiff * yDiff);
            if ((int)dist >= MOUSE_ARRAY.Length)
            {
                dist = MOUSE_ARRAY.Length - 1;
            }

            currentCursorSmooth = 10;

            cursorPosition.X += xDiff * (MOUSE_ARRAY[(int)dist]);
            cursorPosition.Y -= yDiff * (MOUSE_ARRAY[(int)dist]);

            //Off limit screen correction
            if (cursorPosition.X > SystemInformation.VirtualScreen.Width)
            {
                cursorPosition.X = SystemInformation.VirtualScreen.Width;
            }
            else if (cursorPosition.X < 0)
            {
                cursorPosition.X = 0;
            }
            if (cursorPosition.Y > SystemInformation.VirtualScreen.Height)
            {
                cursorPosition.Y = SystemInformation.VirtualScreen.Height;
            }
            else if (cursorPosition.Y < 0)
            {
                cursorPosition.Y = 0;
            }

            AddCursorPositionToSmooth(cursorPosition);
            
            if (parent.isRunning)
            {
                Cursor.Position = CursorPosition();
            }

            //Drawing stuff
            DrawCircle(lastProcessedBitmap, facePosition, 2, Color.Red);
            
            //Show main image
            recipient.Image = lastProcessedBitmap;
        }

        public void DrawCircle(Bitmap oPlace, System.Drawing.Point oCoords, int iRadius, Color oClr)
        {
            int iXAvg = oCoords.X;
            int iYAvg = oCoords.Y;
            int iX, iY;

            if ((iXAvg < oPlace.Width - 1) && (iXAvg > 0) && (iYAvg < oPlace.Height - 1) && (iYAvg > 0))
            {
                //Draw circle
                for (int iA = 0; iA < 628; iA++)
                {
                    iX = (int)(iXAvg + Math.Cos(iA / (double)100) * iRadius);
                    iY = (int)(iYAvg + Math.Sin(iA / (double)100) * iRadius);
                    if ((iX > 0) && (iY > 0) && (iX < oPlace.Width) && (iY < oPlace.Height))
                    {
                        oPlace.SetPixel(iX, iY, oClr);
                    }
                }
            }
        }

        void AddFacePositionToSmooth(Point pnt)
        {
            if (facePositionIndex >= FACE_SMOOTH)
            {
                facePositionIndex = 0;
            }
            facePositionArray[facePositionIndex] = pnt;
            facePositionIndex++;
        }

        Point FacePosition()
        {
            int xCenter, yCenter;

            xCenter = 0;
            yCenter = 0;

            foreach (Point item in facePositionArray)
            {
                xCenter += item.X;
                yCenter += item.Y;
            }

            xCenter = xCenter / FACE_SMOOTH;
            yCenter = yCenter / FACE_SMOOTH;

            return new Point(xCenter, yCenter);
        }

        void AddCursorPositionToSmooth(Point pnt)
        {
            if (currentCursorSmooth > MAX_CURSOR_SMOOTH)
            {
                currentCursorSmooth = MAX_CURSOR_SMOOTH;
            }
            if (cursorPositionIndex >= currentCursorSmooth)
            {
                cursorPositionIndex = 0;
            }
            cursorPositionArray[cursorPositionIndex] = pnt;
            if (cursorPositionIndex > largestIndex)
            {
                largestIndex = cursorPositionIndex;
            }
            cursorPositionIndex++;
        }

        Point CursorPosition()
        {
            int xCenter, yCenter;

            xCenter = 0;
            yCenter = 0;

            if (currentCursorSmooth > MAX_CURSOR_SMOOTH)
            {
                currentCursorSmooth = MAX_CURSOR_SMOOTH;
            }
            for (int index = 0; index < largestIndex; index++)
            {
                xCenter += cursorPositionArray[index].X;
                yCenter += cursorPositionArray[index].Y;
            }

            xCenter = xCenter / largestIndex;
            yCenter = yCenter / largestIndex;

            return new Point(xCenter, yCenter);
        }

        private void ShowAttributesOnForm()
        {
            if (blink[0] == 100)
            {
                if (canClick && parent.isRunning)
                {
                    canClick = false;
                    mouse_event((int)(MouseEventFlags.LEFTDOWN | MouseEventFlags.LEFTUP), Cursor.Position.X, Cursor.Position.Y, 0, 0);
                    parent.player.Play();
                }
            }
            else
            {
                parent.clickAllow.Enabled = true;
            }
        }
    }
}
