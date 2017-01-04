using System;
using System.IO;
using System.Reflection;

using SandcastleBuilder.Utils;
using SandcastleBuilder.Utils.PlugIn;
using SandcastleBuilder.Utils.Gac;

namespace Paraesthesia.SandcastleHelpFileBuilder.Plugin
{
	/// <summary>
	/// Plugin for the Sandcastle Help File Builder that aids in resolving references
	/// on third-party dependencies.
	/// </summary>
	public class UniqueDependencyCopy : IPlugIn
	{

		#region UniqueDependencyCopy Variables

		/// <summary>
		/// Reference to the build process that this plugin will act on.
		/// </summary>
		private BuildProcess _buildProcess = null;

		/// <summary>
		/// Internal storage for the
		/// <see cref="Paraesthesia.SandcastleHelpFileBuilder.Plugin.UniqueDependencyCopy.ExecutionPoints" />
		/// property.
		/// </summary>
		/// <seealso cref="Paraesthesia.SandcastleHelpFileBuilder.Plugin.UniqueDependencyCopy" />
		private ExecutionPointCollection _executionPoints = null;

		#endregion



		#region UniqueDependencyCopy Implementation

		/// <summary>
		/// Copies the dependencies specified into the DLL folder.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This is different than how the standard SHFB copy process works because
		/// this version renames assemblies to a GUID name as it copies to avoid
		/// name clashes between different versions of assemblies.
		/// </para>
		/// </remarks>
		private void CopyDependencies()
		{
			SandcastleProject project = this._buildProcess.CurrentProject;
			if (project.Dependencies.Count == 0)
			{
				this._buildProcess.ReportProgress("No dependencies to copy.");
				return;
			}

			// The "DLL" folder is hard-coded in SHFB 1.6.0.1 so we create it in the same fashion
			// to avoid having to override several different behaviors that assume the location.
			string localDependencyFolderPath = Path.Combine(this._buildProcess.WorkingFolder, "DLL");
			Directory.CreateDirectory(localDependencyFolderPath);

			AssemblyLoader loader = null;
			try
			{
				foreach (DependencyItem item in project.Dependencies)
				{
					string dependencyPath = item.DependencyPath.ToString();

					// Resolve GAC paths to file paths.
					if (dependencyPath.StartsWith("GAC:"))
					{
						if (loader == null)
						{
							loader = AssemblyLoader.CreateAssemblyLoader(project);
						}
						dependencyPath = loader.GetAssemblyLocation(dependencyPath.Substring(4));
					}

					// Handle the "no-wildcard" filenames.
					if (dependencyPath.IndexOfAny(new char[] { '*', '?' }) == -1)
					{
						this.CopyDependency(dependencyPath, localDependencyFolderPath);
						continue;
					}

					// Handle wildcard filenames.
					string searchPattern = Path.GetFileName(dependencyPath);
					string searchDirectory = Path.GetDirectoryName(dependencyPath);
					string[] files = Directory.GetFiles(searchDirectory, searchPattern);
					foreach (string file in files)
					{
						string extension = Path.GetExtension(file).ToLowerInvariant();
						if (extension != ".dll" && extension != ".exe")
						{
							continue;
						}
						this.CopyDependency(file, localDependencyFolderPath);
					}
				}
			}
			finally
			{
				if (loader != null)
				{
					AssemblyLoader.ReleaseAssemblyLoader();
				}
			}
		}

		/// <summary>
		/// Copies a single dependency to the local cache, giving it a GUID name.
		/// </summary>
		/// <param name="dependencyPath">The full path to the original dependency to copy.</param>
		/// <param name="localDependencyFolderPath">The full path to the local dependency folder.</param>
		private void CopyDependency(string dependencyPath, string localDependencyFolderPath)
		{
			string destinationPath = Path.Combine(localDependencyFolderPath, Guid.NewGuid().ToString("D"));
			destinationPath = Path.ChangeExtension(destinationPath, Path.GetExtension(dependencyPath));
			File.Copy(dependencyPath, destinationPath, true);
			File.SetAttributes(destinationPath, FileAttributes.Normal);
			this._buildProcess.ReportProgress("{0} -> {1}", dependencyPath, destinationPath);
		}

		#endregion



		#region IPlugIn Members

