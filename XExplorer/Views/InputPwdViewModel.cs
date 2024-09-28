using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Tools.Extension;

namespace XExplorer.ViewModels;

public partial class InputPwdViewModel : ObservableObject, IDialogResultable<string>
{
	/// <summary>
	/// Stores the user's password input.
	/// </summary>
	[ObservableProperty]
	private string password;

	public string Result { get; set; }
	
	public Action CloseAction { get; set; }
	
	public Action CancelAction { get; set; }
	
	public RelayCommand CloseCmd => new(() => CloseAction?.Invoke());
	
	public RelayCommand CancelCmd => new(() =>
	{
		this.Result = null;
		CloseAction?.Invoke();
	});
}