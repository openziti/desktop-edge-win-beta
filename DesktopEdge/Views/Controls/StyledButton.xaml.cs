﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZitiDesktopEdge {
	/// <summary>
	/// Interaction logic for StyledButton.xaml
	/// </summary>
	public partial class StyledButton : UserControl {

		public delegate void ClickAction();
		public event ClickAction OnClick;
		private string _label = "";
		private string bgColor = "#0069FF";

		public string BgColor {
			get { return bgColor; }
			set { 
				bgColor = value; 
				ButtonBgColor.Color = (Color)ColorConverter.ConvertFromString(bgColor);
			}
		}

		public string Label {
			get {
				return _label;
			}
			set {
				this._label = value;
				ButtonLabel.Content = this._label;
			}
		}

		public StyledButton() {
			InitializeComponent();
		}

		/// <summary>
		/// When the button area is entered slowly make it slightly opaque
		/// </summary>
		/// <param name="sender">The button object</param>
		/// <param name="e">The mouse event</param>
		private void Hover(object sender, MouseEventArgs e) {
			ButtonBgDarken.Opacity = 0.0;
			ButtonBgDarken.BeginAnimation(Grid.OpacityProperty, new DoubleAnimation(0.2, TimeSpan.FromSeconds(.3)));
		}

		/// <summary>
		/// When the mouse leaves the button ara snap the opacity back to full
		/// </summary>
		/// <param name="sender">The button object</param>
		/// <param name="e">The mouse event</param>
		private void Leave(object sender, MouseEventArgs e) {
			ButtonBgDarken.Opacity = 0.2;
			ButtonBgDarken.BeginAnimation(Grid.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromSeconds(.3)));
		}

		/// <summary>
		/// Change the color to visualize a click event
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Down(object sender, MouseButtonEventArgs e) {
			// ButtonBgColor.Color = Color.FromRgb(126, 180, 255);
		}

		/// <summary>
		///  Execute the click operation
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void DoClick(object sender, MouseButtonEventArgs e) {
			this.OnClick?.Invoke();
		}
	}
}
