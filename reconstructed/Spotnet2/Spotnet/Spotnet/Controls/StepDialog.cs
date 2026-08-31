using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Spotnet.Views;

namespace Spotnet.Controls;
public partial class StepDialog : Window
{
    private readonly List<Control> _controls = new List<Control>();
    private int _controlIndex = -1;
    private bool _shown;
    public List<Control> Controls => _controls;
    public List<IStepControl> Steps { get; set; }

    public StepDialog()
    {
        InitializeComponent();
        _controls.Add(new AboutControl());
        RefreshButtonsState();
        _controlIndex = 0;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (!_shown)
        {
            _shown = true;
            ReloadContent();
        }
    }

    private void RefreshButtonsState()
    {
        NextButton.IsEnabled = _controlIndex < _controls.Count - 1;
        PrevButton.IsEnabled = _controlIndex > 0;
    }

    private void ReloadContent()
    {
        ContentFrame.Navigate(Controls[_controlIndex]);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controlIndex < _controls.Count - 1)
        {
            _controlIndex++;
            ReloadContent();
            RefreshButtonsState();
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controlIndex > 0)
        {
            _controlIndex--;
            ReloadContent();
            RefreshButtonsState();
        }
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        base.DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        base.DialogResult = false;
        Close();
    }
}