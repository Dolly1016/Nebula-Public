using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace NebulaInstaller
{
    /// <summary>
    /// FinishPage.xaml の相互作用ロジック
    /// </summary>
    public partial class FinishPage : Page
    {
        string installToDirectoryPath;
        public FinishPage(string installToDirectoryPath)
        {
            InitializeComponent();

            this.installToDirectoryPath = installToDirectoryPath;
        }

        private void ClickBoot(object sender, RoutedEventArgs e)
        {
            Process.Start(installToDirectoryPath + Path.DirectorySeparatorChar + "Among Us.exe");
            Application.Current.Shutdown();
        }

        private void ClickExit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
