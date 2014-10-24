﻿namespace PhotoshopFile
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;

    /// <summary>
    /// Used to decode an image from a PSD layer.
    /// </summary>
    public static class ImageDecoder
    {
        /// <summary>
        /// Decodes a <see cref="Layer"/> into a <see cref="Bitmap"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to decode.</param>
        /// <returns>The <see cref="Bitmap"/> decoded from the layer.</returns>
        public static unsafe Bitmap DecodeImage(Layer layer)
        {
            if (layer.Rect.Width == 0 || layer.Rect.Height == 0)
            {
                return null;
            }

            Bitmap bitmap = new Bitmap(layer.Rect.Width, layer.Rect.Height, PixelFormat.Format32bppArgb);
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapdata = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            byte* numPtr = (byte*)bitmapdata.Scan0.ToPointer();
            for (int y = 0; y < layer.Rect.Height; ++y)
            {
                int num = y * layer.Rect.Width;
                PixelData* pixelDataPtr = (PixelData*)numPtr;
                for (int x = 0; x < layer.Rect.Width; ++x)
                {
                    int pos = num + x;
                    Color baseColor = GetColor(layer, pos);
                    if (layer.SortedChannels.ContainsKey(-2))
                    {
                        int color = GetColor(layer.MaskData, x, y);
                        baseColor = Color.FromArgb(baseColor.A * color / byte.MaxValue, baseColor);
                    }

                    pixelDataPtr->Alpha = baseColor.A;
                    pixelDataPtr->Red = baseColor.R;
                    pixelDataPtr->Green = baseColor.G;
                    pixelDataPtr->Blue = baseColor.B;
                    ++pixelDataPtr;
                }

                numPtr += bitmapdata.Stride;
            }

            bitmap.UnlockBits(bitmapdata);
            return bitmap;
        }

        /// <summary>
        /// Gets the color at the given position in the given <see cref="Layer"/>.
        /// </summary>
        /// <param name="layer">The <see cref="Layer"/> to sample.</param>
        /// <param name="pos">The position to sample.</param>
        /// <returns>The sampled color.</returns>
        private static Color GetColor(Layer layer, int pos)
        {
            Color baseColor = Color.White;
            switch (layer.PsdFile.ColorMode)
            {
                case ColorModes.Grayscale:
                case ColorModes.Duotone:
                    baseColor = Color.FromArgb(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[0].ImageData[pos]);
                    break;
                case ColorModes.Indexed:
                    int index = layer.SortedChannels[0].ImageData[pos];
                    baseColor = Color.FromArgb(layer.PsdFile.ColorModeData[index], layer.PsdFile.ColorModeData[index + 256], layer.PsdFile.ColorModeData[index + 512]);
                    break;
                case ColorModes.RGB:
                    baseColor = Color.FromArgb(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos]);
                    break;
                case ColorModes.CMYK:
                    baseColor = CMYKToRGB(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos], layer.SortedChannels[3].ImageData[pos]);
                    break;
                case ColorModes.Multichannel:
                    baseColor = CMYKToRGB(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos], 0);
                    break;
                case ColorModes.Lab:
                    baseColor = LabToRGB(layer.SortedChannels[0].ImageData[pos], layer.SortedChannels[1].ImageData[pos], layer.SortedChannels[2].ImageData[pos]);
                    break;
            }

            if (layer.SortedChannels.ContainsKey(-1))
            {
                baseColor = Color.FromArgb(layer.SortedChannels[-1].ImageData[pos], baseColor);
            }

            return baseColor;
        }

        /// <summary>
        /// Gets the color at the given pixel position in the given mask.
        /// </summary>
        /// <param name="mask">The mask to sample.</param>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        /// <returns>The mask color.</returns>
        private static int GetColor(Mask mask, int x, int y)
        {
            int num = byte.MaxValue;
            if (mask.PositionIsRelative)
            {
                x -= mask.Rect.X;
                y -= mask.Rect.Y;
            }
            else
            {
                x = x + mask.Layer.Rect.X - mask.Rect.X;
                y = y + mask.Layer.Rect.Y - mask.Rect.Y;
            }

            if (y >= 0 && (y < mask.Rect.Height && x >= 0) && x < mask.Rect.Width)
            {
                int index = (y * mask.Rect.Width) + x;
                num = index >= mask.ImageData.Length ? byte.MaxValue : mask.ImageData[index];
            }

            return num;
        }

        /// <summary>
        /// Converts Lab color to RGB color.
        /// </summary>
        /// <param name="lb">The lb channel.</param>
        /// <param name="ab">The ab channel.</param>
        /// <param name="bb">The bb channel.</param>
        /// <returns>The RGB color.</returns>
        private static Color LabToRGB(byte lb, byte ab, byte bb)
        {
            double num1 = lb;
            double num2 = ab;
            double num3 = bb;
            double num4 = 2.56;
            double num5 = 1.0;
            double num6 = 1.0;
            int num7 = (int)(num1 / num4);
            int num8 = (int)((num2 / num5) - 128.0);
            int num9 = (int)((num3 / num6) - 128.0);
            double x1 = (num7 + 16.0) / 116.0;
            double x2 = (num8 / 500.0) + x1;
            double x3 = x1 - (num9 / 200.0);
            double num10 = Math.Pow(x1, 3.0) <= 0.008856 ? (x1 - 0.0) / 7.787 : Math.Pow(x1, 3.0);
            double num11 = Math.Pow(x2, 3.0) <= 0.008856 ? (x2 - 0.0) / 7.787 : Math.Pow(x2, 3.0);
            double num12 = Math.Pow(x3, 3.0) <= 0.008856 ? (x3 - 0.0) / 7.787 : Math.Pow(x3, 3.0);
            return XYZToRGB(95.047 * num11, 100.0 * num10, 108.883 * num12);
        }

        /// <summary>
        /// Converts XYZ color to RGB color.
        /// </summary>
        /// <param name="x">The x channel.</param>
        /// <param name="y">The y channel.</param>
        /// <param name="z">The z channel.</param>
        /// <returns>The RGB color.</returns>
        private static Color XYZToRGB(double x, double y, double z)
        {
            double num1 = x / 100.0;
            double num2 = y / 100.0;
            double num3 = z / 100.0;

            double x1 = (num1 * 3.2406) + (num2 * -1.5372) + (num3 * -0.4986);
            double x2 = (num1 * -0.9689) + (num2 * 1.8758) + (num3 * 0.0415);
            double x3 = (num1 * 0.0557) + (num2 * -0.204) + (num3 * 1.057);

            double num4 = x1 <= 0.0031308 ? 12.92 * x1 : (1.055 * Math.Pow(x1, 5.0 / 12.0)) - 0.055;
            double num5 = x2 <= 0.0031308 ? 12.92 * x2 : (1.055 * Math.Pow(x2, 5.0 / 12.0)) - 0.055;
            double num6 = x3 <= 0.0031308 ? 12.92 * x3 : (1.055 * Math.Pow(x3, 5.0 / 12.0)) - 0.055;

            int red = (int)(num4 * 256.0);
            int green = (int)(num5 * 256.0);
            int blue = (int)(num6 * 256.0);

            if (red < 0)
            {
                red = 0;
            }
            else if (red > byte.MaxValue)
            {
                red = byte.MaxValue;
            }

            if (green < 0)
            {
                green = 0;
            }
            else if (green > byte.MaxValue)
            {
                green = byte.MaxValue;
            }

            if (blue < 0)
            {
                blue = 0;
            }
            else if (blue > byte.MaxValue)
            {
                blue = byte.MaxValue;
            }

            return Color.FromArgb(red, green, blue);
        }

        /// <summary>
        /// Converts CMYK color to RGB color.
        /// </summary>
        /// <param name="c">The c channel.</param>
        /// <param name="m">The m channel.</param>
        /// <param name="y">The y channel.</param>
        /// <param name="k">The k channel.</param>
        /// <returns>The RGB color.</returns>
        private static Color CMYKToRGB(byte c, byte m, byte y, byte k)
        {
            double num1 = Math.Pow(2.0, 8.0);
            double num6 = 1.0 - (c / num1);
            double num7 = 1.0 - (m / num1);
            double num8 = 1.0 - (y / num1);
            double num9 = 1.0 - (k / num1);
            int red = (int)((1.0 - ((num6 * (1.0 - num9)) + num9)) * byte.MaxValue);
            int green = (int)((1.0 - ((num7 * (1.0 - num9)) + num9)) * byte.MaxValue);
            int blue = (int)((1.0 - ((num8 * (1.0 - num9)) + num9)) * byte.MaxValue);
            if (red < 0)
            {
                red = 0;
            }
            else if (red > byte.MaxValue)
            {
                red = byte.MaxValue;
            }

            if (green < 0)
            {
                green = 0;
            }
            else if (green > byte.MaxValue)
            {
                green = byte.MaxValue;
            }

            if (blue < 0)
            {
                blue = 0;
            }
            else if (blue > byte.MaxValue)
            {
                blue = byte.MaxValue;
            }

            return Color.FromArgb(red, green, blue);
        }

        /// <summary>
        /// Represents the color data of a pixel.
        /// </summary>
        private struct PixelData
        {
            /// <summary>
            /// The blue channel.
            /// </summary>
            public byte Blue;

            /// <summary>
            /// The green channel.
            /// </summary>
            public byte Green;

            /// <summary>
            /// The red channel.
            /// </summary>
            public byte Red;

            /// <summary>
            /// The alpha channel.
            /// </summary>
            public byte Alpha;
        }
    }
}
