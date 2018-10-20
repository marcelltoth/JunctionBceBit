﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge;
using AForge.Imaging;
using AForge.Imaging.ComplexFilters;
using AForge.Imaging.Filters;
using Dicom;
using Dicom.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Point = System.Drawing.Point;

namespace WWFOC
{
    public partial class Form1 : Form
    {

        public string SourcePath { get; set; } = @"C:\Users\Benke Sándor\Documents\Junction\Dataset\386801";
        public string SourceFileMask = "MR.*";


        public int DetectionParam1 { get; set; } = 300;
        public int DetectionParam2 { get; set; } = 100;
        public int DetectionParam3 { get; set; } = 0;

        public int SelectedIndex { get; set; }
        private readonly List<ImageProcessorOutput> _output = new List<ImageProcessorOutput>(); // protected by lock(_output)
        
        
        public Form1()
        {
            InitializeComponent();
           
            Load += OnLoad;
            
            this.MouseWheel += OnMouseWheel;
            buttonDebug.Click += ButtonDebugOnClick;
        }

        private async void ButtonDebugOnClick(object sender, EventArgs e)
        {
            lock (_output)
            {
                if (_output.Count <= SelectedIndex) return;
            }

            ImageProcessorOutput output = _output[SelectedIndex];
            DicomFile sourceFile = await DicomFile.OpenAsync(output.SourceFile.FullName);
            using (Bitmap original = new DicomImage(sourceFile.Dataset).RenderImage().AsClonedBitmap())
            {
                ImageProcessor ip = new ImageProcessor(original, debug: true);
                ip.Process();
            }
            
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            lock (_output)
            {
                SelectedIndex = Math.Min(Math.Max(SelectedIndex + Math.Sign(e.Delta), 0), _output.Count);
            }
            RefreshView();
        }


        public void RefreshView()
        {
            Invoke((MethodInvoker) delegate
            {
                lock (_output)
                {
                    if (_output.Count > SelectedIndex)
                    {
                        var selectedResult = _output[SelectedIndex];
                        this.Text = selectedResult.Title;
                        for (int i = 0; i < selectedResult.Images.Count; i++)
                        {
                            var currentImage = selectedResult.Images[i];
                            if (tabViewer.TabCount <= i)
                            {
                                tabViewer.TabPages.Add(new TabPage(currentImage.Title));
                            }
                            else
                            {
                                tabViewer.TabPages[i].Text = currentImage.Title;
                            }

                            if (tabViewer.TabPages[i].Controls.Count < 1)
                            {
                                tabViewer.TabPages[i].Controls.Add(new PictureBox()
                                {
                                    Dock = DockStyle.Fill, 
                                    SizeMode = PictureBoxSizeMode.Zoom
                                });
                            }

                            ((PictureBox) tabViewer.TabPages[i].Controls[0]).Image =
                                currentImage.Bitmap.Clone(new RectangleF(PointF.Empty, currentImage.Bitmap.Size),
                                    PixelFormat.Format32bppRgb);
                        }
                    }
                }
            });
        }

        private async void OnLoad(object sender, EventArgs e)
        {
            await UpdateImageAsync();
        }

        private async Task UpdateImageAsync()
        {
            var sourceDir = new DirectoryInfo(SourcePath);
            IEnumerable<Task<ImageProcessorOutput>> resultTasks = sourceDir.EnumerateFiles(SourceFileMask)
                .OrderBy(f =>
                {
                    string lastPart = Path.GetFileNameWithoutExtension(f.Name).Split(' ').Last();
                    Int32.TryParse(lastPart, out int index);
                    return index;
                })
                .Select(async file =>
                {
                    return await Task.Run(async () =>
                    {
                        DicomFile sourceFile = await DicomFile.OpenAsync(file.FullName);
                        using (Bitmap original = new DicomImage(sourceFile.Dataset).RenderImage().AsClonedBitmap())
                        {
                            ImageProcessor ip = new ImageProcessor(original);
                            var result = ip.Process();
                            result.Title = file.Name;
                            result.SourceFile = file;
                            return result;
                        }
                    });

                }).ToList();
            
            foreach (var resultTask in resultTasks)
            {
                var result = await resultTask;
                var needRefresh = false;
                lock (_output)
                {
                    _output.Add(result);
                    if (_output.Count-1 == SelectedIndex)
                    {
                        needRefresh = true;
                    }
                }
                if (needRefresh)
                {
                    RefreshView();
                }
            }
        }

        
    }
}