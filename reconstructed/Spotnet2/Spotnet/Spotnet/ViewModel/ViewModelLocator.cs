using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;

namespace Spotnet.ViewModel;

public class ViewModelLocator
{
	public VisibilityViewModel Visibility => ServiceLocator.Current.GetInstance<VisibilityViewModel>();

	public SpotsListViewModel SpotsList => ServiceLocator.Current.GetInstance<SpotsListViewModel>();

	public StatusBarViewModel StatusBar => ServiceLocator.Current.GetInstance<StatusBarViewModel>();

	public MainWindowViewModel MainWindow => ServiceLocator.Current.GetInstance<MainWindowViewModel>();

	public SocksProxyTooltipViewModel SocksProxyTooltip => ServiceLocator.Current.GetInstance<SocksProxyTooltipViewModel>();

	static ViewModelLocator()
	{
		ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
		SimpleIoc.Default.Register<VisibilityViewModel>();
		SimpleIoc.Default.Register<SpotsListViewModel>();
		SimpleIoc.Default.Register<StatusBarViewModel>();
		SimpleIoc.Default.Register<MainWindowViewModel>();
		SimpleIoc.Default.Register<SocksProxyTooltipViewModel>();
	}

	public static void Cleanup()
	{
	}
}
