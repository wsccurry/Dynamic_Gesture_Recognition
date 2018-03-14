//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Documents;
    using System.Collections.Generic;
    using System;

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
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        /// 


        private List<SkeletonPoint> points = new List<SkeletonPoint>();      //存储要记录的points

        private List<SkeletonPoint> allPoints = new List<SkeletonPoint>();    //所有的points

        private bool start = false;    //手势开始

        private bool move = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)           //渲染边界的，就是红色的边界,当超过kinet摄像头检测范围，边界变红
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

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

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

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)           //这里是事件处理，不停的绘制骨骼和骨骼节点图像
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
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);              //画骨骼和骨骼节点


                            SkeletonPoint hrPos = skel.Joints[JointType.HandRight].Position;                //这是自己加的两句，打印左手X,Y坐标，范围(-1,1)， 注意这不是图像中的坐标，而是SkeletonPoint的坐标
                            //System.Console.WriteLine("X坐标:{0},Y坐标{1}",hrPos.X,hrPos.Y);
                            hrPos.X = hrPos.X * 100;                    //放大100倍,好计算误差
                            hrPos.Y = hrPos.Y * 100;
                            allPoints.Add(hrPos);
                            bool ismove = isMove(allPoints);
                            if (!ismove && start == false && allPoints.Count > 15)
                                start = true;
                            else if (ismove && start == true)
                            {
                                move = true;
                                points.Add(hrPos);
                               // Console.WriteLine("方差:{0}",error(allPoints));
                            }
                            else if (!ismove && start == true&&move==true)
                            {
                                start = false;
                                this.sensor.Stop();
                               // for (int i = 0; i < points.Count; i++)
                                //{
                                    //Console.WriteLine("X左标{0},Y坐标{1}", points[i].X/100, points[i].Y/100);
                               // }
                               // Console.WriteLine();
                                List<int> values = EigenvalueProcessing(points);
                                Console.WriteLine(values.Count);
                                saveOriginData(values);                             //存储原始数据
                                saveInterceptData(values, 140);                       //存储截取数据
                               // foreach (int i in values)
                               // {
                                //    Console.Write(i + " ");
                               // }
                               // Console.WriteLine();
                            }
                            //if(allPoints.Count==90)
                            // this.isStart(allPoints);
                            // Console.WriteLine("方差:{0}", isMove(allPoints));
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
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)    //这里的skeleton.Joints 就可以得到存储各个骨骼节点的数组
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);          //JointType是个枚举类型,定义了骨骼节点的枚举值
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
                //               JointType.HandRight           右
                if (drawBrush != null)                         //skeleton.Joints[JointType.HandLeft].Position 为左手掌心的坐标(SkeletonPoint) ,注意这不是我们所看图像中的点，要转换需要SkeletonPointToScreen()函数
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);    //SkeletonPointToScreen将SkeletonPoint转化为深度Point输出
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
            if (joint0.TrackingState == JointTrackingState.Inferred &&         //infer这是推断的骨骼节点，这是有干扰的情况下，推断下绘制的骨骼节点和骨骼颜色为黄色
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
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        private bool isMove(List<SkeletonPoint> points)            //判断手是否开始运动,用样本方差来计算
        {
            int frameLength = 15;                                  //初始化样本个数为15个
            int actualLength = points.Count;                       //实际样本个数
            float error = 0;
            if (actualLength < frameLength)
                return false;
            // return 0;
            else
            {
                int nowPos = actualLength - 1;
                int prePos = nowPos - frameLength + 1;
                float sumX = 0, sumY = 0, aveX = 0, aveY = 0;
                for (int i = prePos; i <= nowPos; i++)
                {
                    sumX += points[i].X;
                    sumY += points[i].Y;
                }
                aveX = sumX / frameLength;
                aveY = sumY / frameLength;
                for (int i = prePos; i <= nowPos; i++)
                {
                    error += (points[i].X - aveX) * (points[i].X - aveX) + (points[i].Y - aveY) * (points[i].Y - aveY);
                }
                error = error / frameLength;
                //return error;
                if (error > 0.1)
                    return true;
                else
                    return false;
            }

        }


        private float error(List<SkeletonPoint> points)
        {
            int frameLength = 15;                                  //初始化样本个数为15个
            int actualLength = points.Count;                       //实际样本个数
            float error = 0;
            if (actualLength < frameLength)
                return 0;
            else
            {
                int nowPos = actualLength - 1;
                int prePos = nowPos - frameLength + 1;
                float sumX = 0, sumY = 0, aveX = 0, aveY = 0;
                for (int i = prePos; i <= nowPos; i++)
                {
                    sumX += points[i].X;
                    sumY += points[i].Y;
                }
                aveX = sumX / frameLength;
                aveY = sumY / frameLength;
                for (int i = prePos; i <= nowPos; i++)
                {
                    error += (points[i].X - aveX) * (points[i].X - aveX) + (points[i].Y - aveY) * (points[i].Y - aveY);
                }
                error = error / frameLength;
                return error;
            }
        }


        private List<int> EigenvalueProcessing(List<SkeletonPoint> list)
        {
            double PI = Math.PI;
            List<int> directions = new List<int>();
            double x = 0, y = 0;//向量的x,y坐标
            double cos = 0;
            for (int i = 0; i < list.Count - 1; i++)
            {
                x = list[i + 1].X - list[i].X;
                y = list[i + 1].Y - list[i].Y;
                cos = y / (Math.Sqrt(x * x + y * y));
                if ((y > 0 && cos > Math.Cos(PI / 8)) || (x < 0 && cos == Math.Cos(PI / 8)))
                {
                    directions.Add(0);
                }
                else if (x > 0 && cos > Math.Cos(PI * 3 / 8) && cos <= Math.Cos(PI / 8))
                {
                    directions.Add(1);
                }
                else if (x > 0 && cos > Math.Cos(PI * 5 / 8) && cos <= Math.Cos(PI * 3 / 8))
                {
                    directions.Add(2);
                }
                else if (x > 0 && cos > Math.Cos(PI * 7 / 8) && cos <= Math.Cos(PI * 5 / 8))
                {
                    directions.Add(3);
                }
                else if ((y < 0 && cos < Math.Cos(PI * 7 / 8)) || (x > 0 && cos == Math.Cos(PI * 7 / 8)))
                {
                    directions.Add(4);
                }
                else if (x < 0 && cos >= Math.Cos(PI * 9 / 8) && cos < Math.Cos(PI * 11 / 8))
                {
                    directions.Add(5);
                }
                else if (x < 0 && cos >= Math.Cos(PI * 11 / 8) && cos < Math.Cos(PI * 13 / 8))
                {
                    directions.Add(6);
                }
                else
                {
                    directions.Add(7);
                }
            }
            return directions;
        }

        private void saveOriginData(List<int> values)
        {
            try
            {
                StreamWriter writer = new StreamWriter("C:\\Users\\lenovo\\Desktop\\OriginData.txt", true);
                foreach (int value in values)
                    writer.Write(value + " ");
                writer.WriteLine();
                writer.Flush();
                writer.Close();
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
        }

        private void saveInterceptData(List<int> values,int length)
        {
            try
            {
                StreamWriter writer = new StreamWriter("C:\\Users\\lenovo\\Desktop\\InterceptDate.txt", true);
                if (values.Count >= length)
                {
                    for (int i = 0; i < length; i++)
                        writer.Write(values[i] + " ");
                }
                else
                {
                    for (int i = 0; i < values.Count; i++)
                        writer.Write(values[i] + " ");
                    for (int i = values.Count; i < length; i++)
                        writer.Write(-1 + " ");
                }
                writer.WriteLine();
                writer.Flush();
                writer.Close();
            }
            catch (IOException e)
            { 
                Console.WriteLine(e);
            }
        }
    }
}