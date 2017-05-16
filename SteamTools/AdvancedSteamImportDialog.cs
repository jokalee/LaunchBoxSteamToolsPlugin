﻿using SteamLaunchable = CarbyneSteamContext.SteamLaunchable;
using SteamLaunchableApp = CarbyneSteamContext.SteamLaunchableApp;
using SteamLaunchableModGoldSrc = CarbyneSteamContext.SteamLaunchableModGoldSrc;
using SteamLaunchableModSource = CarbyneSteamContext.SteamLaunchableModSource;
using CarbyneSteamContextWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins.Data;
using System.IO;
using Unbroken.LaunchBox.Plugins;

namespace SteamTools
{
    public partial class AdvancedSteamImportDialog : Form
    {
        private SteamContext context;
        List<SteamLaunchable> SteamApps;
        //List<SteamLaunchableListViewitem> ListItems;

        //private int clearImageIndex = 0;
        //private Dictionary<string, int> ImageIndexMap = new Dictionary<string, int>();
        private Dictionary<SteamLaunchable, bool> CheckedItems = new Dictionary<SteamLaunchable, bool>();
        private HashSet<UInt64> IsInstalled = new HashSet<UInt64>();
        private HashSet<UInt64> KnownSteamGames = new HashSet<UInt64>();
        private List<Tuple<int, bool>> Sorts = new List<Tuple<int, bool>>();

        public AdvancedSteamImportDialog()
        {
            SteamApps = new List<SteamLaunchable>();
            //ListItems = new List<SteamLaunchableListViewitem>();
            context = SteamContext.GetInstance();
            InitializeComponent();
            this.Icon = ((Icon)(new ComponentResourceManager(typeof(Resources)).GetObject("steam")));
            lvGames.SmallImageList = new ImageList();

            SetPlatforms(PluginHelper.DataManager.GetAllPlatforms());
        }

        private void ScanLocalGames()
        {
            List<IGame> games = PluginHelper.DataManager.GetAllGames().ToList();
            pbScanLaunchBox.Maximum = games.Count;
            int index = 0;
            games.ForEach(game =>
            {
                index++;
                pbScanLaunchBox.Value = index;
                if (game.ApplicationPath?.StartsWith("steam://") ?? false)
                {
                    string GameID = game.ApplicationPath.Split('/').Last();
                    UInt64 GameIDNumber = 0;
                    if (UInt64.TryParse(GameID, out GameIDNumber))
                    {
                        if (!IsInstalled.Contains(GameIDNumber) && context.IsInstalled(GameIDNumber))
                        {
                            IsInstalled.Add(GameIDNumber);
                        }

                        if (!KnownSteamGames.Contains(GameIDNumber))
                        {
                            KnownSteamGames.Add(GameIDNumber);
                        }
                    }
                }
            });
        }

        private void btnScanLaunchBox_Click(object sender, EventArgs e)
        {
            pbScanLaunchBox.Visible = true;
            btnScanLaunchBox.Visible = true;
            ScanLocalGames();
            btnScan.Enabled = true;
        }

        private void SetPlatforms(IPlatform[] platforms)
        {
            cbPlatforms.BeginUpdate();
            cbPlatforms.Items.Clear();
            foreach (IPlatform plat in platforms)
            {
                cbPlatforms.Items.Add(plat.Name);
            }
            cbPlatforms.SelectedItem = "Steam";
            if(cbPlatforms.SelectedIndex == -1)
                cbPlatforms.SelectedItem = "Windows";
            if (cbPlatforms.SelectedIndex == -1)
                cbPlatforms.SelectedIndex = 0;
            cbPlatforms.EndUpdate();
        }

