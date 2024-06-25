using System.Text;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static public MainWindow Instance { get; set; }
        
        public MainWindow()
        {
            InitializeComponent();

            Instance = this;

            InnerFrame.Navigate(new StartPage());
        }

        public void OpenPage(Page page)
        {
            InnerFrame.Navigate(page);
        }
    }
}