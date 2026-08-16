using PKHaX.Mobile.Views;

namespace PKHaX.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("box", typeof(BoxPage));
		
	}
}
