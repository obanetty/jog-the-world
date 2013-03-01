using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.FaceTracking;

namespace WpfGoogleMapClient
{
    public class SkeletonFaceTracker : IDisposable
    {
        private FaceTracker faceTracker;

        private bool lastFaceTrackSucceeded;

        private SkeletonTrackingState skeletonTrackingState;

        public int LastTrackedFrame { get; set; }

        public void Dispose()
        {
            if (this.faceTracker != null)
            {
                this.faceTracker.Dispose();
                this.faceTracker = null;
            }
        }

        /// <summary>
        /// Updates the face tracking information for this skeleton
        /// </summary>
        internal double OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
        {
            this.skeletonTrackingState = skeletonOfInterest.TrackingState;

            if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
            {
                // nothing to do with an untracked skeleton.
                return 500;
            }

            if (this.faceTracker == null)
            {
                try
                {
                    this.faceTracker = new FaceTracker(kinectSensor);
                }
                catch (InvalidOperationException)
                {
                    // During some shutdown scenarios the FaceTracker
                    // is unable to be instantiated.  Catch that exception
                    // and don't track a face.
                    //Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                    this.faceTracker = null;
                }
            }

            if (this.faceTracker != null)
            {
                FaceTrackFrame frame = this.faceTracker.Track(
                    colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                if (this.lastFaceTrackSucceeded)
                {
                    return (double)frame.Rotation.X;
                }
            }

            return 500; //失敗時のマジックナンバー
        }
    }
}
