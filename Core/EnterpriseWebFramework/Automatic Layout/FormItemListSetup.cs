﻿using System.Collections.Generic;
using System.Linq;

namespace EnterpriseWebLibrary.EnterpriseWebFramework {
	/// <summary>
	/// The general configuration for a form-item list.
	/// </summary>
	public class FormItemListSetup {
		internal readonly bool HideIfEmpty;
		internal readonly DisplaySetup DisplaySetup;
		internal readonly ElementClassSet Classes;
		internal readonly IReadOnlyCollection<PhrasingComponent> Button;

		/// <summary>
		/// Creates a form-item-list setup object.
		/// </summary>
		/// <param name="hideIfEmpty">Pass true if you want the list to hide itself if it has no items.</param>
		/// <param name="displaySetup"></param>
		/// <param name="classes">The classes on the list.</param>
		/// <param name="buttonSetup">Pass a value to have a button added as the last form item and formatted automatically.</param>
		/// <param name="enableSubmitButton">Pass true to enable the button to be a a submit button if possible.</param>
		public FormItemListSetup(
			bool hideIfEmpty = false, DisplaySetup displaySetup = null, ElementClassSet classes = null, ButtonSetup buttonSetup = null,
			bool enableSubmitButton = false ) {
			HideIfEmpty = hideIfEmpty;
			DisplaySetup = displaySetup;
			Classes = classes;
			Button = buttonSetup == null
				         ? Enumerable.Empty<PhrasingComponent>().Materialize()
				         : buttonSetup.GetActionComponent( null, ( text, icon ) => new StandardButtonStyle( text, icon: icon ), enableSubmitButton: enableSubmitButton )
					         .ToCollection();
		}
	}
}