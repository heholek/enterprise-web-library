﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using EnterpriseWebLibrary.EnterpriseWebFramework.Controls;
using EnterpriseWebLibrary.IO;
using Humanizer;
using JetBrains.Annotations;
using NodaTime;

namespace EnterpriseWebLibrary.EnterpriseWebFramework {
	/// <summary>
	/// A control capable of displaying chart data. Currently implemented with Chart.js.
	/// </summary>
	public sealed class Chart: WebControl, ControlWithJsInitLogic {
		internal class CssElementCreator: ControlCssElementCreator {
			internal const string CssClass = "ewfChart";

			IReadOnlyCollection<CssElement> ControlCssElementCreator.CreateCssElements() {
				return new[] { new CssElement( "Chart", "div." + CssClass ) };
			}
		}

		#region Chart.js configuration

		// ReSharper disable All

		/// <summary>
		/// Used for Line graphs.
		/// JSON object used to configure Chart.js.
		/// </summary>
		private class Dataset: BaseDataset {
			public readonly string pointStrokeColor = "#fff";
			public readonly string pointColor;

			public Dataset( Color color, IEnumerable<double> data ): base( color, data ) {
				pointColor = strokeColor;
			}
		}

		/// <summary>
		/// Used for Bar graphs.
		/// JSON object used to configure Chart.js.
		/// </summary>
		private class BaseDataset {
			private static string toRgbaString( Color color, string opacity ) {
				return string.Format( "rgba({0},{1},{2},{3})", color.R, color.G, color.B, opacity );
			}

			public readonly string fillColor;
			public readonly string strokeColor;
			public readonly IEnumerable<double> data;

			public BaseDataset( Color color, IEnumerable<double> data ) {
				fillColor = toRgbaString( color, "0.5" );
				strokeColor = toRgbaString( color, "1" );
				this.data = data;
			}
		}

		/// <summary>
		/// JSON object used to configure Chart.js.
		/// </summary>
		private class ChartData {
			public readonly IEnumerable<string> labels;
			public readonly IEnumerable<BaseDataset> datasets;

			public ChartData( IEnumerable<string> labels, IEnumerable<BaseDataset> datasets ) {
				this.labels = labels;
				this.datasets = datasets;
			}
		}

		private class BarOptions: OptionsBase {
			public bool barShowStroke = true;
			public int barStrokeWidth = 1;
			public int barValueSpacing = 5;
			public int barDatasetSpacing = 1;
		}

		private class LineOptions: OptionsBase {
			public bool bezierCurve = true;
			public bool pointDot = true;
			public int pointDotRadius = 3;
			public int pointDotStrokeWidth = 1;
			public bool datasetStroke = true;
			public int datasetStrokeWidth = 2;
			public bool datasetFill = true;
		}

		private class OptionsBase {
			public bool scaleOverlay = false;
			public bool scaleOverride = false;
			public int? scaleSteps = null;
			public int? scaleStepWidth = null;
			public int? scaleStartValue = null;
			public string scaleLineColor = "rgba(0,0,0,.1)";
			public int scaleLineWidth = 1;
			public bool scaleShowLabels = true;
			//public string scaleLabel = ""; // 'null' breaks it; it needs to be "undefined"
			public string scaleFontFamily = "'Arial'";
			public int scaleFontSize = 12;
			public string scaleFontStyle = "normal";
			public string scaleFontColor = "#666";
			public bool scaleShowGridLines = true;
			public string scaleGridLineColor = "rgba(0,0,0,.05)";
			public int scaleGridLineWidth = 1;
			public bool animation = true;
			public int animationSteps = 60;
			public string animationEasing = "easeOutQuart";
			public string onAnimationComplete = null;
		}

		// ReSharper restore All

		#endregion

		private readonly ChartSetup setup;
		private readonly Func<string> jsInitStatementGetter;

		/// <summary>
		/// Creates a chart displaying a supported <see cref="ChartType"/> with the given data. Includes a chart and a table, and allows exporting the data to CSV.
		/// </summary>
		/// <param name="setup">The setup object for the chart.</param>
		/// <param name="series">The data series.</param>
		/// <param name="color">The color to use for the data series.</param>
		public Chart( ChartSetup setup, DataSeries series, Color? color = null ): this( setup, series.ToCollection(), colors: color?.ToCollection() ) {}

