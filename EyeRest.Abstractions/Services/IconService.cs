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

        // Cache different icons for different states
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
                        _logger.LogInformation("Loaded custom application icon from Resources/app.ico");
                        return _cachedIcon;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load custom icon, generating default icon.");
            }

            // Generate a simple eye-themed icon as fallback
            _cachedIcon = CreateModernEyeIcon(Color.FromArgb(76, 175, 80), Color.FromArgb(56, 142, 60));
            _logger.LogInformation("Generated fallback eye-themed icon.");
            return _cachedIcon;
        }

        /// <summary>
        /// Get icon for specific tray state with visual color indicators
        /// Uses the same modern eye design with different colors per state
        /// </summary>
        public Icon GetIconForState(TrayIconState state)
        {
            // Check cache first
            if (_stateIcons.TryGetValue(state, out var cachedIcon) && cachedIcon != null)
            {
                return cachedIcon;
            }

            // Create icon based on state with appropriate colors
            Icon icon = state switch
            {
                TrayIconState.Active => CreateModernEyeIcon(
                    Color.FromArgb(76, 175, 80),    // Green - active/running
                    Color.FromArgb(56, 142, 60)),
                TrayIconState.Paused => CreateModernEyeIcon(
                    Color.FromArgb(255, 193, 7),   // Amber - paused
                    Color.FromArgb(255, 160, 0)),
                TrayIconState.SmartPaused => CreateModernEyeIcon(
                    Color.FromArgb(255, 152, 0),   // Orange - smart paused
                    Color.FromArgb(230, 81, 0)),
                TrayIconState.ManuallyPaused => CreateModernEyeIcon(
                    Color.FromArgb(255, 183, 77),  // Yellow-orange - manual meeting pause
                    Color.FromArgb(255, 143, 0)),
                TrayIconState.MeetingMode => CreateModernEyeIcon(
                    Color.FromArgb(156, 39, 176),  // Purple - meeting mode
                    Color.FromArgb(123, 31, 162)),
                TrayIconState.UserAway => CreateModernEyeIcon(
                    Color.FromArgb(158, 158, 158), // Gray - user away
                    Color.FromArgb(117, 117, 117)),
                TrayIconState.Break => CreateModernEyeIcon(
                    Color.FromArgb(33, 150, 243),  // Blue - break time
                    Color.FromArgb(21, 101, 192)),
                TrayIconState.EyeRest => CreateModernEyeIcon(
                    Color.FromArgb(0, 188, 212),   // Cyan - eye rest
                    Color.FromArgb(0, 151, 167)),
                TrayIconState.Error => CreateModernEyeIcon(
                    Color.FromArgb(244, 67, 54),   // Red - error
                    Color.FromArgb(211, 47, 47)),
                _ => GetApplicationIcon()
            };

            // Cache the icon
            _stateIcons[state] = icon;

            return icon;
        }

        /// <summary>
        /// Creates a modern eye icon matching the app.ico design style
        /// with configurable colors for different states
        /// </summary>
        private Icon CreateModernEyeIcon(Color fillColor, Color borderColor)
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            // Draw outer eye shape - rounded/circular style matching new design
            using var eyeBrush = new SolidBrush(fillColor);
            using var eyePen = new Pen(borderColor, 2);

            // Main eye circle (more circular than ellipse for modern look)
            var eyeRect = new Rectangle(4, 6, 24, 20);
            graphics.FillEllipse(eyeBrush, eyeRect);
            graphics.DrawEllipse(eyePen, eyeRect);

            // Draw white eye background (inner part)
            using var whiteBrush = new SolidBrush(Color.White);
            var innerRect = new Rectangle(8, 10, 16, 12);
            graphics.FillEllipse(whiteBrush, innerRect);

            // Draw pupil (dark center)
            using var pupilBrush = new SolidBrush(Color.FromArgb(33, 33, 33));
            var pupilRect = new Rectangle(12, 12, 8, 8);
            graphics.FillEllipse(pupilBrush, pupilRect);

            // Draw highlight reflection
            using var highlightBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(highlightBrush, new Rectangle(13, 13, 3, 3));

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

            // Dispose all cached state icons
            foreach (var icon in _stateIcons.Values)
            {
                icon?.Dispose();
            }
            _stateIcons.Clear();
        }
    }
}
