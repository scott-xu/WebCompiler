﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using WebCompiler;

namespace WebCompilerVsix
{
    class ErrorList
    {
        private static Dictionary<string, ErrorListProvider> _providers = new Dictionary<string, ErrorListProvider>();

        public static void AddErrors(string file, IEnumerable<CompilerError> errors)
        {
            CleanErrors(file);
            var provider = new ErrorListProvider(WebCompilerPackage.Package);

            foreach (var error in errors)
            {
                var task = CreateTask(error, provider);
                provider.Tasks.Add(task);
            }

            _providers.Add(file, provider);
        }

        public static void CleanErrors(string file)
        {
            if (_providers.ContainsKey(file))
            {
                _providers[file].Tasks.Clear();
                _providers[file].Dispose();
                _providers.Remove(file);
            }
        }

        public static void CleanAllErrors()
        {
            foreach (string file in _providers.Keys)
            {
                var provider = _providers[file];
                if (provider != null)
                {
                    provider.Tasks.Clear();
                    provider.Dispose();
                }
            }

            _providers.Clear();
        }

        private static ErrorTask CreateTask(CompilerError error, ErrorListProvider provider)
        {
            ErrorTask task = new ErrorTask()
            {
                Line = error.LineNumber,
                Column = error.ColumnNumber,
                ErrorCategory = error.IsWarning ? TaskErrorCategory.Warning : TaskErrorCategory.Error,
                Category = TaskCategory.BuildCompile,
                Document = error.FileName,
                Priority = TaskPriority.Normal,
                Text = $"(WebCompiler) {error.Message}",
            };

            EnvDTE.ProjectItem item = WebCompilerPackage._dte.Solution.FindProjectItem(error.FileName);

            if (item != null && item.ContainingProject != null)
                AddHierarchyItem(task, item.ContainingProject);

            task.Navigate += (s, e) =>
            {
                provider.Navigate(task, new Guid(EnvDTE.Constants.vsViewKindPrimary));

                if (task.Column > 0)
                {
                    var doc = (EnvDTE.TextDocument)WebCompilerPackage._dte.ActiveDocument.Object("textdocument");
                    doc.Selection.MoveToLineAndOffset(task.Line, task.Column, false);
                }
            };

            return task;
        }


        private static void AddHierarchyItem(ErrorTask task, EnvDTE.Project project)
        {
            IVsSolution solution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;

            if (solution == null || project == null)
                return;

            try
            {
                IVsHierarchy hierarchyItem = null;
                if (solution.GetProjectOfUniqueName(project.FullName, out hierarchyItem) == 0)
                {
                    task.HierarchyItem = hierarchyItem;
                }
            }
            catch (COMException ex)
            {
                Logger.Log(ex);
            }
        }
    }
}
