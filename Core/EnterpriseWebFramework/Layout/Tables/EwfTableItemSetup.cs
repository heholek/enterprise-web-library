﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EnterpriseWebLibrary.EnterpriseWebFramework {
	/// <summary>
	///  The configuration for an item in a table. Options specified on individual cells take precedence over equivalent options specified here.
	/// </summary>
	public class EwfTableItemSetup {
		internal readonly EwfTableFieldOrItemSetup FieldOrItemSetup;
		internal readonly ReadOnlyCollection<Tuple<string, Action>> TableLevelItemActions;
		internal readonly ReadOnlyCollection<Tuple<string, Action>> GroupLevelItemActions;
		internal readonly int? RankId;

		/// <summary>
		/// Creates an item setup object.
		/// </summary>
		/// <param name="classes">The classes. When used on a column, sets the classes on every cell since most styles don't work on col elements.</param>
		/// <param name="size">The height or width. For an EWF table, this is the row height. For a column primary table, this is the column width. If you specify
		/// percentage widths for some or all columns in a table, these values need not add up to 100; they will be automatically scaled if necessary. The automatic
		/// scaling will not happen if there are any columns without a specified width.</param>
		/// <param name="textAlignment">The text alignment of the cells in this item.</param>
		/// <param name="verticalAlignment">The vertical alignment of the cells in this item.</param>
		/// <param name="activationBehavior">The activation behavior.</param>
		/// <param name="tableLevelItemActions">The list of table level item actions. Each item must have either a null list or a list with the same actions in the
		/// same order as all other items in the table.</param>
		/// <param name="groupLevelItemActions">The list of group level item actions. Each item must have either a null list or a list with the same actions in the
		/// same order as all other items in the group.</param>
		/// <param name="rankId">
		/// The rank ID for this item. Swapping will be enabled for all items that have a non null rank ID. Setting this on at least one item of a table adds a
		/// column on the right of the table containing controls to move each item up or down the list. This consumes a small amount of table width.
		/// </param>
		// NOTE: Change the Tuples to named types here.
		public EwfTableItemSetup(
			ElementClassSet classes = null, CssLength size = null, TextAlignment textAlignment = TextAlignment.NotSpecified,
			TableCellVerticalAlignment verticalAlignment = TableCellVerticalAlignment.NotSpecified, ElementActivationBehavior activationBehavior = null,
			IEnumerable<Tuple<string, Action>> tableLevelItemActions = null, IEnumerable<Tuple<string, Action>> groupLevelItemActions = null, int? rankId = null ) {
			FieldOrItemSetup = new EwfTableFieldOrItemSetup( classes, size, textAlignment, verticalAlignment, activationBehavior );

			if( tableLevelItemActions != null ) {
				var tableLevelItemActionList = tableLevelItemActions.ToList();
				if( !tableLevelItemActionList.Any() )
					throw new ApplicationException();
				TableLevelItemActions = tableLevelItemActionList.AsReadOnly();
			}

			if( groupLevelItemActions != null ) {
				var groupLevelItemActionList = groupLevelItemActions.ToList();
				if( !groupLevelItemActionList.Any() )
					throw new ApplicationException();
				GroupLevelItemActions = groupLevelItemActionList.AsReadOnly();
			}

			RankId = rankId;
		}
	}
}