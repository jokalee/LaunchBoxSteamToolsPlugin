﻿using CarbyneSteamContextWrapper;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace SteamTools
{
    public class SteamInstallGameMenuItem : IGameMenuItemPlugin
    {
        // is this the DLL that can cause some minor oddities?
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// List of Steam's word "Install" in differnt languages to try to find the install window
        /// </summary>
        private static readonly string[] InstallStrings = new string[] { "Install" };

        /// <summary>
        /// List of Steam's word "Install" in differnt languages to try to find the install window RTL
        /// </summary>
        private static readonly string[] InstallStringsEnd = new string[] { };

        public string Caption { get { return "Install with Steam"; } }

        private System.Drawing.Image _IconImage;
        public System.Drawing.Image IconImage { get { return _IconImage; } }

        public bool ShowInBigBox { get { return true; } }

        public bool ShowInLaunchBox { get { return true; } }

        public bool SupportsMultipleGames { get { return false; } }

        public SteamInstallGameMenuItem()
        {
            _IconImage = Resources.steam.ToBitmap();
        }

        public bool GetIsValidForGame(IGame selectedGame)
        {
            if(SteamToolsContext.IsSteamGame(selectedGame))
            {
                UInt64? GameIDNumber = SteamToolsContext.GetSteamGameID(selectedGame);
                if (GameIDNumber.HasValue)
                {
                    SteamContext context = SteamContext.GetInstance();
                    bool? l_IsInstalled = SteamToolsContext.IsInstalled(GameIDNumber.Value, selectedGame);
                    if (l_IsInstalled.HasValue && !l_IsInstalled.Value)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool GetIsValidForGames(IGame[] selectedGames)
        {
            throw new NotImplementedException();
        }

        public void OnSelected(IGame[] selectedGames)
        {
            throw new NotImplementedException();
        }

        public void OnSelected(IGame selectedGame)
        {
            UInt64? GameIDNumber = SteamToolsContext.GetSteamGameID(selectedGame);
            if (GameIDNumber.HasValue)
            {
                bool handled = false;

                if (PluginHelper.StateManager.IsBigBox)
                {
                    BigBoxLogic(ref handled, GameIDNumber.Value);
                }

                if (!handled)
                {
                    if(PluginHelper.StateManager.IsBigBox)
                    {
                        // Start BigPicture first?
                        InstallWithSteamDialog(GameIDNumber.Value);
                    }
                    else
                    {
                        // If we're in BigPicture this will just transparently install, we need to close BigPicture?
                        InstallWithSteamDialog(GameIDNumber.Value);
                    }
                }
            }
        }

        // If this function is called without Caliburn.Micro it will crash.  BigBox loads Caliburn.Micro
        // Encapsilating this logic into a function seems to keep .net happy
        // It must be some form of JIT assembly loading
        private void BigBoxLogic(ref bool handled, UInt64 GameIDNumber)
        {
            try
            {
                string[] libs = SteamToolsContext.GetGameLibraries();
                if (libs.Length > 0)
                {
                    // The ActiveView is just a small area of the UI sadly, but it has the plugin interface active
                    System.Windows.Controls.UserControl ActiveView = (System.Windows.Controls.UserControl)(Unbroken.LaunchBox.Wpf.BigBox.App.MainViewModel.ActiveView);
                    System.Windows.Controls.Panel ActiveViewContent = (System.Windows.Controls.Panel)(ActiveView.Content);

                    // The entire window view, sadly the plugin interface doesn't work here
                    //Unbroken.LaunchBox.Wpf.BigBox.Views.MainView MainView = (Unbroken.LaunchBox.Wpf.BigBox.Views.MainView)(Unbroken.LaunchBox.Wpf.BigBox.App.MainView);
                    Window MainView = Unbroken.LaunchBox.Wpf.BigBox.App.MainView;
                    System.Windows.Controls.Panel MainViewContent = (System.Windows.Controls.Panel)(MainView.Content);

                    GenericPluginProxyView InstallLocationSelectorProxy = new GenericPluginProxyView();
                    SelectSteamInstallLocationView InstallLocationSelector = new SelectSteamInstallLocationView();
                    InstallLocationSelectorProxy.Proxy = InstallLocationSelector;
                    InstallLocationSelector.SetLibraries(libs);
                    InstallLocationSelector.SetHeader("Select Steam Library");

                    ActiveViewContent.Children.Add(InstallLocationSelectorProxy);
                    MainViewContent.Children.Add(InstallLocationSelector);

                    // When the UI is hidden remove it and the proxy from the Views and let the GC delete them
                    InstallLocationSelector.IsVisibleChanged += (object sender, DependencyPropertyChangedEventArgs e) =>
                    {
                        if (InstallLocationSelector.Visibility == Visibility.Hidden)
                        {
                            if (InstallLocationSelector.Accepted)
                            {
                                int index = InstallLocationSelector.SelectedIndex();
                                if (index >= 0)
                                {
                                    try
                                    {
                                        SteamToolsContext.InstallGame(GameIDNumber, index);

                                        try
                                        {
                                            Unbroken.LaunchBox.Wpf.BigBox.ViewModels.TextGamesViewModel ActiveViewModel = Unbroken.LaunchBox.Wpf.BigBox.App.MainViewModel.ActiveViewModel as Unbroken.LaunchBox.Wpf.BigBox.ViewModels.TextGamesViewModel;
                                            if (ActiveViewModel != null)
                                            {
                                                ActiveViewModel.RefreshGame();
                                            }
                                            else
                                            {
                                                // show a popup telling the user they must maually reload the page
                                            }
                                        }
                                        catch(Exception ex5)
                                        {
                                            // show a popup telling the user they must maually reload the page
                                        }
                                    }
                                    catch
                                    {
                                        // in case of error, inform the user it failed and ask them if we should use the alternate method

                                    }
                                }
                            }
                            // I'm removing these from the end just in case they become orphans and get GCed mid way through
                            // Though I guess I could just store the important one in a local variable to play it safe
                            // And there is the wierd external scope stuff going on with this inline delegate too
                            ActiveViewContent.Children.Remove(InstallLocationSelectorProxy);
                            MainViewContent.Children.Remove(InstallLocationSelector);
                        }
                    };

                    InstallLocationSelector.Visibility = Visibility.Visible;

                    handled = true;
                }
            }
            catch { }
        }

        private void InstallWithSteamDialog(UInt64 GameIDNumber)
        {
            SteamToolsContext.InstallGame(GameIDNumber);
            try
            {
                Process steam = SteamContext.GetInstance().GetSteamProcess();
                steam.Refresh();
                string InstallWindowTitle = steam.MainWindowTitle;
                IntPtr InstallWindow = steam.MainWindowHandle;

                // try to get the install window
                int Timeout = 0;
                while ( !InstallStrings.Any(name => InstallWindowTitle.StartsWith(name + @" - "))
                     && !InstallStringsEnd.Any(name => InstallWindowTitle.EndsWith(@" - " + name)))
                {
                    if (Timeout > 10 * 5)
                        break;
                    Thread.Sleep(100);
                    Timeout++;

                    steam.Refresh();
                    InstallWindowTitle = steam.MainWindowTitle;
                    InstallWindow = steam.MainWindowHandle;
                }

                // if we found the install window
                if ( InstallStrings.Any(name => InstallWindowTitle.StartsWith(name + @" - "))
                  || InstallStringsEnd.Any(name => InstallWindowTitle.EndsWith(@" - "  + name)))
                {
                    while (IsWindow(InstallWindow))
                    {
                        Thread.Sleep(100);
                    }

                    SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);
                }
            }
            catch { }
        }
    }
}
