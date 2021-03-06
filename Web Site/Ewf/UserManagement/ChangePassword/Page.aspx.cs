using EnterpriseWebLibrary.Encryption;
using EnterpriseWebLibrary.EnterpriseWebFramework.Ui;
using EnterpriseWebLibrary.EnterpriseWebFramework.UserManagement;
using EnterpriseWebLibrary.WebSessionState;

namespace EnterpriseWebLibrary.EnterpriseWebFramework.EnterpriseWebLibrary.WebSite.UserManagement.ChangePassword {
	partial class Page: EwfPage {
		partial class Info {
			public override string ResourceName => "";
			protected override bool userCanAccessResource => AppTools.User != null;
		}

		private DataValue<string> newPassword;

		protected override void loadData() {
			FormState.ExecuteWithDataModificationsAndDefaultAction(
				PostBack.CreateFull(
						firstModificationMethod: modifyData,
						actionGetter: () => new PostBackAction( new ExternalResourceInfo( es.info.ReturnAndDestinationUrl ) ) )
					.ToCollection(),
				() => {
					newPassword = new DataValue<string>();
					ph.AddControlsReturnThis(
						FormItemList.CreateStack(
								items: newPassword.GetPasswordModificationFormItems(
									firstLabel: "New password".ToComponents(),
									secondLabel: "Re-type new password".ToComponents() ) )
							.ToCollection()
							.GetControls() );
					EwfUiStatics.SetContentFootActions( new ButtonSetup( "Change Password" ).ToCollection() );
				} );
		}

		private void modifyData() {
			var password = new Password( newPassword.Value );
			FormsAuthStatics.SystemProvider.InsertOrUpdateUser(
				AppTools.User.UserId,
				AppTools.User.Email,
				AppTools.User.Role.RoleId,
				AppTools.User.LastRequestTime,
				password.Salt,
				password.ComputeSaltedHash(),
				false );
			AddStatusMessage( StatusMessageType.Info, "Your password has been successfully changed. Use it the next time you log in." );
		}
	}
}