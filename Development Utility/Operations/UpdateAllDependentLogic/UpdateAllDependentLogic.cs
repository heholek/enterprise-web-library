using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnterpriseWebLibrary.DevelopmentUtility.Operations.CodeGeneration;
using EnterpriseWebLibrary.DevelopmentUtility.Operations.CodeGeneration.DataAccess;
using EnterpriseWebLibrary.DevelopmentUtility.Operations.CodeGeneration.DataAccess.Subsystems;
using EnterpriseWebLibrary.DevelopmentUtility.Operations.CodeGeneration.DataAccess.Subsystems.StandardModification;
using EnterpriseWebLibrary.DevelopmentUtility.Operations.CodeGeneration.WebConfig;
using RedStapler.StandardLibrary;
using RedStapler.StandardLibrary.Configuration.SystemDevelopment;
using RedStapler.StandardLibrary.Configuration.SystemGeneral;
using RedStapler.StandardLibrary.DataAccess;
using RedStapler.StandardLibrary.DatabaseSpecification.Databases;
using RedStapler.StandardLibrary.IO;
using RedStapler.StandardLibrary.InstallationSupportUtility;
using RedStapler.StandardLibrary.InstallationSupportUtility.DatabaseAbstraction;
using RedStapler.StandardLibrary.InstallationSupportUtility.InstallationModel;

namespace EnterpriseWebLibrary.DevelopmentUtility.Operations {
	internal class UpdateAllDependentLogic: Operation {
		private static readonly Operation instance = new UpdateAllDependentLogic();
		public static Operation Instance { get { return instance; } }
		private UpdateAllDependentLogic() {}

		bool Operation.IsValid( Installation installation ) {
			return installation is DevelopmentInstallation;
		}

		void Operation.Execute( Installation genericInstallation, OperationResult operationResult ) {
			StandardLibraryMethods.ConfigureIis();

			var installation = genericInstallation as DevelopmentInstallation;

			DatabaseOps.UpdateDatabaseLogicIfUpdateFileExists( installation.DevelopmentInstallationLogic.Database,
			                                                   installation.ExistingInstallationLogic.DatabaseUpdateFilePath,
			                                                   true );

			if( !installation.DevelopmentInstallationLogic.SystemIsEwl ) {
				try {
					copyInStandardLibraryFiles( installation );
				}
				catch( Exception e ) {
					const string message = "Failed to copy Standard Library files into the installation. Please try the operation again.";
					if( e is UnauthorizedAccessException || e is IOException )
						throw new UserCorrectableException( message, e );
					throw new ApplicationException( message, e );
				}
			}

			// Generate code.
			generateLibraryCode( installation );
			if( installation.DevelopmentInstallationLogic.DevelopmentConfiguration.webProjects != null ) {
				foreach( var webProject in installation.DevelopmentInstallationLogic.DevelopmentConfiguration.webProjects )
					generateWebConfigAndCodeForWebProject( installation, webProject );
			}
			foreach( var service in installation.ExistingInstallationLogic.RuntimeConfiguration.WindowsServices )
				generateWindowsServiceCode( installation, service );

			generateXmlSchemaLogicForCustomInstallationConfigurationXsd( installation );
			generateXmlSchemaLogicForOtherXsdFiles( installation );
		}

		private static void copyInStandardLibraryFiles( DevelopmentInstallation installation ) {
			var asposeLicenseFilePath = StandardLibraryMethods.CombinePaths( AppTools.ConfigurationFolderPath, "Aspose.Total.lic" );
			if( File.Exists( asposeLicenseFilePath ) ) {
				IoMethods.CopyFile( asposeLicenseFilePath,
				                    StandardLibraryMethods.CombinePaths( InstallationFileStatics.GetGeneralFilesFolderPath( installation.GeneralLogic.Path, true ),
				                                                         InstallationFileStatics.FilesFolderName,
				                                                         "Aspose.Total.lic" ) );
			}

			// If web projects exist for this installation, copy appropriate files into them from the Test Web Site.
			if( installation.DevelopmentInstallationLogic.DevelopmentConfiguration.webProjects != null ) {
				foreach( var webProject in installation.DevelopmentInstallationLogic.DevelopmentConfiguration.webProjects )
					copyInWebProjectFiles( installation, webProject );
			}
		}

