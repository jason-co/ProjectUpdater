using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Core;
using Core.Commands;
using EnvDTE.Helpers;
using Microsoft.Win32;

namespace ProjectUpdater.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        #region private members

        private const string SolutionExtension = ".sln";

        private readonly ILogger _logger;

        private string _solutionFileName;
        private bool _isUpdatingProjects;
        private bool _displayNonUpdatedProjects;

        private SolutionWrapper _solutionWrapper;
        private VisualStudioVersion _selectedVisualStudioVersion;
        private TargetFramework _selectedTargetFramework;

        private RelayCommand _openSolutionCommand;
        private RelayCommand _aggregateCommand;
        private RelayCommand _viewMissingProjectsCommand;
        private RelayCommand _closeMissingProjectsCommand;

        #endregion

        #region constructor

        public MainViewModel(ILogger logger)
        {
            VisualStudioVersions = Enum.GetValues(typeof(VisualStudioVersion)).Cast<VisualStudioVersion>().ToArray();
            TargetFrameworkVersions = Enum.GetValues(typeof(TargetFramework)).Cast<TargetFramework>().ToArray();
            _selectedVisualStudioVersion = VisualStudioVersion.VisualStudio2015;
            _selectedTargetFramework = TargetFramework.v4_5;
            _logger = logger;
        }

        #endregion

        #region properties

        public ILogger Logger { get { return _logger; } }
        public IReadOnlyList<ProjectWrapper> NonUpdatedProjects { get { return _solutionWrapper.NonUpdatedProjects; } }

        public string SolutionFileName
        {
            get { return _solutionFileName; }
            set { SetProperty(ref _solutionFileName, value); }
        }

        public bool IsUpdatingProjects
        {
            get { return _isUpdatingProjects; }
            set { SetProperty(ref _isUpdatingProjects, value); }
        }

        public VisualStudioVersion[] VisualStudioVersions { get; }

        public VisualStudioVersion SelectedVisualStudioVersion
        {
            get { return _selectedVisualStudioVersion; }
            set { SetProperty(ref _selectedVisualStudioVersion, value); }
        }

        public TargetFramework[] TargetFrameworkVersions { get; }

        public TargetFramework SelectedTargetFramework
        {
            get { return _selectedTargetFramework; }
            set { SetProperty(ref _selectedTargetFramework, value); }
        }

        public bool DisplayNonUpdatedProjects
        {
            get { return _displayNonUpdatedProjects; }
            set { SetProperty(ref _displayNonUpdatedProjects, value); }
        }

        #endregion

        #region ICommands

        public ICommand OpenSolutionCommand => RelayCommand.CreateCommand(ref _openSolutionCommand, OpenSolutionExecute, OpenSolutionCanExecute);
        public ICommand UpdateProjectCommand => RelayCommand.CreateCommand(ref _aggregateCommand, UpdateProjectExecute, UpdateProjectCanExecute);
        public ICommand ViewMissingProjectsCommand => RelayCommand.CreateCommand(ref _viewMissingProjectsCommand, ViewMissingProjectsExecute, ViewMissingProjectsCanExecute);
        public ICommand CloseMissingProjectsCommand => RelayCommand.CreateCommand(ref _closeMissingProjectsCommand, CloseMissingProjectsExecute);


        private void OpenSolutionExecute()
        {
            var fileDialog = new OpenFileDialog();
            fileDialog.Filter = String.Format("Solution files (*{0})|*{0}", SolutionExtension);

            if (fileDialog.ShowDialog().GetValueOrDefault())
            {
                SolutionFileName = fileDialog.FileName;
            }
        }

        private bool OpenSolutionCanExecute()
        {
            return !_isUpdatingProjects;
        }

        private bool UpdateProjectCanExecute()
        {
            return !String.IsNullOrEmpty(_solutionFileName) && !IsUpdatingProjects;
        }

        private void UpdateProjectExecute()
        {
            IsUpdatingProjects = true;

            Task.Run(() => UpdateProjectsAsync());
        }

        private void ViewMissingProjectsExecute()
        {
            DisplayNonUpdatedProjects = true;
            OnPropertyChanged(() => _solutionWrapper.NonUpdatedProjects);
        }

        private bool ViewMissingProjectsCanExecute()
        {
            return !IsUpdatingProjects;
        }

        private void CloseMissingProjectsExecute()
        {
            DisplayNonUpdatedProjects = false;
        }

        #endregion

        #region private methods

        private async Task UpdateProjectsAsync()
        {
            try
            {
                _logger.Log(String.Empty);
                _logger.Log("**************************************************");
                _logger.Log("     Starting the Project Updating process     ");
                _logger.Log("**************************************************");
                _logger.Log(String.Empty);

                _logger.Log("Solution: {0}", _solutionFileName);

                _solutionWrapper = new SolutionWrapper(_solutionFileName, _selectedVisualStudioVersion, _logger);

                await _solutionWrapper.UpdateTargetFrameworkForProjects(_selectedTargetFramework);
            }
            catch (Exception)
            {
                _logger.Log("~~~~~~ An error occurred while attempting to update all the projects ~~~~~~~");

                if (_solutionWrapper != null)
                {
                    await _solutionWrapper.CloseAsync();
                }
            }
            finally
            {
                BeginUpdateUI(() =>
                {

                    IsUpdatingProjects = false;

                    _logger.Log(String.Empty);
                    _logger.Log("**************************************************");
                    _logger.Log("     Project Updating process is finished     ");
                    _logger.Log("**************************************************");
                    _logger.Log(String.Empty);
                });
            }
        }

        #endregion
    }
}
