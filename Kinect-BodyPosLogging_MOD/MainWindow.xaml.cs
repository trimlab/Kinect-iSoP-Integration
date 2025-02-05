﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    //by KO
    //using Ventuz.OSC;
    using SharpOSC;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Logger
        /// </summary>
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //by KO //Init UDP socket
        //UdpWriter udpSender = new UdpWriter("192.168.56.1", 9875);//1024 used for PureData
        //UDPSender udpSender = new UDPSender("192.168.56.1", 9875);
        UDPSender udpSender = new UDPSender("172.29.121.229", 9875);//VT IP Address (My Laptop) 172.29.94.172


        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            log.Info("Begun program.");

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            //by KO
            /*
            int nLoop = 1;
            while (nLoop > 0)
            {
                nLoop -= 1;
                //by KO 

                //variables
                float areaSummation = 0.0f;
                float x = 1.2f,
                    y = 1.3f,
                    z = 1.4f,
                    xVel = 2.5f,
                    yVel = 2.6f,
                    zVel = 2.7f,
                    xAccel = 3.8f,
                    yAccel = 3.9f,
                    zAccel = 3.10f,
                    testV = 4.5f;

                //TEST TYPE3 : SharpOSC

                // setting data set
                // 1. position
                var strMsg1 = new SharpOSC.OscMessage(" /objA", x.ToString(), y.ToString(), z.ToString());
                var strMsg2 = new SharpOSC.OscMessage(" /objB", x.ToString(), z.ToString(), z.ToString());
                //var strMsg1 = new SharpOSC.OscMessage(" /obj1/position", x, y, z);

                // 2. velocity
                //var strMsg2 = new SharpOSC.OscMessage(("/" + "obj1" + "/velocity").ToString(), xVel, yVel, zVel, testV);

                // 3. acceleration
                var strMsg3 = new SharpOSC.OscMessage(("/" + "objC" + "/acceleration").ToString(), xAccel.ToString(), yAccel.ToString(), zAccel.ToString());

                // 4. OSC message for area
                var strMsg4 = new SharpOSC.OscMessage("/area", testV.ToString());

                // Sending UDP
                OscBundle oscBundle1 = new OscBundle(0, strMsg1, strMsg2, strMsg3, strMsg2);
                //OscBundle oscBundle2 = new OscBundle(0, );

                Console.WriteLine("SENDINGSENDINGSENDINGSENDING");

                //Send Method (message)
                udpSender2.Send(oscBundle1);

            }
            Environment.Exit(0);
            */
            ///////////


            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {

            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    int penIndex = 0;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // log joints
                            StringBuilder builder = new StringBuilder();

                            // it's ugly and verbose, but it's clear and in guaranteed order
                            builder.Append(joints[JointType.AnkleLeft].Position.X + " " + joints[JointType.AnkleLeft].Position.Y + " " + joints[JointType.AnkleLeft].Position.Z + " ");
                            builder.Append(joints[JointType.AnkleRight].Position.X + " " + joints[JointType.AnkleRight].Position.Y + " " + joints[JointType.AnkleRight].Position.Z + " ");
                            builder.Append(joints[JointType.ElbowLeft].Position.X + " " + joints[JointType.ElbowLeft].Position.Y + " " + joints[JointType.ElbowLeft].Position.Z + " ");
                            builder.Append(joints[JointType.ElbowRight].Position.X + " " + joints[JointType.ElbowRight].Position.Y + " " + joints[JointType.ElbowRight].Position.Z + " ");
                            builder.Append(joints[JointType.FootLeft].Position.X + " " + joints[JointType.FootLeft].Position.Y + " " + joints[JointType.FootLeft].Position.Z + " ");
                            builder.Append(joints[JointType.FootRight].Position.X + " " + joints[JointType.FootRight].Position.Y + " " + joints[JointType.FootRight].Position.Z + " ");
                            builder.Append(joints[JointType.HandLeft].Position.X + " " + joints[JointType.HandLeft].Position.Y + " " + joints[JointType.HandLeft].Position.Z + " ");
                            builder.Append(joints[JointType.HandRight].Position.X + " " + joints[JointType.HandRight].Position.Y + " " + joints[JointType.HandRight].Position.Z + " ");
                            builder.Append(joints[JointType.HandTipLeft].Position.X + " " + joints[JointType.HandTipLeft].Position.Y + " " + joints[JointType.HandTipLeft].Position.Z + " ");
                            builder.Append(joints[JointType.HandTipRight].Position.X + " " + joints[JointType.HandTipRight].Position.Y + " " + joints[JointType.HandTipRight].Position.Z + " ");
                            builder.Append(joints[JointType.Head].Position.X + " " + joints[JointType.Head].Position.Y + " " + joints[JointType.Head].Position.Z + " ");
                            builder.Append(joints[JointType.HipLeft].Position.X + " " + joints[JointType.HipLeft].Position.Y + " " + joints[JointType.HipLeft].Position.Z + " ");
                            builder.Append(joints[JointType.HipRight].Position.X + " " + joints[JointType.HipRight].Position.Y + " " + joints[JointType.HipRight].Position.Z + " ");
                            builder.Append(joints[JointType.KneeLeft].Position.X + " " + joints[JointType.KneeLeft].Position.Y + " " + joints[JointType.KneeLeft].Position.Z + " ");
                            builder.Append(joints[JointType.KneeRight].Position.X + " " + joints[JointType.KneeRight].Position.Y + " " + joints[JointType.KneeRight].Position.Z + " ");
                            builder.Append(joints[JointType.Neck].Position.X + " " + joints[JointType.Neck].Position.Y + " " + joints[JointType.Neck].Position.Z + " ");
                            builder.Append(joints[JointType.ShoulderLeft].Position.X + " " + joints[JointType.ShoulderLeft].Position.Y + " " + joints[JointType.ShoulderLeft].Position.Z + " ");
                            builder.Append(joints[JointType.ShoulderRight].Position.X + " " + joints[JointType.ShoulderRight].Position.Y + " " + joints[JointType.ShoulderRight].Position.Z + " ");
                            builder.Append(joints[JointType.SpineBase].Position.X + " " + joints[JointType.SpineBase].Position.Y + " " + joints[JointType.SpineBase].Position.Z + " ");
                            builder.Append(joints[JointType.SpineMid].Position.X + " " + joints[JointType.SpineMid].Position.Y + " " + joints[JointType.SpineMid].Position.Z + " ");
                            builder.Append(joints[JointType.SpineShoulder].Position.X + " " + joints[JointType.SpineShoulder].Position.Y + " " + joints[JointType.SpineShoulder].Position.Z + " ");
                            builder.Append(joints[JointType.ThumbLeft].Position.X + " " + joints[JointType.ThumbLeft].Position.Y + " " + joints[JointType.ThumbLeft].Position.Z + " ");
                            builder.Append(joints[JointType.ThumbRight].Position.X + " " + joints[JointType.ThumbRight].Position.Y + " " + joints[JointType.ThumbRight].Position.Z + " ");
                            builder.Append(joints[JointType.WristLeft].Position.X + " " + joints[JointType.WristLeft].Position.Y + " " + joints[JointType.WristLeft].Position.Z + " ");
                            builder.Append(joints[JointType.WristRight].Position.X + " " + joints[JointType.WristRight].Position.Y + " " + joints[JointType.WristRight].Position.Z + " ");

                            string jointStr = builder.ToString();
                            log.Info(jointStr);
                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawBody(joints, jointPoints, dc, drawPen);

                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

                            //by KO 

                            // setting data set : the name of the element + x + y + z

                            //FOR MUSIC
                            
                            var HandRight = new SharpOSC.OscMessage(" /HandRight", " ", joints[JointType.HandRight].Position.X.ToString(), " ", joints[JointType.HandRight].Position.Y.ToString(), " ", joints[JointType.HandRight].Position.Z.ToString());
                            var HandTipLeft = new SharpOSC.OscMessage(" /HandTipLeft", " ", joints[JointType.HandTipLeft].Position.X.ToString(), " ", joints[JointType.HandTipLeft].Position.Y.ToString(), " ", joints[JointType.HandTipLeft].Position.Z.ToString());
                            var AnkleLeft = new SharpOSC.OscMessage(" /AnkleLeft", " ", joints[JointType.AnkleLeft].Position.X.ToString(), " ", joints[JointType.AnkleLeft].Position.Y.ToString(), " ", joints[JointType.AnkleLeft].Position.Z.ToString());
                            var AnkleRight = new SharpOSC.OscMessage(" /AnkleRight", " ", joints[JointType.AnkleRight].Position.X.ToString(), " ", joints[JointType.AnkleRight].Position.Y.ToString(), " ", joints[JointType.AnkleRight].Position.Z.ToString());
                            

                            //PureData
                            //var HandRight = new SharpOSC.OscMessage("/HandRight/position", joints[JointType.HandRight].Position.X, joints[JointType.HandRight].Position.Y,  joints[JointType.HandRight].Position.Z);
                            //var HandTipLeft = new SharpOSC.OscMessage("/HandTipLeft/position", joints[JointType.HandTipLeft].Position.X, joints[JointType.HandTipLeft].Position.Y, joints[JointType.HandTipLeft].Position.Z);
                            //var AnkleLeft = new SharpOSC.OscMessage("/AnkleLeft", " ", joints[JointType.AnkleLeft].Position.X.ToString(), " ", joints[JointType.AnkleLeft].Position.Y.ToString(), " ", joints[JointType.AnkleLeft].Position.Z.ToString());
                            //var AnkleRight = new SharpOSC.OscMessage("/AnkleRight", " ", joints[JointType.AnkleRight].Position.X.ToString(), " ", joints[JointType.AnkleRight].Position.Y.ToString(), " ", joints[JointType.AnkleRight].Position.Z.ToString());



                            var ElbowLeft = new SharpOSC.OscMessage(" /ElbowLeft", " ", joints[JointType.ElbowLeft].Position.X.ToString(), " ", joints[JointType.ElbowLeft].Position.Y.ToString(), " ", joints[JointType.ElbowLeft].Position.Z.ToString());
                            var ElbowRight = new SharpOSC.OscMessage(" /ElbowRight", " ", joints[JointType.ElbowRight].Position.X.ToString(), " ", joints[JointType.ElbowRight].Position.Y.ToString(), " ", joints[JointType.ElbowRight].Position.Z.ToString());
                            var FootLeft = new SharpOSC.OscMessage(" /FootLeft", joints[JointType.FootLeft].Position.X.ToString(), joints[JointType.FootLeft].Position.Y.ToString(), joints[JointType.FootLeft].Position.Z.ToString());
                            var FootRight = new SharpOSC.OscMessage(" /FootRight", joints[JointType.FootRight].Position.X.ToString(), joints[JointType.FootRight].Position.Y.ToString(), joints[JointType.FootRight].Position.Z.ToString());
                            var HandLeft = new SharpOSC.OscMessage(" /HandLeft", " ", joints[JointType.HandLeft].Position.X.ToString(), " ", joints[JointType.HandLeft].Position.Y.ToString(), " ", joints[JointType.HandLeft].Position.Z.ToString());
                            var HandTipRight = new SharpOSC.OscMessage(" /HandTipRight", joints[JointType.HandTipRight].Position.X.ToString(), joints[JointType.HandTipRight].Position.Y.ToString(), joints[JointType.AnkleLeft].Position.Z.ToString());
                            var Head = new SharpOSC.OscMessage(" /Head", joints[JointType.Head].Position.X.ToString(), joints[JointType.Head].Position.Y.ToString(), joints[JointType.Head].Position.Z.ToString());
                            var HipLeft = new SharpOSC.OscMessage(" /HipLeft", joints[JointType.HipLeft].Position.X.ToString(), joints[JointType.HipLeft].Position.Y.ToString(), joints[JointType.HipLeft].Position.Z.ToString());
                            var HipRight = new SharpOSC.OscMessage(" /HipRight", joints[JointType.HipRight].Position.X.ToString(), joints[JointType.HipRight].Position.Y.ToString(), joints[JointType.HipRight].Position.Z.ToString());
                            var KneeLeft = new SharpOSC.OscMessage(" /KneeLeft", joints[JointType.KneeLeft].Position.X.ToString(), joints[JointType.KneeLeft].Position.Y.ToString(), joints[JointType.KneeLeft].Position.Z.ToString());
                            var KneeRight = new SharpOSC.OscMessage(" /KneeRight", joints[JointType.KneeRight].Position.X.ToString(), joints[JointType.KneeRight].Position.Y.ToString(), joints[JointType.KneeRight].Position.Z.ToString());
                            var Neck = new SharpOSC.OscMessage(" /Neck", joints[JointType.Neck].Position.X.ToString(), joints[JointType.Neck].Position.Y.ToString(), joints[JointType.Neck].Position.Z.ToString());
                            var ShoulderLeft = new SharpOSC.OscMessage(" /ShoulderLeft", joints[JointType.ShoulderLeft].Position.X.ToString(), joints[JointType.ShoulderLeft].Position.Y.ToString(), joints[JointType.ShoulderLeft].Position.Z.ToString());
                            var ShoulderRight = new SharpOSC.OscMessage(" /ShoulderRight", joints[JointType.ShoulderRight].Position.X.ToString(), joints[JointType.ShoulderRight].Position.Y.ToString(), joints[JointType.ShoulderRight].Position.Z.ToString());
                            var SpineBase = new SharpOSC.OscMessage(" /SpineBase", joints[JointType.SpineBase].Position.X.ToString(), joints[JointType.SpineBase].Position.Y.ToString(), joints[JointType.SpineBase].Position.Z.ToString());
                            var SpineMid = new SharpOSC.OscMessage(" /SpineMid", joints[JointType.SpineMid].Position.X.ToString(), joints[JointType.SpineMid].Position.Y.ToString(), joints[JointType.SpineMid].Position.Z.ToString());
                            var SpineShoulder = new SharpOSC.OscMessage(" /SpineShoulder", joints[JointType.SpineShoulder].Position.X.ToString(), joints[JointType.SpineShoulder].Position.Y.ToString(), joints[JointType.SpineShoulder].Position.Z.ToString());
                            var ThumbLeft = new SharpOSC.OscMessage(" /ThumbLeft", joints[JointType.ThumbLeft].Position.X.ToString(), joints[JointType.ThumbLeft].Position.Y.ToString(), joints[JointType.ThumbLeft].Position.Z.ToString());
                            var ThumbRight = new SharpOSC.OscMessage(" /ThumbRight", joints[JointType.ThumbRight].Position.X.ToString(), joints[JointType.ThumbRight].Position.Y.ToString(), joints[JointType.ThumbRight].Position.Z.ToString());
                            var WristLeft = new SharpOSC.OscMessage(" /WristLeft", joints[JointType.WristLeft].Position.X.ToString(), joints[JointType.WristLeft].Position.Y.ToString(), joints[JointType.WristLeft].Position.Z.ToString());
                            var WristRight = new SharpOSC.OscMessage(" /WristRight", joints[JointType.WristRight].Position.X.ToString(), joints[JointType.WristRight].Position.Y.ToString(), joints[JointType.WristRight].Position.Z.ToString());

                            // OSC message for area
                            // var Area = new SharpOSC.OscMessage("/area", testV.ToString());

                            // Sending UDP
                            OscBundle oscBundle = new OscBundle(0, HandRight, HandLeft, AnkleRight, AnkleLeft);//FOR MUSIC
                            //OscBundle oscBundle = new OscBundle(0, HandRight);//FOR PureData
                            //OscBundle oscBundle = new OscBundle(0, HandLeft, HandRight, HandTipLeft, HandTipRight);

                            /*FootLeft, FootRight, HandLeft, HandRight, HandTipLeft,
                            HandTipRight, Head, HipLeft, HipRight, KneeLeft, KneeRight, Neck, ShoulderLeft, ShoulderRight, SpineBase, 
                            SpineMid, SpineShoulder, ThumbLeft, ThumbRight, WristLeft, WristRight);
                            */

                            Console.WriteLine("TEST SENDING:");
                            Console.WriteLine(oscBundle.ToString());
                            //Send Method (message)
                            udpSender.Send(oscBundle);

                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
    }
}
