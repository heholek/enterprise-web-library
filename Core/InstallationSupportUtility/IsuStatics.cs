﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using EnterpriseWebLibrary.Configuration;
using EnterpriseWebLibrary.Configuration.InstallationStandard;
using Humanizer;

namespace EnterpriseWebLibrary.InstallationSupportUtility {
	public class IsuStatics {
		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void ConfigureIis( bool iisExpress, bool useServerAppPoolSettings ) {
			executeInIisServerManagerTransaction(
				() => {
					try {
						configureIis( iisExpress, useServerAppPoolSettings );
					}
					catch( FileNotFoundException ) when( iisExpress ) {
						using( var p = new Process() ) {
							p.StartInfo.FileName = @"C:\Program Files (x86)\IIS Express\iisexpress";
							p.Start();
							Thread.Sleep( 2000 );
							if( !p.CloseMainWindow() )
								// We need this when the process runs non-interactively, such as from a scheduled task on a build server.
								p.Kill();
							p.WaitForExit();
						}
						configureIis( true, useServerAppPoolSettings );
					}
				} );
		}

		private static void configureIis( bool iisExpress, bool useServerAppPoolSettings ) {
			IisConfigurationStatics.ExecuteInServerManagerTransaction(
				iisExpress,
				( serverManager, enumGetter ) => {
					if( !iisExpress ) {
						var poolDefaults = serverManager.ApplicationPoolDefaults;
						poolDefaults.StartMode = enumGetter( "Microsoft.Web.Administration.StartMode", useServerAppPoolSettings ? "AlwaysRunning" : "OnDemand" );

						// We use this because it's a consistent account name across all machines, which allows our SQL Server databases [which must grant access to the
						// app pool] to be portable.
						poolDefaults.ProcessModel.IdentityType = enumGetter( "Microsoft.Web.Administration.ProcessModelIdentityType", "NetworkService" );

						// Disable idle time-out.
						poolDefaults.ProcessModel.IdleTimeout = useServerAppPoolSettings ? TimeSpan.Zero : new TimeSpan( 0, 5, 0 );

						// Disable regular time interval recycling.
						poolDefaults.Recycling.PeriodicRestart.Time = TimeSpan.Zero;

						poolDefaults.Recycling.PeriodicRestart.Schedule.Clear();
						if( useServerAppPoolSettings )
							poolDefaults.Recycling.PeriodicRestart.Schedule.Add( new TimeSpan( 23, 55, 0 ) );
					}

					var config = serverManager.GetApplicationHostConfiguration();

					var modulesSection = config.GetSection( "system.webServer/modules", "" );
					foreach( var element in modulesSection.GetCollection() )
						element.SetMetadata( "lockItem", null );

					var serverRuntimeSection = config.GetSection( "system.webServer/serverRuntime", "" );
					serverRuntimeSection.OverrideMode = enumGetter( "Microsoft.Web.Administration.OverrideMode", "Allow" );
				} );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void UpdateIisAppPool( string name ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var pool = serverManager.ApplicationPools[ name ] ?? serverManager.ApplicationPools.Add( name );
						pool.AutoStart = false;
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void DeleteIisAppPool( string name ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var pool = serverManager.ApplicationPools[ name ];
						if( pool != null )
							serverManager.ApplicationPools.Remove( pool );
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void StartIisAppPool( string name ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var pool = serverManager.ApplicationPools[ name ];
						pool.Start();
						while( pool.State != enumGetter( "Microsoft.Web.Administration.ObjectState", "Started" ) )
							Thread.Sleep( 1000 );

						pool.AutoStart = true;
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void StopIisAppPool( string name ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var pool = serverManager.ApplicationPools[ name ];
						if( pool != null ) {
							// Stop throws an exception if the pool isn't started.
							if( pool.State != enumGetter( "Microsoft.Web.Administration.ObjectState", "Stopped" ) &&
							    pool.State != enumGetter( "Microsoft.Web.Administration.ObjectState", "Stopping" ) )
								pool.Stop();

							while( pool.State != enumGetter( "Microsoft.Web.Administration.ObjectState", "Stopped" ) )
								Thread.Sleep( 1000 );

							pool.AutoStart = false;
						}
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void UpdateIisSite( string name, string appPool, string physicalPath, IReadOnlyCollection<IisHostName> hostNames ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						const int dummyPort = 80;
						var site = serverManager.Sites[ name ] ?? serverManager.Sites.Add( name, physicalPath, dummyPort );

						var rootApp = site.Applications[ "/" ];
						rootApp.ApplicationPoolName = appPool;
						rootApp[ "preloadEnabled" ] = true;

						var rootVd = rootApp.VirtualDirectories[ "/" ];
						rootVd.PhysicalPath = physicalPath;

						var bindings = hostNames.SelectMany(
								i => {
									var nonsecureBinding = Tuple.Create( false, i.NonsecurePortSpecified ? i.NonsecurePort : 80, i.Name );
									return i.SecureBinding != null
										       ? new[] { nonsecureBinding, Tuple.Create( true, i.SecureBinding.PortSpecified ? i.SecureBinding.Port : 443, i.Name ) }
										       : nonsecureBinding.ToCollection();
								} )
							.ToList();
						var unrecognizedBindings = new List<dynamic>();
						foreach( var iisBinding in site.Bindings ) {
							if( iisBinding.Protocol != "http" && iisBinding.Protocol != "https" )
								continue;

							var bindingInfo = ( (string)iisBinding.BindingInformation ).Separate( ":", false );
							var binding = Tuple.Create( (string)iisBinding.Protocol == "https", int.Parse( bindingInfo[ 1 ] ), bindingInfo[ 2 ] );
							if( bindings.Contains( binding ) && ( !binding.Item1 || iisBinding.SslFlags == enumGetter( "Microsoft.Web.Administration.SslFlags", "3" ) ) )
								bindings.Remove( binding );
							else
								unrecognizedBindings.Add( iisBinding );
						}

						foreach( var i in unrecognizedBindings )
							site.Bindings.Remove( i );

						foreach( var i in bindings ) {
							if( i.Item1 )
								site.Bindings.Add(
									"*:{0}:{1}".FormatWith( i.Item2, i.Item3 ),
									(byte[])null,
									(string)null,
									enumGetter( "Microsoft.Web.Administration.SslFlags", "3" ) );
							else
								site.Bindings.Add( "*:{0}:{1}".FormatWith( i.Item2, i.Item3 ), "http" );
						}
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void DeleteIisSite( string name ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var site = serverManager.Sites[ name ];
						if( site != null )
							serverManager.Sites.Remove( site );
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void UpdateIisVirtualDirectory( string site, string name, string appPool, string physicalPath ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var iisSite = serverManager.Sites[ site ];

						var path = "/{0}".FormatWith( name );
						var app = iisSite.Applications[ path ] ?? iisSite.Applications.Add( path, physicalPath );
						app.ApplicationPoolName = appPool;
						app[ "preloadEnabled" ] = true;

						var rootVd = app.VirtualDirectories[ "/" ];
						rootVd.PhysicalPath = physicalPath;
					} ) );
		}

		/// <summary>
		/// ISU and internal use only.
		/// </summary>
		public static void DeleteIisVirtualDirectory( string site, string name ) {
			executeInIisServerManagerTransaction(
				() => IisConfigurationStatics.ExecuteInServerManagerTransaction(
					false,
					( serverManager, enumGetter ) => {
						var iisSite = serverManager.Sites[ site ];

						var path = "/{0}".FormatWith( name );
						var app = iisSite.Applications[ path ];
						if( app != null )
							iisSite.Applications.Remove( app );
					} ) );
		}

		private static void executeInIisServerManagerTransaction( Action method ) {
			// Overlapping commitment of changes to server manager do not end well.
			EwlStatics.ExecuteAsCriticalRegion(
				"{1BC5B312-F0F0-11DF-B6B9-118ADFD72085}",
				false,
				delegate {
					try {
						method();
					}
					catch( Exception e ) {
						const string message = "Failed to configure IIS.";
						if( e is UnauthorizedAccessException )
							throw new UserCorrectableException( message, e );
						throw new ApplicationException( message, e );
					}
				} );
		}

		/// <summary>
		/// Installation Support Utility use only.
		/// </summary>
		public static string GetDataPackageZipFilePath( string installationFullName ) {
			return EwlStatics.CombinePaths( ConfigurationStatics.EwlFolderPath, "Local Data Packages", installationFullName + ".zip" );
		}
	}
}