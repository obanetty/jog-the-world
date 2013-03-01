using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Kinect.Toolbox;

namespace WpfGoogleMapClient
{
    public class UpdownGestureDetector : SomePointsGestureDetector
    {
        const double MinimalLength = 0.015;
        const double MinimalDuration = 300;


        public MainWindow window;

        public double count = 0;

        public UpdownGestureDetector(int windowSize = 20)
            : base(windowSize)
        {
            
        }

        protected override void LookForGesture()
        {
            if (Entries.Count < 2)
                return;

            Entry e1 = Entries[Entries.Count - 1];
            Entry e2 = Entries[Entries.Count - 2];

            double value = e1.Positions[0].Y - e2.Positions[0].Y;
            double duration = e1.Time.Subtract(e2.Time).TotalMilliseconds;
            value = Math.Round(value, 3);

            if (Math.Abs(value) > MinimalLength && duration < MinimalDuration)
            {
                RaiseGestureDetected(value.ToString());
            }

        }

        public void OnGesture(string value)
        {
            double val = double.Parse(value);
            if (val > 0)
            {
                count += val;

                if (count > 0.15)
                {
                    count = 0;
                    window._webBrowser.InvokeScript("walkMap");
                }
            }
        }
    }
}
