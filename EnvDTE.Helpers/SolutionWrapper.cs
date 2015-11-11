using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core;

namespace EnvDTE.Helpers
{
    public class SolutionWrapper
    {
        #region fields

        private static object _nonUpdatedLocker = new object();

        private const string SolutionBusyMessage = "(RPC_E_SERVERCALL_RETRYLATER)";
        private const string ClientProfile = ",Profile=Client";
        private const string TargetMoniker = ".NETFramework,Version=v{0}{1}";
        private const string TargetFrameworkMonikerIndex = "TargetFrameworkMoniker";

        private const int SolutionWaitTimeInMS = 3000;

        private readonly IList<ProjectWrapper> _projectWrappers;
        private readonly IList<ProjectWrapper> _nonUpdatedProjects;

        private readonly string _solutionName;
        private readonly VisualStudioVersion _visualStudioVersion;
        private readonly ILogger _logger;

        private DTE _dte;

        #endregion

        #region constructor

        public SolutionWrapper(string solutionName, VisualStudioVersion visualStudioVersion, ILogger logger)
        {
            _solutionName = solutionName;
            _visualStudioVersion = visualStudioVersion;
            _logger = logger;
            _projectWrappers = new List<ProjectWrapper>();
            _dte = EnvDTEFactory.Create(visualStudioVersion);

            _nonUpdatedProjects = new ObservableCollection<ProjectWrapper>();
            NonUpdatedProjects = new ReadOnlyCollection<ProjectWrapper>(_nonUpdatedProjects);
        }

        #endregion

        #region public properties

        public IReadOnlyList<ProjectWrapper> NonUpdatedProjects { get; }

        #endregion

        public async Task UpdateTargetFrameworkForProjects(TargetFramework framework)
        {
            await OpenSolution();

            var sourceDirectory = Path.GetDirectoryName(_solutionName);
            var suoFiles = (new DirectoryInfo(sourceDirectory)).GetFiles("*.suo");
            if (!suoFiles.Any())
            {
                _logger.Log("No .suo file, closing and reopening solution");
                await CloseAsync();
                _dte = EnvDTEFactory.Create(_visualStudioVersion);
                await OpenSolution();
            }

            var iterations = 15;

            _logger.Log("Number of projects updating from the solution: {0}", _projectWrappers.Count());
            _logger.Log("Attempting to upgrade all projects to version {0} in {1} attempts", framework.ToDescription(), iterations);

            _nonUpdatedProjects.Clear();
            _projectWrappers.ToList().ForEach(p => _nonUpdatedProjects.Add(p));
            for (int i = 0; i < iterations && _nonUpdatedProjects.Any(p => !p.IsSpecialProject); i++)
            {
                _logger.Log("************ Attempt {0} ************", i + 1);

                var projects = _nonUpdatedProjects.ToArray();
                Parallel.ForEach(projects, async project =>
                {
                    await AttemptTo(() =>
                    {
                        UpdateProject(project, framework);
                    });
                });
            }

            await SaveAsync();
            await CloseAsync();
        }

        #region Update Process

        private void UpdateProject(ProjectWrapper project, TargetFramework framework)
        {
            project.AttemptToReload();

            if (project.Project.Kind == Constants.vsProjectKindSolutionItems
                || project.Project.Kind == Constants.vsProjectKindMisc)
            {
                project.IsSpecialProject = true;
            }
            else
            {
                if (SetTargetFramework(project.Project, framework))
                {
                    project.Reload();
                    _logger.Log("Project Updated: {0}", project.Name);
                }

                lock (_nonUpdatedLocker)
                {
                    _nonUpdatedProjects.Remove(project);
                }
            }
        }

        private bool SetTargetFramework(Project project, TargetFramework targetFramework)
        {
            var targetMoniker = GetTargetFrameworkMoniker(targetFramework);
            var currentMoniker = project.Properties.Item(TargetFrameworkMonikerIndex).Value;

            if (!currentMoniker.ToString().Contains("Silverlight")
                && !Equals(targetMoniker, currentMoniker))
            {
                project.Properties.Item(TargetFrameworkMonikerIndex).Value = targetMoniker;

                return true;
            }

            if (currentMoniker.ToString().Contains("Silverlight"))
            {
                _logger.Log("Ignoring Silverlight project: {0}", project.Name);
            }
            else if (Equals(targetMoniker, currentMoniker))
            {
                _logger.Log("Project already matches framework: {0}", project.Name);
            }

            return false;
        }

        private string GetTargetFrameworkMoniker(TargetFramework targetFramework, bool isClientProfile = false)
        {
            var version = targetFramework.ToDescription();

            var clientProfile = isClientProfile ? ClientProfile : String.Empty;

            return String.Format(TargetMoniker, version, clientProfile);
        }

        #endregion

        #region Solution Open/Save/Close

        private async Task OpenSolution()
        {
            if (!File.Exists(_solutionName))
            {
                _logger.Log("Since solution does not exist, creating dummy placeholder");
                await SaveAsync();
            }
            else
            {
                _logger.Log("Solution already exists");
            }

            _dte.Solution.Open(_solutionName);

            await AttemptTo(CreateProjectWrappers);
        }

        private async Task SaveAsync()
        {
            _logger.Log("Saving Solution");
            await AttemptTo(() =>
            {
                _dte.Solution.SaveAs(_solutionName);
            });
        }

        public async Task CloseAsync()
        {
            _logger.Log("Closing Solution");
            await AttemptTo(() =>
            {
                _dte.Solution.Close();
                _dte.Quit();
            });
        }

        #endregion

        #region Project Wrappers Creation

        private void CreateProjectWrappers()
        {
            var projs = _dte.Solution.Projects.Cast<Project>().ToArray();

            _projectWrappers.Clear();
            foreach (var project in projs)
            {
                AttemptTo(() =>
                {
                    CreateProjectWrapper(project);
                });
            }
        }

        private void CreateProjectWrapper(Project project)
        {
            if (project != null)
            {
                if (project.Kind == Constants.vsProjectKindSolutionItems
                    || project.Kind == Constants.vsProjectKindMisc)
                {
                    foreach (var projectItem in project.ProjectItems.Cast<ProjectItem>())
                    {
                        CreateProjectWrapper(projectItem.SubProject);
                    }
                }
                else
                {
                    var wrapper = new ProjectWrapper(project);
                    _projectWrappers.Add(wrapper);
                }
            }
        }

        #endregion

        #region Helpers

        private async Task AttemptTo(Action act, int attemptsLeft = 10)
        {
            try
            {
                if (attemptsLeft >= 0)
                {
                    act();
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains(SolutionBusyMessage))
                {
                    _logger.Log("Solution busy... Waiting {0} seconds", SolutionWaitTimeInMS / 1000);
                }
                System.Threading.Thread.Sleep(SolutionWaitTimeInMS);
                Task.Run(() => AttemptTo(act, attemptsLeft - 1)).Wait();
            }
        }

        #endregion
    }
}
