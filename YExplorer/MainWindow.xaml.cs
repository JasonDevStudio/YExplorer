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

namespace YExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 添加鼠标滚动事件处理函数
            this.MouseWheel += MainWindow_MouseWheel;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        
        {
            if (this.DataContext is MainViewModel mvm)
            {
                var scrollViewer = sender as ScrollViewer;
                dynamic obj = new { VerticalOffset = e.VerticalOffset, ScrollableHeight = scrollViewer.ScrollableHeight };
                mvm.ScrollChanged(obj);
            }
        }

        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 更改ScrollViewer的滚动位置
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        }
    }
}