		private static void copyInWebProjectFiles( Installation installation, WebProject webProject ) {
			var webProjectFilesFolderPath = StandardLibraryMethods.CombinePaths( AppTools.InstallationPath, AppStatics.WebProjectFilesFolderName );
			var webProjectPath = StandardLibraryMethods.CombinePaths( installation.GeneralLogic.Path, webProject.name );

			// Copy Ewf folder and customize namespaces in .aspx, .ascx, .master, and .cs files.
			var webProjectEwfFolderPath = StandardLibraryMethods.CombinePaths( webProjectPath, AppStatics.EwfFolderName );
			IoMethods.DeleteFolder( webProjectEwfFolderPath );
			IoMethods.CopyFolder( StandardLibraryMethods.CombinePaths( webProjectFilesFolderPath, AppStatics.EwfFolderName ), webProjectEwfFolderPath, false );
			IoMethods.RecursivelyRemoveReadOnlyAttributeFromItem( webProjectEwfFolderPath );
			var matchingFiles = new List<string>();
			matchingFiles.AddRange( Directory.GetFiles( webProjectEwfFolderPath, "*.aspx", SearchOption.AllDirectories ) );
			matchingFiles.AddRange( Directory.GetFiles( webProjectEwfFolderPath, "*.ascx", SearchOption.AllDirectories ) );
			matchingFiles.AddRange( Directory.GetFiles( webProjectEwfFolderPath, "*.master", SearchOption.AllDirectories ) );
			matchingFiles.AddRange( Directory.GetFiles( webProjectEwfFolderPath, "*.cs", SearchOption.AllDirectories ) );
			foreach( var filePath in matchingFiles )
				File.WriteAllText( filePath, customizeNamespace( File.ReadAllText( filePath ), webProject ) );

			IoMethods.CopyFile( StandardLibraryMethods.CombinePaths( webProjectFilesFolderPath, AppStatics.StandardLibraryFilesFileName ),
			                    StandardLibraryMethods.CombinePaths( webProjectPath, AppStatics.StandardLibraryFilesFileName ) );
			IoMethods.RecursivelyRemoveReadOnlyAttributeFromItem( StandardLibraryMethods.CombinePaths( webProjectPath, AppStatics.StandardLibraryFilesFileName ) );
		}

		private static string customizeNamespace( string text, WebProject webProject ) {
			return text.Replace( "RedStapler.TestWebSite", webProject.@namespace );
		}

		private static void generateLibraryCode( DevelopmentInstallation installation ) {
			var libraryGeneratedCodeFolderPath = StandardLibraryMethods.CombinePaths( installation.DevelopmentInstallationLogic.LibraryPath, "Generated Code" );
			Directory.CreateDirectory( libraryGeneratedCodeFolderPath );
			var isuFilePath = StandardLibraryMethods.CombinePaths( libraryGeneratedCodeFolderPath, "ISU.cs" );
			IoMethods.DeleteFile( isuFilePath );
			using( TextWriter writer = new StreamWriter( isuFilePath ) ) {
				// Don't add "using System" here. It will create a huge number of ReSharper warnings in the generated code file.
				writer.WriteLine( "using System.Collections.Generic;" );
				writer.WriteLine( "using System.Data;" ); // Necessary for stored procedure logic
				writer.WriteLine( "using System.Data.Common;" );
				writer.WriteLine( "using System.Web.UI;" );
				writer.WriteLine( "using System.Web.UI.WebControls;" ); // Necessary for the fill list control functionality in row constants
				writer.WriteLine( "using RedStapler.StandardLibrary;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.Collections;" ); // Necessary for row constants
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess.CommandWriting;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess.CommandWriting.Commands;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess.CommandWriting.InlineConditionAbstraction;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess.CommandWriting.InlineConditionAbstraction.Conditions;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess.RevisionHistory;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess.StandardModification;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.EnterpriseWebFramework;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.Validation;" );

				writer.WriteLine();
				if( !installation.DevelopmentInstallationLogic.SystemIsEwl )
					generateGeneralProvider( writer, installation );
				generateDataAccessCode( writer, installation );
				writer.WriteLine();
				TypedCssClassStatics.Generate( installation.GeneralLogic.Path, installation.DevelopmentInstallationLogic.DevelopmentConfiguration.libraryNamespace, writer );
			}
		}

