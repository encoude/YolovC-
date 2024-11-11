using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;

namespace Yolov
{
    public class Objiect_Detection
    {
        public int class_num;
        public string[] class_names;

        int input_height;
        int input_width;
        float ratio_height;
        float ratio_width;

        DateTime dt1 = DateTime.Now;
        DateTime dt2 = DateTime.Now;

        InferenceSession onnx_session;

        int box_num;
        float conf_threshold;
        float nms_threshold;

        /// <summary>
        /// 目标检测
        /// </summary>
        /// <param name="image_path"></param>
        public void objectDetect(Mat image,out Bitmap OutImage,out string ShowTimes)
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

            //绘制结果
            Mat result_image = image.Clone();
            foreach (DetectionResult r in detResults)
            {
                Cv2.PutText(result_image, $"{r.Class}:{r.Confidence:P0}", new OpenCvSharp.Point(r.Rect.TopLeft.X, r.Rect.TopLeft.Y - 10), HersheyFonts.HersheySimplex, 1, Scalar.Red, 2);
                Cv2.Rectangle(result_image, r.Rect, Scalar.Red, thickness: 2);
            }
            using (var ms = result_image.ToMemoryStream())
            {
                Bitmap bitmap = (Bitmap)Image.FromStream(ms);
                OutImage = bitmap;
            }

            ShowTimes = "推理耗时:" + (dt2 - dt1).TotalMilliseconds + "ms";
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
}