		/// <summary>
		/// Creates a chart displaying a supported <see cref="ChartType"/> with the given data. Includes a chart and a table, and allows exporting the data to CSV.
		/// Assuming <paramref name="seriesCollection"/> has multiple elements, draws multiple sets of Y values on the same chart.
		/// </summary>
		/// <param name="setup">The setup object for the chart.</param>
		/// <param name="seriesCollection">The data series collection.</param>
		/// <param name="colors">The colors to use for the data series collection. Pass null for default colors. If you specify your own colors, the number of
		/// colors does not need to match the number of series. If you pass fewer colors than series, the chart will use random colors for the remaining series.
		/// </param>
		public Chart( ChartSetup setup, [ NotNull ] IEnumerable<DataSeries> seriesCollection, IEnumerable<Color> colors = null ) {
			seriesCollection = seriesCollection.ToArray();

			var rand = new Random();
			colors = ( colors ?? getDefaultColors() ).Take( seriesCollection.Count() )
				.Pad( seriesCollection.Count(), () => Color.FromArgb( rand.Next( 256 ), rand.Next( 256 ), rand.Next( 256 ) ) );

			this.setup = setup;

			CssClass = CssClass.ConcatenateWithSpace( CssElementCreator.CssClass );

			Func<DataSeries, Color, BaseDataset> datasetSelector;
			OptionsBase options;
			switch( setup.ChartType ) {
				case ChartType.Line:
					datasetSelector = ( series, color ) => new Dataset( color, series.Values.TakeLast( setup.MaxXValues ) );
					options = new LineOptions { bezierCurve = false };
					break;
				case ChartType.Bar:
					datasetSelector = ( series, color ) => new BaseDataset( color, series.Values.TakeLast( setup.MaxXValues ) );
					// ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
					options = new BarOptions {};
					break;
				default:
					throw new UnexpectedValueException( setup.ChartType );
			}

			var chartData = new ChartData(
				setup.Labels.TakeLast( setup.MaxXValues ),
				seriesCollection.Zip( colors, ( series, color ) => datasetSelector( series, color ) ).ToArray() );

			var canvas = new HtmlGenericControl( "canvas" );
			switch( setup.ChartType ) {
				case ChartType.Line:
				case ChartType.Bar:
					canvas.Attributes.Add( "height", "400" );
					break;
				default:
					throw new UnexpectedValueException( setup.ChartType );
			}
			Controls.Add( canvas );

			if( seriesCollection.Count() > 1 )
				this.AddControlsReturnThis(
					new Section(
							"Key",
							new LineList(
								chartData.datasets.Select(
									( dataset, i ) => (LineListItem)new TrustedHtmlString(
											"<div style='display: inline-block; vertical-align: middle; width: 20px; height: 20px; background-color: {0}; border: 1px solid {1};'>&nbsp;</div> {2}"
												.FormatWith( dataset.fillColor, dataset.strokeColor, seriesCollection.ElementAt( i ).Name ) ).ToComponent()
										.ToComponentListItem() ) ).ToCollection(),
							style: SectionStyle.Box ).ToCollection()
						.GetControls() );

			// Remove this when ColumnPrimaryTable supports Excel export.
			var headers = setup.XAxisTitle.ToCollection().Concat( seriesCollection.Select( v => v.Name ) );
			var tableData = new List<IEnumerable<object>>( seriesCollection.First().Values.Count() );
			for( var i = 0; i < tableData.Capacity; i++ ) {
				var i1 = i;
				tableData.Add( setup.Labels.ElementAt( i1 ).ToCollection().Concat( seriesCollection.Select( v => v.Values.ElementAt( i1 ).ToString() ) ) );
			}
			var exportAction = getExportAction( headers, tableData );

			var table = new ColumnPrimaryTable(
				tableActions: exportAction.ToCollection(),
				firstDataFieldIndex: 1,
				items: new EwfTableItem( from i in setup.XAxisTitle.ToCollection().Concat( setup.Labels ) select i.ToCell() ).ToCollection()
					.Concat(
						from series in seriesCollection select new EwfTableItem( series.Name.ToCell().Concat( from i in series.Values select i.ToString().ToCell() ) ) )
					.Materialize() );
			this.AddControlsReturnThis( table.ToCollection().GetControls() );

			jsInitStatementGetter = () => {
				using( var writer = new StringWriter() ) {
					writer.WriteLine( "var canvas = document.getElementById( '{0}' );".FormatWith( canvas.ClientID ) );
					writer.WriteLine( "canvas.width = $( canvas ).parent().width();" );
					writer.WriteLine( "new Chart( canvas.getContext( '2d' ) ).{0}( {1}, {2} );".FormatWith( setup.ChartType, chartData.ToJson(), options.ToJson() ) );
					return writer.ToString();
				}
			};
		}

		private IEnumerable<Color> getDefaultColors() {
			yield return Color.FromArgb( 120, 160, 195 );
			yield return Color.FromArgb( 255, 182, 149 );
			yield return Color.FromArgb( 170, 225, 149 );
			yield return Color.FromArgb( 255, 230, 149 );
		}

		private ActionComponentSetup getExportAction( IEnumerable<object> headers, List<IEnumerable<object>> tableData ) =>
			new ButtonSetup(
				"Export",
				behavior: new PostBackBehavior(
					postBack: PostBack.CreateFull(
						id: PostBack.GetCompositeId( setup.PostBackIdBase, "export" ),
						actionGetter: () => new PostBackAction(
							new PageReloadBehavior(
								secondaryResponse: new SecondaryResponse(
									() => EwfResponse.Create(
										ContentTypes.Csv,
										new EwfResponseBodyCreator(
											writer => {
												var csv = new CsvFileWriter();
												csv.AddValuesToLine( headers.ToArray() );
												csv.WriteCurrentLineToFile( writer );
												foreach( var td in tableData ) {
													csv.AddValuesToLine( td.ToArray() );
													csv.WriteCurrentLineToFile( writer );
												}
											} ),
										() => "{0} {1}".FormatWith(
											      setup.ExportFileName,
											      AppRequestState.RequestTime.InZone( DateTimeZoneProviders.Tzdb.GetSystemDefault() ).ToDateTimeUnspecified() ) +
										      FileExtensions.Csv ) ) ) ) ) ) );

		string ControlWithJsInitLogic.GetJsInitStatements() {
			return jsInitStatementGetter();
		}

		/// <summary>
		/// Returns the tag that represents this control in HTML.
		/// </summary>
		protected override HtmlTextWriterTag TagKey { get { return HtmlTextWriterTag.Div; } }
	}
}