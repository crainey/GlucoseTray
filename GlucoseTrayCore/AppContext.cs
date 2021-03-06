﻿using Dexcom.Fetch;
using Dexcom.Fetch.Extensions;
using Dexcom.Fetch.Models;
using GlucoseTrayCore.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlucoseTrayCore
{
    public class AppContext : ApplicationContext
    {
        private readonly ILogger _logger;
        private readonly NotifyIcon trayIcon;
        private bool IsCriticalLow;

        private GlucoseFetchResult FetchResult;
        private GlucoseFetchResult PreviousFetchResult;
        private readonly IconService _iconService;

        public AppContext(ILogger logger)
        {
            _logger = logger;
            _iconService = new IconService(_logger);

            trayIcon = new NotifyIcon()
            {
                ContextMenuStrip = new ContextMenuStrip(new Container()),
                Visible = true
            };

            if (!string.IsNullOrWhiteSpace(Constants.NightscoutUrl))
            {
                _logger.LogDebug("Nightscout url supplied, adding option to context menu.");

                var process = new Process();
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.FileName = Constants.NightscoutUrl;

                trayIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Nightscout", null, (obj, e) => process.Start()));
            }
            trayIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem(nameof(Exit), null, new EventHandler(Exit)));
            trayIcon.DoubleClick += ShowBalloon;
            BeginCycle();
        }

        private async void BeginCycle()
        {
            while (true)
            {
                try
                {
                    Application.DoEvents();
                    await CreateIcon().ConfigureAwait(false);
                    await Task.Delay(Constants.PollingThreshold);
                }
                catch (Exception e)
                {
                    if (Constants.EnableDebugMode)
                        MessageBox.Show($"ERROR: {e}", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _logger.LogError(e.ToString());
                    trayIcon.Visible = false;
                    trayIcon?.Dispose();
                    Environment.Exit(0);
                }
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            _logger.LogInformation("Exiting application.");
            trayIcon.Visible = false;
            trayIcon?.Dispose();
            Application.ExitThread();
            Application.Exit();
        }

        private async Task CreateIcon()
        {
            IsCriticalLow = false;
            var service = new GlucoseFetchService(new GlucoseFetchConfiguration
            {
                DexcomUsername = Constants.DexcomUsername,
                DexcomPassword = Constants.DexcomPassword,
                FetchMethod = Constants.FetchMethod,
                NightscoutUrl = Constants.NightscoutUrl,
                NightscoutAccessToken = Constants.AccessToken,
                UnitDisplayType = Constants.GlucoseUnitType
            }, _logger);
            
            PreviousFetchResult = FetchResult;
            FetchResult = await service.GetLatestReading().ConfigureAwait(false);
            trayIcon.Text = GetGlucoseMessage();
            if (FetchResult.Value <= Constants.CriticalLowBg)
                IsCriticalLow = true;
            _iconService.CreateTextIcon(FetchResult, IsCriticalLow, trayIcon);
            
            if (PreviousFetchResult != null 
                && PreviousFetchResult.TrendIcon != "⮅" && PreviousFetchResult.TrendIcon != "⮇"
                && (FetchResult.TrendIcon == "⮅" || FetchResult.TrendIcon == "⮇"))
            {
                // Show balloon when the trend changes to double arrow
                trayIcon.ShowBalloonTip(3000, "Glucose", $"{FetchResult.TrendIcon} {FetchResult.Value}", ToolTipIcon.Warning);
            }

            if (PreviousFetchResult != null
                && PreviousFetchResult.Value < Constants.HighBg && FetchResult.Value >= Constants.HighBg)
            {
                // Show balloon when it crosses the HighBG threshold
                trayIcon.ShowBalloonTip(3000, "Glucose", $"{FetchResult.TrendIcon} {FetchResult.Value}", ToolTipIcon.Warning);
            }
            
            if (PreviousFetchResult != null
                && PreviousFetchResult.Value > Constants.LowBg && FetchResult.Value <= Constants.LowBg)
            {
                // Show balloon when it crosses the LowBG threshold
                trayIcon.ShowBalloonTip(3000, "Glucose", $"{FetchResult.TrendIcon} {FetchResult.Value}", ToolTipIcon.Warning);
            }
        }

        private void ShowBalloon(object sender, EventArgs e)
        {
            var tooltipIcon = ToolTipIcon.Info;
            if (FetchResult.Value >= Constants.DangerHighBg || FetchResult.Value <= Constants.DangerLowBg)
                tooltipIcon = ToolTipIcon.Error;
            else if (FetchResult.Value >= Constants.HighBg || FetchResult.Value <= Constants.LowBg)
                tooltipIcon = ToolTipIcon.Warning;

            trayIcon.ShowBalloonTip(2000, "Glucose", GetGlucoseMessage(), tooltipIcon);
        }

        private string GetGlucoseMessage() => $"{FetchResult.TrendIcon}{FetchResult.GetFormattedStringValue()}\n{FetchResult.Time.ToLongTimeString()}";
    }
}