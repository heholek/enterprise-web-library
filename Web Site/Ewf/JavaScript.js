// This supports the DisplayLinking subsystem.
function setElementDisplay( id, visible ) {
	if( visible )
		$( '#' + id ).show();
	else
		$( '#' + id ).hide();
	// This forces IE8 to redraw the page, fixing an issue with nested display linking.
	var body = $( 'body' );
	body.attr( 'class', body.attr( 'class' ) );
}

function toggleElementDisplay( id ) {
	setElementDisplay( id, !$( '#' + id ).is( ":visible" ) );
}

function getClientUtcOffset( id ) {
	var utcOffset = $get( id );
	var timeString = new Date().toUTCString();
	utcOffset.value = timeString;
}

// Supports DurationPicker
// Formats numbers entered in the textbox to HH:MM and prevents input out of the range of TimeSpan.
var maxValueLength = 6;

function ApplyTimeSpanFormat( field ) {
	// Turn the string HHHH:MM into an an array of { H, H, H, H, M, M }
	var digits = field.value.replace( ":", "" ).split( "" );

	// Don't allow the minutes to be greater than 59
	if( digits.length > 1 && digits[digits.length - 2] > 5 ) {
		digits[digits.length - 2] = 5;
		digits[digits.length - 1] = 9;
	}

	// Turn the string in the text box to the HHHH:MM format.
	var timeTextValue = ""; // The new text box value
	var timeValueIndex = digits.length - 1; // The greatest index is the right-most digit
	for( var i = 0; i < maxValueLength; i++ ) {
		if( i == 2 ) // Insert the hour-minute separator
			timeTextValue = ":" + timeTextValue;
		timeTextValue = ( timeValueIndex >= 0 ? digits[timeValueIndex--] : "0" ) + timeTextValue;
		field.value = timeTextValue;
	}
}

// Returns true if...
// Key pressed is a command or function key
// There is a selection or Length is <= maxValueLength and
// Key pressed is numerical

function NumericalOnly( evt, field ) {

	if( evt.ctrlKey || evt.altKey )
		return true;

	var charCode = ( evt.which || evt.which == 0 ) ? evt.which : evt.keyCode;
	switch( charCode ) {
	//Enter
	case 13:
		ApplyTimeSpanFormat( field );
	//Backspace
	case 8:
	// Keys that don't produce a character
	case 0:
		return true;
	default:
			// Max of maxValueLength digits, numbers only.
			// If some of the field is selected, let them replace the contents even if it's full
		return ( $( field ).getSelection().text != "" || field.value.length < maxValueLength ) && ( 48 <= charCode && charCode <= 57 );
	}
}

//This function gets called by jQuery's on-document-ready event. This will run the following code after the page has loaded.

function OnDocumentReady() {
	SetupTextBoxFocus();
	RemoveClickScriptBinding();
}

//Finds all EwfTextBoxes and appends onfocus and onblur events to apply focus CSS styles to their parent.

function SetupTextBoxFocus() {
	var textBoxWrapperFocused = "textBoxWrapperFocused";
	var textBoxWrapperInput = ".textBoxWrapper > input";

	var setFocusedClass = function() {
		$( this.parentNode ).addClass( textBoxWrapperFocused );
	};

	$( textBoxWrapperInput ).focus( setFocusedClass ).blur(
		function() {
			$( this.parentNode ).removeClass( textBoxWrapperFocused );
		}
	);
	// Textboxes with focus on load
	$( textBoxWrapperInput + ":focus" ).each( setFocusedClass );
}

//Used for dynamic tables
//Finds ewfClickable rows that are also selectable, altering the JavaScript
//to allow them to be clickable without firing when selected.

function RemoveClickScriptBinding() {
	//Clickable Rows
	$( "tr.ewfClickable" ).each(
		function() {
			//If this row doesn't contain notClickables, don't bother it
			if( $( this ).children( ".ewfNotClickable" ).length == 0 )
				return;
			//Grab the clickscript we want to apply
			var clickScript = new Function( $( this ).attr( "onclick" ) );
			//Unbind it from the row
			$( this ).removeAttr( "onclick" );
			//For each td
			$( this ).children( ":not(.ewfNotClickable)" ).click( clickScript );
		}
	);
}

function postBackRequestStarted() {
	// see http://stackoverflow.com/a/9924844/35349
	for( var i in CKEDITOR.instances )
		CKEDITOR.instances[i].updateElement();

	$( ".ewfTimeOut" ).hide();
	$( ".ewfClickBlocker, .ewfProcessingDialog" ).fadeIn( 0 );
	setTimeout( '$(".ewfTimeOut").fadeIn(0);', 10000 );
}

function stopPostBackRequest() {
	hideProcessingDialog();
	if( window.stop )
		window.stop(); // Firefox
	else
		document.execCommand( 'Stop' ); // IE
}

function hideProcessingDialog() {
	$( ".ewfClickBlocker, .ewfProcessingDialog" ).hide();
}

function fadeOutStatusMessageDialog( duration ) {
	$( ".ewfStatusMessageDialog" ).fadeOut( duration );
}


/* These methods support the Checklist Control */

function changeCheckBoxColor( checkBox ) {
	var checkBoxParentDiv = $( checkBox ).parents( '.ewfBlockCheckBox' ).first().parent();
	var selectedCheckBoxClass = 'checkedChecklistCheckboxDiv';
	if( $( checkBox ).attr( 'checked' ) )
		checkBoxParentDiv.addClass( selectedCheckBoxClass );
	else
		checkBoxParentDiv.removeClass( selectedCheckBoxClass );
}

// Toggles the given checklist boxes based on the text of the link clicked

function toggleCheckBoxes( checklistClientId, setChecked ) {
	$( '#' + checklistClientId ).find( 'input[type=checkbox]' ).attr( 'checked', setChecked ).each( function( i, checkBox ) {
		changeCheckBoxColor( checkBox );
	} );
}


// Supports ModalWindow
// Adds a function to center the calling window when the user resizes or scrolls the page

function HookUpModalWindowMoveEventHandlers( radWindowClientId ) {
	var fixWindowPosition = function() {
		var window = $find( radWindowClientId );
		if( window.isVisible() )
			window.center();
	};
	$( window ).scroll( fixWindowPosition ).resize( fixWindowPosition );
}