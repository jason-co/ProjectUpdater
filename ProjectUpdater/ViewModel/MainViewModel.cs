using Core;

namespace ProjectUpdater.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ILogger _logger;

        public MainViewModel(ILogger logger)
        {
            _logger = logger;
        }
    }
}
