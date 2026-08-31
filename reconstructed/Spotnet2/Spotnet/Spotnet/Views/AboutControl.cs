using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using Microsoft.VisualBasic.CompilerServices;
using Spotnet.Controls;
using Spotnet.Deployment;
using Spotnet.Helpers;
using Spotnet.Properties;

namespace Spotnet.Views;
public partial class AboutControl : UserControl
{
    internal AboutControl()
    {
        base.Initialized += AboutControl_Initialized;
        base.Loaded += AboutControl_Loaded;
        InitializeComponent();
    }

    private void AddFade(object xBoard, object xIn, int sec)
    {
        DoubleAnimation doubleAnimation = new DoubleAnimation();
        TimeSpan timeSpan = new TimeSpan(0, 0, sec);
        NewLateBinding.LateSet(xIn, null, "Opacity", new object[1] { 0 }, null, null);
        NewLateBinding.LateSet(xIn, null, "Visibility", new object[1] { Visibility.Visible }, null, null);
        doubleAnimation.From = 0.0;
        doubleAnimation.To = 1.0;
        doubleAnimation.Duration = new Duration(timeSpan);
        Storyboard.SetTargetName(doubleAnimation, Conversions.ToString(NewLateBinding.LateGet(xIn, null, "Name", new object[0], null, null, null)));
        Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(UIElement.OpacityProperty));
        object instance = NewLateBinding.LateGet(xBoard, null, "Children", new object[0], null, null, null);
        object[] array = new object[1]
        {
            doubleAnimation
        };
        object[] arguments = array;
        bool[] obj = new bool[1]
        {
            true
        };
        bool[] copyBack = obj;
        NewLateBinding.LateCall(instance, null, "Add", arguments, null, null, copyBack, IgnoreReturn: true);
        if (obj[0])
        {
            _ = (DoubleAnimation)Conversions.ChangeType(RuntimeHelpers.GetObjectValue(array[0]), typeof(DoubleAnimation));
        }
    }

    private void AboutControl_Initialized(object sender, EventArgs e)
    {
        VersionLabel.Content = $"v{AppHelper.AppVersion}";
        NewVersionProgressRing.Visibility = Visibility.Collapsed;
        NewVersionProgressRing.IsActive = false;
        NewVersionLabel.Text = ((SquirrelStuff.LastVersion == AppHelper.AppVersion) ? Words.LatestVersionIsUsed : string.Format(Words.NewVersionWillBeInstalledOnNextStart, SquirrelStuff.LastVersion));
    }

    private void AboutControl_Loaded(object sender, RoutedEventArgs e)
    {
        Storyboard storyboard = new Storyboard();
        Storyboard storyboard2 = new Storyboard();
        AddFade(storyboard, Label1, 3);
        AddFade(storyboard, VersionLabel, 3);
        AddFade(storyboard, NewVersionLabel, 3);
        AddFade(storyboard, Label3, 3);
        AddFade(storyboard, Label4, 3);
        storyboard.Begin(this);
        storyboard2.Begin(this);
    }
}