		private static void generateGeneralProvider( TextWriter writer, DevelopmentInstallation installation ) {
			writer.WriteLine( "namespace " + installation.DevelopmentInstallationLogic.DevelopmentConfiguration.libraryNamespace + ".Configuration.Providers {" );
			writer.WriteLine( "internal partial class General: SystemGeneralProvider {" );
			ConfigurationLogic.SystemProvider.WriteGeneralProviderMembers( writer );
			writer.WriteLine( "}" );
			writer.WriteLine( "}" );
		}

		private static void generateDataAccessCode( TextWriter writer, DevelopmentInstallation installation ) {
			var baseNamespace = installation.DevelopmentInstallationLogic.DevelopmentConfiguration.libraryNamespace + ".DataAccess";
			foreach( var database in installation.DevelopmentInstallationLogic.DatabasesForCodeGeneration ) {
				try {
					generateDataAccessCodeForDatabase( database,
					                                   installation.DevelopmentInstallationLogic.LibraryPath,
					                                   writer,
					                                   baseNamespace,
					                                   database.SecondaryDatabaseName.Length == 0
					                                   	? installation.DevelopmentInstallationLogic.DevelopmentConfiguration.database
					                                   	: installation.DevelopmentInstallationLogic.DevelopmentConfiguration.secondaryDatabases.Single(
					                                   		sd => sd.name == database.SecondaryDatabaseName ) );
				}
				catch( Exception e ) {
					throw UserCorrectableException.CreateSecondaryException(
						"An exception occurred while generating data access logic for the " +
						( database.SecondaryDatabaseName.Length == 0 ? "primary" : database.SecondaryDatabaseName + " secondary" ) + " database.",
						e );
				}
			}
			if( installation.DevelopmentInstallationLogic.DatabasesForCodeGeneration.Any( d => d.SecondaryDatabaseName.Length > 0 ) ) {
				writer.WriteLine();
				writer.WriteLine( "namespace " + baseNamespace + " {" );
				writer.WriteLine( "public class SecondaryDatabaseNames {" );
				foreach( var secondaryDatabase in installation.DevelopmentInstallationLogic.DatabasesForCodeGeneration.Where( d => d.SecondaryDatabaseName.Length > 0 ) )
					writer.WriteLine( "public const string " + secondaryDatabase.SecondaryDatabaseName + " = \"" + secondaryDatabase.SecondaryDatabaseName + "\";" );
				writer.WriteLine( "}" );
				writer.WriteLine( "}" );
			}
		}

