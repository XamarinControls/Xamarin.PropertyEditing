﻿using System.Linq;
using AppKit;

namespace Xamarin.PropertyEditing.Mac
{
	internal class SegmentStack : NSView
	{
		class SegmentItem
		{
			public NSImage Image { get; set; }
			public string Label { get; set; }
			public NSView View { get; set; }
		}


		private SegmentItem[] segments = new SegmentItem[0];
		private NSStackView stackView = new NSStackView () {
			Spacing = 3
		};

		public void SetImage (NSImage image, int segment)
		{
			var item = this.segments[segment];

			item.Image = image;
			if (item.Image != null)
				return;

			item.View = new UnderlinedImageView (image.Name);
			this.stackView.AddView (item.View, NSStackViewGravity.Leading);
		}

		public NSImage GetImage (int segment)
		{
			return this.segments[segment].Image;
		}

		public void SetLabel (string label, int segment)
		{
			var item = this.segments[segment];
			item.Label = label;
			if (item.Image != null)
				return;
			
			item.View = new UnderlinedTextField () {
				BackgroundColor = NSColor.Clear,
				Editable = false,
				Bezeled = false,
				StringValue = label
			};
			this.stackView.AddView (item.View, NSStackViewGravity.Leading);
		}

		public string GetLabel (int segment)
		{
			return this.segments[segment].Label;
		}

		public int SegmentCount
		{
			get => this.segments.Length;
			set
			{
				this.segments = new SegmentItem[value];
			}
		}
	}

	internal class UnderlinedTabViewController<TViewModel> : NotifyingTabViewController<TViewModel> where TViewModel : NotifyingObject
	{
		public override void NumberOfItemsChanged (NSTabView tabView)
		{
			base.NumberOfItemsChanged (tabView);
			var items = this.tabStack.Views.ToList ();
			foreach (var view in items) {
				this.tabStack.RemoveView (view);
				view.Dispose ();
			}
			var i = 0;
			foreach (var item in TabViewItems) {
				if (item.Image != null) {
					this.tabStack.AddView (new UnderlinedImageView (item.Image.Name) {
						Selected = i == SelectedTabViewItemIndex,
						ToolTip = item.Label,
					}, NSStackViewGravity.Leading);
				} else {
					this.tabStack.AddView (new UnderlinedTextField () {
						BackgroundColor = NSColor.Clear,
						Editable = false,
						Bezeled = false,
						StringValue = item.Label,
						Selected = i == SelectedTabViewItemIndex,
					}, NSStackViewGravity.Leading);
				}
				i++;
			}
		}

		private NSStackView outerStack;
		private NSStackView innerStack;
		private NSStackView tabStack = new NSStackView () {
			Spacing = 4f,
		};

		private NSEdgeInsets edgeInsets = new NSEdgeInsets (0, 0, 0, 0);
		public NSEdgeInsets EdgeInsets {
			get => this.edgeInsets;
			set {
				this.edgeInsets = value;
				if (this.outerStack != null) {
					this.outerStack.EdgeInsets = value;
					this.innerStack.EdgeInsets = value;
				}
			}
		}

		public override void MouseDown (NSEvent theEvent)
		{
			NSView hit = View.HitTest (View.Superview.ConvertPointFromView (theEvent.LocationInWindow, null));
			if (!(hit is IUnderliningTabView))
				return;

			int i = 0;
			foreach (var label in tabStack.Views) {
				if (hit == label) {
					SelectedTabViewItemIndex = i;
					break;
				}
				i++;
			}
		}

		public override void DidSelect (NSTabView tabView, NSTabViewItem item)
		{
			base.DidSelect (tabView, item);
			for (int i = 0; i < this.tabStack.Views.Length; i++) {
				var tabItem = this.tabStack.Views[i] as IUnderliningTabView;
				if (tabItem != null)
					tabItem.Selected = SelectedTabViewItemIndex == i;
			}
		}

		public override void LoadView ()
		{
			this.outerStack = new NSStackView () {
				Orientation = NSUserInterfaceLayoutOrientation.Horizontal,
				EdgeInsets = EdgeInsets
			};

			this.innerStack = new NSStackView () {
				Spacing = 0,
				Alignment = NSLayoutAttribute.Left,
				Orientation = NSUserInterfaceLayoutOrientation.Vertical,
				EdgeInsets = EdgeInsets
			};

			this.outerStack.AddView (this.innerStack, NSStackViewGravity.Leading);
			this.innerStack.AddView (this.tabStack, NSStackViewGravity.Top);
			this.innerStack.AddView (TabView, NSStackViewGravity.Bottom);
			View = this.outerStack;
		}
	}
}