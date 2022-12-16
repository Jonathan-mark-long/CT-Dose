using ExecutableLogic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


[assembly: ESAPIScript(IsWriteable = true)]

namespace Testing_CT_Images
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                using (Application app = Application.CreateApplication())
                {
                    Perform(app, args);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                Console.Read();
            }
        }
        public static void Perform(Application app, string[] args)
        {
            ScriptContextArgs ctx = ScriptContextArgs.From(args);
            Console.WriteLine(ctx.PlanSetupId);
            Patient patient = app.OpenPatientById(ctx.PatientId);
            Course course = patient.Courses.First(e => e.Id == ctx.CourseId);
            PlanSetup plan = course.PlanSetups.First(e => e.Id == ctx.PlanSetupId);
            List<Beam> beams = plan.Beams.Where(b => !b.IsSetupField).OrderBy(b => b.BeamNumber).ToList();
            Beam beam = beams.FirstOrDefault();
            patient.BeginModifications();

            VMS.TPS.Common.Model.API.Image CT = plan.StructureSet.Image;
            Dose dose = plan.Dose; //Get plan dose.

            CTArrays(CT, dose, beam);






            Console.ReadKey();
            app.ClosePatient();


        }

        public static void SourcetoPng(BitmapSource bmp, String axis)
        {
            using (var fileStream = new FileStream(@"\\Tevirari002\va_data$\ProgramData\Vision\PublishedScripts\TemporaryImages\" + axis + ".png", FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                encoder.Save(fileStream);
                fileStream.Close();
            }

            /* Bitmap is flipped horizontally, not sure why but this deals with it.
            System.Drawing.Image png = System.Drawing.Image.FromFile(@"\\Tevirari002\va_data$\ProgramData\Vision\PublishedScripts\TemporaryImages\" + axis + ".png");
            Bitmap bitmap = new Bitmap(png);
            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
            png.Dispose();
            bitmap.Save(@"\\Tevirari002\va_data$\ProgramData\Vision\PublishedScripts\TemporaryImages\" + axis + ".png", ImageFormat.Png);
            */
        }


        private static void CTArrays( VMS.TPS.Common.Model.API.Image CT , Dose dose, Beam beam)
        {

            List<int> Tranverse_List = new List<int>();
            List<int> Sagital_List = new List<int>();
            List<int> Coronal_List = new List<int>();

            List<int> Transverse_Dose = new List<int>(); // List of dose points to generate bitmap from.

            List<int> Sagital_Dose = new List<int>();
            List<int> Coronal_Dose = new List<int>();



            int Transverse_slicenumber = (int)((153.0/CT.ZRes) - beam.IsocenterPosition.z);

            int Transverse_Dose_slice = 50; // Slice number to get planar dose.
            int Sagital_Dose_slice = 50; // Slice number to get planar dose.
            int Coronal_Dose_slice = 50; // Slice number to get planar dose.



            int Sagital_slicenumber =  (int)(((262.0 + beam.IsocenterPosition.x) / CT.XRes) );
            int Coronal_slicenumebr = (int)(((200.0 + beam.IsocenterPosition.y) / CT.YRes) );
            int[,] pixels = new int[CT.XSize, CT.YSize];
            int[,] doses = new int[dose.XSize, dose.YSize]; // integer array to store dose voxels.

            for (int z = 0; z < CT.ZSize; z++)
            {
                
                CT.GetVoxels(z, pixels);
                Console.WriteLine(z.ToString());

                for (int i = 0; i < CT.XSize; i++)
                {
                    for (int j = 0; j < CT.YSize; j++)
                    {
                        int value = pixels[i, j];
                        if (z == Transverse_slicenumber)
                        {
                            Tranverse_List.Add(value);
                        }
                        if (i == Sagital_slicenumber)
                        {
                            Sagital_List.Add(value);
                        }
                        if (j == Coronal_slicenumebr)
                        {
                            Coronal_List.Add(value);
                        }

                    }

                    
                }
                

            }
            for (int z = 0; z < dose.ZSize; z++)
            {

                dose.GetVoxels(z, doses); // Get dose voxels and copy them to integer array/
                Console.WriteLine(z.ToString());

                for (int i = 0; i < dose.XSize; i++)
                {
                    for (int j = 0; j < dose.YSize; j++)
                    {
                        
                        int dose_value = doses[i, j];
                        if (z == Transverse_Dose_slice) // z = slice we want dose for.
                        {
                            Transverse_Dose.Add(dose_value); // add value to the list.
                        }
                        if (i == Sagital_Dose_slice)
                        {
                            Sagital_Dose.Add(dose_value);
                        }
                        if (j == Coronal_Dose_slice)
                        {
                            Coronal_Dose.Add(dose_value);
                        }

                    }


                }


            }

            var Transverse = BuildTransverseImage( CT, Tranverse_List);
            SourcetoPng(Transverse, "transverse");

            var Transverse_DoseMap = BuildTransverseDoseImage(dose, Transverse_Dose); // Generate bitmap.
            SourcetoPng(Transverse_DoseMap, "transverseDose"); // Generate png.

            var sagital = BuildSagitalImage( CT, Sagital_List);
            SourcetoPng(sagital, "sagital");

            var Sagital_DoseMap = BuildSagitalDoseImage(dose, Sagital_Dose); // Generate bitmap.
            SourcetoPng(Sagital_DoseMap, "sagitalDose"); // Generate png.


            var coronal = BuildCoronalImage( CT, Coronal_List);
            SourcetoPng(coronal, "coronal");

            var Coronal_DoseMap = BuildCoronalDoseImage(dose, Coronal_Dose); // Generate bitmap.
            SourcetoPng(Coronal_DoseMap, "coronalDose"); // Generate png.


        }

        private static WriteableBitmap BuildTransverseImage( VMS.TPS.Common.Model.API.Image CT, List<int> List)
        {

            var max = List.Max();
            var min = List.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (CT.YSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * CT.XSize];

            for (int i = 0; i < List.Count; i++)
            {
                double value = List.ElementAt(i);
                image_bytes[i] = Convert.ToByte(255 * ((value - min) / (max - min)));
            }

            BitmapSource source = BitmapSource.Create(CT.YSize, CT.XSize, 25.4 / CT.XRes, 25.4 / CT.YRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }
        private static WriteableBitmap BuildTransverseDoseImage(Dose dose, List<int> List)
        {

            var max = List.Max();
            var min = List.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (dose.YSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * dose.XSize];

            for (int i = 0; i < List.Count; i++)
            {
                double value = List.ElementAt(i);
                image_bytes[i] = Convert.ToByte(255 * ((value - min) / (max - min)));
            }

            BitmapSource source = BitmapSource.Create(dose.YSize, dose.XSize, 25.4 / dose.XRes, 25.4 / dose.YRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }

        private static WriteableBitmap BuildSagitalImage( VMS.TPS.Common.Model.API.Image CT, List<int> List)
        {

            var max = List.Max();
            var min = List.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (CT.YSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * CT.ZSize];

            for (int i = 0; i < List.Count; i++)
            {
                double value = List.ElementAt(i);
                image_bytes[i] = Convert.ToByte(255 * ((value - min) / (max - min)));
            }

            BitmapSource source = BitmapSource.Create(CT.YSize, CT.ZSize, 25.4 / CT.XRes, 25.4 / CT.ZRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }

        private static WriteableBitmap BuildSagitalDoseImage(Dose dose, List<int> List)
        {

            var max = List.Max();
            var min = List.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (dose.YSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * dose.ZSize];

            for (int i = 0; i < List.Count; i++)
            {
                double value = List.ElementAt(i);
                image_bytes[i] = Convert.ToByte(255 * ((value - min) / (max - min)));
            }

            BitmapSource source = BitmapSource.Create(dose.YSize, dose.ZSize, 25.4 / dose.XRes, 25.4 / dose.ZRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }
        private static WriteableBitmap BuildCoronalImage( VMS.TPS.Common.Model.API.Image CT, List<int> List)
        {

            var max = List.Max();
            var min = List.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (CT.XSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * CT.ZSize];

            for (int i = 0; i < List.Count; i++)
            {
                double value = List.ElementAt(i);
                image_bytes[i] = Convert.ToByte(255 * ((value - min) / (max - min)));
            }

            BitmapSource source = BitmapSource.Create(CT.XSize, CT.ZSize, 25.4 / CT.YRes, 25.4 / CT.ZRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }

        private static WriteableBitmap BuildCoronalDoseImage(Dose dose, List<int> List)
        {

            var max = List.Max();
            var min = List.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (dose.XSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * dose.ZSize];

            for (int i = 0; i < List.Count; i++)
            {
                double value = List.ElementAt(i);
                image_bytes[i] = Convert.ToByte(255 * ((value - min) / (max - min)));
            }

            BitmapSource source = BitmapSource.Create(dose.XSize, dose.ZSize, 25.4 / dose.YRes, 25.4 / dose.ZRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }


    }
}
