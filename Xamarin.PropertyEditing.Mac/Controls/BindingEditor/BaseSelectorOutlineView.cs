using System;
using AppKit;
using Foundation;

namespace Xamarin.PropertyEditing.Mac
{
	public class BaseSelectorOutlineView : NSOutlineView
	{
		public BaseSelectorOutlineView ()
		{
			Initialize ();
		}

		// Called when created from unmanaged code
		public BaseSelectorOutlineView (IntPtr handle) : base (handle)
		{
			Initialize ();
		}

		// Called when created directly from a XIB file
		[Export ("initWithCoder:")]
		public BaseSelectorOutlineView (NSCoder coder) : base (coder)
		{
			Initialize ();
		}

		public override bool ValidateProposedFirstResponder (NSResponder responder, NSEvent forEvent)
		{
			return true;
		}

		private void Initialize ()
		{
			HeaderView = null;
			TranslatesAutoresizingMaskIntoConstraints = false;
		}
	}
}
