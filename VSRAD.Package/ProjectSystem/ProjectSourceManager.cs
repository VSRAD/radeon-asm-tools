﻿using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public enum DocumentSaveType
    {
        ActiveDocument = 1,
        OpenDocuments = 2,
        ProjectDocuments = 3,
        SolutionDocuments = 4,
        None = 5,
    }

    public interface IProjectSourceManager
    {
        string ProjectRoot { get; }
        Task SaveDocumentsAsync(DocumentSaveType type);
        Task<IEnumerable<(string absolutePath, string relativePath)>> ListProjectFilesAsync();
    }

    [Export(typeof(IProjectSourceManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProjectSourceManager : IProjectSourceManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly UnconfiguredProject _unconfiguredProject;

        public string ProjectRoot { get; }

        [ImportingConstructor]
        public ProjectSourceManager(SVsServiceProvider serviceProvider, UnconfiguredProject unconfiguredProject)
        {
            _serviceProvider = serviceProvider;
            _unconfiguredProject = unconfiguredProject;
            ProjectRoot = Path.GetDirectoryName(unconfiguredProject.FullPath);
        }

        public async Task SaveDocumentsAsync(DocumentSaveType type)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);
            switch (type)
            {
                case DocumentSaveType.ActiveDocument:
                    if (dte.ActiveDocument?.Saved == false)
                        dte.ActiveDocument.Save();
                    break;
                case DocumentSaveType.OpenDocuments:
                    try
                    {
                        // TODO: try to find a better way to save open documents
                        // The dte.Documents collection could be in invalid state
                        // preventing any access to its Items or Count
                        dte.Documents.SaveAll();
                    }
                    catch (Exception e)
                    {
                        if (e.HResult == Microsoft.VisualStudio.VSConstants.E_FAIL)
                        {
                            VSRAD.Package.Errors.ShowCritical("Unable to save opened files. Try to close tabs with deleted or unavailable files.");
                        }
                        else
                        {
                            throw e;
                        }
                    }
                    break;
                case DocumentSaveType.ProjectDocuments:
                    if (dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
                        if (activeSolutionProjects.GetValue(0) is EnvDTE.Project activeProject)
                            foreach (ProjectItem item in activeProject.ProjectItems)
                                SaveDocumentsRecursively(item);
                    break;
                case DocumentSaveType.SolutionDocuments:
                    foreach (EnvDTE.Project project in dte.Solution.Projects)
                        foreach (ProjectItem item in project.ProjectItems)
                            SaveDocumentsRecursively(item);
                    break;
            }
        }

        public async Task<IEnumerable<(string absolutePath, string relativePath)>> ListProjectFilesAsync()
        {
            var configuredProject = _unconfiguredProject.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;
            var projectItems = await configuredProject.Services.SourceItems.GetItemsAsync();

            var files = new List<(string absolutePath, string relativePath)>();
            foreach (var item in projectItems)
            {
                string name;
                if (item.EvaluatedIncludeAsFullPath.StartsWith(ProjectRoot, StringComparison.Ordinal))
                {
                    name = item.EvaluatedIncludeAsRelativePath;
                }
                else
                {
                    name = await item.Metadata.GetEvaluatedPropertyValueAsync("Link"); // CPS-style links, used in RADProject
                    if (string.IsNullOrEmpty(name))
                        name = Path.GetFileName(item.EvaluatedIncludeAsFullPath); // VisualC-style links (project-relative Include starting with "..")
                }

                files.Add((item.EvaluatedIncludeAsFullPath, name));
            }

            return files;
        }

        private void SaveDocumentsRecursively(ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (projectItem.Document?.Saved == false)
                projectItem.Document.Save();

            if (projectItem.ProjectItems != null)
                foreach (ProjectItem subItem in projectItem.ProjectItems)
                    SaveDocumentsRecursively(subItem);
        }
    }
}
