using System;
using System.Collections.Generic;
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
        
        // ENHANCED: Cache different icons for different states
        private readonly Dictionary<TrayIconState, Icon?> _stateIcons = new();

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
        
        /// <summary>
        /// ENHANCED: Get icon for specific tray state with visual indicators
        /// </summary>
        public Icon GetIconForState(TrayIconState state)
        {
            // Check cache first
            if (_stateIcons.ContainsKey(state) && _stateIcons[state] != null)
            {
                return _stateIcons[state]!;
            }
            
            // Create icon based on state
            Icon icon = state switch
            {
                TrayIconState.Active => CreateActiveIcon(),
                TrayIconState.Paused => CreatePausedIcon(),
                TrayIconState.SmartPaused => CreateSmartPausedIcon(),
                TrayIconState.ManuallyPaused => CreateManuallyPausedIcon(), // NEW: Manual meeting pause
                TrayIconState.MeetingMode => CreateMeetingIcon(),
                TrayIconState.UserAway => CreateAwayIcon(),
                TrayIconState.Break => CreateBreakIcon(),
                TrayIconState.EyeRest => CreateEyeRestIcon(),
                TrayIconState.Error => CreateErrorIcon(),
                _ => GetApplicationIcon()
            };
            
            // Cache the icon
            _stateIcons[state] = icon;
            
            return icon;
        }
        
        /// <summary>
        /// Create icon for active/running state - Green eye
        /// </summary>
        private Icon CreateActiveIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with green color for active
            using var eyeBrush = new SolidBrush(Color.FromArgb(76, 175, 80)); // Green
            using var eyePen = new Pen(Color.FromArgb(56, 142, 60), 2); // Dark green
            
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

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for paused state - Yellow eye with pause symbol
        /// </summary>
        private Icon CreatePausedIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with yellow/amber color for paused
            using var eyeBrush = new SolidBrush(Color.FromArgb(255, 193, 7)); // Amber
            using var eyePen = new Pen(Color.FromArgb(255, 160, 0), 2); // Dark amber
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw pause bars instead of pupil
            using var pauseBrush = new SolidBrush(Color.FromArgb(33, 33, 33));
            graphics.FillRectangle(pauseBrush, new Rectangle(12, 15, 3, 6));
            graphics.FillRectangle(pauseBrush, new Rectangle(17, 15, 3, 6));

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for smart paused state - Orange eye with AI symbol
        /// </summary>
        private Icon CreateSmartPausedIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with orange color for smart paused
            using var eyeBrush = new SolidBrush(Color.FromArgb(255, 152, 0)); // Orange
            using var eyePen = new Pen(Color.FromArgb(230, 81, 0), 2); // Dark orange
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw small "AI" or smart indicator
            using var font = new Font("Arial", 6, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            graphics.DrawString("AI", font, textBrush, new PointF(12, 15));

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for manually paused state - Yellow-orange eye with meeting timer symbol
        /// </summary>
        private Icon CreateManuallyPausedIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with yellow-orange color for manual meeting pause
            using var eyeBrush = new SolidBrush(Color.FromArgb(255, 183, 77)); // Yellow-orange
            using var eyePen = new Pen(Color.FromArgb(255, 143, 0), 2); // Darker yellow-orange
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw meeting timer symbol (clock + meeting icon)
            using var symbolBrush = new SolidBrush(Color.FromArgb(33, 33, 33));
            
            // Draw small clock circle
            graphics.DrawEllipse(new Pen(symbolBrush, 1), new Rectangle(12, 15, 6, 6));
            
            // Draw clock hands pointing to "30" (representing 30-minute pause)
            using var handPen = new Pen(symbolBrush, 1);
            graphics.DrawLine(handPen, new Point(15, 18), new Point(15, 16)); // 12 o'clock (short hand)
            graphics.DrawLine(handPen, new Point(15, 18), new Point(17, 20)); // 6 o'clock (long hand at 30 min)
            
            // Small meeting indicator dot
            graphics.FillEllipse(symbolBrush, new Rectangle(18, 15, 2, 2));

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for meeting mode - Purple eye with video camera symbol
        /// </summary>
        private Icon CreateMeetingIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with purple color for meeting
            using var eyeBrush = new SolidBrush(Color.FromArgb(156, 39, 176)); // Purple
            using var eyePen = new Pen(Color.FromArgb(123, 31, 162), 2); // Dark purple
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw video camera icon
            using var cameraBrush = new SolidBrush(Color.White);
            graphics.FillRectangle(cameraBrush, new Rectangle(11, 16, 6, 4));
            graphics.FillPolygon(cameraBrush, new Point[] {
                new Point(17, 17),
                new Point(20, 15),
                new Point(20, 21),
                new Point(17, 19)
            });

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for user away state - Gray eye (closed/sleeping)
        /// </summary>
        private Icon CreateAwayIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw closed eye (line) with gray color for away
            using var eyePen = new Pen(Color.FromArgb(158, 158, 158), 3); // Gray
            graphics.DrawLine(eyePen, new Point(4, 18), new Point(28, 18));
            
            // Draw "Z" symbols for sleeping
            using var font = new Font("Arial", 8, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(117, 117, 117));
            graphics.DrawString("Z", font, textBrush, new PointF(20, 5));
            using var smallFont = new Font("Arial", 6, FontStyle.Bold);
            graphics.DrawString("z", smallFont, textBrush, new PointF(26, 8));

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for break state - Blue eye with clock
        /// </summary>
        private Icon CreateBreakIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with blue color
            using var eyeBrush = new SolidBrush(Color.FromArgb(33, 150, 243)); // Blue
            using var eyePen = new Pen(Color.FromArgb(21, 101, 192), 2);
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw clock hands
            using var clockPen = new Pen(Color.White, 2);
            graphics.DrawLine(clockPen, new Point(16, 18), new Point(16, 14)); // 12 o'clock
            graphics.DrawLine(clockPen, new Point(16, 18), new Point(19, 18)); // 3 o'clock

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for eye rest state - Cyan eye blinking
        /// </summary>
        private Icon CreateEyeRestIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw half-closed eye with cyan color
            using var eyeBrush = new SolidBrush(Color.FromArgb(0, 188, 212)); // Cyan
            using var eyePen = new Pen(Color.FromArgb(0, 151, 167), 2);
            
            var eyeRect = new Rectangle(4, 14, 24, 8); // Narrower for half-closed effect
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            return CreateIconFromBitmap(bitmap);
        }
        
        /// <summary>
        /// Create icon for error state - Red eye with X
        /// </summary>
        private Icon CreateErrorIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Draw eye shape with red color for error
            using var eyeBrush = new SolidBrush(Color.FromArgb(244, 67, 54)); // Red
            using var eyePen = new Pen(Color.FromArgb(211, 47, 47), 2); // Dark red
            
            var eyeRect = new Rectangle(4, 12, 24, 12);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw X mark
            using var xPen = new Pen(Color.White, 2);
            graphics.DrawLine(xPen, new Point(12, 15), new Point(20, 21));
            graphics.DrawLine(xPen, new Point(20, 15), new Point(12, 21));

            return CreateIconFromBitmap(bitmap);
        }

        public void Dispose()
        {
            _cachedIcon?.Dispose();
            _cachedIcon = null;
            
            // ENHANCED: Dispose all cached state icons
            foreach (var icon in _stateIcons.Values)
            {
                icon?.Dispose();
            }
            _stateIcons.Clear();
        }
    }
}