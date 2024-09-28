using System.Windows.Controls;
using XExplorer.ViewModels;

namespace XExplorer.Views;

public partial class InputPwd
{
	public InputPwd()
	{
		InitializeComponent();
		this.DataContext = new InputPwdViewModel();
	}
}