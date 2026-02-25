using System;
using System.Windows;
using System.Windows.Controls;

namespace HtmlHelp.Wpf.Controls;

public static class TreeViewSelectedItemBehavior
{
	private static readonly object Uninitialized = new();

	public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.RegisterAttached(
		"SelectedItem",
		typeof(object),
		typeof(TreeViewSelectedItemBehavior),
		// NOTE: Use a non-null sentinel as the default value so that an initial binding value of null
		// still triggers the property-changed callback. We use that callback to hook the TreeView event.
		new FrameworkPropertyMetadata(Uninitialized, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

	public static object GetSelectedItem(DependencyObject obj)
		=> obj.GetValue(SelectedItemProperty);

	public static void SetSelectedItem(DependencyObject obj, object value)
		=> obj.SetValue(SelectedItemProperty, value);

	private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TreeView treeView)
			return;

		treeView.SelectedItemChanged -= TreeView_SelectedItemChanged;
		treeView.SelectedItemChanged += TreeView_SelectedItemChanged;

		var newValue = e.NewValue;
		if (ReferenceEquals(newValue, Uninitialized))
			newValue = null;

		// Try to select the item in the visual tree when VM changes selection.
		if (newValue != null)
			SelectItem(treeView, newValue);
	}

	private static void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		if (sender is not TreeView treeView)
			return;

		var current = GetSelectedItem(treeView);
		if (ReferenceEquals(current, Uninitialized))
			current = null;
		if (ReferenceEquals(current, e.NewValue))
			return;

		SetSelectedItem(treeView, e.NewValue);
	}

	private static bool SelectItem(ItemsControl parent, object target)
	{
		if (parent == null)
			return false;

		if (parent.ItemContainerGenerator.ContainerFromItem(target) is TreeViewItem directContainer)
		{
			directContainer.IsSelected = true;
			directContainer.BringIntoView();
			return true;
		}

		foreach (var item in parent.Items)
		{
			if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container)
				continue;

			if (SelectItem(container, target))
			{
				container.IsExpanded = true;
				return true;
			}
		}

		return false;
	}
}
