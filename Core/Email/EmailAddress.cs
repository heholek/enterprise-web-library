﻿namespace EnterpriseWebLibrary.Email {
	/// <summary>
	/// An email address.
	/// </summary>
	public class EmailAddress {
		/// <summary>
		/// The email address.
		/// </summary>
		public readonly string Address;

		/// <summary>
		/// The friendly name that gets shown in email clients.
		/// </summary>
		public readonly string DisplayName;

		/// <summary>
		/// Creates an email address with the given address and display name.
		/// </summary>
		/// <param name="address">Do not pass null.</param>
		/// <param name="displayName">Do not pass null.</param>
		public EmailAddress( string address, string displayName ) {
			Address = address;
			DisplayName = displayName;
		}

		/// <summary>
		/// Creates an email address with the given address.
		/// </summary>
		/// <param name="address">Do not pass null.</param>
		public EmailAddress( string address ): this( address, address ) {}

		/// <summary>
		/// Converts this to a System.Net.Mail.MailAddress.
		/// </summary>
		public System.Net.Mail.MailAddress ToMailAddress() {
			if( DisplayName.Length > 0 )
				return new System.Net.Mail.MailAddress( Address, DisplayName );
			return new System.Net.Mail.MailAddress( Address );
		}
	}
}