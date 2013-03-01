using System;
using System.Windows.Shapes;
using Kinect.Toolbox;

namespace WpfGoogleMapClient
{
    public class Entry
    {
        public DateTime Time { get; set; }
        public Vector3[] Positions { get; set; }
        public Ellipse[] DisplayEllipses { get; set; }
    }
}
