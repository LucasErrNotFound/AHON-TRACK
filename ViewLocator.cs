using System;
using System.Collections.Generic;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AHON_TRACK
{
    public class ViewLocator : IDataTemplate
    {
        private readonly Dictionary<string, string> _componentMappings = new()
        {
            // Add mappings for your components
            // Pattern: "ViewModelName" -> "ComponentFolder"
            { "EmployeeProfileInformationViewModel", "EmployeeProfile" },
            { "AddNewEmployeeDialogCardViewModel", "AddNewEmployeeDialogCard" },
        };

        public Control? Build(object? param)
        {
            if (param is null)
                return null;

            var viewModelType = param.GetType();
            var viewModelName = viewModelType.FullName!;

            var view = TryFindView(viewModelName, viewModelType);

            if (view != null)
                return view;

            return new TextBlock { Text = "Not Found: " + viewModelName };
        }

        private Control? TryFindView(string viewModelName, Type viewModelType)
        {
            var viewModelClassName = viewModelType.Name;

            if (viewModelName.Contains("Components.ViewModels") &&
                _componentMappings.TryGetValue(viewModelClassName, out var componentFolder))
            {
                var componentViewName = viewModelName
                    .Replace("Components.ViewModels", $"Components.{componentFolder}", StringComparison.Ordinal)
                    .Replace("ViewModel", "View", StringComparison.Ordinal);

                var view = TryCreateView(componentViewName);
                if (view != null) return view;
            }
            var defaultViewName = viewModelName.Replace("ViewModel", "View", StringComparison.Ordinal);
            var view2 = TryCreateView(defaultViewName);
            if (view2 != null) return view2;
            return null;
        }

        private Control? TryCreateView(string viewName)
        {
            try
            {
                var type = Type.GetType(viewName);
                if (type != null)
                {
                    return (Control)Activator.CreateInstance(type)!;
                }
            }
            catch
            {
            }
            return null;
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}