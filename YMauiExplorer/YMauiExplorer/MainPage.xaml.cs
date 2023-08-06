using YMauiExplorer.ViewModels;

namespace YMauiExplorer;

/// <summary>
/// 主页
/// </summary>
public partial class MainPage : ContentPage
{
    /// <summary>
    /// 初始化 MainPage 类的新实例。
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 当 ScrollView 控件滚动时调用。
    /// </summary>
    /// <param name="sender">触发事件的 ScrollView 对象。</param>
    /// <param name="e">包含事件数据的 ScrolledEventArgs 对象。</param>
    private void ScrollView_Scrolled(object sender, ScrolledEventArgs e)
    {
        // 检查绑定上下文是否为 MainViewModel 类的实例
        if (this.BindingContext is MainViewModel mvm)
        {
            // 将触发事件的对象转换为 ScrollView
            var scrollViewer = sender as ScrollView;

            // 如果已经滚动到底部
            if (e.ScrollY >= scrollViewer.ContentSize.Height - scrollViewer.Height)
            {
                // 触发滚动到底部的事件
                dynamic obj = new { VerticalOffset = e.ScrollY, ScrollableHeight = scrollViewer.Height };
                mvm.ScrollChanged(obj);
            }
        }
    }

    private async void CollectionView_Scrolled(object sender, ItemsViewScrolledEventArgs e)
    { 
        if (e.HorizontalDelta > 0)
        {
            await MainScroll.ScrollToAsync(0, MainScroll.ScrollY + e.HorizontalDelta, true);
        }
        else if (e.HorizontalDelta < 0)
        {
            await MainScroll.ScrollToAsync(0, MainScroll.ScrollY - e.HorizontalDelta, true);
        }
    }
}


