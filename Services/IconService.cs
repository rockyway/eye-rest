using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class IconService
    {
        private readonly ILogger<IconService> _logger;
        private Icon? _cachedIcon;

        public IconService(ILogger<IconService> logger)
        {
            _logger = logger;
        }

        public Icon GetApplicationIcon()
        {
            if (_cachedIcon != null)
                return _cachedIcon;

            try
            {
                // Try to load custom icon first
                var customIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(customIconPath))
                {
                    // Check if it's a valid icon file by trying to load it
                    using var fileStream = new FileStream(customIconPath, FileMode.Open, FileAccess.Read);
                    var firstBytes = new byte[4];
                    fileStream.Read(firstBytes, 0, 4);
                    
                    // ICO files start with 0x00 0x00 0x01 0x00
                    if (firstBytes[0] == 0x00 && firstBytes[1] == 0x00 && 
                        firstBytes[2] == 0x01 && firstBytes[3] == 0x00)
                    {
                        fileStream.Seek(0, SeekOrigin.Begin);
                        _cachedIcon = new Icon(fileStream);
                        _logger.LogInformation("Loaded custom application icon.");
                        return _cachedIcon;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load custom icon, generating default icon.");
            }

            // Generate a simple eye-themed icon
            _cachedIcon = CreateEyeIcon();
            _logger.LogInformation("Generated default eye-themed icon.");
            return _cachedIcon;
        }

        private Icon CreateEyeIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape (ellipse)
            using var eyeBrush = new SolidBrush(Color.FromArgb(33, 150, 243)); // Blue color
            using var eyePen = new Pen(Color.FromArgb(21, 101, 192), 2);
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw pupil
            using var pupilBrush = new SolidBrush(Color.FromArgb(33, 33, 33));
            var pupilRect = new Rectangle(14, 16, 4, 4);
            graphics.FillEllipse(pupilBrush, pupilRect);

            // Draw highlight
            using var highlightBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(highlightBrush, new Rectangle(15, 17, 2, 2));

            // Convert bitmap to icon
            var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Seek(0, SeekOrigin.Begin);

            // Create ICO format manually for better compatibility
            return CreateIconFromBitmap(bitmap);
        }

        private Icon CreateIconFromBitmap(Bitmap bitmap)
        {
            var memoryStream = new MemoryStream();
            
            // ICO header
            memoryStream.Write(new byte[] { 0, 0, 1, 0, 1, 0 }, 0, 6); // Header + 1 image
            
            // Image directory entry
            memoryStream.WriteByte(32); // Width
            memoryStream.WriteByte(32); // Height
            memoryStream.WriteByte(0);  // Color count
            memoryStream.WriteByte(0);  // Reserved
            memoryStream.Write(BitConverter.GetBytes((short)1), 0, 2); // Color planes
            memoryStream.Write(BitConverter.GetBytes((short)32), 0, 2); // Bits per pixel
            
            // Save bitmap to get actual size
            var pngStream = new MemoryStream();
            bitmap.Save(pngStream, ImageFormat.Png);
            var pngData = pngStream.ToArray();
            
            memoryStream.Write(BitConverter.GetBytes(pngData.Length), 0, 4); // Image size
            memoryStream.Write(BitConverter.GetBytes(22), 0, 4); // Image offset
            
            // Write PNG data
            memoryStream.Write(pngData, 0, pngData.Length);
            
            memoryStream.Seek(0, SeekOrigin.Begin);
            return new Icon(memoryStream);
        }

        public void Dispose()
        {
            _cachedIcon?.Dispose();
            _cachedIcon = null;
        }
    }
}