using System.Web.UI.WebControls;
using EnterpriseWebLibrary.EnterpriseWebFramework.Controls;
using EnterpriseWebLibrary.EnterpriseWebFramework.UserManagement;

namespace EnterpriseWebLibrary.EnterpriseWebFramework.EnterpriseWebLibrary.WebSite.Admin {
	partial class SystemUsers: EwfPage {
		partial class Info {
			protected override AlternativeResourceMode createAlternativeMode() =>
				UserManagementStatics.UserManagementEnabled ? null : new DisabledResourceMode( "User management is not enabled in this system." );
		}

		protected override void loadData() {
			var table = new DynamicTable( new EwfTableColumn( "Email", Unit.Percentage( 50 ) ), new EwfTableColumn( "Role", Unit.Percentage( 50 ) ) );
			table.AddActionLink( new HyperlinkSetup( new EditUser.Info( es.info, null ), "Create User" ) );
			foreach( var user in UserManagementStatics.GetUsers() ) {
				table.AddTextRow(
					new RowSetup { ActivationBehavior = ElementActivationBehavior.CreateRedirectScript( new EditUser.Info( es.info, user.UserId ) ) },
					user.Email,
					user.Role.Name );
			}
			ph.AddControlsReturnThis( table );
		}
	}
}