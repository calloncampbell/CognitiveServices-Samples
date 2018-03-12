using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CognitiveServices.FaceAPI.Verification
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Face securedFace;
        double imageResizeFactor;

        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient(
            "YOUR-SUBSCRIPTION-KEY-GOES-HERE",
            "YOUR-SUBSCRIPTION-ENDPOINT-URL-GOES-HERE");

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BrowseButtonIdentification_Click(object sender, RoutedEventArgs e)
        {
            var filePath = BrowsePhoto();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            // Detect any faces in the image.
            var detectedFaces = await UploadAndDetectFaces2(filePath, FaceIdentificationPhoto);
            securedFace = detectedFaces[0];
        }

        private async void BrowseButtonVerification_Click(object sender, RoutedEventArgs e)
        {
            var filePath = BrowsePhoto();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            // Verify face in the image.
            var result = await VerifyFaces(filePath, FaceVerificationPhoto);
            if (result == null)
            {
                faceDescriptionStatusBar.Text = "Verification result: No face detected. Please try again.";
                return;
            }

            faceDescriptionStatusBar.Text = $"Verification result: The two faces belong to the same person. Confidence is {result.Confidence}.";
        }

        private string BrowsePhoto()
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            if (!(bool)result)
            {
                return string.Empty;
            }

            return openDlg.FileName;
        }

        private async Task<Face[]> UploadAndDetectFaces2(string imageFilePath, Image image)
        {
            Title = "Detecting...";

            // The list of Face attributes to return.
            IEnumerable<FaceAttributeType> faceAttributes = new FaceAttributeType[]
            {
                FaceAttributeType.Gender,
                FaceAttributeType.Age,
                FaceAttributeType.Smile,
                FaceAttributeType.Emotion,
                FaceAttributeType.Glasses,
                FaceAttributeType.Hair
            };

            var faces = new Face[0];

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    faces = await faceServiceClient.DetectAsync(
                        imageFileStream,
                        returnFaceId: true,
                        returnFaceLandmarks: false,
                        returnFaceAttributes: faceAttributes);
                }
            }
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
            }

            Title = String.Format("Detection Finished. {0} face(s) detected", faces.Length);

            if (faces.Length > 0)
            {
                Uri fileUri = new Uri(imageFilePath);
                BitmapImage bitmapSource = new BitmapImage();

                bitmapSource.BeginInit();
                bitmapSource.CacheOption = BitmapCacheOption.None;
                bitmapSource.UriSource = fileUri;
                bitmapSource.EndInit();

                image.Source = bitmapSource;

                // Prepare to draw rectangles around the faces.
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource, new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
                double dpi = bitmapSource.DpiX;
                imageResizeFactor = 96 / dpi;

                for (int i = 0; i < faces.Length; ++i)
                {
                    Face face = faces[i];

                    Brush genderBrush;
                    if (face.FaceAttributes.Gender == "male")
                        genderBrush = Brushes.Blue;
                    else
                        genderBrush = Brushes.DeepPink;

                    // Draw a rectangle on the face.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(genderBrush, 2),
                        new Rect(
                            face.FaceRectangle.Left * imageResizeFactor,
                            face.FaceRectangle.Top * imageResizeFactor,
                            face.FaceRectangle.Width * imageResizeFactor,
                            face.FaceRectangle.Height * imageResizeFactor
                            )
                    );
                }

                drawingContext.Close();

                // Display the image with the rectangle around the face.
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * imageResizeFactor),
                    (int)(bitmapSource.PixelHeight * imageResizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                image.Source = faceWithRectBitmap;
            }

            return faces;
        }

        private async Task<VerifyResult> VerifyFaces(string imageFilePath, Image image)
        {
            Title = "Verifying...";
            var verificationFaces = await UploadAndDetectFaces2(imageFilePath, image);

            if (verificationFaces.Length == 0)
            {
                return null;
            }

            return await faceServiceClient.VerifyAsync(verificationFaces[0].FaceId, securedFace.FaceId);
        }
      
    }
}