		private static void generateDataAccessCodeForDatabase( RedStapler.StandardLibrary.InstallationSupportUtility.DatabaseAbstraction.Database database,
		                                                       string libraryBasePath, TextWriter writer, string baseNamespace,
		                                                       RedStapler.StandardLibrary.Configuration.SystemDevelopment.Database configuration ) {
			// Ensure that all revision history tables named in the configuration file actually exist.
			var tableNames = database.GetTables();
			if( configuration.revisionHistoryTables != null ) {
				foreach( var revisionHistoryTable in configuration.revisionHistoryTables ) {
					var tableExists = false;
					foreach( var table in tableNames ) {
						if( table.ToLower() == revisionHistoryTable.ToLower() ) {
							tableExists = true;
							break;
						}
					}
					if( !tableExists )
						throw new UserCorrectableException( "Revision history table '" + revisionHistoryTable + "' does not exist." );
				}
			}

			database.ExecuteDbMethod( delegate( DBConnection cn ) {
				// database logic access - standard
				if( !configuration.EveryTableHasKeySpecified || configuration.EveryTableHasKey ) {
					writer.WriteLine();
					TableConstantStatics.Generate( cn, writer, baseNamespace, database );
				}

				// database logic access - custom
				writer.WriteLine();
				RowConstantStatics.Generate( cn, writer, baseNamespace, database, configuration );

				// retrieval and modification commands - standard
				if( !configuration.EveryTableHasKeySpecified || configuration.EveryTableHasKey ) {
					writer.WriteLine();
					CommandConditionStatics.Generate( cn, writer, baseNamespace, database );

					writer.WriteLine();
					var tableRetrievalNamespaceDeclaration = TableRetrievalStatics.GetNamespaceDeclaration( baseNamespace, database );
					TableRetrievalStatics.Generate( cn, writer, tableRetrievalNamespaceDeclaration, database, configuration );

					writer.WriteLine();
					var modNamespaceDeclaration = StandardModificationStatics.GetNamespaceDeclaration( baseNamespace, database );
					StandardModificationStatics.Generate( cn, writer, modNamespaceDeclaration, database, configuration );

					foreach( var tableName in database.GetTables() ) {
						TableRetrievalStatics.WritePartialClass( libraryBasePath, tableRetrievalNamespaceDeclaration, database, tableName );
						StandardModificationStatics.WritePartialClass( libraryBasePath,
						                                               modNamespaceDeclaration,
						                                               database,
						                                               tableName,
						                                               DataAccessStatics.IsRevisionHistoryTable( tableName, configuration ) );
					}
				}

				// retrieval and modification commands - custom
				writer.WriteLine();
				QueryRetrievalStatics.Generate( cn, writer, baseNamespace, database, configuration );
				writer.WriteLine();
				CustomModificationStatics.Generate( cn, writer, baseNamespace, database, configuration );

				// other commands
				if( cn.DatabaseInfo is OracleInfo ) {
					writer.WriteLine();
					SequenceStatics.Generate( cn, writer, baseNamespace, database );
					writer.WriteLine();
					ProcedureStatics.Generate( cn, writer, baseNamespace, database );
				}
			} );
		}

		private static void generateWebConfigAndCodeForWebProject( DevelopmentInstallation installation, WebProject webProject ) {
			var webProjectPath = StandardLibraryMethods.CombinePaths( installation.GeneralLogic.Path, webProject.name );

			// This must be done before web meta logic generation, which can be affected by the contents of Web.config files.
			WebConfigStatics.GenerateWebConfig( webProject, webProjectPath, installation.ExistingInstallationLogic.RuntimeConfiguration.SystemShortName );

			var webProjectGeneratedCodeFolderPath = StandardLibraryMethods.CombinePaths( webProjectPath, "Generated Code" );
			Directory.CreateDirectory( webProjectGeneratedCodeFolderPath );
			var webProjectIsuFilePath = StandardLibraryMethods.CombinePaths( webProjectGeneratedCodeFolderPath, "ISU.cs" );
			IoMethods.DeleteFile( webProjectIsuFilePath );
			using( TextWriter writer = new StreamWriter( webProjectIsuFilePath ) ) {
				writer.WriteLine( "using System;" );
				writer.WriteLine( "using System.Collections.Generic;" );
				writer.WriteLine( "using System.Collections.ObjectModel;" );
				writer.WriteLine( "using System.Linq;" );
				writer.WriteLine( "using System.Web;" );
				writer.WriteLine( "using System.Web.UI;" );
				writer.WriteLine( "using RedStapler.StandardLibrary;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.DataAccess;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.EnterpriseWebFramework;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.Validation;" );
				writer.WriteLine();
				CodeGeneration.WebMetaLogic.WebMetaLogicStatics.Generate( writer, webProjectPath, webProject );
			}
		}

