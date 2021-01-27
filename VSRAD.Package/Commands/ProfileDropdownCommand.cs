﻿using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Profiles;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProfileDropdownCommand : ICommandHandler
    {
        private readonly IProject _project;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public ProfileDropdownCommand(IProject project, SVsServiceProvider serviceProvider)
        {
            _project = project;
            _serviceProvider = serviceProvider;
            _project.Options.PropertyChanged += ProjectOptionsChanged;
        }

        private void ProjectOptionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (e.PropertyName == nameof(ProjectOptions.ActiveProfile))
            {
                var shell = (IVsUIShell)_serviceProvider.GetService(typeof(SVsUIShell));
                Assumes.Present(shell);
                shell.UpdateCommandUI(0); // Force VS to refresh dropdown items
            }
        }

        public Guid CommandSet => Constants.ProfileDropdownCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (_project.Options.Profile == null)
                return;
            if (commandId == Constants.ProfileTargetMachineDropdownListId && variantOut != IntPtr.Zero)
                ListTargetMachines(variantOut);
            if (commandId == Constants.ProfileTargetMachineDropdownId && variantOut != IntPtr.Zero)
                GetCurrentTargetMachine(variantOut);
            if (commandId == Constants.ProfileTargetMachineDropdownId && variantIn != IntPtr.Zero)
            {
                var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);
                if (selected == "Edit...")
                    OpenHostsEditor();
                else
                    SetNewTargetMachine(selected);
            }
        }

        private void ListTargetMachines(IntPtr variantOut)
        {
            if (_project.Options.TargetHosts.Count == 0)
            {
                foreach (var profile in _project.Options.Profiles)
                    _project.Options.TargetHosts.Add(profile.Value.General.Connection.ToString());
            }

            // Add the current host to the list in case the user switches to a different profile
            // (if the profile is not changed this is a no-op because the current host is already at the top of the list)
            _project.Options.TargetHosts.Add(_project.Options.Profile.General.Connection.ToString());

            var displayItems = _project.Options.TargetHosts.Prepend("Local").Append("Edit...").ToArray();
            Marshal.GetNativeVariantForObject(displayItems, variantOut);
        }

        private void GetCurrentTargetMachine(IntPtr variantOut)
        {
            var currentHost = _project.Options.Profile.General.RunActionsLocally ? "Local" : _project.Options.Profile.General.Connection.ToString();
            Marshal.GetNativeVariantForObject(currentHost, variantOut);
        }

        private void SetNewTargetMachine(string selected)
        {
            var updatedProfile = (ProfileOptions)_project.Options.Profile.Clone();

            if (selected == "Local")
            {
                updatedProfile.General.RunActionsLocally = true;
            }
            else
            {
                if (!RecentlyUsedHostsEditor.TryParseHost(selected, out var formattedHost, out var hostname, out var port))
                    return;

                _project.Options.TargetHosts.Add(formattedHost);

                updatedProfile.General.RemoteMachine = hostname;
                updatedProfile.General.Port = port;
                updatedProfile.General.RunActionsLocally = false;
            }

            _project.Options.UpdateActiveProfile(updatedProfile);
        }

        private void OpenHostsEditor()
        {
            var editor = new RecentlyUsedHostsEditor(_project)
            {
                Owner = Application.Current.MainWindow,
                ShowInTaskbar = false
            };
            editor.ShowDialog();
        }
    }
}
