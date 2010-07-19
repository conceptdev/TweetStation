// Copyright 2010 Miguel de Icaza
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE  SOFTWARE.

//
// TODO:
//
//   * Menu needs to be cancelled when items are added
//

#if true || SWIPE_SUPPORT

using System;
using MonoTouch.ObjCRuntime;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;
using System.Runtime.InteropServices;

namespace TweetStation
{
	public partial class BaseTimelineViewController
	{
		
		public override UITableView MakeTableView (RectangleF bounds, UITableViewStyle style)
		{
			return new SwipeDetectingTableView (bounds, style, this);
		}

		internal class SwipeDetectingTableView : UITableView {
			BaseTimelineViewController container;
			bool swipeDetectionDisabled;
			PointF? touchStart;
			
			public SwipeDetectingTableView (RectangleF bounds, UITableViewStyle style, BaseTimelineViewController container)
				: base (bounds, style)
			{
				this.container = container;
			}
			
			class TouchEvent {
				public NSSet Touches;
				public UIEvent Event;
				
				public TouchEvent (NSSet touches, UIEvent evt)
				{
					Touches = touches;
					Event = evt;
				}
			}
			
			TouchEvent capturedEnded;
			
			public override void TouchesBegan (NSSet touches, UIEvent evt)
			{
				var touch = touches.AnyObject as UITouch;
				touchStart = touch.LocationInView (this);

				// If the menu is not active
				if (container.MenuHostElement == null || container.MenuHostElement != container.TweetElementFromPath (IndexPathForRowAtPoint (touchStart.Value))){
					if (container.CancelMenu ()){
						if (capturedEnded != null){
							TouchesCancelled (capturedEnded.Touches, capturedEnded.Event);
							capturedEnded = null;
						}
					}
					swipeDetectionDisabled = false;
				}

				base.TouchesBegan (touches, evt);
			}
			
			public override void TouchesMoved (NSSet touches, UIEvent evt)
			{
				if (!swipeDetectionDisabled){
					if (container.MenuHostElement == null){
						if (touchStart != null){
							var touch = touches.AnyObject as UITouch;
							var currentPos = touch.LocationInView (this);
							var deltaX = Math.Abs (touchStart.Value.X - currentPos.X);
							var deltaY = Math.Abs (touchStart.Value.Y - currentPos.Y);
							
							if (deltaY < 10 && deltaX > 16){
								var menuPath = IndexPathForRowAtPoint (currentPos);
								var cell = CellAt (menuPath);
								
								container.OnSwipe (menuPath, cell);
								swipeDetectionDisabled = true;
								touchStart = null;
								return;
							}
						}
					}
				}
				base.TouchesMoved (touches, evt);
			}
			
			public override void TouchesEnded (NSSet touches, UIEvent evt)
			{
				if (container.MenuHostElement != null){
					if (capturedEnded == null)
						capturedEnded = new TouchEvent (touches, evt);
					container.TouchesEnded (touches, evt);
					return;
				}
				
				if (swipeDetectionDisabled)
					return;
				
				if (container.DisableSelection)
					return;
				
				base.TouchesEnded (touches, evt);
				touchStart = null;
			}
			
			public override void TouchesCancelled (NSSet touches, UIEvent evt)
			{
				base.TouchesCancelled (touches, evt);
			}
		}

		// The element that is hosting the swipe action
		TweetElement MenuHostElement;
		
		// Animation delay for the swipe
		const double globalDelay = 0.3;
		const double hideDelay = 0.5;
		
		// The current UIVIew that shows the menu
		UIView currentMenuView;
		
		// The cell where the menu is shown
		UITableViewCell menuCell;
	
		static void Move (UIView view, float xoffset)
		{
			var frame = view.Frame;
			frame.Offset (xoffset, 0);
			view.Frame = frame;
		}
		
