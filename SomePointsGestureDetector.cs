using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using Microsoft.Kinect;
using Kinect.Toolbox;

namespace WpfGoogleMapClient
{
    public abstract class SomePointsGestureDetector
    {
        public int MinimalPeriodBetweenGestures { get; set; }

        readonly List<Entry> entries = new List<Entry>();

        public event Action<string> OnGestureDetected;

        DateTime lastGestureDate = DateTime.Now;

        readonly int windowSize;

        Canvas displayCanvas;
        Color displayColor;

        protected SomePointsGestureDetector(int windowSize = 20)
        {
            this.windowSize = windowSize;
            MinimalPeriodBetweenGestures = 0;
        }

        protected List<Entry> Entries
        {
            get { return entries; }
        }

        public int WindowSize
        {
            get { return windowSize; }
        }

        public virtual void Add(SkeletonPoint[] positions, KinectSensor sensor)
        {
            // SkeletonPointから3次元ベクトルに変換
            Entry newEntry = new Entry {Positions = positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray<Vector3>(), Time = DateTime.Now};
            Entries.Add(newEntry);

            if (displayCanvas != null)
            {
                // Kinectが認識している2次元空間にSkeletonPointをマッピングする
                Vector2[] vector2Array = positions.Select(p => Tools.Convert(sensor, p)).ToArray<Vector2>();

                List<Ellipse> ellipseList = new List<Ellipse>();
                foreach (Vector2 vector2 in vector2Array)
                {
                    // スティックマンの頭を描画
                    Ellipse e = new Ellipse
                    {
                        Width = 4,
                        Height = 4,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        StrokeThickness = 2.0,
                        Stroke = new SolidColorBrush(displayColor),
                        StrokeLineJoin = PenLineJoin.Round
                    };

                    float x = (float)(vector2.X * displayCanvas.ActualWidth);
                    float y = (float)(vector2.Y * displayCanvas.ActualHeight);

                    Canvas.SetLeft(e, x - e.Width / 2);
                    Canvas.SetTop(e, y - e.Height / 2);
                    displayCanvas.Children.Add(e);

                    ellipseList.Add(e);
                }

                newEntry.DisplayEllipses = ellipseList.ToArray();
            }

            if (Entries.Count > WindowSize)
            {
                Entry entryToRemove = Entries[0];
                
                if (displayCanvas != null)
                {
                    foreach (Ellipse e in entryToRemove.DisplayEllipses)
                    {
                        displayCanvas.Children.Remove(e);
                    }
                }

                Entries.Remove(entryToRemove);
            }

            LookForGesture();
        }

        protected void RaiseGestureDetected(string gesture)
        {
            if (DateTime.Now.Subtract(lastGestureDate).TotalMilliseconds > MinimalPeriodBetweenGestures)
            {
                if (OnGestureDetected != null)
                    OnGestureDetected(gesture);

                lastGestureDate = DateTime.Now;
            }

            // TODO ForEach => foreach( KAKKOWARUI
            Entries.ForEach(e=>
                                {
                                    if (displayCanvas != null)
                                    {
                                        foreach (Ellipse ellipse in e.DisplayEllipses)
                                        {
                                            displayCanvas.Children.Remove(ellipse);
                                        }
                                    }
                                });
            Entries.Clear();
        }

        protected abstract void LookForGesture();

        public void TraceTo(Canvas canvas, Color color)
        {
            displayCanvas = canvas;
            displayColor = color;
        }
    }
}
