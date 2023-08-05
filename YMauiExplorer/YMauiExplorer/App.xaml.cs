namespace YMauiExplorer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell() { };
        MainPage.Window.Width = 1400; 
        MainPage.Window.Width = 1000;
    }
}