		void ShowMenu (UIView menuView, UITableViewCell cell)
		{
			HideMenu ();
			
			var ip = TableView.IndexPathForCell (cell);
			if (ip != null)
				TableView.DeselectRow (ip, false);
			
			DisableSelection = true;
			float offset = cell.ContentView.Frame.Width;

			currentMenuView = menuView;
			menuCell = cell;
			
			cell.ContentView.InsertSubview (menuView, 0);

			UIView.BeginAnimations ("Foo");
			UIView.SetAnimationDuration (globalDelay);
			UIView.SetAnimationCurve (UIViewAnimationCurve.EaseIn);
			Move (cell.SelectedBackgroundView, offset);
			foreach (var view in cell.ContentView.Subviews){
				if (view == menuView)
					continue;
				Move (view, offset);
				
				// Ok this is a gross hack that deals with the fact that the
				// selection is *partially* there, but if we insert our
				// animated layer *below* the selection, the animation looks
				// *horrible*, it seems like something in the selection background
				// layer interferes with the animation of our layer 
				// if our animation layer sits *below* the background layer
				//
				// This changes the transparent background that the UITableView
				// sets on the cell during selection to a solid background.
				
				// Possible alternative: "grow" the menu Frame as the menu
				// gets swiped out
				view.BackgroundColor = UIColor.White;
			}
			UIView.CommitAnimations ();
		}

		void AnimateBack (UIView view, CAAnimation animation)
		{
			var b = view.Bounds;
			view.Layer.Position = new PointF (b.Width/2, b.Height/2);
			view.Layer.AddAnimation (animation, "position");
		}
		
		bool HideMenu ()
		{
			if (menuCell == null)
				return false;
			
			float offset = menuCell.ContentView.Frame.Width;
			
			// Pass the currentMenuView as the view to remove, as it is a global that can be overwritten by
			// another menu showing up.
			var copy = currentMenuView;
			var gch = GCHandle.Alloc (copy);
			UIView.BeginAnimations ("Foo", GCHandle.ToIntPtr (gch));
			UIView.SetAnimationDuration (hideDelay);
			UIView.SetAnimationDidStopSelector (new Selector ("animationDidStop:finished:context:"));
			UIView.SetAnimationDelegate (this);
			UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);			
			
			var animation = MakeBounceAnimation (Math.Abs (offset));
			
			menuCell.Selected = false;
			foreach (var view in menuCell.ContentView.Subviews){
				if (view == currentMenuView)
					continue;

				view.SetNeedsDisplay ();
				AnimateBack (view, animation);
			}
			AnimateBack (menuCell.SelectedBackgroundView, animation);

			UIView.CommitAnimations ();
			menuCell = null;
			DisableSelection = false;
			MenuHostElement = null;
			currentMenuView = null;
			return true;
		}
		
		CAAnimation MakeBounceAnimation (float offset)
		{
			var animation = (CAKeyFrameAnimation) CAKeyFrameAnimation.FromKeyPath ("position.x");
			
			animation.Duration = hideDelay;
			float left = offset/2;
			animation.Values = new NSNumber [] {
				NSNumber.FromFloat (offset),
				//NSNumber.FromFloat (left-60),
				//NSNumber.FromFloat (left+40),
				NSNumber.FromFloat (left-30),
				NSNumber.FromFloat (left+10),
				NSNumber.FromFloat (left-10),
				NSNumber.FromFloat (left),
			};
			
			return animation;
		}
		
		[Export ("animationDidStop:finished:context:")]
		[Preserve]
		public void HideFinished (string name, NSNumber numFinished, IntPtr context)
		{
			GCHandle h = GCHandle.FromIntPtr (context);
			var view = (UIView) h.Target;
			view.RemoveFromSuperview ();
			h.Free ();
		}

		internal class SwipeMenuView : UIView {
			static UIImage texture;
			static UIColor textureColor;
			BaseTimelineViewController parent;
			CALayer [] layers;
			UIImage [] images;
			
