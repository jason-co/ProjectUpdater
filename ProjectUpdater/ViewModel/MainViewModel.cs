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
        private readonly IList<ProjectWrapper> _nonUpdatedProjects; 

        private string _solutionFileName;
        private bool _isUpdatingProjects;
        private bool _displayNonUpdatedProjects;

        private SolutionWrapper _solutionWrapper;
        private VisualStudioVersion _selectedVisualStudioVersion;
        private TargetFramework _selectedTargetFramework;

        private RelayCommand _openSolutionCommand;
        private RelayCommand _aggregateCommand;
        private RelayCommand _viewNonUpdatedProjectsCommand;
        private RelayCommand _closeNonUpdatedProjectsCommand;

        #endregion

        #region constructor

        public MainViewModel(ILogger logger)
        {
            VisualStudioVersions = Enum.GetValues(typeof(VisualStudioVersion)).Cast<VisualStudioVersion>().ToArray();
            TargetFrameworkVersions = Enum.GetValues(typeof(TargetFramework)).Cast<TargetFramework>().ToArray();
            _selectedVisualStudioVersion = VisualStudioVersion.VisualStudio2015;
            _selectedTargetFramework = TargetFramework.v4_5;
            _logger = logger;

            _nonUpdatedProjects = new ObservableCollection<ProjectWrapper>();

        }

        #endregion

        #region properties

        public ILogger Logger { get { return _logger; } }
        public IList<ProjectWrapper> NonUpdatedProjects { get { return _nonUpdatedProjects; } }

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
        public ICommand ViewNonUpdatedProjectsCommand => RelayCommand.CreateCommand(ref _viewNonUpdatedProjectsCommand, ViewNonUpdatedProjectsExecute, ViewNonUpdatedProjectsCanExecute);
        public ICommand CloseNonUpdatedProjectsCommand => RelayCommand.CreateCommand(ref _closeNonUpdatedProjectsCommand, CloseNonUpdatedProjectsExecute);

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

        private void ViewNonUpdatedProjectsExecute()
        {
            _nonUpdatedProjects.Clear();
            if (_solutionWrapper != null)
            {
                foreach (var project in _solutionWrapper.NonUpdatedProjects)
                {
                    _nonUpdatedProjects.Add(project);
                }
            }
            DisplayNonUpdatedProjects = true;
        }

        private bool ViewNonUpdatedProjectsCanExecute()
        {
            return !IsUpdatingProjects;
        }

        private void CloseNonUpdatedProjectsExecute()
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
