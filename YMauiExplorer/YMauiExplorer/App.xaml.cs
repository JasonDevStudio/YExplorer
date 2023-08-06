namespace YMauiExplorer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell() { }; 
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        Window window = base.CreateWindow(activationState);

        // 设置窗口的宽度和高度
        window.Width = 1600;
        window.Height = 1200;

        return window;
    }
}
