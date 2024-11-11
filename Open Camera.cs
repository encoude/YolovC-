using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using DirectShowLib;
using Microsoft.ML.OnnxRuntime;
using static Yolov.Objiect_Detection;
using System.Drawing;


namespace Yolov
{
    public class Open_Camera
    {
        string model_path;
        public string[] class_names;
        public int class_num;

        DateTime dt1 = DateTime.Now;
        DateTime dt2 = DateTime.Now;  

        private int selectDeviceIndex = 0;
        private List<DsDevice> devices;

        private List<VideoCapture> captures = new List<VideoCapture>();
        private List<System.Windows.Forms.Timer> timers = new List<System.Windows.Forms.Timer>();

        private bool isCloseCameraFlag = true;

        Objiect_Detection Detection = new Objiect_Detection();



        //打开摄像头
        public void openCamera(object SelectedItem)
        {
            //关闭
            closeCamera();
            // 检查是否有选中项
            if (SelectedItem != null)
            {
                // 获取选中项
                string selectedItemText = SelectedItem.ToString();
                selectDeviceIndex = getDeviceByName(selectedItemText);
                // 初始化摄像头和定时器
                InitializeCameraAndTimer(selectDeviceIndex, selectedItemText);
            }
            else
            {
                MessageBox.Show("请选择摄像头");
                return;
            }
        }

        //通过名称获取摄像头
        public int getDeviceByName(string name)
        {
            int device = 0;
            if (!String.IsNullOrEmpty(name))
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    string serialNum = GetUsbDeviceSerialNumber(devices[i].DevicePath);
                    string deviceName = devices[i].Name;
                    string deviceNamePath = deviceName + "_" + serialNum;
                    if (deviceNamePath.Equals(name))
                    {
                        device = i;
                        return device;
                    }
                }
            }
            return device;
        }

        //获取设备序列号
        public static string GetUsbDeviceSerialNumber(string devicePath)
        {
            string serialNumber = "";
            string pattern = @"vid_([0-9a-z]{4})&pid_([0-9a-z]{4})&mi_00#([0-9a-f&]{1,})";
            //"@device:pnp:\\\\?\\usb#vid_13d3&pid_56f8&mi_00#6&24054308&0&0000#{65e8773d-8f56-11d0-a3b9-00a0c9223196}\\global"
            Match match = Regex.Match(devicePath, pattern);
            if (match.Success)
            {
                string vendorId = match.Groups[1].Value;
                string productId = match.Groups[2].Value;
                serialNumber = match.Groups[3].Value.Replace("&", "");

                Console.WriteLine("Vendor ID: " + vendorId);
                Console.WriteLine("Product ID: " + productId);
                Console.WriteLine("Serial Number: " + serialNumber);
            }
            else
            {
                Console.WriteLine("No match found.");
            }
            return serialNumber;
        }

        //摄像头初始化
        private void InitializeCameraAndTimer(int selectDeviceIndex, string deviceName)
        {
            // 尝试打开摄像头
            VideoCapture capture = new VideoCapture(selectDeviceIndex); // 0 是默认摄像头的索引
            captures.Add(capture);
            // 检查摄像头是否成功打开
            if (!capture.IsOpened())
            {
                MessageBox.Show("无法打开摄像头");
            }
            isCloseCameraFlag = false;
            //labelList[selectDeviceIndex].Text = deviceName;
            // 创建定时器，用于定时从摄像头读取帧并更新PictureBox
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 60; // 约60帧/秒
            timer.Tick += (sender, e) => UpdateFrame(capture);
            timer.Start();
            timers.Add(timer);
        }

        //释放摄像头资源
        private void closeCamera()
        {
            isCloseCameraFlag = true;
            // 释放摄像头资源
            // 释放摄像头资源和停止定时器
            if (null != captures && captures.Count > 0)
            {
                foreach (var capture in captures)
                {
                    capture.Release();
                }
            }
            if (null != timers && timers.Count > 0)
            {
                foreach (var timer in timers)
                {
                    timer.Stop();
                }
            }
        }

        private void UpdateFrame(VideoCapture capture)
        {
            Mat frame = new Mat();
            if (capture.Read(frame) && !isCloseCameraFlag)
            {
                //目标检测 识别
                 Detection.objectDetect(frame,out Bitmap Images,out string ShowTimes);
            }
        }

        //释放摄像头资源
        public void CloseCamera()
        {
            isCloseCameraFlag = true;
            // 释放摄像头资源
            // 释放摄像头资源和停止定时器
            if (null != captures && captures.Count > 0)
            {
                foreach (var capture in captures)
                {
                    capture.Release();
                }
            }
            if (null != timers && timers.Count > 0)
            {
                foreach (var timer in timers)
                {
                    timer.Stop();
                }
            }
        }
    }
}
