﻿using System.Collections.Generic;

namespace EnterpriseWebLibrary.EnterpriseWebFramework {
	/// <summary>
	/// A paragraph. See https://html.spec.whatwg.org/multipage/dom.html#paragraph.
	/// </summary>
	public class Paragraph: FlowComponent {
		private readonly IReadOnlyCollection<DisplayableElement> children;

		/// <summary>
		/// Creates a paragraph.
		/// </summary>
		/// <param name="content"></param>
		/// <param name="displaySetup"></param>
		/// <param name="classes">The classes on the element.</param>
		/// <param name="etherealChildren"></param>
		public Paragraph(
			IReadOnlyCollection<PhrasingComponent> content, DisplaySetup displaySetup = null, ElementClassSet classes = null,
			IReadOnlyCollection<EtherealComponent> etherealChildren = null ) {
			children = new DisplayableElement(
				context => new DisplayableElementData(
					displaySetup,
					() => new DisplayableElementLocalData( "p" ),
					classes: classes,
					children: content,
					etherealChildren: etherealChildren ) ).ToCollection();
		}

		IEnumerable<FlowComponentOrNode> FlowComponent.GetChildren() {
			return children;
		}
	}
}