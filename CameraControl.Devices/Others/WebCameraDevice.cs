﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Accord.Video;
using Accord.Video.DirectShow;
using CameraControl.Devices.Classes;

namespace CameraControl.Devices.Others
{
    public class WebCameraDevice: BaseCameraDevice
    {
        private VideoCaptureDevice _captureDevice;

        private LiveViewData _liveViewData = new LiveViewData();

        private volatile bool _operationInProgress;
        private Object _lock = new Object();
        private volatile bool _processLiveView = false;

        
        public WebCameraDevice()
        {
            Capabilities.Add(CapabilityEnum.LiveView);
        }

        public void Init(string id)
        {
            PortName = id;
            _captureDevice = new VideoCaptureDevice(id);
            _captureDevice.NewFrame += _localWebCam_NewFrame;
            _captureDevice.SnapshotFrame += _localWebCam_SnapshotFrame;
            _captureDevice.ProvideSnapshots = true;
            IsConnected = true;
            LiveViewImageZoomRatio = new PropertyValue<long>();
            StartLiveView();
        }

        private void _localWebCam_NewFrame(object sender, NewFrameEventArgs eventargs)
        {
            if (_operationInProgress || !_processLiveView)
                return;
            try
            {
                _operationInProgress = true;
                Image img = (Bitmap)eventargs.Frame.Clone();
                MemoryStream ms = new MemoryStream();
                img.Save(ms, ImageFormat.Jpeg);
                ms.Seek(0, SeekOrigin.Begin);
                _liveViewData.ImageData = ms.ToArray();
            }
            catch (Exception ex)
            {
                Log.Error("Error get webcam frame ", ex);
            }
        }

        public override void TransferFile(object o, string filename)
        {
            byte[] data = (byte[])o;
            File.WriteAllBytes(filename, data);
        }

        public override void CapturePhotoNoAf()
        {
            CapturePhoto();
        }

        public override  void CapturePhoto()
        {
            StartLiveView();

            _processLiveView = true;
            GetLiveViewImage();
            if (_liveViewData.ImageData != null)
            {
                PhotoCaptureEvent(_liveViewData.ImageData);
                _liveViewData.ImageData = null;
            }
            else
            {
                throw new Exception("Could not capture photo from webcam.");
            }
            _processLiveView = false;

            //StopLiveView();
        }

        public override void StartLiveView()
        {
            lock (_lock)
            {
                if (!_captureDevice.IsRunning)
                {
                    _captureDevice.Start();
                   
                }
                _operationInProgress = false;
                _processLiveView = true;
            }
        }

        public override void StopLiveView()
        {
            lock (_lock)
            {
                _captureDevice.SignalToStop();
                _captureDevice.WaitForStop();
                _operationInProgress = false;
                _processLiveView = false;
            }
        }

        public override LiveViewData GetLiveViewImage()
        {
            _operationInProgress = false;
            return _liveViewData;
        }

        private void PhotoCaptureEvent(byte[] data)
        {
            try
            {
                PhotoCapturedEventArgs args = new PhotoCapturedEventArgs
                {
                    WiaImageItem = null,
                    EventArgs = null,
                    CameraDevice = this,
                    FileName = "img0000.jpg",
                    Handle = data
                };
                OnPhotoCapture(this, args);

            }
            catch (Exception e)
            {
                Log.Error("Photo capture error", e);
            }

        }

        private void _localWebCam_SnapshotFrame(object sender, NewFrameEventArgs eventargs)
        {
            try
            {
                System.Drawing.Image img = (Bitmap)eventargs.Frame.Clone();
                using (MemoryStream ms = new MemoryStream())
                {
                    img.Save(ms, ImageFormat.Jpeg);
                    ms.Seek(0, SeekOrigin.Begin);
                    PhotoCaptureEvent(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to execute event", ex);
            }
        }

        public override void Close()
        {
            if (_captureDevice != null)
           {
                _captureDevice.SignalToStop();
                _captureDevice.SnapshotFrame -= _localWebCam_SnapshotFrame;
                _captureDevice.NewFrame -= _localWebCam_NewFrame;
                _captureDevice.WaitForStop();
                _captureDevice = null;
            }
        }
    }
}
