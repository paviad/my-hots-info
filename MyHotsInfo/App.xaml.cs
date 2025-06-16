using MyHotsInfo.Utils;

namespace MyHotsInfo {
    public partial class App : Application {
        private readonly MyNavigator _myNavigator;
        private readonly IServiceProvider _svcp;

        public App(IServiceProvider svcp, MyNavigator myNavigator) {
            _svcp = svcp;
            _myNavigator = myNavigator;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState) {
            var appShell = new AppShell(_svcp, _myNavigator);
            return new Window(appShell);
        }
    }
}