        private void ScanSteamGames()
        {
            lvGames.BeginUpdate();
            SteamApps.Clear();
            lvGames.SmallImageList.Images.Clear();
            CheckedItems.Clear();
            //clearImageIndex = 0;
            //ImageIndexMap.Clear();
            //ListItems.Clear();
            List<SteamLaunchableApp> apps = context.GetOwnedApps().Where(dr => new[] { "game", "demo", "application" }.Contains(dr.appType)).ToList();
            List<SteamLaunchableModGoldSrc> gmods = context.GetGoldSrcMods();
            List<SteamLaunchableModSource> smods = context.GetSourceMods();
            SteamApps.AddRange(apps);
            SteamApps.AddRange(gmods);
            SteamApps.AddRange(smods);

            /*foreach(SteamLaunchable app in SteamApps)
            {
                ListItems.Add(new SteamLaunchableListViewitem(app));
            }*/

            //lvGames.VirtualListSize = ListItems.Count;
            lvGames.VirtualListSize = SteamApps.Count;
            lvGames.EndUpdate();

            SortList();
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            btnImport.Enabled = false;
            btnScan.Enabled = false;
            ScanSteamGames();
            lvGames.Enabled = true;
            btnScan.Enabled = true;
            btnImport.Enabled = true;
        }

        private void lvGames_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            SteamLaunchable item = SteamApps[e.ItemIndex];
            if (e.Item == null) e.Item = new ListViewItem(item.Title);
            UInt64 gameid = item.GetShortcutID();
            e.Item.SubItems.Add(gameid.ToString());
            e.Item.SubItems.Add(item.AppType);
            if (IsInstalled.Contains(gameid))
            {
                e.Item.SubItems.Add("Y");
            }
            else
            {
                e.Item.SubItems.Add("N");
            }
            if (KnownSteamGames.Contains(gameid))
            {
                e.Item.SubItems.Add("Y");
            }
            else
            {
                e.Item.SubItems.Add("N");
            }
            if (!string.IsNullOrEmpty(item.Icon) && File.Exists(item.Icon))
            {
                if (!lvGames.SmallImageList.Images.ContainsKey(item.Icon))
                {
                    lvGames.SmallImageList.Images.Add(item.Icon, Icon.ExtractAssociatedIcon(item.Icon));
                }
                e.Item.ImageKey = item.Icon;
            }
            else
            {
                e.Item.ImageKey = "default";
            }

            
        }

        private void SortList()
        {
            lvGames.BeginUpdate();
            IQueryable<SteamLaunchable> query = SteamApps.AsQueryable();
            IOrderedQueryable<SteamLaunchable> query2 = null;

            for (int x = Sorts.Count - 1; x >= 0; x--)
            {
                var sorting = Sorts[x];
                if (x == Sorts.Count - 1)
                {
                    switch (sorting.Item1)
                    {
                        case 1:
                            query2 = !sorting.Item2 ? query.OrderBy(dr => dr.GetShortcutID()) : query.OrderByDescending(dr => dr.GetShortcutID());
                            break;
                        case 2:
                            query2 = !sorting.Item2 ? query.OrderBy(dr => dr.AppType) : query.OrderByDescending(dr => dr.AppType);
                            break;
                        case 3:
                            query2 = !sorting.Item2 ? query.OrderBy(dr => IsInstalled.Contains(dr.GetShortcutID()) ? 0 : 1) : query.OrderByDescending(dr => IsInstalled.Contains(dr.GetShortcutID()) ? 0 : 1);
                            break;
                        case 4:
                            query2 = !sorting.Item2 ? query.OrderBy(dr => KnownSteamGames.Contains(dr.GetShortcutID()) ? 0 : 1) : query.OrderByDescending(dr => KnownSteamGames.Contains(dr.GetShortcutID()) ? 0 : 1);
                            break;
                        case 0:
                        default:
                            query2 = !sorting.Item2 ? query.OrderBy(dr => dr.Title) : query.OrderByDescending(dr => dr.Title);
                            break;
                    }
                }
                else
                {
                    switch (sorting.Item1)
                    {
                        case 1:
                            query2 = !sorting.Item2 ? query2.ThenBy(dr => dr.GetShortcutID()) : query2.ThenByDescending(dr => dr.GetShortcutID());
                            break;
                        case 2:
                            query2 = !sorting.Item2 ? query2.ThenBy(dr => dr.AppType) : query2.ThenByDescending(dr => dr.AppType);
                            break;
                        case 3:
                            query2 = !sorting.Item2 ? query2.ThenBy(dr => IsInstalled.Contains(dr.GetShortcutID()) ? 0 : 1) : query2.ThenByDescending(dr => IsInstalled.Contains(dr.GetShortcutID()) ? 0 : 1);
                            break;
                        case 4:
                            query2 = !sorting.Item2 ? query2.ThenBy(dr => KnownSteamGames.Contains(dr.GetShortcutID()) ? 0 : 1) : query2.ThenByDescending(dr => KnownSteamGames.Contains(dr.GetShortcutID()) ? 0 : 1);
                            break;
                        case 0:
                        default:
                            query2 = !sorting.Item2 ? query2.ThenBy(dr => dr.Title) : query2.ThenByDescending(dr => dr.Title);
                            break;
                    }
                }
            }

            SteamApps = (query2 ?? query).ToList();
            lvGames.EndUpdate();
        }

