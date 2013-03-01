using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfGoogleMapClient
{
    /// <summary>
    /// KinectImageWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class KinectImageWindow : Window
    {
        public MainWindow mainWindow;

        public KinectImageWindow()
        {
            InitializeComponent();
        }

        void replayButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.replayButton_Click(sender, e);
        }

        void recordOption_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.recordOption_Click(sender, e);
        }
    }
}