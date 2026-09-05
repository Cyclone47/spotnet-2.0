using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Spotnet.Extensions;

public static class ParentOfTypeExtensions
{
	public static T ParentOfType<T>(this DependencyObject element) where T : DependencyObject
	{
		if (element == null)
		{
			return null;
		}
		return element.GetParents().OfType<T>().FirstOrDefault();
	}

	internal static IEnumerable<T> GetAncestors<T>(this DependencyObject element) where T : class
	{
		return element.GetParents().OfType<T>();
	}

	internal static T GetParent<T>(this DependencyObject element) where T : FrameworkElement
	{
		return element.ParentOfType<T>();
	}

	public static IEnumerable<DependencyObject> GetParents(this DependencyObject element)
	{
		if (element == null)
		{
			throw new ArgumentNullException("element");
		}
		while (true)
		{
			DependencyObject parent;
			element = (parent = element.GetParent());
			if (parent != null)
			{
				yield return element;
				continue;
			}
			break;
		}
	}

	private static DependencyObject GetParent(this DependencyObject element)
	{
		DependencyObject dependencyObject = null;
		try
		{
			dependencyObject = VisualTreeHelper.GetParent(element);
		}
		catch (InvalidOperationException)
		{
			dependencyObject = null;
		}
		if (dependencyObject == null && element is FrameworkElement frameworkElement)
		{
			dependencyObject = frameworkElement.Parent;
		}
		return dependencyObject;
	}
}