        private void lvGames_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if(Sorts.Count > 0)
            {
                // last applied sort is same column
                if (Sorts.Last().Item1 == e.Column)
                {
                    Tuple<int, bool> LastSort = Sorts.Last();
                    Sorts.Remove(Sorts.Last());
                    Sorts.Add(new Tuple<int, bool>(LastSort.Item1, !LastSort.Item2));
                    SortList();
                    return;
                }
                // remove any items earlier in the list that are the same column
                Sorts.RemoveAll(dr => dr.Item1 == e.Column);
            }

            Sorts.Add(new Tuple<int, bool>(e.Column, false));
            SortList();
        }

        private void lvGames_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
            if (lvGames.SmallImageList.Images.ContainsKey(e.Item.ImageKey))
            {
                Rectangle rect = e.Bounds;
                rect.Width = 16;
                rect.Height = 16;
                rect.X += 16;
                e.Graphics.DrawImage(lvGames.SmallImageList.Images[e.Item.ImageKey], rect);
            }
            /*if (!e.Item.Checked)
            {
                e.Item.Checked = true;
                e.Item.Checked = false;
            }*/
            {
                Rectangle rect = e.Bounds;
                rect.Width = 10;
                rect.Height = 10;
                rect.X += 2;
                rect.Y += 2;
                e.Graphics.DrawRectangle(Pens.Black, rect);
            }
            SteamLaunchable app = SteamApps[e.ItemIndex];
            if (CheckedItems.ContainsKey(app) && CheckedItems[app])
            {
                Rectangle rect = e.Bounds;
                rect.Width = 7;
                rect.Height = 7;
                rect.X += 4;
                rect.Y += 4;
                e.Graphics.FillRectangle(Brushes.Black, rect);
            }
        }

        private void lvGames_MouseClick(object sender, MouseEventArgs e)
        {
            ListView lv = (ListView)sender;
            ListViewItem lvi = lv.GetItemAt(e.X, e.Y);
            if (lvi != null)
            {
                if (e.X < (lvi.Bounds.Left + 16))
                {
                    if (lv.SelectedIndices.Contains(lvi.Index))
                    {
                        SteamLaunchable app = SteamApps[lvi.Index];
                        if (!CheckedItems.ContainsKey(app)) CheckedItems[app] = false;
                        CheckedItems[app] = !CheckedItems[app];
                        if (lv.SelectedIndices.Count > 0)
                        {
                            foreach (int index in lv.SelectedIndices)
                            {
                                SteamLaunchable appX = SteamApps[index];
                                if (!CheckedItems.ContainsKey(appX)) CheckedItems[appX] = false;
                                CheckedItems[appX] = CheckedItems[app];
                            }
                            lv.Invalidate();
                        }
                    }
                    else
                    {
                        SteamLaunchable app = SteamApps[lvi.Index];
                        if (!CheckedItems.ContainsKey(app)) CheckedItems[app] = false;
                        CheckedItems[app] = !CheckedItems[app];
                        lv.Invalidate(lvi.Bounds);
                    }
                }
            }
        }

        private void lvGames_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListView lv = (ListView)sender;
            ListViewItem lvi = lv.GetItemAt(e.X, e.Y);
            if (lvi != null)
                lv.Invalidate(lvi.Bounds);
        }

        private void lvGames_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void lvGames_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }
    }

    /*internal class SteamLaunchableListViewitem : ListViewItem
    {
        private SteamLaunchable app;

        public SteamLaunchableListViewitem(SteamLaunchable app)
        {
            this.app = app;
        }

        public override string ToString()
        {
            return app.Title;
        }
    }*/
}
