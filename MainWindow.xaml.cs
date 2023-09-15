﻿using Lab1.Objects;
using Lab1.Parser;
using Lab1.Primitives;
using Lab1.Rasterization;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const Key OPEN_FILE_KEY = Key.O;
        private const Key CLOSE_APP_KEY = Key.Escape;
        private const Key INVERT_COLORS_KEY = Key.I;

        private OpenFileDialog openFileDialog;
        private ObjParser parser;
        private WriteableBitmap renderBuffer;
        private Model model;
        private Camera camera;

        private Point mouseClickPosition;

        private Color fillColor = Colors.Black;
        private Color drawColor = Colors.White;

        private DDALine DDALineRasterization = new DDALine();
        private Bresenham BresenhamRasterizaton = new Bresenham();
        private IRasterization rasterization;

        public MainWindow()
        {
            InitializeComponent();
            openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Wavefront files (.obj)|*.obj";
            parser = new ObjParser();
            rasterization = BresenhamRasterizaton;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            InitializeRenderBuffer();
            FillRenderBuffer(fillColor);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            InitializeRenderBuffer();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case OPEN_FILE_KEY:
                    if (openFileDialog.ShowDialog() == true)
                    {
                        model = parser.Parse(openFileDialog.FileName);
                        camera = new Camera();
                        DrawModel(model, camera);
                    }
                    break;
                case INVERT_COLORS_KEY:
                    Color buffer = fillColor;
                    fillColor = drawColor;
                    drawColor = buffer;
                    DrawModel(model, camera);
                    break;
                case CLOSE_APP_KEY:
                    Application.Current.Shutdown();
                    break;
                default:
                    break;
            }
        }

        private void InitializeRenderBuffer()
        {
            renderBuffer = new WriteableBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Bgr32, null);
            imgScreen.Source = renderBuffer;
        }

        private void DrawModel(Model model, Camera camera)
        {
            FillRenderBuffer(fillColor);
            double width = ActualWidth;
            double height = ActualHeight;
            List<Vector3> convertedVertices = new List<Vector3>();
            foreach (var vertex in model.Vertices)
            {
                Vector3 convertedVertex = Matrix4.Projection(MathF.PI / 2, 2f, 0, 100) * (camera.View() * (Matrix4.Scale(new Vector3(1, 1, 1)) * vertex));
                convertedVertex.Update(convertedVertex / convertedVertex.W);
                convertedVertices.Add(convertedVertex);
            }

            foreach (var polygon in model.Polygons)
            {
                for (int i = 0; i < polygon.Indices.Count; i++)
                {
                    Vector3 startVertex = convertedVertices[polygon.Indices[i]];
                    Vector3 endVertex = convertedVertices[polygon.Indices[(i + 1) % polygon.Indices.Count]];

                    if (startVertex.X < -1 || startVertex.X > 1 || startVertex.Y < -1 || startVertex.Y > 1 || startVertex.Z < -1 || startVertex.Z > 1) continue;
                    if (endVertex.X < -1 || endVertex.X > 1 || endVertex.Y < -1 || endVertex.Y > 1 || endVertex.Z < -1 || endVertex.Z > 1) continue;

                    Vector3 screenStart = (Matrix4.Viewport(width - 1, height - 1, 0, 0) * startVertex);
                    Vector3 screenEnd = (Matrix4.Viewport(width - 1, height - 1, 0, 0) * endVertex);
                    DrawLine(screenStart.X, screenStart.Y, screenEnd.X, screenEnd.Y, drawColor);
                }
            }
        }

        private void FillRenderBuffer(Color fillColor)
        {
            if (renderBuffer == null)
            {
                return;
            }

            int width = renderBuffer.PixelWidth;
            int height = renderBuffer.PixelHeight;
            int bytesPerPixel = (renderBuffer.Format.BitsPerPixel + 7) / 8;
            int stride = width * bytesPerPixel;
            byte[] pixelData = new byte[width * height * bytesPerPixel];

            for (int i = 0; i < pixelData.Length; i += bytesPerPixel)
            {
                pixelData[i + 2] = fillColor.R;
                pixelData[i + 1] = fillColor.G;
                pixelData[i + 0] = fillColor.B;
                pixelData[i + 3] = 0;
            }
            renderBuffer.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);
        }

        private void DrawPixel(int x, int y, Color color)
        {
            try
            {
                // Reserve the back buffer for updates
                renderBuffer.Lock();

                unsafe
                {
                    // Get a pointer to the back buffer
                    IntPtr pBackBuffer = renderBuffer.BackBuffer;

                    // Find the address of the pixel to draw
                    pBackBuffer += y * renderBuffer.BackBufferStride;
                    pBackBuffer += x * 4;

                    // Compute the pixel's color
                    int colorData = color.R << 16;
                    colorData |= color.G << 8;
                    colorData |= color.B << 0;

                    // Assign the color data to the pixel
                    *((int*)pBackBuffer) = colorData;
                }

                // Specify the area of the bitmap that changed
                renderBuffer.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            }
            finally
            {
                // Release the back buffer and make it available for display
                renderBuffer.Unlock();
            }
        }

        private void DrawLine(float xStart, float yStart, float xEnd, float yEnd, Color color)
        {
            foreach (Pixel pixel in rasterization.Rasterize(xStart, yStart, xEnd, yEnd))
            {
                DrawPixel(pixel.X, pixel.Y, color);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mouseClickPosition = e.GetPosition(this);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point clickPosition = e.GetPosition(this);
                double deltaX = clickPosition.X - mouseClickPosition.X;
                double deltaY = clickPosition.Y - mouseClickPosition.Y;
                mouseClickPosition = clickPosition;
                camera.MoveAzimuth(-deltaX);
                camera.MoveZenith(-deltaY);
                DrawModel(model, camera);
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
            {
                camera.ZoomIn();
            }
            else
            {
                camera.ZoomOut();
            }
            DrawModel(model, camera);
        }
    }
}
