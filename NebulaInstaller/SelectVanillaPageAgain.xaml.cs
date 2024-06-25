using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NebulaInstaller
{
    /// <summary>
    /// SelectVanillaPageAgain.xaml の相互作用ロジック
    /// </summary>
    public partial class SelectVanillaPageAgain : Page
    {
        public SelectVanillaPageAgain()
        {
            InitializeComponent();
        }

        private void ClickNext(object sender, RoutedEventArgs e)
        {
            string? path = AUInstalling.GetAmongUsDirectoryPath();

            if (path == null) Application.Current.Shutdown();

            if (!AUInstalling.IsVanillaAmongUsDirectory(path!))
                MainWindow.Instance.OpenPage(this);
            else
                MainWindow.Instance.OpenPage(new SelectInstallToPage(path!));
        }

        private void ClickPrev(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