			CALayer MakeBackgroundLayer (RectangleF frame)
			{
				if (textureColor == null){
 					texture = UIImage.FromBundle ("Images/texture.png");
					textureColor = UIColor.FromPatternImage (texture);
				}

				BeginImageContext (frame.Size);
				var c = UIGraphics.GetCurrentContext ();
				
				texture.DrawAsPatternInRect (frame);

				var shadow = UIImage.FromBundle ("Images/menu-shadow.png");
				shadow.Draw (frame);
				var result = UIGraphics.GetImageFromCurrentImageContext ();
				
				UIGraphics.EndImageContext ();

				var back = new CALayer (){
					Frame = frame
				};
				Graphics.ConfigLayerHighRes (back);
				back.Contents = result.CGImage;
				return back;
			}
			
			internal SwipeMenuView (BaseTimelineViewController parent, UIImage [] images, RectangleF frame) : base (frame)
			{
				this.parent = parent;
				this.images = images;

				Layer.AddSublayer (MakeBackgroundLayer (frame));

				layers = new CALayer [images.Length];
				
				float slotsize = frame.Width/layers.Length;
				double delay = globalDelay; 
				for (int i = 0; i < layers.Length; i++){
					var image = images [i];
					var layer = layers [i] = new CALayer ();
					Graphics.ConfigLayerHighRes (layer);					

					image = RenderImageWithShadow (image, 3, UIColor.Black);
					layer.Contents = image.CGImage;
					
					var alpha = (CABasicAnimation) CABasicAnimation.FromKeyPath ("opacity");
					alpha.From = new NSNumber (0);
					alpha.To = new NSNumber (1);
					alpha.BeginTime = delay/layers.Length*i;
					
					var size = (CAKeyFrameAnimation) CAKeyFrameAnimation.FromKeyPath ("transform.scale");
					size.Values = new NSNumber [] {
						NSNumber.FromFloat (0.8f),
						NSNumber.FromFloat (1.2f),
						NSNumber.FromFloat (1),
					};
					
					var group = CAAnimationGroup.CreateAnimation ();
					group.Animations = new CAAnimation [] { alpha, size };
					group.Duration = delay; 
					
					layer.AddAnimation (group, "showup");					
					
					layer.Frame = new RectangleF (
						(int) (slotsize*i+image.Size.Width/2), 
						(int) (frame.Height-image.Size.Height)/2, 
						image.Size.Width, image.Size.Height);

					Layer.AddSublayer (layer);
				}
			}
			
			// 
			// Temporary prototype, published MonoTouch has a bug.
			[DllImport (MonoTouch.Constants.UIKitLibrary, EntryPoint="UIGraphicsBeginImageContextWithOptions")]
			public extern static void BeginImageContextWithOptions (SizeF size, bool opaque, float scale);
			
			void BeginImageContext (SizeF size)
			{
				if (Graphics.HighRes)
					BeginImageContextWithOptions (size, false, 0);
				else
					UIGraphics.BeginImageContext (size);
			}
			
			UIImage RenderImageWithShadow (UIImage image, float radius, UIColor color)
			{
				var size = new SizeF (image.Size.Width+8, image.Size.Height+8);
				
				BeginImageContext (size);
				var ctx = UIGraphics.GetCurrentContext ();

				ctx.SaveState ();
				ctx.SetShadowWithColor (new SizeF (1, 1), radius, color.CGColor);
				image.Draw (new PointF (4, 4));
				ctx.RestoreState ();

				image.Draw (new PointF (4, 4));
				
				image = UIGraphics.GetImageFromCurrentImageContext ();

				UIGraphics.EndImageContext ();
				
				return image;
			}
			
			CALayer cover;
			int selected;
			
			void StopTracking ()
			{
				if (selected == -1)
					return;
				
				layers [selected].BorderWidth = 0;
				selected = -1;
				cover.RemoveFromSuperLayer ();
				cover = null;
			}
			
