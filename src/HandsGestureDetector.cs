using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Kinect.Toolbox;

namespace WpfGoogleMapClient
{
    public class HandsGestureDetector : SomePointsGestureDetector
    {
        private enum HandsState {Double, SingleRight, SingleLeft, None, NoState}
        private enum Mode {MoveByRightHand, MoveByLeftHand, Zoom, ReadyForNone, None }
        class HandsStateResult
        {
            public HandsState state { get; set; }
            public Entry entry{get; set;}
            public HandsState preState { get; set; }
            public double period { get; set; }

            public HandsStateResult()
            {
                this.state = HandsState.NoState;
                this.entry = null;
                this.preState = HandsState.NoState;
                this.period = 0;
            }
        }

        const double MinimalDuration = 300;
        const double MinimalShortDuration = 100;
        const double MinimalReadyForNoneDuration = 1000;
        const double ZoomUnit = 0.06;
        const double MinimalZoomLength = 0.03;
        const double MinimalMoveLength = 0.07;
        const double MinimalZSubtract = 0.45;
        const double MinimalHandsDistance = 0.6;
        const double MaximumZSubtractBetweenHands = 0.1;
        const double ratio = 500;

        public MainWindow window;

        private Mode mode = Mode.None;
        private double zoomCounter = 0;
        private double readyTime = 0;

        public HandsGestureDetector(int windowSize = 20)
            : base(windowSize)
        {
            
        }

        protected override void LookForGesture()
        {
            if (mode == Mode.None)
            {
                changeModeWithCheck(mode);
                return;
            }
            else if (mode == Mode.ReadyForNone)
            {
                changeModeWithCheck(mode);
                return;
            }
            else if (mode == Mode.Zoom)
            {
                Entry e1 = Entries[Entries.Count - 1];
                Entry e2 = Entries[Entries.Count - 2];

                if (changeModeWithCheck(mode))
                {
                    return;
                }

                float newLength = (makeVector2FromVector3(e1.Positions[0]) - makeVector2FromVector3(e1.Positions[1])).Length;
                float oldLength = (makeVector2FromVector3(e2.Positions[0]) - makeVector2FromVector3(e2.Positions[1])).Length;
                zoomCounter += (newLength - oldLength);
                int zoom = calculateZoom();
                if (zoom != 0)
                {
                    window._webBrowser.InvokeScript("mapDragZoom", zoom);
                }
            }
            else if (mode == Mode.MoveByRightHand || mode == Mode.MoveByLeftHand)
            {
                Entry e1 = Entries[Entries.Count - 1];
                Entry e2 = Entries[Entries.Count - 2];

                if (changeModeWithCheck(mode))
                {
                    return;
                }

                Vector3 handPoint1;
                Vector3 handPoint2;
                if (mode == Mode.MoveByRightHand)
                {
                    handPoint1 = e1.Positions[0];
                    handPoint2 = e2.Positions[0];
                }
                else if (mode == Mode.MoveByLeftHand)
                {
                    handPoint1 = e1.Positions[1];
                    handPoint2 = e2.Positions[1];
                }
                else
                {
                    throw new Exception("モード異常");
                }

                Vector2 v = new Vector2(handPoint1.X - handPoint2.X, handPoint1.Y - handPoint2.Y);
                float vLeng = v.Length;
                if (vLeng > 0)
                {
                    window._webBrowser.InvokeScript("mapDragMove", -v.X * ratio, v.Y * ratio);
                }
            }
        }

        private Vector2 makeVector2FromVector3(Vector3 v)
        {
            return new Vector2(v.X, v.Y);
        }

