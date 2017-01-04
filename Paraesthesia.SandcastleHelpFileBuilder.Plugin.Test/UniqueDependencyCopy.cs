using System;
using System.CodeDom.Compiler;
using System.Compiler;
using System.Globalization;
using System.IO;
using System.Reflection;

using NUnit.Framework;

using SandcastleBuilder.Utils;
using SandcastleBuilder.Utils.Gac;
using SandcastleBuilder.Utils.PlugIn;

using TypeMock;

namespace Paraesthesia.SandcastleHelpFileBuilder.Plugin.Test
{
	[TestFixture]
	[VerifyMocks]
	public class UniqueDependencyCopy
	{
		[Test(Description = "Ensures that an empty value will pass through the configuration method.")]
		public void ConfigurePlugIn_Empty()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.AreEqual("", plugin.ConfigurePlugIn(""), "Empty values should pass through configuration.");
		}

		[Test(Description = "Ensures that a non-empty value will pass through the configuration method.")]
		public void ConfigurePlugIn_NonEmpty()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.AreEqual("someval", plugin.ConfigurePlugIn("someval"), "Non-empty values should pass through configuration.");
		}

		[Test(Description = "Ensures that a null value will pass through the configuration method.")]
		public void ConfigurePlugIn_Null()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.IsNull(plugin.ConfigurePlugIn(null), "Null values should pass through configuration.");
		}

		[Test(Description = "Checks logging and early exit if there are no dependencies to copy.")]
		public void CopyDependencies_NoDependencies()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			SandcastleProject project = new SandcastleProject();
			BuildProcess process = new BuildProcess(project);
			using (RecordExpectations recorder = RecorderManager.StartRecording())
			{
				process.ReportProgress("No dependencies to copy.");
				recorder.CheckArguments();
			}
			plugin.Initialize(process, null);

			MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
			ExecutionContext context = mockContext.Object;
			plugin.Execute(context);
		}

		[Test(Description = "Verifies the correct temporary folder is created for local copies.")]
		public void CopyDependencies_CreatesTemporaryFolder()
		{
			TempFileCollection tempFiles = this.InitializeTempFiles();
			try
			{
				DependencyItem dependency = new DependencyItem();
				dependency.DependencyPath = new FileFolderGacPath(typeof(Test.UniqueDependencyCopy).Assembly.Location);

				SandcastleProject project = new SandcastleProject();
				project.Dependencies.Add(dependency);
				BuildProcess process = new BuildProcess(project);
				string tempFolder = tempFiles.BasePath;
				using (RecordExpectations recorder = RecorderManager.StartRecording())
				{
					string dummyWorkingFolder = process.WorkingFolder;
					recorder.Return(tempFolder);
					recorder.RepeatAlways();
				}

				Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
				plugin.Initialize(process, null);

				MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
				ExecutionContext context = mockContext.Object;
				plugin.Execute(context);

				string dependencyFolder = Path.Combine(tempFolder, "DLL");
				Assert.IsTrue(Directory.Exists(dependencyFolder), "The temporary dependency DLL folder was not created under the working folder.");
			}
			finally
			{
				this.CleanTempFiles(tempFiles);
			}
		}

		[Test(Description = "Tests local copying of a single, known assembly in the GAC.")]
		public void CopyDependencies_GacNoWildcard()
		{
			TempFileCollection tempFiles = this.InitializeTempFiles();
			try
			{
				DependencyItem projectDependency = new DependencyItem();
				projectDependency.DependencyPath = new FileFolderGacPath("GAC:" + typeof(MockManager).Assembly.FullName);

				SandcastleProject project = new SandcastleProject();
				project.Dependencies.Add(projectDependency);
				BuildProcess process = new BuildProcess(project);
				string tempFolder = tempFiles.BasePath;
				using (RecordExpectations recorder = RecorderManager.StartRecording())
				{
					string dummyWorkingFolder = process.WorkingFolder;
					recorder.Return(tempFolder);
					recorder.RepeatAlways();

					process.ReportProgress(null);
					recorder.RepeatAlways();
				}

				Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
				plugin.Initialize(process, null);

				MockObject<AssemblyLoader> mockLoader = MockManager.MockObject<AssemblyLoader>();
				mockLoader.ExpectAndReturn("GetAssemblyLocation", typeof(MockManager).Assembly.Location).Args(typeof(MockManager).Assembly.FullName);
				AssemblyLoader loader = mockLoader.Object;
				using (RecordExpectations recorder = RecorderManager.StartRecording())
				{
					AssemblyLoader dummy = AssemblyLoader.CreateAssemblyLoader(null);
					recorder.Return(loader);
					recorder.RepeatAlways();
				}

				MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
				ExecutionContext context = mockContext.Object;
				plugin.Execute(context);

				DirectoryInfo dependencyFolderInfo = new DirectoryInfo(Path.Combine(tempFolder, "DLL"));
				FileInfo[] dependencies = dependencyFolderInfo.GetFiles();
				Assert.AreEqual(1, dependencies.Length, "Only one file should be found in the dependency folder.");
				Assert.AreEqual(".dll", dependencies[0].Extension.ToLowerInvariant(), "The copied dependency should have a .dll extension.");
				Assert.IsTrue(this.IsGuid(Path.GetFileNameWithoutExtension(dependencies[0].Name)), "The copied dependency should have a GUID filename.");
				Assert.AreEqual(FileAttributes.Normal, dependencies[0].Attributes, "The copied dependency should have 'Normal' attributes.");
				using (AssemblyNode dependencyNode = AssemblyNode.GetAssembly(dependencies[0].FullName))
				{
					Assert.AreEqual(typeof(MockManager).Assembly.FullName, dependencyNode.StrongName, "The metadata name of the copied dependency should match the original.");
				}
			}
			finally
			{
				this.CleanTempFiles(tempFiles);
			}
		}

		[Test(Description = "Tests local copying of a single, known assembly not in the GAC.")]
		public void CopyDependencies_NoGacNoWildcard()
		{
			TempFileCollection tempFiles = this.InitializeTempFiles();
			try
			{
				DependencyItem projectDependency = new DependencyItem();
				projectDependency.DependencyPath = new FileFolderGacPath(Assembly.GetExecutingAssembly().Location);

				SandcastleProject project = new SandcastleProject();
				project.Dependencies.Add(projectDependency);
				BuildProcess process = new BuildProcess(project);
				string tempFolder = tempFiles.BasePath;
				using (RecordExpectations recorder = RecorderManager.StartRecording())
				{
					string dummyWorkingFolder = process.WorkingFolder;
					recorder.Return(tempFolder);
					recorder.RepeatAlways();

					process.ReportProgress(null);
					recorder.RepeatAlways();
				}

				Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
				plugin.Initialize(process, null);

				MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
				ExecutionContext context = mockContext.Object;
				plugin.Execute(context);

				DirectoryInfo dependencyFolderInfo = new DirectoryInfo(Path.Combine(tempFolder, "DLL"));
				FileInfo[] dependencies = dependencyFolderInfo.GetFiles();
				Assert.AreEqual(1, dependencies.Length, "Only one file should be found in the dependency folder.");
				Assert.AreEqual(".dll", dependencies[0].Extension.ToLowerInvariant(), "The copied dependency should have a .dll extension.");
				Assert.IsTrue(this.IsGuid(Path.GetFileNameWithoutExtension(dependencies[0].Name)), "The copied dependency should have a GUID filename.");
				Assert.AreEqual(FileAttributes.Normal, dependencies[0].Attributes, "The copied dependency should have 'Normal' attributes.");
				using (AssemblyNode dependencyNode = AssemblyNode.GetAssembly(dependencies[0].FullName))
				{
					Assert.AreEqual(Assembly.GetExecutingAssembly().FullName, dependencyNode.StrongName, "The metadata name of the copied dependency should match the original.");
				}
			}
			finally
			{
				this.CleanTempFiles(tempFiles);
			}
		}

		[Test(Description = "Tests local copying of a multiple known assemblies (via wildcard) not in the GAC.")]
		public void CopyDependencies_NoGacWildcard()
		{
			TempFileCollection tempFiles = this.InitializeTempFiles();
			try
			{
				DependencyItem projectDependency = new DependencyItem();
				projectDependency.DependencyPath = new FileFolderGacPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Paraesthesia.SandcastleHelpFileBuilder.*.dll"));

				SandcastleProject project = new SandcastleProject();
				project.Dependencies.Add(projectDependency);
				BuildProcess process = new BuildProcess(project);
				string tempFolder = tempFiles.BasePath;
				using (RecordExpectations recorder = RecorderManager.StartRecording())
				{
					string dummyWorkingFolder = process.WorkingFolder;
					recorder.Return(tempFolder);
					recorder.RepeatAlways();

					process.ReportProgress(null);
					recorder.RepeatAlways();
				}

				Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
				plugin.Initialize(process, null);

				MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
				ExecutionContext context = mockContext.Object;
				plugin.Execute(context);

				DirectoryInfo dependencyFolderInfo = new DirectoryInfo(Path.Combine(tempFolder, "DLL"));
				FileInfo[] dependencies = dependencyFolderInfo.GetFiles();
				Assert.AreEqual(2, dependencies.Length, "Two files should be found in the dependency folder.");
				for (int i = 0; i < dependencies.Length; i++ )
				{
					Assert.AreEqual(".dll", dependencies[i].Extension.ToLowerInvariant(), "The copied dependency should have a .dll extension.");
					Assert.AreEqual(FileAttributes.Normal, dependencies[i].Attributes, "The copied dependency should have 'Normal' attributes.");
					Assert.IsTrue(this.IsGuid(Path.GetFileNameWithoutExtension(dependencies[i].Name)), "The copied dependency should have a GUID filename.");
					using (AssemblyNode dependencyNode = AssemblyNode.GetAssembly(dependencies[i].FullName))
					{
						Assert.IsTrue(dependencyNode.StrongName == typeof(Plugin.UniqueDependencyCopy).Assembly.FullName || dependencyNode.StrongName == typeof(Plugin.Test.UniqueDependencyCopy).Assembly.FullName, "The metadata name of the copied dependency should match the original.");
					}
				}
			}
			finally
			{
				this.CleanTempFiles(tempFiles);
			}
		}

		[Test(Description = "Tests copying of dependencies where the wildcard includes non-referenceable files (pdb, xml).")]
		public void CopyDependencies_WildcardIncludesNonReferences()
		{
			TempFileCollection tempFiles = this.InitializeTempFiles();
			try
			{
				DependencyItem projectDependency = new DependencyItem();
				projectDependency.DependencyPath = new FileFolderGacPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Paraesthesia.SandcastleHelpFileBuilder.*.*"));

				SandcastleProject project = new SandcastleProject();
				project.Dependencies.Add(projectDependency);
				BuildProcess process = new BuildProcess(project);
				string tempFolder = tempFiles.BasePath;
				using (RecordExpectations recorder = RecorderManager.StartRecording())
				{
					string dummyWorkingFolder = process.WorkingFolder;
					recorder.Return(tempFolder);
					recorder.RepeatAlways();

					process.ReportProgress(null);
					recorder.RepeatAlways();
				}

				Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
				plugin.Initialize(process, null);

				MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
				ExecutionContext context = mockContext.Object;
				plugin.Execute(context);

				DirectoryInfo dependencyFolderInfo = new DirectoryInfo(Path.Combine(tempFolder, "DLL"));
				FileInfo[] dependencies = dependencyFolderInfo.GetFiles();
				Assert.AreEqual(2, dependencies.Length, "Two files should be found in the dependency folder.");
				for (int i = 0; i < dependencies.Length; i++)
				{
					Assert.AreEqual(".dll", dependencies[i].Extension.ToLowerInvariant(), "The copied dependency should have a .dll extension.");
					Assert.AreEqual(FileAttributes.Normal, dependencies[i].Attributes, "The copied dependency should have 'Normal' attributes.");
					Assert.IsTrue(this.IsGuid(Path.GetFileNameWithoutExtension(dependencies[i].Name)), "The copied dependency should have a GUID filename.");
					using (AssemblyNode dependencyNode = AssemblyNode.GetAssembly(dependencies[i].FullName))
					{
						Assert.IsTrue(dependencyNode.StrongName == typeof(Plugin.UniqueDependencyCopy).Assembly.FullName || dependencyNode.StrongName == typeof(Plugin.Test.UniqueDependencyCopy).Assembly.FullName, "The metadata name of the copied dependency should match the original.");
					}
				}
			}
			finally
			{
				this.CleanTempFiles(tempFiles);
			}
		}

		[Test(Description = "Checks to see if the copyright is empty.")]
		public void Copyright_NotEmpty()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			string copyright = plugin.Copyright;
			Assert.IsNotNull(copyright, "The copyright information should not be null.");
			Assert.AreNotEqual("", copyright, "The copyright information should not be empty.");
		}

		[Test(Description = "Checks to see if the description is empty.")]
		public void Description_NotEmpty()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			string description = plugin.Description;
			Assert.IsNotNull(description, "The description information should not be null.");
			Assert.AreNotEqual("", description, "The description information should not be empty.");
		}

		[Test(Description = "Verifies the CopyDependencies method will be executed at the correct stage of the process.")]
		public void Execute_CopyDependenciesRightBuildStep()
		{
			MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf);
			ExecutionContext context = mockContext.Object;

			MockObject<Plugin.UniqueDependencyCopy> mock = MockManager.MockObject<Plugin.UniqueDependencyCopy>();
			mock.ExpectCall("CopyDependencies");
			Plugin.UniqueDependencyCopy plugin = mock.Object;

			plugin.Initialize(new BuildProcess(null), null);
			plugin.Execute(context);
		}

		[Test(Description = "Verifies the CopyDependencies method will not be executed at a different stage of the process.")]
		public void Execute_CopyDependenciesWrongBuildStep()
		{
			MockObject<ExecutionContext> mockContext = MockManager.MockObject<ExecutionContext>(BuildStep.CopyAdditionalContent, ExecutionBehaviors.InsteadOf);
			ExecutionContext context = mockContext.Object;

			MockObject<Plugin.UniqueDependencyCopy> mock = MockManager.MockObject<Plugin.UniqueDependencyCopy>();
			mock.AlwaysThrow("CopyDependencies", new Exception("The CopyDependencies method should not run if it's not the CopyDependencies build step."));
			Plugin.UniqueDependencyCopy plugin = mock.Object;

			plugin.Initialize(new BuildProcess(null), null);
			plugin.Execute(context);
		}

		[Test(Description = "Verifies the set of execution points the plugin will run in.")]
		public void ExecutionPoints_Populated()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			ExecutionPointCollection points = plugin.ExecutionPoints;
			Assert.IsNotNull(points, "The collection of execution points should not be null.");
			Assert.IsNotEmpty(points, "The collection of execution points should not be empty.");
			Assert.AreEqual(1, points.Count, "The wrong number of execution points were registered.");
			Assert.IsTrue(points.RunsAt(SandcastleBuilder.Utils.BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf), "The plugin should run instead of the default copy dependencies behavior.");
		}

		[Test(Description = "Ensures the plugin can't run without a build process.")]
		[ExpectedException(typeof(System.ArgumentNullException))]
		public void Initialize_NullBuildProcess()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			plugin.Initialize(null, new System.Xml.XmlDocument().CreateNavigator());
		}

		[Test(Description = "Ensures the plugin can run without configuration.")]
		public void Initialize_NullNavigator()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			plugin.Initialize(new BuildProcess(null), null);
		}

		[Test(Description = "Checks the value of the minimum SHFB version against a known correct value.")]
		public void MinimumHelpFileBuilderVersion_Value()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.AreEqual(new Version("1.6.0.1"), plugin.MinimumHelpFileBuilderVersion, "The minimum SHFB version was incorrect.");
		}

		[Test(Description = "Verifies that the friendly name is correct.")]
		public void Name_MatchesType()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.AreEqual("Unique Dependency Copy", plugin.Name, "The name of the plugin should just be the expected simple friendly name.");
		}

		[Test(Description = "Verifies the plugin will run in a partial build.")]
		public void RunsInPartialBuild_True()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.IsTrue(plugin.RunsInPartialBuild, "The plugin should run in a partial build.");
		}

		[Test(Description = "Verifies that the version matches that of the assembly.")]
		public void Version_Correct()
		{
			Plugin.UniqueDependencyCopy plugin = new Plugin.UniqueDependencyCopy();
			Assert.AreEqual(typeof(Plugin.UniqueDependencyCopy).Assembly.GetName().Version, plugin.Version, "The assembly version did not match the plugin version.");
		}

		/// <summary>
		/// Initializes a temporary file collection for use in testing the copy
		/// process.
		/// </summary>
		/// <returns>
		/// A <see cref="System.CodeDom.Compiler.TempFileCollection"/> that can
		/// be added to and has a temporary directory already created for it.
		/// </returns>
		private TempFileCollection InitializeTempFiles()
		{
			TempFileCollection tempFiles = new TempFileCollection();
			tempFiles.KeepFiles = false;
			Directory.CreateDirectory(tempFiles.BasePath);
			return tempFiles;
		}

		/// <summary>
		/// Cleans up a temporary file collection and the temporary folder it
		/// is associated with.
		/// </summary>
		/// <param name="tempFiles">The collection of temporary files to clean up.</param>
		private void CleanTempFiles(TempFileCollection tempFiles)
		{
			tempFiles.Delete();
			if (Directory.Exists(tempFiles.BasePath))
			{
				Directory.Delete(tempFiles.BasePath, true);
			}
		}

		/// <summary>
		/// Tests to see if a string can be converted to a GUID.
		/// </summary>
		/// <param name="guidToTest">The string to test.</param>
		/// <returns>
		/// <see langword="true" /> if <paramref name="guidToTest" /> can be
		/// converted to a <see cref="System.Guid"/>, <see langword="false" /> if not.
		/// </returns>
		private bool IsGuid(string guidToTest)
		{
			if (String.IsNullOrEmpty(guidToTest))
			{
				return false;
			}
			try
			{
				Guid g = new Guid(guidToTest);
			}
			catch (FormatException)
			{
				return false;
			}
			catch (OverflowException)
			{
				return false;
			}
			return true;
		}
	}

}