			void HighlightSelection ()
			{
				if (cover == null){
					cover = new CALayer () {
						Frame = new RectangleF (new PointF (0, 0), layers [selected].Frame.Size),
					};
					cover.Contents = RenderImageWithShadow (images [selected], 4, UIColor.White).CGImage;
				}
				layers [selected].AddSublayer (cover);
			}

			void RemoveHighlight ()
			{
				if (cover == null)
					return;
				cover.RemoveFromSuperLayer ();
			}
			
			int ItemFromEvent (NSSet touches)
			{
				var touch = touches.AnyObject as UITouch;
				var location = touch.LocationInView (this);
				
				if (location.Y < 0 || location.Y > Frame.Height)
					return -1;
				
				return (int) (location.X / (Frame.Width / images.Length));
			}
			
			public override void TouchesBegan (NSSet touches, UIEvent evt)
			{
				base.TouchesBegan (touches, evt);
				
				selected = ItemFromEvent (touches);
				HighlightSelection ();
			}
			
			public event Action<int> Selected;
			
			public override void TouchesEnded (NSSet touches, UIEvent evt)
			{
				base.TouchesEnded (touches, evt);

				bool currentlySelected = cover.SuperLayer != null;
				int selIdx = selected;
				StopTracking ();
				
				if (currentlySelected){
					if (Selected != null)
						Selected (selIdx);
					parent.CancelMenu ();
				} 
			}
			
			public override void TouchesMoved (NSSet touches, UIEvent evt)
			{
				base.TouchesMoved (touches, evt);
				if (ItemFromEvent (touches) != selected)
					RemoveHighlight ();
				else if (cover.SuperLayer == null)
					HighlightSelection ();
			}
			
			public override void TouchesCancelled (NSSet touches, UIEvent evt)
			{
				base.TouchesCancelled (touches, evt);
				StopTracking ();
			}
		}
		
		TweetElement TweetElementFromPath (NSIndexPath path)
		{
			var e = Root [path.Section][path.Row];
			return e as TweetElement;
		}
		
		UIImage [] swipeMenuImages, onImages, offImages;
		
		internal virtual void OnSwipe (NSIndexPath path, UITableViewCell cell)
		{
			MenuHostElement = TweetElementFromPath (path);
			if (MenuHostElement != null){
				var frame = cell.ContentView.Frame;				
				TableView.ScrollEnabled = false;
				
				if (swipeMenuImages == null){
					swipeMenuImages = new UIImage [] {
						UIImage.FromBundle ("Images/swipe-reply.png"),
						UIImage.FromBundle ("Images/swipe-retweet.png"),
						UIImage.FromBundle ("Images/swipe-profile.png"),
						UIImage.FromBundle ("Images/swipe-star-off.png"),
						UIImage.FromBundle ("Images/swipe-star-on.png"),
					};

					onImages = new UIImage [] {
						swipeMenuImages [0], swipeMenuImages [1], swipeMenuImages [2], swipeMenuImages [4]
					};
					offImages = new UIImage [] {
						swipeMenuImages [0], swipeMenuImages [1], swipeMenuImages [2], swipeMenuImages [3]
					};
				}
				
				var menu = new SwipeMenuView (this, MenuHostElement.Tweet.Favorited ? onImages : offImages, frame);
				menu.Selected += idx => {
					switch (idx){
					case 0:
						AppDelegate.MainAppDelegate.Reply (this, MenuHostElement.Tweet);
						break;
						
					case 1:
						AppDelegate.MainAppDelegate.Retweet (this, MenuHostElement.Tweet);
						break;
						
					case 2:
						ActivateController (new FullProfileView (MenuHostElement.Tweet.UserId));
						break;
						
					case 3:
						AppDelegate.MainAppDelegate.ToggleFavorite (MenuHostElement.Tweet);
						break;
					}
				};
				ShowMenu (menu, cell);
			}
		}
		
		// Returns true if the menu was cancelled, false if there was no menu to cancel
		public virtual bool CancelMenu ()
		{
			TableView.ScrollEnabled = true;
			return HideMenu ();
		}
	}
}
#endif