        private bool changeModeWithCheck(Mode m)
        {
            if (m == Mode.Zoom)
            {
                HandsStateResult result = getHandsStateWithPeriod(MinimalDuration);
                if (result.preState == HandsState.SingleRight && result.period > MinimalShortDuration)
                {
                    changeMode(Mode.MoveByRightHand);
                    return true;
                }
                else if (result.preState == HandsState.SingleLeft && result.period > MinimalShortDuration)
                {
                    changeMode(Mode.MoveByLeftHand);
                    return true;
                }
                else if (result.state == HandsState.None)
                {
                    changeMode(mode = Mode.ReadyForNone);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (m == Mode.MoveByRightHand)
            {
                HandsStateResult result = getHandsStateWithPeriod(MinimalDuration);
                if (result.preState == HandsState.SingleLeft && result.period > MinimalShortDuration)
                {
                    changeMode(Mode.MoveByLeftHand);
                    return true;
                }
                else if (result.preState == HandsState.Double && result.period > MinimalShortDuration)
                {
                    changeMode(Mode.Zoom);
                    return true;
                }
                else if (result.state == HandsState.None)
                {
                    changeMode(Mode.ReadyForNone);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (m == Mode.MoveByLeftHand)
            {
                HandsStateResult result = getHandsStateWithPeriod(MinimalDuration);
                if (result.preState == HandsState.SingleRight && result.period > MinimalShortDuration)
                {
                    changeMode(Mode.MoveByRightHand);
                    return true;
                }
                else if (result.preState == HandsState.Double && result.period > MinimalShortDuration)
                {
                    changeMode(Mode.Zoom);
                    return true;
                }
                else if (result.state == HandsState.None)
                {
                    changeMode(Mode.ReadyForNone);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (m == Mode.ReadyForNone)
            {
                HandsStateResult result = getHandsStateWithPeriod(MinimalShortDuration);
                if (result.state == HandsState.SingleRight)
                {
                    changeMode(Mode.MoveByRightHand);
                    return true;
                }
                else if (result.state == HandsState.SingleLeft)
                {
                    changeMode(Mode.MoveByLeftHand);
                    return true;
                }
                else if (result.state == HandsState.Double)
                {
                    changeMode(Mode.Zoom);
                    return true;
                }
                else
                {
                    if (TimeSpan.FromTicks(System.DateTime.Now.Ticks).TotalMilliseconds - readyTime > MinimalReadyForNoneDuration)
                    {
                        changeMode(Mode.None);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else if (m == Mode.None)
            {
                HandsStateResult result = getHandsStateWithPeriod(MinimalDuration);

                if (result.state == HandsState.Double)
                {
                    changeMode(Mode.Zoom);
                    return true;
                }
                else if (result.state == HandsState.SingleRight)
                {
                    changeMode(Mode.MoveByRightHand);
                    return true;
                }
                else if (result.state == HandsState.SingleLeft)
                {
                    changeMode(Mode.MoveByLeftHand);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void changeMode(Mode m)
        {
            mode = m;

            if (m == Mode.Zoom || m == Mode.MoveByRightHand || m == Mode.MoveByLeftHand)
            {
                window._webBrowser.InvokeScript("switchDragOrZoom", true);
            }
            else if (m == Mode.ReadyForNone)
            {
                readyTime = TimeSpan.FromTicks(System.DateTime.Now.Ticks).TotalMilliseconds;
            }
            else if (m == Mode.None)
            {
                readyTime = 0;
                window._webBrowser.InvokeScript("switchDragOrZoom", false);
            }
        }

        /**
         * 両手、もしくは片手を前方に伸ばした状態で一定時間経過したかどうかを調べる。
         * 条件を満たした場合は一定時間経過した直近のEntryを返す。
         * 条件を満たさなければnullを返す。
         */
        private HandsStateResult getHandsStateWithPeriod(double period)
        {
            if (Entries.Count < 2)
            {
                return new HandsStateResult();
            }

            Entry lastEntry = Entries[Entries.Count - 1];
            HandsState tempHandsState = checkHandsState(lastEntry);
            long endTime = lastEntry.Time.Ticks;

            HandsStateResult result = new HandsStateResult();
            result.preState = tempHandsState;
            for (int i = Entries.Count - 2; 0 <= i; i--)
            {
                Entry e = Entries[i];
                HandsState state = checkHandsState(e);
                if (tempHandsState == state)
                {
                    double tempPeriod = TimeSpan.FromTicks(endTime - e.Time.Ticks).TotalMilliseconds;
                    if (tempPeriod > period)
                    {
                        result.state = tempHandsState;
                        result.entry = e;
                        result.period = tempPeriod;
                        return result;
                    }
                    else
                    {
                        result.period = tempPeriod;
                    }
                }
                else
                {
                    return result;
                }
            }

            return result;
        }

        private int calculateZoom()
        {
            int zoom = (int)(Math.Abs(zoomCounter) / ZoomUnit);
            if (zoomCounter >= 0)
            {
                zoomCounter -= ZoomUnit * zoom;
            }
            else
            {
                zoomCounter += ZoomUnit * zoom;
                zoom = -zoom;
            }

            return zoom;
        }

        private HandsState checkHandsState(Entry e)
        {
            Vector3 rightHand = e.Positions[0];
            Vector3 leftHand = e.Positions[1];
            Vector3 rightShoulder = e.Positions[2];
            Vector3 leftShoulder = e.Positions[3];
            Vector3 handsMidpoint = (e.Positions[0] + e.Positions[1]) / 2;
            Vector3 shoulderCenter = e.Positions[4];

            //右手チェック
            double rightHandDistance = (rightHand - shoulderCenter).Length;
            double rightZSubtract = Math.Abs(rightHand.Z - shoulderCenter.Z);
            //左手チェク
            double leftHandDistance = (leftHand - shoulderCenter).Length;
            double leftZSubtract = Math.Abs(leftHand.Z - shoulderCenter.Z);
            //両手チェック
            double handsDistance = (handsMidpoint - shoulderCenter).Length;
            double zSubtract = Math.Abs(handsMidpoint.Z - shoulderCenter.Z);
            double zSubtractBetweenHands = Math.Abs(rightHand.Z - leftHand.Z);

            bool rightHandUpped = (rightHandDistance > MinimalHandsDistance) && (rightZSubtract > MinimalZSubtract);
            bool leftHandUpped = (leftHandDistance > MinimalHandsDistance) && (leftZSubtract > MinimalZSubtract);

            if(rightHandUpped && !leftHandUpped)
            {
                return HandsState.SingleRight;
            }
            else if(leftHandUpped && !rightHandUpped)
            {
                return HandsState.SingleLeft;
            }
            else if (handsDistance > MinimalHandsDistance && zSubtract > MinimalZSubtract && MaximumZSubtractBetweenHands > zSubtractBetweenHands)
            {
                return HandsState.Double;
            }
            else
            {
                return HandsState.None;
            }
        }
    }
}
