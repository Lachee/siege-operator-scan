using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SiegeOperatorDigest
{
    class ImageProcessor
    {
        const double xRatio = 500 / 1280.0;
        const double yRatio = 50 / 720.0;
        const double widthRatio = 22 / 1280.0;
        const double heightRatio = 22 / 720.0;

        public static byte[] ProcessImage(string filepath)
        {
            if (!File.Exists(filepath))
                throw new Exception("Image " + filepath + " does not exist");

            using (var image = new MagickImage(filepath))
            {
                return ProcessImage(image);
            }
        }

        public static byte[] ProcessImage(byte[] bytes, int index, int count)
        {
            using (var image = new MagickImage(bytes, index, count))
                return ProcessImage(image);
        }

        public static byte[] ProcessImage(MagickImage image)
        {
            if (image.Width < 1000 || image.Height < 500)
                throw new ArgumentException("Image supplied is too small. Need at least 1000x500");

            if (image.Width > 2000 || image.Height > 2000)
                throw new ArgumentException("Image supplied is too large. Need at maximum 2000x2000");

            //Calculate the relative position
            int x = (int)Math.Round(xRatio * image.Width);
            int y = (int)Math.Round(yRatio * image.Height);
            int width = (int)Math.Round(widthRatio * image.Width);
            int height = (int)Math.Round(heightRatio * image.Height);


            //Perform the crop, repage and return the bytes
            image.Crop(new MagickGeometry(x, y, width, height));
            image.Format = MagickFormat.Jpeg;
            image.RePage();

            return image.ToByteArray();
        }
    }
}
