using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Spotnet.Extensions;

public static class ChildrenOfTypeExtensions
{
	public static IEnumerable<T> ChildrenOfType<T>(this DependencyObject element) where T : DependencyObject
	{
		return element.GetChildrenRecursive().OfType<T>();
	}

	public static T FindChildByType<T>(this DependencyObject element) where T : DependencyObject
	{
		return element.ChildrenOfType<T>().FirstOrDefault();
	}

	internal static FrameworkElement GetChildByName(this FrameworkElement element, string name)
	{
		if (element == null)
		{
			return null;
		}
		return (element.FindName(name) as FrameworkElement) ?? element.ChildrenOfType<FrameworkElement>().FirstOrDefault((FrameworkElement c) => c.Name == name);
	}

	private static IEnumerable<DependencyObject> GetChildrenRecursive(this DependencyObject element)
	{
		if (element == null)
		{
			throw new ArgumentNullException("element");
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(element, i);
			yield return child;
			foreach (DependencyObject item in child.GetChildrenRecursive())
			{
				yield return item;
			}
		}
	}
}
