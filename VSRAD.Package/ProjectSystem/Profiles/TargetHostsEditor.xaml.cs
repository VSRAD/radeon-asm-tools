﻿using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed partial class TargetHostsEditor : DialogWindow
    {
        public sealed class HostItem : DefaultNotifyPropertyChanged
        {
            private string _host = "";
            public string Host { get => _host; set => SetField(ref _host, value); }

            public bool UsedInActiveProfile { get; }

            public HostItem(string host, bool usedInActiveProfile)
            {
                Host = host;
                UsedInActiveProfile = usedInActiveProfile;
            }
        }

        public static bool TryParseHost(string input, out string formatted, out string hostname, out ushort port)
        {
            formatted = "";
            hostname = "";
            port = 0;

            var hostnamePort = input.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (hostnamePort.Length == 0)
                return false;

            hostname = hostnamePort[0];
            if (hostnamePort.Length < 2 || !ushort.TryParse(hostnamePort[1], out port))
                port = 9339;

            formatted = $"{hostname}:{port}";
            return true;
        }

        public ObservableCollection<HostItem> Hosts { get; }
        public ICommand DeleteHostCommand { get; }

        private readonly IProject _project;
        private bool _promptUnsavedOnClose = true;

        public TargetHostsEditor(IProject project)
        {
            _project = project;
            Hosts = new ObservableCollection<HostItem>(project.Options.TargetHosts.Select(h =>
                new HostItem(h, usedInActiveProfile: !project.Options.Profile.General.RunActionsLocally && project.Options.Connection.ToString() == h)));
            DeleteHostCommand = new WpfDelegateCommand(DeleteHost);

            InitializeComponent();
        }

        private void AddHost(object sender, RoutedEventArgs e)
        {
            var item = new HostItem("", usedInActiveProfile: false);
            Hosts.Add(item);

            // Finish editing the current host before moving the focus away from it
            HostGrid.CommitEdit();

            HostGrid.SelectedItem = item;
            HostGrid.CurrentCell = new DataGridCellInfo(item, HostGrid.Columns[0]);
#pragma warning disable VSTHRD001 // Using BeginInvoke to focus on the added host item _after_ it's been added to the DataGrid
            Dispatcher.BeginInvoke((Action)(() => HostGrid.BeginEdit()), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
        }

        private void DeleteHost(object param)
        {
            var item = (HostItem)param;
            var prompt = MessageBox.Show($"Are you sure you want to delete {item.Host}?", "Confirm host deletion", MessageBoxButton.YesNo);
            if (prompt == MessageBoxResult.Yes)
                Hosts.Remove(item);
        }

        private void SaveChanges()
        {
            _project.Options.TargetHosts.Clear();
            _project.Options.TargetHosts.AddRange(Hosts.Select(h => h.Host).Distinct());

            var updatedProfile = (Options.ProfileOptions)_project.Options.Profile.Clone();
            if (Hosts.FirstOrDefault(h => h.UsedInActiveProfile) is HostItem hi && TryParseHost(hi.Host, out _, out var hostname, out var port))
            {
                _project.Options.RemoteMachine = hostname;
                _project.Options.Port = port;
            }
            else
            {
                updatedProfile.General.RunActionsLocally = true;
            }
            _project.Options.UpdateActiveProfile(updatedProfile);

            _project.SaveOptions();
        }

        private void ValidateHostAfterEdit(object sender, DataGridRowEditEndingEventArgs e)
        {
            var item = (HostItem)e.Row.DataContext;
            if (TryParseHost(item.Host, out var formattedHost, out _, out _))
                item.Host = formattedHost;
            else
#pragma warning disable VSTHRD001 // Using BeginInvoke to delete item after all post-edit events fire
                Dispatcher.BeginInvoke((Action)(() => Hosts.Remove(item)), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
        }

        private void HandleDeleteKey(object sender, KeyEventArgs e)
        {
            IEditableCollectionView itemsView = HostGrid.Items;
            if (e.Key == Key.Delete && !itemsView.IsAddingNew && !itemsView.IsEditingItem && HostGrid.CurrentItem is HostItem item)
            {
                DeleteHost(item);
                e.Handled = true;
            }
        }

        private void HandleOK(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            SaveChanges();
            Close();
        }

        private void HandleCancel(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_promptUnsavedOnClose && HasChanges())
            {
                var result = MessageBox.Show($"Save changes to hosts?", "Target Hosts Editor", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    SaveChanges();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }

        private bool HasChanges()
        {
            if (Hosts.Count != _project.Options.TargetHosts.Count)
                return true;

            for (int i = 0; i < Hosts.Count; ++i)
            {
                if (Hosts[i].Host != _project.Options.TargetHosts[i])
                    return true;
            }

            return false;
        }
    }
}