		private static void generateWindowsServiceCode( DevelopmentInstallation installation, WindowsService service ) {
			var serviceProjectGeneratedCodeFolderPath = StandardLibraryMethods.CombinePaths( installation.GeneralLogic.Path, service.Name, "Generated Code" );
			Directory.CreateDirectory( serviceProjectGeneratedCodeFolderPath );
			var isuFilePath = StandardLibraryMethods.CombinePaths( serviceProjectGeneratedCodeFolderPath, "ISU.cs" );
			IoMethods.DeleteFile( isuFilePath );
			using( TextWriter writer = new StreamWriter( isuFilePath ) ) {
				writer.WriteLine( "using System;" );
				writer.WriteLine( "using System.ComponentModel;" );
				writer.WriteLine( "using System.ServiceProcess;" );
				writer.WriteLine( "using RedStapler.StandardLibrary;" );
				writer.WriteLine( "using RedStapler.StandardLibrary.WindowsServiceFramework;" );
				writer.WriteLine();
				writer.WriteLine( "namespace " + service.NamespaceAndAssemblyName + " {" );

				writer.WriteLine( "internal static partial class Program {" );

				writer.WriteLine( "[ MTAThread ]" );
				writer.WriteLine( "private static void Main() {" );
				writer.WriteLine( "InitAppTools();" );
				writer.WriteLine( "AppTools.ExecuteAppWithStandardExceptionHandling( delegate { ServiceBase.Run( new ServiceBaseAdapter( new " + service.Name.ToPascalCase() +
				                  "() ) ); } );" );
				writer.WriteLine( "}" );

				writer.WriteLine( "internal static void InitAppTools() {" );
				writer.WriteLine( "SystemLogic globalLogic = null;" );
				writer.WriteLine( "initGlobalLogic( ref globalLogic );" );
				writer.WriteLine( "AppTools.Init( \"" + service.Name + "\" + \" Executable\", false, globalLogic );" );
				writer.WriteLine( "}" );

				writer.WriteLine( "static partial void initGlobalLogic( ref SystemLogic globalLogic );" );

				writer.WriteLine( "}" );

				writer.WriteLine( "[ RunInstaller( true ) ]" );
				writer.WriteLine( "public class Installer: System.Configuration.Install.Installer {" );

				writer.WriteLine( "public Installer() {" );
				writer.WriteLine( "Program.InitAppTools();" );
				writer.WriteLine( "var code = AppTools.ExecuteAppWithStandardExceptionHandling( delegate {" );
				writer.WriteLine( "Installers.Add( WindowsServiceMethods.CreateServiceProcessInstaller() );" );
				writer.WriteLine( "Installers.Add( WindowsServiceMethods.CreateServiceInstaller( new " + service.Name.ToPascalCase() + "() ) );" );
				writer.WriteLine( "} );" );
				writer.WriteLine( "if( code != 0 )" );
				writer.WriteLine(
					"throw new ApplicationException( \"Service installer objects could not be created. More information should be available in a separate error email from the service executable.\" );" );
				writer.WriteLine( "}" );

				writer.WriteLine( "}" );

				writer.WriteLine( "internal partial class " + service.Name.ToPascalCase() + ": WindowsServiceBase {" );
				writer.WriteLine( "internal " + service.Name.ToPascalCase() + "() {}" );
				writer.WriteLine( "string WindowsServiceBase.Name { get { return \"" + service.Name + "\"; } }" );
				writer.WriteLine( "}" );

				writer.WriteLine( "}" );
			}
		}

