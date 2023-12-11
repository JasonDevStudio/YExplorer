using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using YExplorer.ViewModels;

namespace YExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : HandyControl.Controls.Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();

            // 添加鼠标滚动事件处理函数
            this.MouseWheel += MainWindow_MouseWheel;
            WeakReferenceMessenger.Default.Register<string>(this, OnMessageReceived);
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

        private async void PicScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;

            // 检查是否已经滚动到底部
            bool isAtBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight;

            if (isAtBottom)
            {
                // 滚动条已经到达底部的逻辑处理 
                if (this.DataContext is MainViewModel mvm)
                { 
                    dynamic obj = new { VerticalOffset = e.VerticalOffset, ScrollableHeight = scrollViewer.ScrollableHeight };
                    await mvm.PicScrollChanged(obj);
                    var newOffset = scrollViewer.VerticalOffset - 1; // 减少 1 像素
                    scrollViewer.ScrollToVerticalOffset(newOffset);
                }
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

        private void OnMessageReceived(object sender, string message)
        {
            // 判断消息是否是 "ScrollToTop"
            if (message == "ScrollToTop")
            {
                // 调用 ScrollViewer 的 ScrollToVerticalOffset 方法，将垂直滚动位置设置为 0
                scrollViewer.ScrollToTop();
            }
            else
            {
                scrollViewer.ScrollToBottom();
            }
        }
    }
}
