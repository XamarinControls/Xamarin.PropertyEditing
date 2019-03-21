using System;
using System.Collections.Generic;
using AppKit;
using CoreGraphics;
using Foundation;
using Xamarin.PropertyEditing.ViewModels;

namespace Xamarin.PropertyEditing.Mac
{
	internal class BindingPathSelectorControl 
		: NotifyingView<CreateBindingViewModel>
	{
		private PathOutlineView pathOutlineView;
		internal const string PathSelectorColumnColId = "PathSelectorColumn";

        public NSTextField CustomPath { get; }

		private const float HorizontalCustomOffSet = 30.5f;

		private HeaderView pathHeader;
		private NSView pathBox;

		public BindingPathSelectorControl (CreateBindingViewModel viewModel)
		{
			if (viewModel == null)
				throw new ArgumentNullException (nameof (viewModel));

			ViewModel = viewModel;

			TranslatesAutoresizingMaskIntoConstraints = false;

			this.pathBox = new NSView {
				TranslatesAutoresizingMaskIntoConstraints = false,
				WantsLayer = true,

				// Layer out of alphabetical order so that WantsLayer creates the layer first
				Layer = {
					CornerRadius = 1.0f,
					BorderColor = new CGColor (.5f, .5f, .5f, 1.0f),
					BorderWidth = 1,
				},
			};

			AddSubview (this.pathBox);

			this.pathHeader = new HeaderView {
				Title = Properties.Resources.Path,
			};
			this.pathHeader.HorizonalTitleOffset = -HorizontalCustomOffSet;

			this.pathBox.AddSubview (this.pathHeader);

			this.pathBox.AddConstraints (new[] {
				NSLayoutConstraint.Create (this.pathHeader, NSLayoutAttribute.Top, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Top, 1f, 0f),
				NSLayoutConstraint.Create (this.pathHeader, NSLayoutAttribute.Left, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Left, 1f, 0f),
				NSLayoutConstraint.Create (this.pathHeader, NSLayoutAttribute.Width, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Width, 1f, 0f),
				NSLayoutConstraint.Create (this.pathHeader, NSLayoutAttribute.Height, NSLayoutRelation.Equal, 1f, BindingEditorWindow.HeaderHeight),
			});

			var customCheckBox = new NSButton {
				ControlSize = NSControlSize.Small,
				Font = NSFont.FromFontName (PropertyEditorControl.DefaultFontName, PropertyEditorControl.DefaultFontSize),
				Title = Properties.Resources.Custom,
				TranslatesAutoresizingMaskIntoConstraints = false,
			};

			customCheckBox.SetButtonType (NSButtonType.Switch);

			this.pathHeader.AddSubview (customCheckBox);
			this.pathHeader.AddConstraints (new[] {
				NSLayoutConstraint.Create (customCheckBox, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, this.pathHeader, NSLayoutAttribute.CenterX, 1, HorizontalCustomOffSet),
				NSLayoutConstraint.Create (customCheckBox, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal, this.pathHeader, NSLayoutAttribute.CenterY, 1, 0f),
			});

			this.CustomPath = new NSTextField {
				ControlSize = NSControlSize.Regular,
				Enabled = false,
				TranslatesAutoresizingMaskIntoConstraints = false,
			};

			var customPathHeightConstraint = NSLayoutConstraint.Create (this.CustomPath, NSLayoutAttribute.Height, NSLayoutRelation.Equal, 1f, 0);

			this.pathBox.AddSubview (this.CustomPath);
			this.pathBox.AddConstraints (new[] {
				NSLayoutConstraint.Create (this.CustomPath, NSLayoutAttribute.Top, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Top, 1f, 28f),
				NSLayoutConstraint.Create (this.CustomPath, NSLayoutAttribute.Width, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Width, 1f, 0f),
				NSLayoutConstraint.Create (this.CustomPath, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.CenterX, 1, 0),
				customPathHeightConstraint,
			});

			// create a table view and a scroll view
			var outlineViewContainer = new NSScrollView {
				TranslatesAutoresizingMaskIntoConstraints = false,
			};

			customCheckBox.Activated += (sender, e) => {
				this.CustomPath.Enabled = customCheckBox.State == NSCellStateValue.On;
				customPathHeightConstraint.Constant = this.CustomPath.Enabled ? 22 : 0;
			};

			this.CustomPath.Changed += (sender, e) => {
				viewModel.Path = this.CustomPath.StringValue;
			};

			this.pathOutlineView = new PathOutlineView {

			};

			this.pathOutlineView.Activated += OnPathOutlineViewSelected;

			var pathColumn = new NSTableColumn (PathSelectorColumnColId);
			this.pathOutlineView.AddColumn (pathColumn);

			// Set OutlineTableColumn or the arrows showing children/expansion will not be drawn
			this.pathOutlineView.OutlineTableColumn = pathColumn;

			// add the panel to the window
			outlineViewContainer.DocumentView = this.pathOutlineView;

			this.pathBox.AddSubview (outlineViewContainer);

			this.pathBox.AddConstraints (new[] {
				NSLayoutConstraint.Create (outlineViewContainer, NSLayoutAttribute.Top, NSLayoutRelation.Equal, this.CustomPath, NSLayoutAttribute.Bottom, 1f, 0f),
				NSLayoutConstraint.Create (outlineViewContainer, NSLayoutAttribute.Width, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Width, 1f, 0f),
				NSLayoutConstraint.Create (outlineViewContainer, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.CenterX, 1, 0),
				NSLayoutConstraint.Create (outlineViewContainer, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, this.pathBox, NSLayoutAttribute.Bottom, 1f, 0f),
			});

			AddConstraints (new[] {
				NSLayoutConstraint.Create (this.pathBox, NSLayoutAttribute.Top, NSLayoutRelation.Equal, this, NSLayoutAttribute.Top, 1f, 0f),
				NSLayoutConstraint.Create (this.pathBox, NSLayoutAttribute.Left, NSLayoutRelation.Equal, this, NSLayoutAttribute.Left, 1f, 0f),
				NSLayoutConstraint.Create (this.pathBox, NSLayoutAttribute.Height, NSLayoutRelation.Equal, this, NSLayoutAttribute.Height, 1f, 0f),
				NSLayoutConstraint.Create (this.pathBox, NSLayoutAttribute.Width, NSLayoutRelation.Equal, this, NSLayoutAttribute.Width, 1f, 0f),
			});

			viewModel.PropertyChanged += OnPropertyChanged;
		}

		public override async void OnPropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof (CreateBindingViewModel.PropertyRoot)) {
				if (ViewModel.PropertyRoot != null) {
					this.pathOutlineView.PropertyTreeRoot = await ViewModel.PropertyRoot.Task;
				} else {
					this.pathOutlineView.PropertyTreeRoot = null;
				}
			}

			if (e.PropertyName == nameof (CreateBindingViewModel.Path)) {
				this.CustomPath.StringValue = ViewModel.Path ?? string.Empty;
			}
		}

		private void OnPathOutlineViewSelected (object sender, EventArgs e)
		{
			if (sender is PathOutlineView pov) {
				if (pov.SelectedRow != -1) {
					if (pov.ItemAtRow (pov.SelectedRow) is NSObjectFacade facade) {
						switch (facade.Target) {
						case PropertyTreeElement propertyTreeElement:
							ViewModel.SelectedPropertyElement = propertyTreeElement;
							break;

						default:
							break;
						}
					}
				}
			}
		}

	}
}
