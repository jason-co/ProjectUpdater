using System;
using System.IO;

namespace EnvDTE.Helpers
{
    public class ProjectWrapper
    {
        private Project _project;

        public ProjectWrapper(Project project)
        {
            _project = project;
        }

        public Project Project { get { return _project; } }
        public string FullName { get { return _project.FullName; } }
        public string Name { get { return _project.Name; } }

        public bool IsSpecialProject { get; set; }

        public void Reload()
        {
            _project = (Project)((Array)(_project.DTE.ActiveSolutionProjects)).GetValue(0);
        }
    }
}
