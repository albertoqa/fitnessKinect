//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Diagnostics;
    using System.Threading;


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);


        private int nmov; // actual movement

        // sc - actual score
        // repes - actual number of reps
        // ex - info of each exercise
        // stopwatch - chronometer for the exercise
        // playing - only track when user press start and until it has finished the complete exercise
        // arriba - movement 2 and 3 aux variable
        private int ntr = 0, sc = 0, repes = 0;
        private string[] ex;
        private bool arriba = false, playing = false;
        Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // extra information for each exercise
            this.ex = new string[5];
            this.ex[0] = "Posición inicial - brazos relajados y piernas juntas";
            this.ex[1] = "Levanta los brazos por encima de la cabeza";
            this.ex[2] = "Manten los brazos, levanta la pierna izquierda x grados y vuelve a bajarla";
            this.ex[3] = "Manten los brazos, levanta la pierna derecha x grados y vuelve a bajarla";
            this.ex[4] = "Vuelve a la posición inicial - brazos relajados y piernas juntas";
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // when loading the program set the variables
            nmov = 0;

            this.exer.Content = ex[nmov];
            this.ntrys.Content = ntr;
            this.scor.Content = sc;
            this.rep.Content = repes;
            this.inf.Content = "Empezamos el juego!";

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            SkeletalImage.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug,
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {

                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ColorImage.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }
        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {

            startGameWithMovement(skeleton);
            // Function that control the complete game
            gameControl(skeleton, drawingContext);
            
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
        
        
        private bool posIni(Skeleton skeleton) {
            
            
            
            
            
        }
        
        private void startGameWithMovement(Skeleton skeleton) {
            
            if(playing == false) {
                if(posIni(skeleton)) {
                    
                    startButton.Visibility = Visibility.Hidden;
                    score.Visibility = Visibility.Visible;
                    ntry.Visibility = Visibility.Visible;
                    exercise.Visibility = Visibility.Visible;
                    exer.Visibility = Visibility.Visible;
                    ntrys.Visibility = Visibility.Visible;
                    scor.Visibility = Visibility.Visible;
                    reps.Visibility = Visibility.Visible;
                    rep.Visibility = Visibility.Visible;
                    of5.Visibility = Visibility.Visible;
                    
                    // start and restart the chronometer
                    stopwatch.Reset();
                    stopwatch.Start();
                    this.ntrys.Content = stopwatch.Elapsed;
                    
                    // start to track the game
                    playing = true;
                    
                    nmov = ntr = repes = 0;
                    arriba = false;
                    // maximum score is 10
                    sc = 10;
                    this.scor.Content = sc;

                }
                
                
            }
            
        }
        

        // action of the button startGame
        private void startGame(object sender, RoutedEventArgs e)
        {
            startButton.Visibility = Visibility.Hidden;
            score.Visibility = Visibility.Visible;
            ntry.Visibility = Visibility.Visible;
            exercise.Visibility = Visibility.Visible;
            exer.Visibility = Visibility.Visible;
            ntrys.Visibility = Visibility.Visible;
            scor.Visibility = Visibility.Visible;
            reps.Visibility = Visibility.Visible;
            rep.Visibility = Visibility.Visible;
            of5.Visibility = Visibility.Visible;

            // start and restart the chronometer
            stopwatch.Reset();
            stopwatch.Start();
            this.ntrys.Content = stopwatch.Elapsed;

            // start to track the game
            playing = true;

            nmov = ntr = repes = 0;
            arriba = false;
            // maximum score is 10
            sc = 10;
            this.scor.Content = sc;

        }

        private void endGame()
        {

            this.inf.Content = "Felicidades!!! Lo has conseguido ;)";
            // stop chronometer
            stopwatch.Stop();
            this.ntrys.Content = stopwatch.Elapsed;

            // stop tracking
            playing = false;

            startButton.Visibility = Visibility.Visible;
            //score.Visibility = Visibility.Hidden;
            //ntry.Visibility = Visibility.Hidden;
            exercise.Visibility = Visibility.Hidden;
            exer.Visibility = Visibility.Hidden;
            //ntrys.Visibility = Visibility.Hidden;
            //scor.Visibility = Visibility.Hidden;
            reps.Visibility = Visibility.Hidden;
            rep.Visibility = Visibility.Hidden;
            of5.Visibility = Visibility.Hidden;

            // restart counters
            nmov = ntr = sc = repes = 0;
            arriba = false;
        }


        private void gameControl(Skeleton skeleton, DrawingContext drawingContext)
        {
            // when user press start playing = true, when game finish playing = false
            if (playing)
            {
                // movimiento en reposo
                if (nmov == 0)
                {
                    this.exer.Content = ex[0];
                    if (pos0(skeleton))
                    {
                        this.inf.Content = "Muy bien, has superado el primer movimiento";
                        nmov++;
                    }

                }
                // brazos en cruz
                else if (nmov == 1)
                {
                    this.exer.Content = ex[1];
                    if (pos1(skeleton))
                    {
                        this.inf.Content = "Perfecto! Pasamos al siguiente nivel";
                        nmov++;
                    }

                }
                // ya hemos puesto los brazos en cruz
                else if (nmov == 2)
                {
                    this.exer.Content = ex[2];

                    // si en algun momento bajamos los brazos, empezamos de cero el ejercicio
                    if (!pos1(skeleton))
                    {
                        this.inf.Content = "Has bajado los brazos, tenemos que empezar de nuevo.";
                        nmov = 0;
                        repes = 0;
                        arriba = false;
                        sc--;
                        this.scor.Content = sc;
                    }

                    // si llegamos arriba
                    if (!arriba && pos2(skeleton))
                    {
                        // nmov++;
                        // repes++;
                        arriba = true;
                    }

                    // si ya hemos estado arriba, tenemos que ponernos rectos y con los brazos en cruz
                    if (arriba && posx(skeleton) && repes <= 4)
                    {
                        arriba = false;
                        repes++;
                        this.rep.Content = repes;
                    }

                    // si ya hemos subido y bajado 5 veces
                    if (repes == 4)
                    {
                        this.inf.Content = "Eres todo un atleta! Intenta repetirlo con la otra pierna!";
                        nmov++;
                        repes = 0;
                        this.rep.Content = repes;
                    }
                }
                else if (nmov == 3)
                {
                    this.exer.Content = ex[3];

                    // si en algun momento bajamos los brazos, empezamos de cero el ejercicio
                    if (!pos1(skeleton))
                    {
                        this.inf.Content = "Has bajado los brazos, tenemos que empezar de nuevo.";
                        nmov = 0;
                        repes = 0;
                        arriba = false;
                        sc--;
                        this.scor.Content = sc;
                    }

                    // si llegamos arriba
                    if (!arriba && pos3(skeleton))
                    {
                        // nmov++;
                        // repes++;
                        arriba = true;
                    }

                    // si ya hemos estado arriba, tenemos que ponernos rectos y con los brazos en cruz
                    if (arriba && posx(skeleton) && repes <= 4)
                    {
                        arriba = false;
                        repes++;
                        this.rep.Content = repes;
                    }

                    // si ya hemos subido y bajado 5 veces
                    if (repes == 4)
                    {
                        this.inf.Content = "Ya casi hemos terminado, solo falta que te relajes...";
                        nmov++;
                        repes = 0;
                        this.rep.Content = repes;
                    }
                }
                // volver a la posicion inicial
                else if (nmov == 4)
                {
                    this.exer.Content = ex[4];
                    if (pos0(skeleton))
                    {
                        nmov++;
                    }
                }
                // finish the game
                else
                {
                    endGame();
                }

                this.ntrys.Content = stopwatch.Elapsed;
            }

        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {

                this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }
        }

        //brazos levantados, piernas juntas
        private bool posx(Skeleton skeleton)
        {
            bool arms = pos1(skeleton);
            bool legs = AreFeetTogether(skeleton);

            return (arms && legs);
        }

        // Obtenido de Carla Simoes
        private bool pos0(Skeleton skeleton)
        {
            bool body = IsAlignedBodyAndArms(skeleton);
            bool legs = AreFeetTogether(skeleton);
            legs = true;
            if (body && legs)
                return true;
            else
                return false;
        }

        // boolean method that return true if body is completely aligned and arms are in a relaxed position
        private bool IsAlignedBodyAndArms(Skeleton received)
        {
            double HipCenterPosX = received.Joints[JointType.HipCenter].Position.X;
            double HipCenterPosY = received.Joints[JointType.HipCenter].Position.Y;
            double HipCenterPosZ = received.Joints[JointType.HipCenter].Position.Z;

            double ShoulCenterPosX = received.Joints[JointType.ShoulderCenter].Position.X;
            double ShoulCenterPosY = received.Joints[JointType.ShoulderCenter].Position.Y;
            double ShoulCenterPosZ = received.Joints[JointType.ShoulderCenter].Position.Z;

            double HeadCenterPosX = received.Joints[JointType.Head].Position.X;
            double HeadCenterPosY = received.Joints[JointType.Head].Position.Y;
            double HeadCenterPosZ = received.Joints[JointType.Head].Position.Z;

            double ElbLPosX = received.Joints[JointType.ElbowLeft].Position.X;
            double ElbLPosY = received.Joints[JointType.ElbowLeft].Position.Y;

            double ElbRPosX = received.Joints[JointType.ElbowRight].Position.X;
            double ElbRPosY = received.Joints[JointType.ElbowRight].Position.Y;

            double WriLPosX = received.Joints[JointType.WristLeft].Position.X;
            double WriLPosY = received.Joints[JointType.WristLeft].Position.Y;
            double WriLPosZ = received.Joints[JointType.WristLeft].Position.Z;

            double WriRPosX = received.Joints[JointType.WristRight].Position.X;
            double WriRPosY = received.Joints[JointType.WristRight].Position.Y;
            double WriRPosZ = received.Joints[JointType.WristRight].Position.Z;

            double ShouLPosX = received.Joints[JointType.ShoulderLeft].Position.X;
            double ShouLPosY = received.Joints[JointType.ShoulderLeft].Position.Y;
            double ShouLPosZ = received.Joints[JointType.ShoulderLeft].Position.Z;

            double ShouRPosX = received.Joints[JointType.ShoulderRight].Position.X;
            double ShouRPosY = received.Joints[JointType.ShoulderRight].Position.Y;
            double ShouRPosZ = received.Joints[JointType.ShoulderRight].Position.Z;

            //have to change to correspond to the 5% error
            //distance from Shoulder to Wrist for the projection in line with shoulder
            double distShouLtoWristL = ShouLPosY - WriLPosY;
            //caldulate admited error 5% that correspond to 9 degrees for each side
            double radian = (9 * Math.PI) / 180;
            double DistErrorL = distShouLtoWristL * Math.Tan(radian);

            double distShouLtoWristR = ShouRPosY - WriRPosY;
            //caldulate admited error 5% that correspond to 9 degrees for each side

            double DistErrorR = distShouLtoWristR * Math.Tan(radian);
            //double ProjectionWristX = ShouLPosX;
            //double ProjectionWristZ = WriLPosZ;

            //determine of projected point from shoulder to wrist LEFT and RIGHT and then assume error
            double ProjectedPointWristLX = ShouLPosX;
            double ProjectedPointWristLY = WriLPosY;
            double ProjectedPointWristLZ = ShouLPosZ;

            double ProjectedPointWristRX = ShouRPosX;
            double ProjectedPointWristRY = WriRPosY;
            double ProjectedPointWristRZ = ShouRPosZ;


            //Create method to verify if the center of the body is completely aligned
            //head with shoulder center and with hip center
            if (Math.Abs(HeadCenterPosX - ShoulCenterPosX) <= 0.05 && Math.Abs(ShoulCenterPosX - HipCenterPosX) <= 0.05)
            {
                //if position of left wrist is between [ProjectedPointWrist-DistError,ProjectedPointWrist+DistError]
                if (Math.Abs(WriLPosX - ProjectedPointWristLX) <= DistErrorL && Math.Abs(WriRPosX - ProjectedPointWristRX) <= DistErrorR)
                {
                    return true;
                }
                else return false;
            }
            else return false;

        }
        //first position to be Tracked and Accepted
        private bool AreFeetTogether(Skeleton received)
        {
            if (null != this.sensor)
            {
                foreach (Joint joint in received.Joints)
                {
                    if (joint.TrackingState == JointTrackingState.Tracked)
                    {//first verify if the body is alignet and arms are in a relaxed position

                        //{here verify if the feet are together
                        //use the same strategy that was used in the previous case of the arms in a  relaxed position
                        double HipCenterPosX = received.Joints[JointType.HipCenter].Position.X;
                        double HipCenterPosY = received.Joints[JointType.HipCenter].Position.Y;
                        double HipCenterPosZ = received.Joints[JointType.HipCenter].Position.Z;

                        //if left ankle is very close to right ankle then verify the rest of the skeleton points
                        //if (received.Joints[JointType.AnkleLeft].Equals(received.Joints[JointType.AnkleRight]))
                        double AnkLPosX = received.Joints[JointType.AnkleLeft].Position.X;
                        double AnkLPosY = received.Joints[JointType.AnkleLeft].Position.Y;
                        double AnkLPosZ = received.Joints[JointType.AnkleLeft].Position.Z;

                        double AnkRPosX = received.Joints[JointType.AnkleRight].Position.X;
                        double AnkRPosY = received.Joints[JointType.AnkleRight].Position.Y;
                        double AnkRPosZ = received.Joints[JointType.AnkleRight].Position.Z;
                        //assume that the distance Y between HipCenter to each foot is the same
                        double distHiptoAnkleL = HipCenterPosY - AnkLPosY;
                        //caldulate admited error 5% that correspond to 9 degrees for each side
                        double radian1 = (4.5 * Math.PI) / 180;
                        double DistErrorL = distHiptoAnkleL * Math.Tan(radian1);
                        //double DistErrorL = 10;
                        //determine of projected point from HIP CENTER to LEFT ANKLE and RIGHT and then assume error
                        double ProjectedPointFootLX = HipCenterPosX;
                        double ProjectedPointFootLY = AnkLPosY;
                        double ProjectedPointFootLZ = HipCenterPosZ;

                        /*if (Math.Abs(AnkRPosX - AnkLPosX) < 1)
                            return true;
                        else
                            return false;*/

                        // could variate AnkLposX and AnkLPosY
                        if (Math.Abs(AnkLPosX - ProjectedPointFootLX) <= DistErrorL+0.05 && Math.Abs(AnkRPosX - ProjectedPointFootLX) <= DistErrorL+0.05)
                            return true;
                        else
                            return false;

                    }//CLOSE if (joint.TrackingState == JointTrackingState.Tracked)
                    else return false;
                }//close foreach

            }//close if !null
            return false;
        }//close method AreFeetTogether


        // code from Pedrojp
        private bool pos1(Skeleton skeleton)
        {
            bool a = BrazoDerechoLevantado(skeleton);
            bool b = BrazoIzquierdoLevantado(skeleton);

            if (a && b)
                return true;
            else
                return false;
        }

        private bool BrazoIzquierdoLevantado(Skeleton skeleton)
        {
            // Si el brazo derecho se encuentra por encima de la cabeza
            return (skeleton.Joints[JointType.ElbowLeft].Position.Y >= skeleton.Joints[JointType.ShoulderLeft].Position.Y) &&
                                    (skeleton.Joints[JointType.WristLeft].Position.Y >= skeleton.Joints[JointType.ShoulderLeft].Position.Y) &&
                                    (skeleton.Joints[JointType.WristLeft].Position.Y < skeleton.Joints[JointType.Head].Position.Y);
        }

        private bool BrazoDerechoLevantado(Skeleton skeleton)
        {
            // Si el brazo izquierdo se encuentra por encima de la cabeza
            return (skeleton.Joints[JointType.ElbowRight].Position.Y >= skeleton.Joints[JointType.ShoulderRight].Position.Y) &&
                                  (skeleton.Joints[JointType.WristRight].Position.Y >= skeleton.Joints[JointType.ShoulderRight].Position.Y) &&
                                  (skeleton.Joints[JointType.WristRight].Position.Y < skeleton.Joints[JointType.Head].Position.Y);
        }

        private bool pos2(Skeleton skeleton)
        {
            double angle = 40;
            double allowed_error = 5;

            return angleLegL(skeleton, angle, allowed_error);
        }

        /// <summary>
        /// Check position of the left leg
        /// </summary>
        /// <param name="skeleton">skeleton to check</param>
        /// <param name="angle">angle searched</param>
        /// <param name="allowed_error">error allowed in the comprobation</param>
        private bool angleLegL(Skeleton skeleton, double angle, double allowed_error)
        {

            //return variable - true if position is valid, false if invalid
            bool check = true;

            float distA, distB;
            distA = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.Y - skeleton.Joints[JointType.AnkleLeft].Position.Y);
            distB = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.AnkleLeft].Position.X);

            // the angle I'm looking for is the formed between the HipCenter, the floor and the AnkleLeft
            // I know the position of the HipCenter and the AnkleLeft so it can be calculed by
            // arctang[(Hip.X-Ank.X) / (Hip.Y-Ank.Y)]
            double segmentAngle = Math.Atan(distB / distA);

            // With this operation I transform the angle to degrees
            double degrees = segmentAngle * (180 / Math.PI);
            degrees = degrees % 360;

            // Check if the actual angle is equal to the angle specified
            if (Math.Abs(angle - degrees) < allowed_error)
                check = true;
            else
                check = false;

            return check;

        }

        private bool pos3(Skeleton skeleton)
        {
            double angle = 40;
            double allowed_error = 5;

            return angleLegR(skeleton, angle, allowed_error);
        }

        private bool angleLegR(Skeleton skeleton, double angle, double allowed_error)
        {

            //return variable - true if position is valid, false if invalid
            bool check = true;

            float distA, distB;
            distA = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.Y - skeleton.Joints[JointType.AnkleRight].Position.Y);
            distB = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.AnkleRight].Position.X);

            // the angle I'm looking for is the formed between the HipCenter, the floor and the AnkleLeft
            // I know the position of the HipCenter and the AnkleLeft so it can be calculed by
            // arctang[(Hip.X-Ank.X) / (Hip.Y-Ank.Y)]
            double segmentAngle = Math.Atan(distB / distA);

            // With this operation I transform the angle to degrees
            double degrees = segmentAngle * (180 / Math.PI);
            degrees = degrees % 360;

            // Check if the actual angle is equal to the angle specified
            if (Math.Abs(angle - degrees) < allowed_error)
                check = true;
            else
                check = false;

            return check;

        }


    }
}