		private static void generateXmlSchemaLogicForCustomInstallationConfigurationXsd( DevelopmentInstallation installation ) {
			var libraryProjectPath = StandardLibraryMethods.CombinePaths( installation.GeneralLogic.Path, "Library" );
			const string customInstallationConfigSchemaPathInProject = @"Configuration\Installation\Custom.xsd";
			if( File.Exists( StandardLibraryMethods.CombinePaths( libraryProjectPath, customInstallationConfigSchemaPathInProject ) ) ) {
				generateXmlSchemaLogic( libraryProjectPath,
				                        customInstallationConfigSchemaPathInProject,
				                        installation.DevelopmentInstallationLogic.DevelopmentConfiguration.libraryNamespace + ".Configuration.Installation",
				                        "Installation Custom Configuration.cs",
				                        true );
			}
		}

		private static void generateXmlSchemaLogicForOtherXsdFiles( DevelopmentInstallation installation ) {
			if( installation.DevelopmentInstallationLogic.DevelopmentConfiguration.xmlSchemas != null ) {
				foreach( var xmlSchema in installation.DevelopmentInstallationLogic.DevelopmentConfiguration.xmlSchemas ) {
					generateXmlSchemaLogic( StandardLibraryMethods.CombinePaths( installation.GeneralLogic.Path, xmlSchema.project ),
					                        xmlSchema.pathInProject,
					                        xmlSchema.@namespace,
					                        xmlSchema.codeFileName,
					                        xmlSchema.useSvcUtil );
				}
			}
		}

		private static void generateXmlSchemaLogic( string projectPath, string schemaPathInProject, string nameSpace, string codeFileName, bool useSvcUtil ) {
			var projectGeneratedCodeFolderPath = StandardLibraryMethods.CombinePaths( projectPath, "Generated Code" );
			if( useSvcUtil ) {
				try {
					StandardLibraryMethods.RunProgram( StandardLibraryMethods.CombinePaths( AppStatics.DotNetToolsFolderPath, "SvcUtil" ),
					                                   "/d:\"" + projectGeneratedCodeFolderPath + "\" /noLogo \"" +
					                                   StandardLibraryMethods.CombinePaths( projectPath, schemaPathInProject ) + "\" /o:\"" + codeFileName + "\" /dconly /n:*," +
					                                   nameSpace + " /ser:DataContractSerializer",
					                                   "",
					                                   true );
				}
				catch( Exception e ) {
					throw new UserCorrectableException( "Failed to generate XML schema logic using SvcUtil.", e );
				}
			}
			else {
				Directory.CreateDirectory( projectGeneratedCodeFolderPath );
				try {
					StandardLibraryMethods.RunProgram( StandardLibraryMethods.CombinePaths( AppStatics.DotNetToolsFolderPath, "xsd" ),
					                                   "/nologo \"" + StandardLibraryMethods.CombinePaths( projectPath, schemaPathInProject ) + "\" /c /n:" + nameSpace +
					                                   " /o:\"" + projectGeneratedCodeFolderPath + "\"",
					                                   "",
					                                   true );
				}
				catch( Exception e ) {
					throw new UserCorrectableException( "Failed to generate XML schema logic using xsd.", e );
				}
				var outputCodeFilePath = StandardLibraryMethods.CombinePaths( projectGeneratedCodeFolderPath,
				                                                              Path.GetFileNameWithoutExtension( schemaPathInProject ) + ".cs" );
				var desiredCodeFilePath = StandardLibraryMethods.CombinePaths( projectGeneratedCodeFolderPath, codeFileName );
				if( outputCodeFilePath != desiredCodeFilePath ) {
					try {
						IoMethods.MoveFile( outputCodeFilePath, desiredCodeFilePath );
					}
					catch( IOException e ) {
						throw new UserCorrectableException( "Failed to move the generated code file for an XML schema. Please try the operation again.", e );
					}
				}
			}
		}
	}
}