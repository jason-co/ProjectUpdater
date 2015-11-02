using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core;

namespace ProjectUpdater
{
    public class UpdaterLogger : ViewModelBase, ILogger
    {
        private readonly ObservableCollection<string> _logs;

        public UpdaterLogger()
        {
            _logs = new ObservableCollection<string>();
        }
        public IReadOnlyList<string> Logs { get { return new ReadOnlyObservableCollection<string>(_logs); } }

        public void Log(string format, params object[] args)
        {
            BeginUpdateUI(() => _logs.Add(String.Format(format, args)));
        }
    }
}
