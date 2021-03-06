﻿using System.Linq;
using System.ServiceProcess;
using EnterpriseWebLibrary.Configuration;

namespace EnterpriseWebLibrary.WindowsServiceFramework {
	/// <summary>
	/// A collection of service-related static methods.
	/// </summary>
	public static class WindowsServiceMethods {
		/// <summary>
		/// Creates a service process installer.
		/// </summary>
		public static ServiceProcessInstaller CreateServiceProcessInstaller() {
			return new ServiceProcessInstaller { Account = ServiceAccount.NetworkService };
		}

		/// <summary>
		/// Creates a service installer for the specified service.
		/// </summary>
		public static ServiceInstaller CreateServiceInstaller( WindowsServiceBase service ) {
			return new ServiceInstaller { ServiceName = GetServiceInstalledName( service ), Description = service.Description };
		}

		internal static string GetServiceInstalledName( WindowsServiceBase service ) {
			return ConfigurationStatics.InstallationConfiguration.WindowsServices.Single( s => s.Name == service.Name ).InstalledName;
		}
	}
}