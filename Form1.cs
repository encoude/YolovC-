using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using DirectShowLib;
using static Yolov.Open_Camera;
using static Yolov.Objiect_Detection;

namespace Yolov
{
    public partial class Form1 : Form
    {
        string model_path;
        public string[] class_names;
        public int class_num;

        DateTime dt1 = DateTime.Now;
        DateTime dt2 = DateTime.Now;

        int input_height;
        int input_width;
        float ratio_height;
        float ratio_width;

        InferenceSession onnx_session;

        int box_num;
        float conf_threshold;
        float nms_threshold;


        private int selectDeviceIndex = 0;
        private List<DsDevice> devices;

        private List<VideoCapture> captures = new List<VideoCapture>();
        private List<System.Windows.Forms.Timer> timers = new List<System.Windows.Forms.Timer>();

        private bool isCloseCameraFlag = true;

        public Form1()
        {
            InitializeComponent();
            cmbDeviceList.Items.Clear();
            getComputerCameraList();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            getComputerCameraList();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            closeCamera();
            if (model_path == null)
            {
                MessageBox.Show("请先加载模型", "PROMPT");
                return;
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.*|*.bmp;*.jpg;*.jpeg;*.tiff;*.tiff;*.png";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            //pictureBox1.Image = null;
            //pictureBox1.Image = new Bitmap(ofd.FileName);
            Mat image = new Mat(ofd.FileName);
            string ResultName =  objectDetect(image);
            if(ResultName == "None")
            {
                MessageBox.Show("None");
            }
            else
            {
                MessageBox.Show("Have");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (model_path == null)
            {
                MessageBox.Show("请先加载模型", "PROMPT");
                return;
            }
            openCamera();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.*|*.onnx;";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            model_path = ofd.FileName;

            //创建输出会话，用于输出模型读取信息
            SessionOptions options = new SessionOptions();
            options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO;
            options.AppendExecutionProvider_CPU(0);// 设置为CPU上运行

            // 创建推理模型类，读取模型文件
            onnx_session = new InferenceSession(model_path, options);//model_path 为onnx模型文件的路径

            input_height = 640;
            input_width = 640;

            box_num = 8400;
            conf_threshold = 0.25f;
            nms_threshold = 0.5f;

            class_names = [ "Have","None"];
            class_num = class_names.Length;
        }

        private void getComputerCameraList()
        {
            cmbDeviceList.Items.Clear();
            cmbDeviceList.SelectedText = "";
            devices = new List<DsDevice>(DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice));
            for (int i = 0; i < devices.Count; i++)
            {
                string serialNum = GetUsbDeviceSerialNumber(devices[i].DevicePath);
                string deviceName = devices[i].Name;
                string deviceNamePath = deviceName + "_" + serialNum;
                cmbDeviceList.Items.Add(deviceNamePath);
                if (selectDeviceIndex == i)
                {
                    cmbDeviceList.SelectedItem = deviceNamePath;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 这里可以执行一些操作，比如提示用户确认关闭窗体
            DialogResult result = MessageBox.Show("您确定要关闭应用程序吗？",
                                                  "确认关闭", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            // 如果用户选择“是”，则允许关闭窗体
            if (result == DialogResult.Yes)
            {
                closeCamera();
            }
            else
            {
                // 用户选择“否”，则取消关闭窗体
                e.Cancel = true;
            }
        }

        //打开摄像头
        private void openCamera()
        {
            //关闭
            closeCamera();
            // 检查是否有选中项
            if (cmbDeviceList.SelectedItem != null)
            {
                // 获取选中项
                string selectedItemText = cmbDeviceList.SelectedItem.ToString();
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
        private int getDeviceByName(string name)
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

        public unsafe float[] Transpose(float[] tensorData, int rows, int cols)
        {
            float[] transposedTensorData = new float[tensorData.Length];

            fixed (float* pTensorData = tensorData)
            {
                fixed (float* pTransposedData = transposedTensorData)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            int index = i * cols + j;
                            int transposedIndex = j * rows + i;
                            pTransposedData[transposedIndex] = pTensorData[index];
                        }
                    }
                }
            }
            return transposedTensorData;
        }

        /// <summary>
        /// 目标检测
        /// </summary>
        /// <param name="image_path"></param>
        private string objectDetect(Mat image)
        {

            //图片缩放
            int height = image.Rows;
            int width = image.Cols;
            Mat temp_image = image.Clone();
            if (height > input_height || width > input_width)
            {
                float scale = Math.Min((float)input_height / height, (float)input_width / width);
                OpenCvSharp.Size new_size = new OpenCvSharp.Size((int)(width * scale), (int)(height * scale));
                Cv2.Resize(image, temp_image, new_size);
            }
            ratio_height = (float)height / temp_image.Rows;
            ratio_width = (float)width / temp_image.Cols;
            Mat input_img = new Mat();
            Cv2.CopyMakeBorder(temp_image, input_img, 0, input_height - temp_image.Rows, 0, input_width - temp_image.Cols, BorderTypes.Constant);

            //输入Tensor
            Tensor<float> input_tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });

            for (int y = 0; y < input_img.Height; y++)
            {
                for (int x = 0; x < input_img.Width; x++)
                {
                    input_tensor[0, 0, y, x] = input_img.At<Vec3b>(y, x)[0] / 255f;
                    input_tensor[0, 1, y, x] = input_img.At<Vec3b>(y, x)[1] / 255f;
                    input_tensor[0, 2, y, x] = input_img.At<Vec3b>(y, x)[2] / 255f;
                }
            }

            List<NamedOnnxValue> input_container = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", input_tensor)
        };

            //推理
            dt1 = DateTime.Now;
            var ort_outputs = onnx_session.Run(input_container).ToArray();
            dt2 = DateTime.Now;

            float[] data = Transpose(ort_outputs[0].AsTensor<float>().ToArray(), 4 + class_num, box_num);

            float[] confidenceInfo = new float[class_num];
            float[] rectData = new float[4];

            List<DetectionResult> detResults = new List<DetectionResult>();

            for (int i = 0; i < box_num; i++)
            {
                Array.Copy(data, i * (class_num + 4), rectData, 0, 4);
                Array.Copy(data, i * (class_num + 4) + 4, confidenceInfo, 0, class_num);

                float score = confidenceInfo.Max(); // 获取最大值

                int maxIndex = Array.IndexOf(confidenceInfo, score); // 获取最大值的位置

                int _centerX = (int)(rectData[0] * ratio_width);
                int _centerY = (int)(rectData[1] * ratio_height);
                int _width = (int)(rectData[2] * ratio_width);
                int _height = (int)(rectData[3] * ratio_height);

                detResults.Add(new DetectionResult(
                    maxIndex,
                    class_names[maxIndex],
                    new Rect(_centerX - _width / 2, _centerY - _height / 2, _width, _height),
                    score));
            }

            //NMS
            CvDnn.NMSBoxes(detResults.Select(x => x.Rect), detResults.Select(x => x.Confidence), conf_threshold, nms_threshold, out int[] indices);
            detResults = detResults.Where((x, index) => indices.Contains(index)).ToList();

            string ClassNmae = "";
            //绘制结果
            Mat result_image = image.Clone();
            foreach (DetectionResult r in detResults)
            {
                ClassNmae = r.Class;
                Cv2.PutText(result_image, $"{r.Class}:{r.Confidence:P0}", new OpenCvSharp.Point(r.Rect.TopLeft.X, r.Rect.TopLeft.Y - 10), HersheyFonts.HersheySimplex, 1, Scalar.Red, 2);
                Cv2.Rectangle(result_image, r.Rect, Scalar.Red, thickness: 2);
            }
            using (var ms = result_image.ToMemoryStream())
            {
                Bitmap bitmap = (Bitmap)Image.FromStream(ms);
                pictureBox1.Image = bitmap;
            }

            labelUsedTime.Text = "推理耗时:" + (dt2 - dt1).TotalMilliseconds + "ms";
            return ClassNmae;
        }

        private void UpdateFrame(VideoCapture capture)
        {
            Mat frame = new Mat();
            if (capture.Read(frame) && !isCloseCameraFlag)
            {
                //目标检测 识别
                objectDetect(frame);
            }
        }
    }
    public class DetectionResult
    {
        public DetectionResult(int ClassId, string Class, Rect Rect, float Confidence)
        {
            this.ClassId = ClassId;
            this.Confidence = Confidence;
            this.Rect = Rect;
            this.Class = Class;
        }

        public string Class { get; set; }

        public int ClassId { get; set; }

        public float Confidence { get; set; }

        public Rect Rect { get; set; }

    }
}
