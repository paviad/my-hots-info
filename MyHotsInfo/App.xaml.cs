using MyHotsInfo.Utils;

namespace MyHotsInfo
{
    public partial class App : Application {
        public App(IServiceProvider svcp, MyNavigator myNavigator) {
            InitializeComponent();

            MainPage = new AppShell(svcp, myNavigator);
        }
    }
}
