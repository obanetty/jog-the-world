using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kinect.Toolbox;
using Kinect.Toolbox.Record;
using System.IO;
using Microsoft.Kinect;
using Microsoft.Win32;

namespace WpfGoogleMapClient
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
    {
        public KinectImageWindow subWindow;
        KinectSensor kinectSensor;

        readonly ColorStreamManager colorManager = new ColorStreamManager();
        readonly DepthStreamManager depthManager = new DepthStreamManager();
        AudioStreamManager audioManager;
        SkeletonDisplayManager skeletonDisplayManager;
        readonly BarycenterHelper barycenterHelper = new BarycenterHelper();
        readonly AlgorithmicPostureDetector algorithmicPostureRecognizer = new AlgorithmicPostureDetector();

        SkeletonRecorder recorder;
        SkeletonReplay replay;

        BindableNUICamera nuiCamera;

        private Skeleton[] skeletons;


        public Dictionary<int, UpdownGestureDetector> skeletonGestureDetectorDic = new Dictionary<int, UpdownGestureDetector>();
        public Dictionary<int, Skeleton> skeletonDic = new Dictionary<int, Skeleton>();
        public Dictionary<int, SkeletonFaceTracker> faceTrackedSkeletonDic = new Dictionary<int, SkeletonFaceTracker>();
        public Dictionary<int, HandsGestureDetector> handsGestureDetectorDic = new Dictionary<int, HandsGestureDetector>();

        private const uint MaxMissedFrames = 100;
        private ColorImageFrame colorImageFrame;
        private ColorImageFormat colorImageFormat;
        private byte[] colorImage = null;
        private DepthImageFrame depthImageFrame;
        private DepthImageFormat depthImageFormat;
        private short[] depthImage = null;
        private SkeletonFrame skeletonFrame;

		/// <summary>
		/// インスタンスを初期化します。
		/// </summary>
		public MainWindow()
		{
			var uri = String.Format( "file://{0}index.html", AppDomain.CurrentDomain.BaseDirectory );
			this.DataContext = new MainWindowViewModel( uri );

			this.InitializeComponent();
		}

        void Kinects_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    if (kinectSensor == null)
                    {
                        kinectSensor = e.Sensor;
                        Initialize();
                    }
                    break;
                case KinectStatus.Disconnected:
                    if (kinectSensor == e.Sensor)
                    {
                        Clean();
                        MessageBox.Show("Kinect was disconnected");
                    }
                    break;
                case KinectStatus.NotReady:
                    break;
                case KinectStatus.NotPowered:
                    if (kinectSensor == e.Sensor)
                    {
                        Clean();
                        MessageBox.Show("Kinect is no more powered");
                    }
                    break;
                default:
                    MessageBox.Show("Unhandled Status: " + e.Status);
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //listen to any status change for Kinects
                KinectSensor.KinectSensors.StatusChanged += Kinects_StatusChanged;

                //loop through all the Kinects attached to this PC, and start the first that is connected without an error.
                foreach (var kinect in KinectSensor.KinectSensors)
                {
                    if (kinect.Status == KinectStatus.Connected)
                    {
                        kinectSensor = kinect;
                        break;
                    }
                }

                subWindow = new KinectImageWindow();
                subWindow.mainWindow = this;

                if (KinectSensor.KinectSensors.Count == 0)
                    MessageBox.Show("No Kinect found");
                else
                    Initialize();

                subWindow.Show();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Initialize()
        {
            if (kinectSensor == null)
                return;

            audioManager = new AudioStreamManager(kinectSensor.AudioSource);

            kinectSensor.ColorStream.Enable(ColorImageFormat.YuvResolution640x480Fps15);
            kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution80x60Fps30);

            kinectSensor.SkeletonStream.Enable(new TransformSmoothParameters
            {
                Smoothing = 0.5f,
                Correction = 0.5f,
                Prediction = 0.5f,
                JitterRadius = 0.05f,
                MaxDeviationRadius = 0.04f, 
            });

            //トラッキングするプレイヤーをアプリケーションが選択する
            kinectSensor.SkeletonStream.AppChoosesSkeletons = true;

            //上半身モード
            //kinectSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

            kinectSensor.AllFramesReady += kinectRuntime_AllFramesReady;

            skeletonDisplayManager = new SkeletonDisplayManager(kinectSensor, subWindow.kinectCanvas);

            kinectSensor.Start();

            nuiCamera = new BindableNUICamera(kinectSensor);

            subWindow.elevationSlider.DataContext = nuiCamera;

            subWindow.kinectDisplay.DataContext = colorManager;
        }

        void kinectRuntime_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (var colorFrame = e.OpenColorImageFrame())
            using (var depthFrame = e.OpenDepthImageFrame())
            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (colorFrame == null || depthFrame == null || skeletonFrame == null)
                    return;

                this.colorImageFrame = colorFrame;
                this.depthImageFrame = depthFrame;
                this.skeletonFrame = skeletonFrame;

                colorManager.Update(colorImageFrame);

                if (recorder != null)
                    recorder.Record(skeletonFrame);

                Tools.GetSkeletons(skeletonFrame, ref skeletons);

                //観測範囲から外れたSkeletonを削除
                var removeTargets = skeletonDic.Keys.Except(skeletons.Select(s => s.TrackingId));
                removeSkeletons(removeTargets);

                ProcessFrame(skeletonFrame, false);
            }
        }

        private void removeSkeletons(IEnumerable<int> removeTargets)
        {
            foreach (var id in removeTargets)
            {
                if (skeletonGestureDetectorDic.ContainsKey(id))
                {
                    skeletonGestureDetectorDic[id].OnGestureDetected -= skeletonGestureDetectorDic[id].OnGesture;
                    skeletonGestureDetectorDic.Remove(id);
                }
                if (faceTrackedSkeletonDic.ContainsKey(id))
                {
                    faceTrackedSkeletonDic.Remove(id);
                }
                if (handsGestureDetectorDic.ContainsKey(id))
                {
                    handsGestureDetectorDic.Remove(id);
                }
                if (skeletonDic.ContainsKey(id))
                {
                    skeletonDic.Remove(id);
                }
            }

            updateChoosingSkeleton();
        }

        void ProcessFrame(ReplaySkeletonFrame frame, bool replay)
        {
            double faceAngle = 0;//全員の顔の角度の合計
            double angle = 0; //全員の体の向きの合計
            int skeletonCount = 0;

            // トラッキングしているスケルトンの洗い替え
            var allActives = frame.Skeletons
                .Where(s => s.TrackingState != SkeletonTrackingState.NotTracked);
            var actives = frame.Skeletons
                .Where(s => skeletonDic.ContainsKey(s.TrackingId)
                    && s.TrackingState == SkeletonTrackingState.Tracked);
            if (actives.Count() < 1)
            {
                var s = allActives.First();
                skeletonDic[s.TrackingId] = s;
                updateChoosingSkeleton();
                actives = new[] { s };
                this.RemoveOldTrackers(skeletonFrame.FrameNumber);
            }
            
            foreach (var skeleton in actives)
            {
                //重心移動の測定処理
                var hipCenterJoint = skeleton.Joints[JointType.HipCenter];
                var shoulderLeftJoint = skeleton.Joints[JointType.ShoulderLeft];
                var shoulderRightJoint = skeleton.Joints[JointType.ShoulderRight];
                if (hipCenterJoint.TrackingState != JointTrackingState.NotTracked)
                {
                    UpdownGestureDetector det;
                    if (skeletonGestureDetectorDic.ContainsKey(skeleton.TrackingId))
                    {
                        det = skeletonGestureDetectorDic[skeleton.TrackingId];
                    }
                    else
                    {
                        det = new UpdownGestureDetector();
                        det.window = this;
                        skeletonGestureDetectorDic.Add(skeleton.TrackingId, det);
                        det.OnGestureDetected += det.OnGesture;
                    }

                    det.Add(new[] { hipCenterJoint.Position }, kinectSensor);


                    //両肩から体のY軸に対する角度を取得
                    var aAngle = bodyAngle(skeleton);
                    if (aAngle != Double.NaN)
                    {
                        angle += aAngle;
                    }

                    skeletonCount++;
                }


                //両手による地図のドラッグ操作検出部分
                var handRightJoint = skeleton.Joints[JointType.HandRight];
                var handLeftJoint = skeleton.Joints[JointType.HandLeft];
                var shoulderCenterJoint = skeleton.Joints[JointType.ShoulderCenter];
                if (new[] { handLeftJoint, handRightJoint, shoulderCenterJoint, shoulderLeftJoint, shoulderRightJoint }
                    .All(j => j.TrackingState != JointTrackingState.NotTracked))
                {
                    HandsGestureDetector handsDet;
                    if (handsGestureDetectorDic.ContainsKey(skeleton.TrackingId))
                    {
                        handsDet = handsGestureDetectorDic[skeleton.TrackingId];
                    }
                    else
                    {
                        handsDet = new HandsGestureDetector();
                        handsDet.window = this;
                        handsGestureDetectorDic.Add(skeleton.TrackingId, handsDet);
                    }

                    handsDet.Add(new[] {handRightJoint.Position, 
                        handLeftJoint.Position, 
                        shoulderRightJoint.Position, 
                        shoulderLeftJoint.Position, 
                        shoulderCenterJoint.Position}, 
                        kinectSensor);
                }


                //以下、FaceTrackingによる顔の角度取得部分
                if (!replay)
                {
                    try
                    {
                        if (new Object[] { colorImageFrame, depthImageFrame, skeletonFrame }
                            .Any(f => f == null))
                        {
                            return;
                        }

                        // Check for image format changes.  The FaceTracker doesn't
                        // deal with that so we need to reset.
                        if (this.depthImageFormat != depthImageFrame.Format)
                        {
                            this.depthImage = null;
                            this.depthImageFormat = depthImageFrame.Format;
                        }

                        if (this.colorImageFormat != colorImageFrame.Format)
                        {
                            this.colorImage = null;
                            this.colorImageFormat = colorImageFrame.Format;
                        }

                        // Create any buffers to store copies of the data we work with
                        if (this.depthImage == null)
                        {
                            this.depthImage = new short[depthImageFrame.PixelDataLength];
                        }

                        if (this.colorImage == null)
                        {
                            this.colorImage = new byte[colorImageFrame.PixelDataLength];
                        }

                        colorImageFrame.CopyPixelDataTo(this.colorImage);
                        depthImageFrame.CopyPixelDataTo(this.depthImage);

                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                            || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            // We want keep a record of any skeleton, tracked or untracked.
                            if (!this.faceTrackedSkeletonDic.ContainsKey(skeleton.TrackingId))
                            {
                                this.faceTrackedSkeletonDic.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                            }

                            // Give each tracker the upated frame.
                            SkeletonFaceTracker skeletonFaceTracker;
                            if (this.faceTrackedSkeletonDic.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                            {
                                faceAngle += skeletonFaceTracker.OnFrameReady(this.kinectSensor, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                                skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;
                            }
                        }

                        this.RemoveOldTrackers(skeletonFrame.FrameNumber);

                        this.InvalidateVisual();
                    }
                    finally
                    {
                    }
                }
                //-------------------------------------------
            }

            //全スケルトンの平均を算出
            angle /= skeletonCount;
            faceAngle /= skeletonCount;

            if (Math.Abs(angle) > 15)
            {
                _webBrowser.InvokeScript("setAngleSpeed", angle);
            }
            else
            {
                _webBrowser.InvokeScript("setAngleSpeed", 0);
            }

            if (-90 <= faceAngle && faceAngle <= 90)
            {
                _webBrowser.InvokeScript("setFaceAngle", faceAngle);
            }

            if (subWindow.viewSkeleton.IsChecked == true)
            {
                skeletonDisplayManager.Draw(frame);
            }
            else
            {
                subWindow.kinectCanvas.Children.Clear();
            }
        }

        private void updateChoosingSkeleton()
        {
            var skeletonIds = skeletonDic.Keys;

            switch (skeletonDic.Count){
                case 0:
                    kinectSensor.SkeletonStream.ChooseSkeletons();
                    break;
                case 1:
                    kinectSensor.SkeletonStream.ChooseSkeletons(skeletonIds.First());
                    break;
                case 2:
                    kinectSensor.SkeletonStream.ChooseSkeletons(skeletonIds.First(), skeletonIds.Last());
                    break;
                default:
                    throw new Exception("skeletonDicの登録数異常");
            }
        }

        private Double bodyAngle(Skeleton skeleton)
        {
            if (skeleton.TrackingState != SkeletonTrackingState.Tracked)
            {
                return Double.NaN;
            }

            var shoulderLeftJoint = skeleton.Joints[JointType.ShoulderLeft];
            var shoulderRightJoint = skeleton.Joints[JointType.ShoulderRight];

            if (shoulderLeftJoint.TrackingState != JointTrackingState.NotTracked &&
                    shoulderRightJoint.TrackingState != JointTrackingState.NotTracked)
            {

                //両肩から体のY軸に対する角度を取得
                var v1 = new Vector2(shoulderLeftJoint.Position.X, shoulderLeftJoint.Position.Z);
                var v2 = new Vector2(shoulderRightJoint.Position.X, shoulderRightJoint.Position.Z);
                var v = new Vector2(v2.X - v1.X, v2.Y - v1.Y);
                return Math.Atan2(v.Y, v.X) * 180 / Math.PI;
            }
            else
            {
                return Double.NaN;
            }
        }

        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = this.faceTrackedSkeletonDic
                .Where(e => ((uint)currentFrameNumber - (uint)e.Value.LastTrackedFrame > MaxMissedFrames))
                .Select(e => e.Key);

            this.RemoveTracker(trackersToRemove);
        }

        private void RemoveTracker(IEnumerable<int> trackingIds)
        {
            foreach (var id in trackingIds)
            {
                this.faceTrackedSkeletonDic[id].Dispose();
                this.faceTrackedSkeletonDic.Remove(id);
            }
        }

        private void ResetFaceTracking()
        {
            this.RemoveTracker(this.faceTrackedSkeletonDic.Keys.ToArray());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (subWindow != null)
            {
                subWindow.Close();
            }
            Clean();
        }

        private void Clean()
        {
            if (audioManager != null)
            {
                audioManager.Dispose();
                audioManager = null;
            }

            if (recorder != null)
            {
                recorder.Stop();
                recorder = null;
            }

            if (kinectSensor != null)
            {
                kinectSensor.AllFramesReady -= kinectRuntime_AllFramesReady;
                kinectSensor.Stop();
                kinectSensor = null;
            }
        }

        public void replayButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Title = "Select filename", Filter = "Replay files|*.replay" };

            if (openFileDialog.ShowDialog() == true)
            {
                if (replay != null)
                {
                    replay.SkeletonFrameReady -= replay_SkeletonFrameReady;
                    replay.Stop();
                }
                var recordStream = File.OpenRead(openFileDialog.FileName);

                replay = new SkeletonReplay(recordStream);

                replay.SkeletonFrameReady += replay_SkeletonFrameReady;

                replay.Start();
            }
        }

        void replay_SkeletonFrameReady(object sender, ReplaySkeletonFrameReadyEventArgs e)
        {
            ProcessFrame(e.SkeletonFrame, true);
        }

		/// <summary>
		/// 移動ボタンが押された時に発生するイベントです。
		/// </summary>
		/// <param name="sender">イベント発生元。</param>
		/// <param name="e">イベント データ。</param>
		private void OnClickTurnButton( object sender, RoutedEventArgs e )
		{
			var context = this.DataContext as MainWindowViewModel;
			if( context == null ) { return; }

			// JavaScript の関数を呼び出す。
            this._webBrowser.InvokeScript("setFaceAngle", context.Angle);
		}

        /// <summary>
        /// 進行ボタンが押された時に発生するイベントです。
        /// </summary>
        /// <param name="sender">イベント発生元。</param>
        /// <param name="e">イベント データ。</param>
        private void OnClickWalkButton(object sender, RoutedEventArgs e)
        {

            if (WindowState == WindowState.Maximized)
            {
                WindowStyle = WindowStyle.ThreeDBorderWindow;
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
        }
	}

	/// <summary>
	/// MainWindow の Model と View を仲介するクラスです。
	/// </summary>
	class MainWindowViewModel
	{
		/// <summary>
		/// インスタンスを初期化します。
		/// </summary>
		/// <param name="uri">ブラウザに表示するページの URI。</param>
		public MainWindowViewModel( string uri )
		{
			this.Uri = uri;
		}

		/// <summary>
		/// Google Map に指定する住所を取得または設定します。
		/// </summary>
		public int Angle { get; set; }

        /// <summary>
        /// Google Map に指定する方角を取得または設定します。
        /// </summary>
        public int Heading { get; set; }

		/// <summary>
		/// ブラウザに表示するページの URI を取得します。
		/// </summary>
		public string Uri { get; private set; }
	}
}
