using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YExplorer.ViewModels;

namespace YExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : HandyControl.Controls.GlowWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();

            // 添加鼠标滚动事件处理函数
            this.MouseWheel += MainWindow_MouseWheel;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        
        {
            if (this.DataContext is MainViewModel mvm)
            {
                var scrollViewer = sender as HandyControl.Controls.ScrollViewer;
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
