using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using LaunchEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace Mdv;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        var cli = System.Environment.GetCommandLineArgs();
        if (cli.Length > 1)
        {
            var arg = cli[1];
            if (System.IO.Directory.Exists(arg)) _ = _window.ViewModel.LoadDirectoryAsync(arg);
            else if (System.IO.File.Exists(arg)) _ = _window.ViewModel.LoadFileAsync(arg);
        }
    }
}
