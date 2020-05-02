using Dexcom.Fetch.Enums;
using Dexcom.Fetch.Extensions;
using Dexcom.Fetch.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GlucoseTrayCore.Services
{
    public class IconService
    {
        private readonly ILogger _logger;
        private readonly float _standardOffset = -3f;
        private readonly int _defaultFontSize = 10;
        private readonly int _smallerFontSize = 9;
        private Font _fontToUse;
        private bool _useDefaultFontSize = true;

        public IconService(ILogger logger) => _logger = logger;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public void DestroyMyIcon(IntPtr handle) => DestroyIcon(handle);

        internal void CreateTextIcon(GlucoseFetchResult fetchResult, bool isCriticalLow, NotifyIcon trayIcon)
        {
            var result = fetchResult.GetFormattedStringValue().Replace('.', '\''); // Use ' instead of . since it is narrower and allows a better display of a two digit number + decimal place.

            if (result == "0")
            {
                _logger.LogWarning("Empty glucose result received.");
                result = "ERR";
            }
            else if (isCriticalLow)
            {
                _logger.LogInformation("Critical low glucose read.");
                result = "LOW";
            }

            var xOffset = CalculateXPosition(fetchResult);
            var fontSize = _useDefaultFontSize ? _defaultFontSize : _smallerFontSize;
            _fontToUse = new Font("Roboto", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);

            var bitmapText = new Bitmap(16, 16);
            var g = Graphics.FromImage(bitmapText);
            
            
            if (fetchResult.Value <= Constants.DangerLowBg || fetchResult.Value >= Constants.DangerHighBg)
            {
                g.Clear(Color.OrangeRed);
                var pen = new Pen(new SolidBrush(Color.Black));
                g.DrawLine(pen, 0, 2, 16, 2);
                g.DrawLine(pen, 0, 13, 16, 13);
                g.DrawString(result, _fontToUse, new SolidBrush(Color.White), xOffset, 1f);
            }
            else if (fetchResult.Value <= Constants.LowBg || fetchResult.Value >= Constants.HighBg)
            {
                // If glucose is outside of target range, then add a top and bottom line on the icon to add emphasis.
                var pen = new Pen(new SolidBrush(Color.Yellow));
                g.DrawLine(pen, 0, 0, 16, 0);
                g.DrawLine(pen, 0, 15, 16, 15);
                g.DrawString(result, _fontToUse, new SolidBrush(Color.Yellow), xOffset, 1f);
            }
            else // in normal range
            {
                g.Clear(Color.Transparent);
                g.DrawString(result, _fontToUse, new SolidBrush(Color.LimeGreen), xOffset, 1f);


            }
            

            
            var hIcon = bitmapText.GetHicon();
            var myIcon = Icon.FromHandle(hIcon);
            trayIcon.Icon = myIcon;

            DestroyMyIcon(myIcon.Handle);
            bitmapText.Dispose();
            g.Dispose();
            myIcon.Dispose();
        }

        private float CalculateXPosition(GlucoseFetchResult result)
        {
            _useDefaultFontSize = true;
            var value = result.Value;
            if (result.UnitDisplayType == GlucoseUnitType.MG) // Non MMOL display, use our standard offset.
                return _standardOffset;
            if (value > 9.9) // MMOL with 3 digits over 20. This requires also changing the font size from 10 to 9.
            {
                _useDefaultFontSize = false;
                return _standardOffset;
            }
            return _standardOffset; // MMOL display with only two digits, use our standard offset.
        }
    }
}