		/// <summary>
		/// This method is used by the Sandcastle Help File Builder to let the
		/// plug-in perform its own configuration.
		/// </summary>
		/// <param name="currentConfig">The current configuration XML fragment</param>
		/// <returns>
		/// A string containing the new configuration XML fragment
		/// </returns>
		/// <remarks>
		/// <para>
		/// The configuration data will be stored in the help file
		/// builder project.
		/// </para>
		/// <para>
		/// This plugin does not use configuration, so whatever gets passed in
		/// will be returned.
		/// </para>
		/// </remarks>
		public string ConfigurePlugIn(string currentConfig)
		{
			return currentConfig;
		}

		/// <summary>
		/// This read-only property returns the copyright information for the
		/// plug-in.
		/// </summary>
		/// <value>
		/// A <see cref="System.String"/> with the assembly copyright information.
		/// </value>
		public string Copyright
		{
			get
			{
				AssemblyCopyrightAttribute copyright = (AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute));
				return copyright.Copyright;
			}
		}

		/// <summary>
		/// This read-only property returns a brief description of the plug-in
		/// </summary>
		/// <value>
		/// A <see cref="System.String"/> with a simple description of the plugin.
		/// </value>
		public string Description
		{
			get
			{
				return "Overrides the local dependency copy routine with a version that uniquely names locally copied files. Helps to avoid name clashes when you indirectly rely on different versions of the same assembly.";
			}
		}

		/// <summary>
		/// This method is used to execute the plug-in during the build process
		/// </summary>
		/// <param name="context">The current execution context</param>
		public void Execute(ExecutionContext context)
		{
			if (context.BuildStep == BuildStep.CopyDependencies)
			{
				this.CopyDependencies();
			}
		}

		/// <summary>
		/// This read-only property returns a collection of execution points
		/// that define when the plug-in should be invoked during the build
		/// process.
		/// </summary>
		/// <value>
		/// A <see cref="SandcastleBuilder.Utils.PlugIn.ExecutionPointCollection"/>
		/// containing the list of points at which this plugin should execute.
		/// </value>
		public ExecutionPointCollection ExecutionPoints
		{
			get
			{
				if (this._executionPoints == null)
				{
					this._executionPoints = new ExecutionPointCollection();
					this._executionPoints.Add(new ExecutionPoint(BuildStep.CopyDependencies, ExecutionBehaviors.InsteadOf));
				}
				return this._executionPoints;
			}
		}

		/// <summary>
		/// This method is used to initialize the plug-in at the start of the
		/// build process.
		/// </summary>
		/// <param name="buildProcess">
		/// A reference to the current build process.
		/// </param>
		/// <param name="configuration">
		/// The configuration data that the plug-in
		/// should use to initialize itself.
		/// </param>
		/// <exception cref="System.ArgumentNullException">
		/// Thrown if <paramref name="buildProcess" /> is <see langword="null" />.
		/// </exception>
		public void Initialize(BuildProcess buildProcess, System.Xml.XPath.XPathNavigator configuration)
		{
			if (buildProcess == null)
			{
				throw new ArgumentNullException("buildProcess");
			}
			this._buildProcess = buildProcess;
		}

		/// <summary>
		/// This read-only property returns the minimum version of the
		/// help file builder with which it is compatible.
		/// </summary>
		/// <value>
		/// A <see cref="System.Version"/> with the earliest SHFB version this
		/// plugin will work with.
		/// </value>
		/// <remarks>
		/// <para>
		/// This may not guarantee compatibility with future versions
		/// of the help file builder.  It only prevents it from running in
		/// an older version where known incompatibilities exist.
		/// </para>
		/// </remarks>
		public Version MinimumHelpFileBuilderVersion
		{
			get
			{
				return new Version("1.6.0.1");
			}
		}

		/// <summary>
		/// This read-only property returns a friendly name for the plug-in
		/// </summary>
		/// <value>
		/// A <see cref="System.String"/> with a short, friendly plugin name.
		/// </value>
		public string Name
		{
			get
			{
				return "Unique Dependency Copy";
			}
		}

		/// <summary>
		/// This read-only property returns true if the plug-in should run in
		/// a partial build or false if it should not.
		/// </summary>
		/// <value>
		/// If this returns false, the plug-in will not be loaded when
		/// a partial build is performed.
		/// </value>
		public bool RunsInPartialBuild
		{
			get
			{
				return true;
			}
		}

		/// <summary>
		/// This read-only property returns the version of the plug-in
		/// </summary>
		/// <value>
		/// A <see cref="System.Version"/> with the version of the plugin. Matches
		/// the assembly version.
		/// </value>
		public Version Version
		{
			get
			{
				return Assembly.GetExecutingAssembly().GetName().Version;
			}
		}

		#endregion



		#region IDisposable Members

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			// Nothing to do.
		}

		#endregion
	}
}
