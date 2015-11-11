using System;
using System.IO;

namespace EnvDTE.Helpers
{
    public class ProjectWrapper
    {
        private const string ProjectUnavailableMessage = "Project unavailable.";

        private Project _project;

        public ProjectWrapper(Project project)
        {
            _project = project;

            DirectoryName = Path.GetDirectoryName(project.FullName);
        }

        public Project Project
        {
            get { return _project; }
        }

        public string FullName
        {
            get { return _project.FullName; }
        }

        public string Name
        {
            get { return _project.Name; }
        }

        public string DirectoryName { get; }

        public bool IsSpecialProject { get; set; }

        public void Reload()
        {
            _project = (Project)((Array)(_project.DTE.ActiveSolutionProjects)).GetValue(0);
        }

        public void AttemptToReload()
        {
            try
            {
                // only way to determine if project needs to be reloaded is through accessing a property
                var properties = _project.Properties;
            }
            catch (Exception ex)
            {

                if (ex.Message.Contains(ProjectUnavailableMessage))
                {
                    Reload();
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
